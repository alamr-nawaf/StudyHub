using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Configurations;

public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        // اسم الجدول
        builder.ToTable("Notes");

        // تحديد المفتاح الأساسي
        builder.HasKey(n => n.Id);

        // تطبيق فلتر عام (Global Query Filter) لمنع جلب الملاحظات المحذوفة نهائياً
        builder.HasQueryFilter(n => !n.IsDeleted);

        // تطبيق قيد حماية قاعدة البيانات الذي اتفقنا عليه
        // يمنع إدراج سجل فارغ العنوان والمحتوى معاً على مستوى محرك قاعدة البيانات
        builder.HasCheckConstraint("CK_Note_TitleOrContent", "\"Title\" IS NOT NULL OR \"Content\" IS NOT NULL")
    }
}