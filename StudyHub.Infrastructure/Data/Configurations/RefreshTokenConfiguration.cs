using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);

        // 64 محرفًا = طول هاش SHA-256 بالنظام الست عشري
        builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(64);

        // فريد لأنه مفتاح البحث في كل تجديد جلسة
        builder.HasIndex(r => r.TokenHash).IsUnique();

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}