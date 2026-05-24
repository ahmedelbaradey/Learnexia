using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Domain.Entities;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Parent.Infrastructure.Service;

/// <summary>
/// Persists the parent↔student linkage in the Parent module's <c>ParentDbContext</c> (schema "parent").
/// Relocated from Identity; it no longer references the Identity User table. Writes commit immediately via
/// <c>ParentDbContext.SaveChangesAsync(userId)</c> (original semantics — the child account is already
/// committed through the IChildAccountService seam before LinkAsync runs). Reads return child user ids only.
/// </summary>
public class LinkParentStudentService : ILinkParentStudentService
{
    private readonly ParentDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILoggerManager _logger;

    public LinkParentStudentService(
        ParentDbContext dbContext,
        ICurrentUserService currentUserService,
        ILoggerManager logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> LinkAsync(int parentId, int studentId, CancellationToken cancellationToken = default)
    {
        var alreadyLinked = await _dbContext.ParentStudents
            .AnyAsync(ps => ps.ParentId == parentId && ps.StudentId == studentId, cancellationToken);

        if (alreadyLinked)
        {
            _logger.LogInfo($"ParentStudent link already exists for parent {parentId} / student {studentId}; no-op.");
            return false;
        }

        var link = new ParentStudent
        {
            ParentId = parentId,
            StudentId = studentId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserId,
        };

        await _dbContext.ParentStudents.AddAsync(link, cancellationToken);
        await _dbContext.SaveChangesAsync(_currentUserService.UserId ?? parentId);

        _logger.LogInfo($"Created ParentStudent link for parent {parentId} / student {studentId}.");
        return true;
    }

    public Task<bool> IsLinkedAsync(int parentId, int studentId, CancellationToken cancellationToken = default)
        => _dbContext.ParentStudents
            .AnyAsync(ps => ps.ParentId == parentId && ps.StudentId == studentId, cancellationToken);

    public Task<bool> StudentHasAnyParentAsync(int studentId, CancellationToken cancellationToken = default)
        => _dbContext.ParentStudents
            .AnyAsync(ps => ps.StudentId == studentId, cancellationToken);

    public Task<bool> ParentHasAnyChildAsync(int parentId, CancellationToken cancellationToken = default)
        => _dbContext.ParentStudents
            .AnyAsync(ps => ps.ParentId == parentId, cancellationToken);

    public Task<int> CountParentsForStudentAsync(int studentId, CancellationToken cancellationToken = default)
        => _dbContext.ParentStudents
            .Where(ps => ps.StudentId == studentId)
            .Select(ps => ps.ParentId)
            .Distinct()
            .CountAsync(cancellationToken);

    public async Task<bool> UnlinkAsync(int parentId, int studentId, CancellationToken cancellationToken = default)
    {
        var link = await _dbContext.ParentStudents
            .FirstOrDefaultAsync(ps => ps.ParentId == parentId && ps.StudentId == studentId, cancellationToken);

        if (link is null)
        {
            _logger.LogInfo($"Unlink no-op: no ParentStudent link for parent {parentId} / student {studentId}.");
            return false;
        }

        _dbContext.ParentStudents.Remove(link);
        await _dbContext.SaveChangesAsync(_currentUserService.UserId ?? parentId);

        _logger.LogInfo($"Removed ParentStudent link for parent {parentId} / student {studentId}.");
        return true;
    }

    public async Task<IReadOnlyList<int>> GetLinkedStudentIdsAsync(int parentId, CancellationToken cancellationToken = default)
    {
        // Strictly parent-scoped: only rows where ParentId == the caller's id.
        return await _dbContext.ParentStudents
            .Where(ps => ps.ParentId == parentId)
            .Select(ps => ps.StudentId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Atomically enforces the last-parent invariant and removes the link, closing the TOCTOU race where
    /// two concurrent unlinks for the same child both pass the parent-count check before either commits.
    /// This method runs INSIDE the ambient <c>UnitOfWorkBehavior</c> transaction (it must NOT open its own
    /// — EF Core throws when a transaction is already active). It serializes all unlink operations for THIS
    /// child via a transaction-scoped Postgres advisory lock (<c>pg_advisory_xact_lock</c>), which is held
    /// until the UoW commits/rolls back. A second concurrent unlink for the same child blocks on the lock,
    /// then re-reads the (now committed) parent count — so a child can never be left with zero parents.
    /// The delete is STAGED only; the UnitOfWorkBehavior performs the single SaveChanges + commit.
    /// </summary>
    public async Task<bool> UnlinkIfNotLastParentAsync(int parentId, int studentId, CancellationToken cancellationToken = default)
    {
        // Transaction-scoped advisory lock keyed on the child id — serializes concurrent unlinks for the
        // same child within the ambient UoW transaction (auto-released on commit/rollback).
        await _dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0})", new object[] { (long)studentId }, cancellationToken);

        var parentCount = await _dbContext.ParentStudents
            .Where(ps => ps.StudentId == studentId)
            .Select(ps => ps.ParentId)
            .Distinct()
            .CountAsync(cancellationToken);

        if (parentCount <= 1)
            return false; // Caller: block the unlink (last parent).

        var link = await _dbContext.ParentStudents
            .FirstOrDefaultAsync(ps => ps.ParentId == parentId && ps.StudentId == studentId, cancellationToken);

        if (link is null)
            return false; // Link already gone (harmless race).

        // STAGE only — UnitOfWorkBehavior saves + commits once after the handler returns.
        _dbContext.ParentStudents.Remove(link);

        _logger.LogInfo($"UnlinkIfNotLastParentAsync staged removal of ParentStudent link for parent {parentId} / student {studentId}.");
        return true;
    }
}
