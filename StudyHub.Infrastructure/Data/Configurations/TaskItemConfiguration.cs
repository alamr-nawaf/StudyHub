using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(250);

        // فلتر الحذف المنطقي
        builder.HasQueryFilter(t => !t.IsDeleted);

        // حماية المهام من الحذف العشوائي عند حذف الكيانات المرتبطة
        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(t => t.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
                .WithMany()
                .HasForeignKey(t => t.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Note>()
               .WithMany()
               .HasForeignKey(t => t.SourceNoteId)
               .OnDelete(DeleteBehavior.SetNull);

    }
}