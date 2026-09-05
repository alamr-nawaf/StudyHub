using Microsoft.EntityFrameworkCore;
using StudyHub.Application.Common.Interfaces;
using StudyHub.Domain.Entities;

namespace StudyHub.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly StudyHubDbContext _context;

    public UserRepository(StudyHubDbContext context) => _context = context;

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
        => _context.Users.AnyAsync(u => u.Email == email.Trim().ToLowerInvariant(), cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
        => await _context.Users.AddAsync(user, cancellationToken);
}