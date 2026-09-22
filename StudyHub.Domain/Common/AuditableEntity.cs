namespace StudyHub.Domain.Common;

/// <summary>
/// The base for entities that change after they are created; everything else keeps only
/// the CreatedAt of <see cref="BaseEntity"/>.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime? UpdatedAt { get; protected set; }
}