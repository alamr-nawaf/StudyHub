using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client.NativeInterop;
using StudyHub.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

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
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // هذه الدالة السحرية ستبحث في المشروع عن أي كلاس يطبق IEntityTypeConfiguration
        // وتنفذ إعداداته تلقائياً، مما يحافظ على نظافة ملف الـ DbContext
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudyHubDbContext).Assembly);
    }
}