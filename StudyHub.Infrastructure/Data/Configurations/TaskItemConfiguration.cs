using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // فهرس جزئي على صفوف المهام وحدها — الملاحظات لا تدخله
        builder.HasIndex(t => new { t.UserId, t.DueDate })
               .HasFilter("\"Kind\" = 1");
    }
}