using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Modules.Moderation.Domain.Enums;

namespace Learnexia.Modules.Moderation.Application.Abstractions;

/// <summary>
/// Application-layer seam for the Moderation write side (review command).
/// The implementation (in Infrastructure) owns the DbContext, entity load, mutation, and
/// SaveChangesAsync — called via the UnitOfWorkBehavior transaction boundary.
///
/// <para>The handler calls <see cref="LoadTrackedAsync"/> to get a tracked entity, then mutates
/// it directly (Status, ReviewedBy*, Reason), and returns it so the UoW behavior can commit.
/// The domain event is raised by the handler on the tracked aggregate before returning.</para>
/// </summary>
public interface IModerationItemService
{
    /// <summary>
    /// Loads a <see cref="ModerationItem"/> by <paramref name="id"/> with EF change tracking
    /// so the UnitOfWorkBehavior can detect and commit the mutation.
    /// Returns <c>null</c> when not found.
    /// </summary>
    Task<ModerationItem?> LoadTrackedAsync(int id, CancellationToken cancellationToken = default);
}
