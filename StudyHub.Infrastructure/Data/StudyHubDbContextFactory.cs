using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StudyHub.Infrastructure.Data;

public class StudyHubDbContextFactory : IDesignTimeDbContextFactory<StudyHubDbContext>
{
    public StudyHubDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StudyHubDbContext>();

        // هذا النص مخصص فقط لعمليات Migration وقت التطوير والتخطيط
        // تأكد من مطابقة كلمة المرور لما وضعته في ملف docker-compose.yml
        var connectionString = "Host=localhost;Port=5432;Database=StudyHubDb;Username=postgres;Password=YourSecurePassword";

        optionsBuilder.UseNpgsql(connectionString);

        return new StudyHubDbContext(optionsBuilder.Options);
    }
}
