using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    // 23505 = unique_violation في بوستجرس
    private const string UniqueViolation = "23505";

    private readonly StudyHubDbContext _context;

    public UnitOfWork(StudyHubDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // الفحص المسبق في المعالِج للرسالة اللطيفة، وهذا للسباق الحقيقي
            throw new ConflictException("A record with the same unique value already exists.");
        }
    }
}