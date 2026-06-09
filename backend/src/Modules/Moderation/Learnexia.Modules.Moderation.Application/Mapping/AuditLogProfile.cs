using AutoMapper;
using Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;
using Learnexia.Modules.Moderation.Domain.Entities;

namespace Learnexia.Modules.Moderation.Application.Mapping;

/// <summary>
/// AutoMapper profile for <see cref="AuditLog"/> → <see cref="AuditLogDto"/>.
/// No Command→Entity map because the only write path is the event handler (no command).
/// </summary>
public class AuditLogProfile : Profile
{
    public AuditLogProfile()
    {
        CreateMap<AuditLog, AuditLogDto>()
            .ConstructUsing(src => new AuditLogDto(
                src.Id,
                src.EventId,
                src.AdminUserId,
                src.Action,
                src.TargetEntityType,
                src.TargetEntityId,
                src.Details,
                src.OccurredAtUtc,
                src.CreatedAt));
    }
}
