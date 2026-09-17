using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Common.Interfaces;
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
        return services;
    }
}