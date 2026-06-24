using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.RejectKGSuggestion;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.KGSuggestions.Commands;

/// <summary>
/// Handles <see cref="RejectKGSuggestionCommand"/> (BL-03-BE-9).
///
/// <para>Flow:
/// <list type="number">
///   <item>Load suggestion (404 if missing).</item>
///   <item>Guard: must be Pending (409 if already resolved).</item>
///   <item>Stamp as Rejected + ReviewedByUserId + ReviewedAt.</item>
///   <item>Commit in an explicit transaction.</item>
/// </list>
/// </para>
///
/// <para>No <c>KnowledgeEdge</c> is written on reject — Decision E invariant holds.</para>
/// <para>No Unit of Work — explicit transaction (CLAUDE.md rule 3).</para>
/// </summary>
public sealed class RejectKGSuggestionCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RejectKGSuggestionCommand, BaseResponse<KGSuggestionDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RejectKGSuggestionCommandHandler(
        CurriculumDbContext db,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
        _currentUserService = currentUserService;
        _mapper             = mapper;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<KGSuggestionDto>> Handle(
        RejectKGSuggestionCommand request,
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

            // ── Step 3: Stamp as Rejected ────────────────────────────────────────────────────────
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            suggestion.Status           = KGSuggestionStatus.Rejected;
            suggestion.ReviewedByUserId = userId.Value;
            suggestion.ReviewedAt       = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(userId.Value);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-03 RejectKGSuggestion: suggestion id={suggestion.Id} rejected by user={userId}.");

            var response = Success(_mapper.Map<KGSuggestionDto>(suggestion));
            response.Message = _localizer[SharedResourcesKey.KGSuggestionRejected];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RejectKGSuggestionCommand");
            return ServerError<KGSuggestionDto>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }
}
