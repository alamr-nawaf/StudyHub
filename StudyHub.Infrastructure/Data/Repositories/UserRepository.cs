using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;
using StudyHub.Domain.ValueObjects;

namespace StudyHub.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly StudyHubDbContext _context;

    public UserRepository(StudyHubDbContext context) => _context = context;

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        // التطبيع خارج شجرة التعبير — لا داخلها
        var normalized = Email.Create(email);
        return _context.Users.AnyAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
        => await _context.Users.AddAsync(user, cancellationToken);
}