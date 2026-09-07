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

        builder.HasQueryFilter(t => !t.IsDeleted);

        // التعداد يُخزَّن رقمًا، والقاعدة تقبل أي رقم بلا هذا القيد.
        // تنبيه: إضافة قيمة رابعة للتعداد لاحقًا تستلزم هجرة تعدّل الحدّ.
        builder.ToTable("Tasks", t =>
        {
            t.HasCheckConstraint("CK_Task_Status", "\"Status\" BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Task_Priority", "\"Priority\" BETWEEN 0 AND 2");
        });

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