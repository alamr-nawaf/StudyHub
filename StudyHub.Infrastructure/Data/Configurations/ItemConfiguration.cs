using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items", t =>
        {
            // خمسة مستويات: صفر إلى أربعة
            t.HasCheckConstraint("CK_Item_Depth", "\"Depth\" BETWEEN 0 AND 4");

            // الأب فارغ إذا وفقط إذا كان العمق صفرًا — لا حالة وسط
            t.HasCheckConstraint("CK_Item_RootDepth",
                "(\"ParentItemId\" IS NULL) = (\"Depth\" = 0)");

            // حقول المهمة تُملأ في المهام وحدها
            t.HasCheckConstraint("CK_Item_TaskFields",
                "(\"Kind\" = 1) = (\"Status\" IS NOT NULL AND \"Priority\" IS NOT NULL)");

            // العنوان يحوي محرفًا واحدًا على الأقل ليس مسافة
            t.HasCheckConstraint("CK_Item_Title", "\"Title\" ~ '\\S'");
        });

        builder.HasKey(i => i.Id);

        // المميِّز رقم لا نص: إعادة تسمية الصنف لاحقًا لا تُفسد الصفوف المخزَّنة
        builder.HasDiscriminator<int>("Kind")
               .HasValue<Note>(0)
               .HasValue<TaskItem>(1);

        builder.Property(i => i.Title).IsRequired().HasMaxLength(250);

        // Content يبقى نصًا بلا حد عن قصد: هو جسم الملاحظة
        builder.HasQueryFilter(i => !i.IsDeleted);

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(i => i.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Course>()
               .WithMany()
               .HasForeignKey(i => i.CourseId)
               .OnDelete(DeleteBehavior.Restrict);

        // مفتاح أجنبي ذاتي: الأب عنصر آخر في نفس الجدول
        builder.HasOne<Item>()
               .WithMany()
               .HasForeignKey(i => i.ParentItemId)
               .OnDelete(DeleteBehavior.Restrict);

        // حذف الكورس بجملة واحدة، وجلب جذور المستخدم
        builder.HasIndex(i => new { i.UserId, i.CourseId });
        builder.HasIndex(i => i.ParentItemId);
    }
}