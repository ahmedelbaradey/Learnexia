using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Parent;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Parent.Infrastructure.Service;

/// <summary>
/// Parent-side implementation of the <see cref="IParentChildQuery"/> Shared.Contracts seam. Lets Identity's
/// self-scoped Me endpoint surface <c>HasChildren</c> after the ParentStudent link table moved into the
/// Parent module — without Identity referencing the Parent module's projects. Read-only; strictly
/// parent-scoped. Registered at the Host (mirrors the IUserLookup registration pattern).
/// </summary>
public sealed class ParentChildQuery : IParentChildQuery
{
    private readonly ParentDbContext _dbContext;

    public ParentChildQuery(ParentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ParentHasAnyChildAsync(int parentId, CancellationToken ct = default)
        => _dbContext.ParentStudents.AnyAsync(ps => ps.ParentId == parentId, ct);

    public Task<bool> IsParentOfChildAsync(int parentId, int childId, CancellationToken ct = default)
        => _dbContext.ParentStudents.AnyAsync(ps => ps.ParentId == parentId && ps.StudentId == childId, ct);

    public Task<int?> FindParentForChildAsync(int childId, CancellationToken ct = default)
        => _dbContext.ParentStudents
            .Where(ps => ps.StudentId == childId)
            .Select(ps => (int?)ps.ParentId)
            .FirstOrDefaultAsync(ct);
}
