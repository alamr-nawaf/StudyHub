using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Infrastructure.Data;
using StudyHub.Infrastructure.Data.Repositories;
using StudyHub.Infrastructure.Security;

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
        // تسجيل الـ DbContext مع محرك PostgreSQL
        services.AddDbContext<StudyHubDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}