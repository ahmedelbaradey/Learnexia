using AutoMapper;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Modules.Moderation.Domain.Entities;

namespace Learnexia.Modules.Moderation.Application.Mapping;

/// <summary>
/// AutoMapper profile for <see cref="ModerationItem"/> → <see cref="ModerationItemDto"/>
/// and <see cref="ModerationItem"/> → <see cref="ModerationItemDetailDto"/>.
/// Used by the Infrastructure query service (<c>ModerationQueryService</c>) via <c>ProjectTo</c>.
/// Enum values are projected as their string names (mirrors the serialized DB column convention).
/// </summary>
public class ModerationItemProfile : Profile
{
    public ModerationItemProfile()
    {
        CreateMap<ModerationItem, ModerationItemDto>()
            .ConstructUsing(src => new ModerationItemDto(
                src.Id,
                src.SourceEventId,
                src.Source.ToString(),
                src.ContentReference,
                src.StudentId,
                src.TaskKind,
                src.SubjectCode,
                src.Grade,
                src.Status.ToString(),
                src.SafetyVerdict,
                src.DetectedAt,
                src.CreatedAt));

        CreateMap<ModerationItem, ModerationItemDetailDto>()
            .ConstructUsing(src => new ModerationItemDetailDto(
                src.Id,
                src.SourceEventId,
                src.Source.ToString(),
                src.ContentReference,
                src.StudentId,
                src.TaskKind,
                src.SubjectCode,
                src.Grade,
                src.Status.ToString(),
                src.SafetyVerdict,
                src.DetectedAt,
                src.CreatedAt,
                src.ReviewedByUserId,
                src.ReviewedAtUtc,
                src.ReviewDecisionReason));
    }
}
