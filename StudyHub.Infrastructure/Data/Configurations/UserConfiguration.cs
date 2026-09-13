using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;
using StudyHub.Domain.ValueObjects;
using StudyHub.Domain.Enums;

namespace StudyHub.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
        {
            // الوجود ليس صحة: بلا هذا القيد تقبل القاعدة أي عدد صحيح (A11)
            t.HasCheckConstraint("CK_User_RoleValue", "\"Role\" BETWEEN 0 AND 1");
        });
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Role)
       .HasConversion<int>()
       .IsRequired()
       .HasDefaultValue(UserRole.User);   // الصفوف القائمة تصير User

        // تحويل كائن القيمة: الكيان يحمل النوع، والقاعدة تخزّن نصًا عاديًا
        builder.Property(u => u.Email)
               .HasConversion(
                   email => email.Value,               // إلى القاعدة
                   value => Email.FromPersisted(value)) // من القاعدة — بلا تحقق
               .IsRequired()
               .HasMaxLength(150);
    }
}