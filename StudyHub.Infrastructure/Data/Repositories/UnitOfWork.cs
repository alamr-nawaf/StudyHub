using Microsoft.EntityFrameworkCore;
using Npgsql;
using StudyHub.Application.Common.Exceptions;
using StudyHub.Application.Common.Interfaces;

namespace StudyHub.Infrastructure.Data.Repositories;

/// <summary>
/// One save is one transaction, and the place where a database error becomes an Application
/// exception: nothing PostgreSQL-shaped leaves this class (ADR-13).
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    // 23505 = unique_violation in PostgreSQL
    private const string UniqueViolation = "23505";

    // 23503 = foreign_key_violation: a referenced row disappeared between the handler's check
    // and the save
    private const string ForeignKeyViolation = "23503";

    private readonly StudyHubDbContext _context;

    public UnitOfWork(StudyHubDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed the same row after this one read it
            throw new ConflictException("The resource was changed by another request.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // The handler's pre-check exists for the friendly message; this catch is for the
            // real race between two requests
            throw new ConflictException("A record with the same unique value already exists.");
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: ForeignKeyViolation })
        {
            throw new ConflictException("A referenced record no longer exists.");
        }
    }
}