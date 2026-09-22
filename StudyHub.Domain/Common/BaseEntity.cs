namespace StudyHub.Domain.Common;

/// <summary>
/// The common base of every entity: an id and a creation instant, nothing more.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}