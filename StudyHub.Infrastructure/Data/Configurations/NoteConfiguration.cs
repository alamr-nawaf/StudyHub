using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {

        // تحديد المفتاح الأساسي
        builder.HasKey(n => n.Id);

        // تطبيق فلتر عام (Global Query Filter) لمنع جلب الملاحظات المحذوفة نهائياً
        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.ToTable("Notes", t => t.HasCheckConstraint("CK_Note_TitleOrContent", "\"Title\" IS NOT NULL OR \"Content\" IS NOT NULL"));
        builder.HasOne<User>()
       .WithMany()
       .HasForeignKey(n => n.UserId)
       .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
               .WithMany()
               .HasForeignKey(n => n.CourseId)
               .OnDelete(DeleteBehavior.SetNull); // Course اختياري، فلو انحذف الكورس، الملاحظة تبقى بس بدون كورس
    }

}