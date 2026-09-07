using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title).HasMaxLength(200);

        // فلتر عام يمنع جلب الملاحظات المحذوفة منطقيًا
        builder.HasQueryFilter(n => !n.IsDeleted);

        // القيد القديم كان يقبل النص الفارغ بينما الدومين يرفضه.
        // التعبير: العمود يحوي محرفًا واحدًا على الأقل ليس مسافة.
        builder.ToTable("Notes", t => t.HasCheckConstraint(
            "CK_Note_TitleOrContent",
            @"COALESCE(""Title"", '') ~ '\S' OR COALESCE(""Content"", '') ~ '\S'"));

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(n => n.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
               .WithMany()
               .HasForeignKey(n => n.CourseId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}