using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

/// <summary>
/// Maps the single Items table that holds both notes and tasks through TPH, with the check
/// constraints that keep a row honest whatever writes it.
/// </summary>
public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items", t =>
        {
            // Five levels, 0 through 4
            t.HasCheckConstraint("CK_Item_Depth", "\"Depth\" BETWEEN 0 AND 4");

            // The parent is null if and only if the depth is zero
            t.HasCheckConstraint("CK_Item_RootDepth",
                "(\"ParentItemId\" IS NULL) = (\"Depth\" = 0)");

            // The task fields are filled on tasks and on nothing else
            t.HasCheckConstraint("CK_Item_TaskFields",
                "(\"Kind\" = 1) = (\"Status\" IS NOT NULL AND \"Priority\" IS NOT NULL)");

            // The title holds at least one character that is not whitespace
            t.HasCheckConstraint("CK_Item_Title", "\"Title\" ~ '\\S'");

            // The value itself, not merely its presence: an enum is an int in the database,
            // so nothing but a constraint keeps 99 out
            t.HasCheckConstraint("CK_Item_StatusValue",
                "\"Status\" IS NULL OR \"Status\" BETWEEN 0 AND 2");

            t.HasCheckConstraint("CK_Item_PriorityValue",
                "\"Priority\" IS NULL OR \"Priority\" BETWEEN 0 AND 2");
        });

        builder.HasKey(i => i.Id);

        // The discriminator is an int, not a string: renaming the class then leaves the
        // stored rows intact
        builder.HasDiscriminator<int>("Kind")
               .HasValue<Note>(0)
               .HasValue<TaskItem>(1);

        builder.Property(i => i.Title).IsRequired().HasMaxLength(250);

        builder.HasQueryFilter(i => !i.IsDeleted);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(i => i.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
               .WithMany()
               .HasForeignKey(i => i.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        // A self-referencing foreign key: the parent is another row of this same table
        builder.HasOne<Item>()
               .WithMany()
               .HasForeignKey(i => i.ParentItemId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.UserId, i.CourseId });
        builder.HasIndex(i => i.ParentItemId);
    }
}