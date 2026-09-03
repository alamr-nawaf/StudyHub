using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudyHub.Infrastructure.Data;

public class StudyHubDbContextFactory : IDesignTimeDbContextFactory<StudyHubDbContext>
{
    public StudyHubDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StudyHubDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("STUDYHUB_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=StudyHubDb;Username=postgres;Password=YourSecurePassword";

        optionsBuilder.UseNpgsql(connectionString);

        return new StudyHubDbContext(optionsBuilder.Options);
    }
}