using Learnexia.Modules.Learning.Api.Bases;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.RollbackToVersion;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Commands.TransitionLifecycle;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetPreview;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetPublicationCoverage;
using Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetVersionHistory;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Learning.Api.Controllers;

/// <summary>
/// Admin lifecycle/publishing controller for curriculum content (P7-05).
///
/// All endpoints are AdminOnly. Students MUST NOT be able to reach any endpoint here.
///
/// Routes follow the pattern: POST /api/learning/ContentLifecycle/Transition
/// matching the module's established [Route("api/learning/[controller]")] convention.
///
/// Deliverables wired here:
///   POST   Transition          — publish / archive / unpublish a curriculum entity
///   POST   Rollback            — roll back to a previous ContentVersion
///   GET    VersionHistory      — list published versions for an entity
///   GET    Preview             — admin preview of the current (possibly Draft) entity state
///   GET    PublicationCoverage — per-(SubjectCode,Language) publish state for a grade
/// </summary>
[Route("api/learning/[controller]")]
[ApiController]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class ContentLifecycleController : AppControllerBase
{
    /// <summary>
    /// Transitions a curriculum entity's LifecycleState.
    ///
    /// Allowed transitions:
    ///   Draft → Published (creates a ContentVersion snapshot)
    ///   Published → Archived
    ///   Published → Draft (unpublish)
    ///   Archived → Draft (restore)
    ///
    /// Returns 400 for illegal transitions (e.g. Archived → Published directly).
    ///
    /// POST /api/learning/ContentLifecycle/Transition
    /// Body: { "entityType": 3, "entityId": 42, "targetState": 2 }
    /// </summary>
    [HttpPost("Transition")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Transition([FromBody] TransitionLifecycleCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Rolls back a curriculum entity to a previous published ContentVersion.
    ///
    /// Restores the editorial fields from the snapshot and re-publishes the entity
    /// (LifecycleState = Published). Creates a new ContentVersion row to record the rollback.
    ///
    /// POST /api/learning/ContentLifecycle/Rollback
    /// Body: { "entityType": 3, "entityId": 42, "versionNumber": 2 }
    /// </summary>
    [HttpPost("Rollback")]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollback([FromBody] RollbackToVersionCommand command)
        => NewResult(await Mediator.Send(command));

    /// <summary>
    /// Returns the version history for a curriculum entity (newest first).
    ///
    /// GET /api/learning/ContentLifecycle/VersionHistory?entityType=3&entityId=42
    /// </summary>
    [HttpGet("VersionHistory")]
    [ProducesResponseType(typeof(BaseResponse<List<ContentVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersionHistory(
        [FromQuery] VersionedEntityType entityType,
        [FromQuery] int entityId)
        => NewResult(await Mediator.Send(new GetVersionHistoryQuery { EntityType = entityType, EntityId = entityId }));

    /// <summary>
    /// Returns an admin preview of the current (possibly Draft) state of a curriculum entity.
    ///
    /// GET /api/learning/ContentLifecycle/Preview?entityType=3&entityId=42
    /// </summary>
    [HttpGet("Preview")]
    [ProducesResponseType(typeof(BaseResponse<PreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<PreviewDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview(
        [FromQuery] VersionedEntityType entityType,
        [FromQuery] int entityId)
        => NewResult(await Mediator.Send(new GetPreviewQuery { EntityType = entityType, EntityId = entityId }));

    /// <summary>
    /// Returns publication coverage for all (SubjectCode, Language) slots in a grade.
    ///
    /// Shows which trees are Published (live to students) vs Draft/Archived.
    ///
    /// GET /api/learning/ContentLifecycle/PublicationCoverage?gradeId=1
    /// </summary>
    [HttpGet("PublicationCoverage")]
    [ProducesResponseType(typeof(BaseResponse<PublicationCoverageReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicationCoverage([FromQuery] int gradeId)
        => NewResult(await Mediator.Send(new GetPublicationCoverageQuery { GradeId = gradeId }));
}
