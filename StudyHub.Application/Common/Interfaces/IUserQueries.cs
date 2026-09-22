using StudyHub.Application.Auth.Queries.GetCurrentUser;

namespace StudyHub.Application.Common.Interfaces;

/// <summary>
/// Read-side access to users: returns DTOs, never entities (ADR-32).
/// </summary>
public interface IUserQueries
{
    // monthStartUtc bounds the usage sum: the caller decides where the month begins (ADR-40)
    Task<CurrentUserDto?> GetCurrentAsync(Guid userId, DateTime monthStartUtc, CancellationToken cancellationToken);
}
