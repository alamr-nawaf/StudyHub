using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;
using StudyHub.Domain.ValueObjects;

namespace StudyHub.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="StudyHub.Application.Common.Interfaces.IUserRepository"/>.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly StudyHubDbContext _context;

    public UserRepository(StudyHubDbContext context) => _context = context;

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        // The normalization happens outside the expression tree, never inside it
        var normalized = Email.Create(email);
        return _context.Users.AnyAsync(u => u.Email == normalized, cancellationToken);
    }

    public void Add(User user) => _context.Users.Add(user);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // The normalization happens outside the expression tree, as in EmailExistsAsync
        var normalized = Email.Create(email);
        return _context.Users.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}