using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyHub.Domain.Entities;
using StudyHub.Infrastructure.Data;

namespace StudyHub.IntegrationTests.Infrastructure;

/// <summary>
/// Seeding and reading straight through the application's own <see cref="StudyHubDbContext"/>,
/// so a test can arrange more data than the API would make convenient and can look at rows
/// the API hides. Entities are built by their domain factory methods, never by SQL.
/// </summary>
public static class Seeding
{
    public static async Task InScopeAsync(this StudyHubApiFactory factory, Func<StudyHubDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StudyHubDbContext>();

        await work(context);
    }

    public static async Task<T> InScopeAsync<T>(this StudyHubApiFactory factory, Func<StudyHubDbContext, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<StudyHubDbContext>();

        return await work(context);
    }

    /// <summary>
    /// A timestamp that has to lie in the past. CreatedAt is stamped by the base entity and
    /// has no setter, so the value is pushed through the change tracker rather than through
    /// SQL, which would bypass the soft-delete filter and the value converters.
    /// </summary>
    public static void BackdateCreatedAt<TEntity>(this DbContext context, TEntity entity, DateTime createdAtUtc)
        where TEntity : class
    {
        context.Entry(entity).Property("CreatedAt").CurrentValue = createdAtUtc;
    }
}
