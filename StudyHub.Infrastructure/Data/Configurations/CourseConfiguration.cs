using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

/// <summary>
/// Maps courses: the title constraint, the soft-delete filter and the owner relationship.
/// </summary>
public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Title).IsRequired().HasMaxLength(200);
        builder.HasQueryFilter(c => !c.IsDeleted);

        // The same title constraint as Items: both tables now follow one rule
        builder.ToTable("Courses", t =>
            t.HasCheckConstraint("CK_Course_Title", "\"Title\" ~ '\\S'"));

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(c => c.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}