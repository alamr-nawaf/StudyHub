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

    public async Task<CurrentUserDto?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Email يُسقَط كائنًا كاملًا عبر المحوّل، ويُقرأ Value بعد وصوله للذاكرة:
        // EF لا يعرف أعضاء كائن القيمة، فلا ترجمة لـ Email.Value داخل الاستعلام (D6)
        var row = await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Role,
                u.MonthlyTokenQuota,
                u.TokensUsedThisMonth
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
