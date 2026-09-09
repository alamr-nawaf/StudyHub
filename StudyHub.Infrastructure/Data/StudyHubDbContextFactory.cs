using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudyHub.Infrastructure.Data;

public class StudyHubDbContextFactory : IDesignTimeDbContextFactory<StudyHubDbContext>
{
    public StudyHubDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StudyHubDbContext>();

        // لا قيمة احتياطية: أوامر dotnet ef لا تقرأ user-secrets،
        // فالمتغيّر هو المصدر الوحيد ويجب أن يفشل بوضوح إن غاب.
        var connectionString = Environment.GetEnvironmentVariable("STUDYHUB_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "STUDYHUB_DB_CONNECTION is not set. Design-time commands do not read user-secrets.");

        optionsBuilder.UseNpgsql(connectionString);

        return new StudyHubDbContext(optionsBuilder.Options);
    }
}