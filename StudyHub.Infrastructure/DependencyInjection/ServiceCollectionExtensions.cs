using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Infrastructure.Data;

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

        // تسجيل الـ DbContext مع محرك PostgreSQL
        services.AddDbContext<StudyHubDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}