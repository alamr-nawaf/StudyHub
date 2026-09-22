namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// One save is one transaction. Handlers change entities and call this once; nothing else
/// commits (ADR-13).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}