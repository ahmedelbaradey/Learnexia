using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ClearFlag;
using Learnexia.Modules.Learning.Application.Features.Calibration.Commands.ConfirmFlag;
using Learnexia.Modules.Learning.Application.Features.Calibration.Commands.PromoteProposal;
using Learnexia.Modules.Learning.Application.Features.Calibration.Commands.RejectProposal;
using Learnexia.Modules.Learning.Application.Features.Calibration.Dtos;
using Learnexia.Modules.Learning.Application.Features.Calibration.Queries.ListFlagged;
using Learnexia.Modules.Learning.Application.Features.Calibration.Queries.ListProposals;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// AdminOnly endpoints for reviewing, promoting, and rejecting calibration proposals,
/// and managing the quality flag on individual questions (P5-07 BE-5).
///
/// Route: api/Learning/Calibration
///
/// All actions are <c>[Authorize(Policy = AdminOnly)]</c> — no student/parent can
/// list proposals, promote, reject, unflag, or confirm a flag.
///
/// Response envelope: <see cref="BaseResponse{T}"/> via <see cref="NewResult{T}"/>.
/// Reason codes (ReasonCode / FlagReason) are localized to the request culture
/// via the handlers; stable codes are also returned for programmatic filtering.
/// </summary>
[Route("api/Learning/[controller]")]
[ApiController]
public class CalibrationController : AppControllerBase
{
    // ── BE-5: Proposals ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paged list of recalibration proposals, optionally filtered by status.
    /// GET /api/Learning/Calibration/Proposals?status=1&amp;page=1&amp;pageSize=20
    ///
    /// Status values: 1=Pending, 2=Promoted, 3=Rejected. Omit to return all.
    /// </summary>
    [HttpGet("Proposals")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<CalibrationProposalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProposals([FromQuery] ListProposalsQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Promotes a recalibration proposal — applies the proposed difficulty to the question.
    /// This is the ONE human-driven authored-band change (LOCKED safety model).
    /// POST /api/Learning/Calibration/Proposals/{id}/Promote
    /// </summary>
    [HttpPost("Proposals/{id:int}/Promote")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Promote(int id)
        => NewResult(await Mediator.Send(new PromoteProposalCommand { ProposalId = id }));

    /// <summary>
    /// Rejects a recalibration proposal — closes without changing the authored band.
    /// POST /api/Learning/Calibration/Proposals/{id}/Reject
    /// </summary>
    [HttpPost("Proposals/{id:int}/Reject")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(int id)
        => NewResult(await Mediator.Send(new RejectProposalCommand { ProposalId = id }));

    // ── BE-5: Flagged questions ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paged list of flagged questions (<c>QualityState == FlaggedForReview</c>).
    /// Includes evidence p-value from the stats row and localized flag-reason string.
    /// GET /api/Learning/Calibration/Flagged?page=1&amp;pageSize=20
    /// </summary>
    [HttpGet("Flagged")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<PaginatedResult<FlaggedQuestionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFlagged([FromQuery] ListFlaggedQuery query)
        => NewResult(await Mediator.Send(query));

    /// <summary>
    /// Clears the quality flag on a question — sets <c>QualityState = Approved</c> and
    /// re-enables it for auto-serve. Audited via <c>AdminActionPerformedDomainEvent</c>.
    /// POST /api/Learning/Calibration/Flagged/{questionId}/Clear
    /// </summary>
    [HttpPost("Flagged/{questionId:int}/Clear")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearFlag(int questionId)
        => NewResult(await Mediator.Send(new ClearFlagCommand { QuestionId = questionId }));

    /// <summary>
    /// Confirms the quality flag on a question — admin acknowledges the flag and keeps the
    /// question excluded from auto-serve. Audited via <c>AdminActionPerformedDomainEvent</c>.
    /// POST /api/Learning/Calibration/Flagged/{questionId}/Confirm
    /// </summary>
    [HttpPost("Flagged/{questionId:int}/Confirm")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmFlag(int questionId)
        => NewResult(await Mediator.Send(new ConfirmFlagCommand { QuestionId = questionId }));
}
