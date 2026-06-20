using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ITimedEventParticipationService"/>.
/// Provides the student-facing read path for the BE-8 query endpoint.
/// Delegates to the Scoped <see cref="IStudentTimedEventParticipationQuery"/> cross-module seam.
/// </summary>
internal sealed class TimedEventParticipationService
    : BaseResponseHandler, ITimedEventParticipationService
{
    private readonly IStudentTimedEventParticipationQuery _query;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public TimedEventParticipationService(
        IStudentTimedEventParticipationQuery query,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _query     = query;
        _logger    = logger;
        _localizer = localizer;
    }

    /// <inheritdoc />
    public async Task<BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>> GetActiveParticipationsAsync(
        int studentId, CancellationToken ct = default)
    {
        try
        {
            var snapshots = await _query.GetActiveByStudentIdAsync(studentId, ct);
            var response = Success(snapshots);
            response.Message = _localizer[SharedResourcesKey.TimedEventParticipationsRetrievedSuccessfully].Value;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-12: Error in TimedEventParticipationService.GetActiveParticipationsAsync " +
                $"for studentId={studentId}.");
            return ServerError<IReadOnlyList<TimedEventParticipationSnapshot>>();
        }
    }
}
