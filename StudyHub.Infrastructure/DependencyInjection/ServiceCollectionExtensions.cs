using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Infrastructure.Ai;
using StudyHub.Infrastructure.Authentication;
using StudyHub.Infrastructure.Data;
using StudyHub.Infrastructure.Data.Queries;
using StudyHub.Infrastructure.Data.Repositories;
using StudyHub.Infrastructure.Security;
using System.Text;


namespace StudyHub.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // سحب نص الاتصال من الإعدادات بأمان
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured.");
        }
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        // جانب القراءة منفصل عن المستودعات: المستودع يحمّل كيانات للأوامر، والاستعلام يُسقط DTO (ADR-32)
        services.AddScoped<ICourseQueries, CourseQueries>();
        services.AddScoped<IItemQueries, ItemQueries>();
        services.AddScoped<IUserQueries, UserQueries>();
        // تسجيل الـ DbContext مع محرك PostgreSQL
        services.AddDbContext<StudyHubDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddOptions<JwtSettings>()
    .Bind(configuration.GetSection(JwtSettings.SectionName))
    .Validate(s => Encoding.UTF8.GetByteCount(s.Key) >= 32, "Jwt:Key must be at least 32 bytes.")
    .Validate(s => !string.IsNullOrWhiteSpace(s.Issuer) && !string.IsNullOrWhiteSpace(s.Audience),
              "Jwt:Issuer and Jwt:Audience must be set.")
    .ValidateOnStart();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton(ReadUserQuotaSettings(configuration));
        services.AddAiProvider(configuration);

        return services;
    }

    /// <summary>
    /// Wires the AI provider: the real one when a key is configured, the fake one when it
    /// is not. The choice is made once, here, and never again after a failed call (ADR-35).
    /// </summary>
    private static IServiceCollection AddAiProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = ReadAiSettings(configuration);
        services.AddSingleton(settings);
        services.AddScoped<IAiUsageRecorder, AiUsageRecorder>();

        var apiKey = configuration[$"{AiSettings.SectionName}:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var failEveryCall = configuration.GetValue<bool>($"{AiSettings.SectionName}:FakeFailure");
            services.AddScoped<IAiService>(_ => new FakeAiService(settings, failEveryCall));
            return services;
        }

        // A typed client from the factory, not a new HttpClient per request: one per request
        // exhausts sockets under load and caches DNS for the life of the process. The key is
        // attached here, so it never travels through a type the Application layer can see,
        // and the timeout is explicit because the 100-second default is an absence of a
        // decision rather than one (§15.3).
        services.AddHttpClient<IAiService, GeminiAiService>(client =>
        {
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
        });

        return services;
    }

    // Read once at startup and fail loudly, exactly like the connection string above:
    // a wrong quota discovered on the first registration is a 500 nobody can explain.
    private static UserQuotaSettings ReadUserQuotaSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(UserQuotaSettings.SectionName);
        var defaultMonthlyTokens = Positive(section, "DefaultMonthlyTokens");

        return new UserQuotaSettings(defaultMonthlyTokens);
    }

    private static AiSettings ReadAiSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(AiSettings.SectionName);

        var baseUrl = section["BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Ai:BaseUrl is not configured.");

        // The model is required only with a key, because without one the fake provider
        // runs and never calls anything. It is never defaulted: a guessed model name
        // fails at the first real call, far from the configuration that caused it.
        var model = section["Model"] ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(section["ApiKey"]) && string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                "Ai:ApiKey is set, so Ai:Model must name the model to call.");

        // A relative request path resolves against the last '/' of the base address, so a
        // base URL without one would silently drop its last segment.
        baseUrl = baseUrl.Trim();
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        return new AiSettings(
            baseUrl,
            model.Trim(),
            Positive(section, "TimeoutSeconds"),
            Positive(section, "MaxOutputTokens"),
            Positive(section, "CharsPerToken"),
            Positive(section, "ResponseReserveTokens"),
            Positive(section, "MaxSuggestions"),
            // Optional, and deliberately not guarded by Positive: absent is valid and means
            // "ask for no thinking configuration", while a budget of 0 is a real instruction
            // to a model that supports it — the one value Positive would reject.
            section.GetValue<int?>("ThinkingBudget"),
            section["ThinkingLevel"]);
    }

    private static int Positive(IConfigurationSection section, string key)
    {
        var value = section.GetValue<int?>(key);

        if (value is null or <= 0)
            throw new InvalidOperationException(
                $"{section.Key}:{key} must be configured with a positive number.");

        return value.Value;
    }
}