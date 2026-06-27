using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.ApproveKGSuggestion;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.KGSuggestions.Commands;

/// <summary>
/// Handles <see cref="ApproveKGSuggestionCommand"/> (BL-03-BE-8).
///
/// <para><strong>THE Decision-E single-write-path:</strong>
/// The ONLY code path that publishes an inferred suggestion to <c>learning.KnowledgeEdge</c>.
/// Mirrors <see cref="Learnexia.Modules.Curriculum.Infrastructure.Features.IngestionReview.Commands.ApproveIngestionReviewItemCommandHandler"/>
/// exactly: load → guard → open tx → seam call → stamp → commit.
/// </para>
///
/// <para>Flow:
/// <list type="number">
///   <item>Load suggestion (404 if missing).</item>
///   <item>Guard: must be Pending (409 if already resolved).</item>
///   <item>Open explicit transaction.</item>
///   <item>Call <see cref="IKnowledgeEdgeWriter.PublishApprovedEdgeAsync"/> — runs all guards
///         (cross-language, duplicate, acyclic) inside the learning module.</item>
///   <item>Map <see cref="EdgePublishResult"/> to outcome:
///         <list type="bullet">
///           <item>Published=true or Duplicate=true → stamp Approved + commit.</item>
///           <item>Cycle=true → 422 domain error, rollback.</item>
///           <item>CrossLanguage=true → 422 domain error, rollback.</item>
///         </list>
///   </item>
///   <item>Commit (edge publish + Approved stamp are atomic).</item>
/// </list>
/// </para>
///
/// <para>No Unit of Work — explicit transaction (CLAUDE.md rule 3).</para>
/// </summary>
public sealed class ApproveKGSuggestionCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ApproveKGSuggestionCommand, BaseResponse<KGSuggestionDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IKnowledgeEdgeWriter _edgeWriter;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ApproveKGSuggestionCommandHandler(
        CurriculumDbContext db,
        ICurrentUserService currentUserService,
        IKnowledgeEdgeWriter edgeWriter,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
        _currentUserService = currentUserService;
        _edgeWriter         = edgeWriter;
        _mapper             = mapper;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<KGSuggestionDto>> Handle(
        ApproveKGSuggestionCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<KGSuggestionDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── Step 1: Load suggestion ──────────────────────────────────────────────────────────
            var suggestion = await _db.KGSuggestions
                .FirstOrDefaultAsync(s => s.Id == request.SuggestionId, cancellationToken);

            if (suggestion is null)
                return NotFound<KGSuggestionDto>(
                    _localizer[SharedResourcesKey.KGSuggestionNotFound]);

            // ── Step 2: Guard — must be Pending ──────────────────────────────────────────────────
            if (suggestion.Status != KGSuggestionStatus.Pending)
                return Conflict<KGSuggestionDto>(
                    _localizer[SharedResourcesKey.KGSuggestionAlreadyResolved]);

            // ── Step 3: Publish edge AND stamp Approved in a single atomic transaction ───────────
            // Finding #3 equivalent: both succeed or neither does.
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Call the cross-module edge-publish seam.
            // The seam runs cross-language + duplicate + acyclic checks inside Learning.
            var publishResult = await _edgeWriter.PublishApprovedEdgeAsync(
                suggestion.SourceNodeId,
                suggestion.TargetNodeId,
                (int)suggestion.RelationshipType,
                suggestion.Strength,
                cancellationToken);

            if (publishResult.Cycle)
            {
                // Rollback transaction — no edge written, suggestion stays Pending.
                await tx.RollbackAsync(cancellationToken);
                _logger.LogInfo(
                    $"ApproveKGSuggestion: cycle detected for suggestion id={suggestion.Id}; " +
                    "publish rejected, suggestion remains Pending.");
                return UnprocessableEntity<KGSuggestionDto>(
                    _localizer[SharedResourcesKey.KnowledgeEdgeWouldCreateCycle]);
            }

            if (publishResult.CrossLanguage)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogInfo(
                    $"ApproveKGSuggestion: cross-language edge rejected for suggestion id={suggestion.Id}; " +
                    "suggestion remains Pending.");
                return UnprocessableEntity<KGSuggestionDto>(
                    _localizer[SharedResourcesKey.KnowledgeEdgeCrossLanguageForbidden]);
            }

            if (publishResult.NodeMissing)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogInfo(
                    $"ApproveKGSuggestion: one or both nodes missing for suggestion id={suggestion.Id} " +
                    $"(SourceNodeId={suggestion.SourceNodeId}, TargetNodeId={suggestion.TargetNodeId}); " +
                    "suggestion remains Pending.");
                return UnprocessableEntity<KGSuggestionDto>(
                    _localizer[SharedResourcesKey.KGSuggestionPublishNodeMissing]);
            }

            // Published=true or Duplicate=true → success (Duplicate = edge already exists, treat as no-op).
            suggestion.Status           = KGSuggestionStatus.Approved;
            suggestion.ReviewedByUserId = userId.Value;
            suggestion.ReviewedAt       = DateTimeOffset.UtcNow;
            // Note: ReviewNotes not on KGSuggestion entity — stored as part of suggestion context only.

            await _db.SaveChangesAsync(userId.Value);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-03 ApproveKGSuggestion: suggestion id={suggestion.Id} approved by user={userId}. " +
                $"EdgeId={publishResult.EdgeId} Duplicate={publishResult.Duplicate}.");

            var response = Success(_mapper.Map<KGSuggestionDto>(suggestion));
            response.Message = _localizer[SharedResourcesKey.KGSuggestionApproved];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ApproveKGSuggestionCommand");
            return ServerError<KGSuggestionDto>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }
}
