using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudyHub.Infrastructure.Data;

/// <summary>
/// Builds a context for design-time commands such as `dotnet ef`, which never start the
/// application and therefore never read its configuration (B3).
/// </summary>
public class StudyHubDbContextFactory : IDesignTimeDbContextFactory<StudyHubDbContext>
{
    public StudyHubDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StudyHubDbContext>();

        // No fallback value: dotnet ef commands do not read user secrets, so this environment
        // variable is the only source and its absence has to fail loudly rather than quietly
        // connect somewhere else.
        var connectionString = Environment.GetEnvironmentVariable("STUDYHUB_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "STUDYHUB_DB_CONNECTION is not set. Design-time commands do not read user-secrets.");

        optionsBuilder.UseNpgsql(connectionString);

        return new StudyHubDbContext(optionsBuilder.Options);
    }
}