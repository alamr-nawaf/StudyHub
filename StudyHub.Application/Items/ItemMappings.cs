using StudyHub.Domain.Entities;
using StudyHub.Domain.Enums;

namespace StudyHub.Application.Items;

/// <summary>
/// Maps notes and tasks to <see cref="ItemDto"/> (ADR-17).
/// </summary>
public static class ItemMappings
{
    public const int NoteKind = 0;
    public const int TaskKind = 1;

    // Kind مميِّز TPH لا خاصية على الكيان، فيُشتق من النوع نفسه: EF يترجم "is TaskItem" إلى فحص العمود.
    // حقول المهمة null للملاحظة، مثل أعمدتها في القاعدة (§3.1)
    public static IQueryable<ItemDto> ToDto(this IQueryable<Item> items) =>
        items.Select(i => new ItemDto(
            i.Id,
            i.ParentItemId,
            i.CourseId,
            i.Depth,
            i is TaskItem ? TaskKind : NoteKind,
            i.Title,
            i.Content,
            i is TaskItem ? ((TaskItem)i).Status : (StudyTaskStatus?)null,
            i is TaskItem ? ((TaskItem)i).Priority : (TaskPriority?)null,
            i is TaskItem ? ((TaskItem)i).DueDate : null,
            i.CreatedAt,
            i.UpdatedAt));
}
