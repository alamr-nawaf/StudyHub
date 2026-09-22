using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

/// <summary>
/// Maps refresh tokens, including the xmin concurrency token rotation depends on (ADR-27).
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);

        // 64 characters: the length of a SHA-256 hash in hex
        builder.Property(r => r.TokenHash).IsRequired().HasMaxLength(64);

        // Unique, because it is the lookup key of every refresh
        builder.HasIndex(r => r.TokenHash).IsUnique();

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.Property<uint>("xmin")
       .HasColumnType("xid")
       .IsRowVersion();
    }
}