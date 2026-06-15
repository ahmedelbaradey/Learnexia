using Hangfire;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Application.Features.ReEmbed.Commands.TriggerReEmbed;
using Learnexia.Modules.Curriculum.Infrastructure.Jobs;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.ReEmbed.Commands.TriggerReEmbed;

/// <summary>
/// Handles <see cref="TriggerReEmbedCommand"/> — enqueues <see cref="ReEmbedCurriculumJob"/>
/// as a Hangfire background job and returns the job ID for the admin to track.
///
/// <para><strong>Precondition check (WI-A2 graceful degrade):</strong>
/// If the embedding provider is not configured (BaseUrl or ModelVersion absent), returns a
/// <c>BadRequest</c> with a localized error message — does NOT enqueue the job.
/// This prevents the admin from triggering a job that will immediately fail all TEI calls.</para>
///
/// <para>The actual re-embed logic lives in <see cref="ReEmbedCurriculumJob.RunAsync"/>.
/// This handler only enqueues the job and returns the Hangfire job ID.</para>
/// </summary>
public sealed class TriggerReEmbedCommandHandler
    : BaseResponseHandler, ICommandHandler<TriggerReEmbedCommand, BaseResponse<ReEmbedJobHandle>>
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public TriggerReEmbedCommandHandler(
        IEmbeddingProvider embeddingProvider,
        IBackgroundJobClient backgroundJobClient,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _embeddingProvider   = embeddingProvider;
        _backgroundJobClient = backgroundJobClient;
        _logger              = logger;
        _localizer           = localizer;
    }

    public Task<BaseResponse<ReEmbedJobHandle>> Handle(
        TriggerReEmbedCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // WI-A2 graceful degrade: refuse to enqueue if TEI is not configured.
            // A job enqueued without a working provider would process zero rows and leave
            // the admin confused. Return a clear error so they know what to fix first.
            if (_embeddingProvider is not BgeM3EmbeddingProvider bgeProvider || !bgeProvider.IsConfigured)
            {
                _logger.LogWarn(
                    "P3-07 TriggerReEmbedCommand: embedding endpoint not configured. " +
                    "Admin must set Curriculum:Embedding:BaseUrl + ModelVersion before triggering re-embed.");
                return Task.FromResult(
                    BadRequest<ReEmbedJobHandle>(_localizer[SharedResourcesKey.ReEmbedEndpointNotConfigured]));
            }

            // Enqueue the Hangfire job. IBackgroundJobClient.Enqueue returns the Hangfire job ID.
            var jobId = _backgroundJobClient.Enqueue<ReEmbedCurriculumJob>(
                job => job.RunAsync(CancellationToken.None));

            _logger.LogInfo(
                $"P3-07 TriggerReEmbedCommand: ReEmbedCurriculumJob enqueued with jobId={jobId}.");

            return Task.FromResult(
                Success(new ReEmbedJobHandle(
                    JobId:   jobId,
                    Message: _localizer[SharedResourcesKey.ReEmbedJobEnqueued].Value)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 TriggerReEmbedCommand: failed to enqueue ReEmbedCurriculumJob.");
            return Task.FromResult(ServerError<ReEmbedJobHandle>());
        }
    }
}
