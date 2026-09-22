using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Application.Common.Settings;
using StudyHub.Application.Common.Time;
using StudyHub.Infrastructure.Ai;
using StudyHub.Infrastructure.Authentication;
using StudyHub.Infrastructure.Data;
using StudyHub.Infrastructure.Data.Queries;
using StudyHub.Infrastructure.Data.Repositories;
using StudyHub.Infrastructure.Security;
using System.Text;


namespace StudyHub.Infrastructure.DependencyInjection;

/// <summary>
/// Wires everything Infrastructure implements: the context, the repositories and read-side
/// queries, the security services, the settings that are validated at startup, and the AI
/// provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // The connection string comes from configuration, and its absence stops startup
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
        // The read side is separate from the repositories: a repository loads entities for
        // commands, a query projects DTOs (ADR-32)
        services.AddScoped<ICourseQueries, CourseQueries>();
        services.AddScoped<IItemQueries, ItemQueries>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        // The DbContext, on the PostgreSQL provider
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
        services.AddSingleton(ReadRefreshTokenCleanupSettings(configuration));
        services.AddSingleton(new BusinessCalendar(ReadBusinessTimeZone(configuration)));
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
        services.AddScoped<IAiUsageLedger, AiUsageLedger>();

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

    // Read once at startup like every other setting. A cleanup that never runs, or runs
    // with a retention of zero, is a data-loss bug found long after the value was written,
    // so both numbers are refused here rather than defaulted (ADR-46).
    private static RefreshTokenCleanupSettings ReadRefreshTokenCleanupSettings(IConfiguration configuration)
    {
        var section = configuration.GetSection(RefreshTokenCleanupSettings.SectionName);

        // Absent means enabled: the cleanup is the normal state, and turning it off is the
        // deliberate act that has to be written down
        var enabled = section.GetValue<bool?>("Enabled") ?? true;

        return new RefreshTokenCleanupSettings(
            enabled,
            Positive(section, "IntervalHours"),
            Positive(section, "RetentionDaysAfterExpiry"));
    }

    // Read once at startup like every other setting: an unknown zone found on the first
    // AI call would be a 500 far from the configuration that caused it (ADR-40). The id is
    // resolved by the operating system, and .NET accepts IANA ids such as Asia/Riyadh on
    // Windows as well as on Linux.
    private static TimeZoneInfo ReadBusinessTimeZone(IConfiguration configuration)
    {
        const string key = "BusinessTime:TimeZoneId";
        var timeZoneId = configuration[key];

        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new InvalidOperationException($"{key} is not configured.");

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException(
                $"{key} names a time zone this machine does not know: '{timeZoneId}'.", exception);
        }
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