using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.ReEmbed.Commands.TriggerReEmbed;

/// <summary>
/// Admin-triggered command that enqueues the <c>ReEmbedCurriculumJob</c> Hangfire job.
///
/// <para>Returns a <see cref="ReEmbedJobHandle"/> containing the Hangfire job ID so the caller
/// can poll the Hangfire dashboard or log for completion. The job itself is idempotent:
/// re-running it when all rows are already up-to-date is a safe no-op.</para>
///
/// <para><strong>Precondition:</strong> <c>Curriculum:Embedding:BaseUrl</c> and
/// <c>Curriculum:Embedding:ModelVersion</c> must be configured. If the embedding provider
/// is not configured the command returns a failed response (no job enqueued) so the admin
/// receives a clear error instead of silently enqueueing a no-op job.</para>
/// </summary>
public sealed record TriggerReEmbedCommand : ICommand<BaseResponse<ReEmbedJobHandle>>;
