using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.HasQueryFilter(c => !c.IsDeleted);

        // نفس قيد العنوان في Items — الجدولان يتبعان نفس القاعدة الآن
        builder.ToTable("Courses", t =>
            t.HasCheckConstraint("CK_Course_Title", "\"Title\" ~ '\\S'"));

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}