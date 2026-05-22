using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Persists the parent↔student linkage. Writes and commits the <c>ParentStudent</c> row directly via
/// <c>IdentityModuleDbContext.SaveChangesAsync(userId)</c> (NOT via the deferred UnitOfWorkBehavior),
/// because the auto-link hook (P1-03) runs after <c>UserManager.CreateAsync</c> has already committed
/// the child user. See <see cref="ILinkParentStudentService"/> for the sequencing contract.
/// </summary>
public class LinkParentStudentService : ILinkParentStudentService
{
    private readonly IdentityModuleDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILoggerManager _logger;

    public LinkParentStudentService(
        IdentityModuleDbContext dbContext,
        ICurrentUserService currentUserService,
        ILoggerManager logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> LinkAsync(int parentId, int studentId, CancellationToken cancellationToken = default)
    {
        // AC-6 idempotency: never create a duplicate (ParentId, StudentId) row. The composite PK
        // would also reject it, but the AnyAsync pre-check keeps the no-op path exception-free.
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
            // Stamp the acting user when available (web request); null when called from a background/seed
            // path where there is no current user (e.g. the P1-03 auto-link before the parent context exists).
            CreatedBy = _currentUserService.UserId,
        };

        await _dbContext.ParentStudents.AddAsync(link, cancellationToken);

        // Commit immediately — see class remarks (the child user is already committed by UserManager).
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

    public async Task<IReadOnlyList<User>> GetLinkedStudentsAsync(int parentId, CancellationToken cancellationToken = default)
    {
        // Strictly parent-scoped (AC-3): only rows where ParentId == the caller's id.
        return await _dbContext.ParentStudents
            .Where(ps => ps.ParentId == parentId)
            .Select(ps => ps.Student)
            .ToListAsync(cancellationToken);
    }
}
