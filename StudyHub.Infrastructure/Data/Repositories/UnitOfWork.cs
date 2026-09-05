using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly StudyHubDbContext _context;

    public UnitOfWork(StudyHubDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        => _context.SaveChangesAsync(cancellationToken);
}