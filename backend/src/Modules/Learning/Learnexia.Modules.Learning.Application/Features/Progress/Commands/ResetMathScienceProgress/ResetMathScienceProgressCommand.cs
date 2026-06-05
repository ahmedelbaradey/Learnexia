using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Progress.Commands.ResetMathScienceProgress;

/// <summary>
/// Internal Learning-module command dispatched by <c>LearningLanguageChangedIntegrationEventHandler</c>
/// to delete all Math/Science <c>Attempt</c> rows for a student.
///
/// Running through MediatR ensures the delete commits via Learning's <c>UnitOfWorkBehavior</c>
/// (ADR 0001) — the handler must NOT call <c>SaveChangesAsync</c> directly.
///
/// <c>StudentAnswer</c> rows cascade from <c>Attempt</c> via <c>DeleteBehavior.Cascade</c>
/// (configured in <c>StudentAnswerConfig</c>) — no explicit StudentAnswer delete is needed.
///
/// Naturally idempotent: deleting an already-empty set is a no-op with no error.
/// </summary>
/// <param name="StudentId">Id of the student whose Math/Science Attempt rows are to be reset.</param>
/// <param name="OriginEventId">
/// The <c>LearningLanguageChangedIntegrationEvent.EventId</c> that triggered this reset.
/// Carried for observability/deduplication; not stored in this MVP (Outbox deferred, ADR 0002 §5).
/// </param>
public record ResetMathScienceProgressCommand(int StudentId, Guid OriginEventId)
    : ICommand<BaseResponse<bool>>;
