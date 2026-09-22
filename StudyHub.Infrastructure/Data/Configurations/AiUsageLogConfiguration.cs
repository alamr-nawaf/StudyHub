using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

/// <summary>
/// Maps the write-once AI usage log; its index is what makes the monthly sum cheap (ADR-39).
/// </summary>
public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OperationType).IsRequired().HasMaxLength(50);

        // A composite index for the one question asked of this table: how much did this user
        // spend in this period
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(a => a.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}