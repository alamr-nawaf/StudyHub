namespace StudyHub.Domain.Entities;

/// <summary>
/// A note: text a user keeps, and the only kind of item the AI endpoints read.
/// </summary>
public sealed class Note : Item
{
    private Note() { }

    public static Note Create(
        Guid userId,
        string title,
        string? content = null,
        Item? parent = null,
        Guid? courseId = null)
    {
        var note = new Note();
        note.Initialize(userId, title, content, parent, courseId);
        return note;
    }
}