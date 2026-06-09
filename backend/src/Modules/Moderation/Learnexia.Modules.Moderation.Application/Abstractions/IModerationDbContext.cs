using Learnexia.Modules.Moderation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Learnexia.Modules.Moderation.Application.Abstractions;

/// <summary>
/// Application-layer abstraction over <c>ModerationDbContext</c>.
/// Keeps the Application layer infrastructure-independent (module isolation).
/// </summary>
public interface IModerationDbContext
{
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Exposed so write paths (event handler, future P7-09 commands) can call SaveChangesAsync directly.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
