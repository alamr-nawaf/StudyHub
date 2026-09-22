using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;
using StudyHub.Domain.ValueObjects;
using StudyHub.Domain.Enums;

namespace StudyHub.Infrastructure.Data.Configurations;

/// <summary>
/// Maps accounts, including the Email value-object conversion and the role column.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
        {
            // Existence is not validity: without this constraint the database accepts any
            // integer at all (A11)
            t.HasCheckConstraint("CK_User_RoleValue", "\"Role\" BETWEEN 0 AND 1");
        });
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Role)
       .HasConversion<int>()
       .IsRequired()
       .HasDefaultValue(UserRole.User);   // existing rows become User

        // The value-object conversion: the entity carries the type, the database stores plain text
        builder.Property(u => u.Email)
               .HasConversion(
                   email => email.Value,               // to the database
                   value => Email.FromPersisted(value)) // from the database, without validating:
                                                       // a converter is a mapping, not a gate
               .IsRequired()
               .HasMaxLength(150);
    }
}