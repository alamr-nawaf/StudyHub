using StudyHub.Domain.Entities;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Write-side access to accounts: loads entities for commands to change (ADR-32).
/// </summary>
public interface IUserRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    // Synchronous on purpose: the id is generated in the entity, so adding one needs no
    // round trip to the database
    void Add(User user);

    // For login: the comparison is against the whole Email value object, not its string
    // value, because EF cannot translate a member of a converted type (D6)
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);

    // For refresh: the identity comes from the stored row, never from the token
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}