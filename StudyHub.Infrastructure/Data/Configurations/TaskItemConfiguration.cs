using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

/// <summary>
/// The task-only part of the Items mapping: the due-date index the dashboard's urgent list
/// is served by.
/// </summary>
public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // A partial index over task rows alone: notes never enter it
        builder.HasIndex(t => new { t.UserId, t.DueDate })
               .HasFilter("\"Kind\" = 1");
    }
}