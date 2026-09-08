using Microsoft.EntityFrameworkCore;
using StudyHub.Domain.Entities;


namespace StudyHub.Infrastructure.Data;

public class StudyHubDbContext : DbContext
{
    public StudyHubDbContext(DbContextOptions<StudyHubDbContext> options)
        : base(options)
    {
    }

    // تعريف الجداول (DbSets)
    public DbSet<User> Users => Set<User>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudyHubDbContext).Assembly);
    }
}