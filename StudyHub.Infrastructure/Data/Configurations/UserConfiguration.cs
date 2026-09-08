using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;
using StudyHub.Domain.ValueObjects;

namespace StudyHub.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);

        // تحويل كائن القيمة: الكيان يحمل النوع، والقاعدة تخزّن نصًا عاديًا
        builder.Property(u => u.Email)
               .HasConversion(
                   email => email.Value,          // إلى القاعدة
                   value => Email.Create(value))  // من القاعدة
               .IsRequired()
               .HasMaxLength(150);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}