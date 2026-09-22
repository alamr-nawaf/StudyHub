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

    // Kind is the TPH discriminator rather than a property on the entity, so it is derived
    // from the type itself: EF translates "is TaskItem" into a check on the column. The task
    // fields are null for a note, exactly as their columns are in the database (§3.1)
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
