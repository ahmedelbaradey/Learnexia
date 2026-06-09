namespace Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;

/// <summary>
/// Read-side DTO for a single audit-log entry. PII-safe — only ids + enum/state values.
/// The <c>details</c> field carries the PII-safe summary already enforced at produce time.
/// </summary>
public record AuditLogDto(
    int Id,
    Guid EventId,
    int AdminUserId,
    string Action,
    string TargetEntityType,
    int TargetEntityId,
    string? Details,
    DateTime OccurredAtUtc,
    DateTime CreatedAt);
