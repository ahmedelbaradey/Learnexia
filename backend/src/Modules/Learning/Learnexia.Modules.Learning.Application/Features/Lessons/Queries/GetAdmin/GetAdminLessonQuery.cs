using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.GetAdmin;

/// <summary>
/// Admin-only lesson detail query. Returns <see cref="AdminLessonDetailDto"/> which includes:
/// - IsActive, EstimatedMinutes (admin-only fields).
/// - All non-deleted ContentBlocks (including inactive), ordered by SequenceOrder.
/// </summary>
public record GetAdminLessonQuery : IQuery<BaseResponse<AdminLessonDetailDto>>
{
    public int Id { get; init; }
}
