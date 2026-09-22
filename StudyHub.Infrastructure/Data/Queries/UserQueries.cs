using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Auth.Queries.GetCurrentUser;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Data.Queries;

/// <summary>
/// EF Core implementation of <see cref="IUserQueries"/>; every read projects with Select.
/// </summary>
public class UserQueries : IUserQueries
{
    private readonly StudyHubDbContext _context;

    public UserQueries(StudyHubDbContext context) => _context = context;

    public async Task<CurrentUserDto?> GetCurrentAsync(
        Guid userId, DateTime monthStartUtc, CancellationToken cancellationToken)
    {
        // Email is projected as the whole value object through the converter, and Value is
        // read once it is in memory: EF knows nothing of a value object's members, so
        // Email.Value cannot be translated inside the query (D6)
        var row = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.MonthlyTokenQuota,
                // A correlated sub-sum inside the same SELECT: one statement per request,
                // and the month's usage is read from the log it is recorded in (ADR-39).
                // int? because SUM over no rows is NULL
                TokensUsedThisMonth = _context.AiUsageLogs
                    .Where(l => l.UserId == u.Id && l.CreatedAt >= monthStartUtc)
                    .Sum(l => (int?)l.TokensConsumed) ?? 0
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new CurrentUserDto(
                row.Id,
                row.FullName,
                row.Email.Value,
                row.Role,
                row.MonthlyTokenQuota,
                row.TokensUsedThisMonth);
    }
}
