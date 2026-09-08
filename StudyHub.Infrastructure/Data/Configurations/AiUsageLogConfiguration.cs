using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class AiUsageLogConfiguration : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OperationType).IsRequired().HasMaxLength(50);

        // فهرس مركّب: استعلام «استخدام هذا المستخدم في هذه الفترة»
        builder.HasIndex(a => new { a.UserId, a.CreatedAt });

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(a => a.UserId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}