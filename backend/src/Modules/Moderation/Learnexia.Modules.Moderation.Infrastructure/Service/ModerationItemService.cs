using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Moderation.Infrastructure.Service;

/// <summary>
/// Option-C implementation of <see cref="IModerationItemService"/>.
/// Loads a <see cref="ModerationItem"/> by id with EF change tracking.
/// The handler mutates the tracked entity; the UnitOfWorkBehavior commits.
/// </summary>
internal sealed class ModerationItemService : IModerationItemService
{
    private readonly ModerationDbContext _db;

    public ModerationItemService(ModerationDbContext db)
    {
        _db = db;
    }

    public async Task<ModerationItem?> LoadTrackedAsync(int id, CancellationToken cancellationToken = default)
        => await _db.ModerationItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
}
