namespace StudyHub.Domain.Entities;

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