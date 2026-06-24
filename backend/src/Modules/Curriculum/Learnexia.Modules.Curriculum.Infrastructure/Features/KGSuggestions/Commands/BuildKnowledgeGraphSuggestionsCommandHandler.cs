using System.Text.Json;
using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Commands.BuildKGSuggestions;
using Learnexia.Modules.Curriculum.Domain.Entities;
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
/// Handles <see cref="BuildKnowledgeGraphSuggestionsCommand"/> (BL-03-BE-3).
///
/// <para>Enqueues an <c>infer_edges</c> PipelineJob so the Python worker can infer
/// prerequisite/related edge candidates from the KnowledgeNodes for the given SubjectCode+GradeId.
/// </para>
///
/// <para><strong>Decision E invariant:</strong> this handler NEVER writes <c>KGSuggestion</c>
/// or <c>KnowledgeEdge</c>. Only the Python worker + <c>EdgeInferenceAdvanceService</c>
/// produce suggestions; only admin approval publishes to <c>KnowledgeEdge</c>.</para>
///
/// <para>Admin-only. No Unit of Work — direct DbContext save (no multi-entity txn needed).</para>
/// </summary>
public sealed class BuildKnowledgeGraphSuggestionsCommandHandler
    : BaseResponseHandler,
      ICommandHandler<BuildKnowledgeGraphSuggestionsCommand, BaseResponse<string>>
{
    private readonly CurriculumDbContext _db;
    private readonly IKnowledgeNodeReader _nodeReader;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BuildKnowledgeGraphSuggestionsCommandHandler(
        CurriculumDbContext db,
        IKnowledgeNodeReader nodeReader,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db         = db;
        _nodeReader = nodeReader;
        _logger     = logger;
        _localizer  = localizer;
    }

    public async Task<BaseResponse<string>> Handle(
        BuildKnowledgeGraphSuggestionsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // ── Step 1: Load learning nodes for this subject+grade via the cross-module read seam ──
            var nodes = await _nodeReader.GetNodesForSubjectAsync(
                request.SubjectCode,
                request.GradeId,
                cancellationToken);

            // Require at least one node with a SkillKey (Python identifies edges by SkillKey).
            var nodesWithSkillKey = nodes.Where(n => !string.IsNullOrWhiteSpace(n.SkillKey)).ToList();
            if (nodesWithSkillKey.Count == 0)
            {
                _logger.LogInfo(
                    $"BuildKnowledgeGraphSuggestions: no nodes with SkillKey found for " +
                    $"subjectCode={request.SubjectCode} gradeId={request.GradeId}. Skipping enqueue.");
                return BadRequest<string>(_localizer[SharedResourcesKey.KGBuildNoNodesWithSkillKey]);
            }

            // ── Step 2: Serialize node list as PayloadJson (keyed by SkillKey) ───────────────────
            // Python InferPayload.parse reads per-node:
            //   skill_key (str), title (str), node_type (str e.g. "Skill"/"Concept"/"Review"),
            //   subject_code (str lowercase e.g. "math"), grade (int — grade NUMBER), difficulty (int).
            // DEFECT-BL03-1 fixed: was "name", now "title"
            // DEFECT-BL03-2 fixed: node_type emitted as string enum name (not int)
            // DEFECT-BL03-2 fixed: subject_code emitted per-node as lowercase string (not int, not top-level only)
            // DEFECT-BL03-3 fixed: grade emitted per-node as grade NUMBER (not top-level grade_id FK)
            var payloadJson = JsonSerializer.Serialize(new
            {
                subject_code = request.SubjectCode,
                grade_id     = request.GradeId,
                nodes        = nodesWithSkillKey.Select(n => new
                {
                    node_id      = n.NodeId,
                    skill_key    = n.SkillKey,
                    title        = n.Name,
                    node_type    = MapNodeTypeToString(n.NodeType),
                    subject_code = MapSubjectCodeToString(n.SubjectCode),
                    grade        = n.GradeNumber,
                    difficulty   = n.Difficulty,
                }).ToList(),
            });

            // ── Step 3: Enqueue the infer_edges job ──────────────────────────────────────────────
            // JobType='infer_edges' is a new string value on the existing PipelineJobs table —
            // no schema migration needed (JobType is varchar per ADR-0004 §2 string contract).
            var job = new PipelineJob
            {
                JobType     = "infer_edges",
                Status      = "Pending",
                PayloadJson = payloadJson,
                RetryCount  = 0,
            };

            _db.PipelineJobs.Add(job);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInfo(
                $"BuildKnowledgeGraphSuggestions: enqueued infer_edges job id={job.Id} " +
                $"subjectCode={request.SubjectCode} gradeId={request.GradeId} " +
                $"nodeCount={nodesWithSkillKey.Count}.");

            var response = Success<string>(
                _localizer[SharedResourcesKey.KGBuildJobEnqueued]);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in BuildKnowledgeGraphSuggestionsCommand");
            return ServerError<string>();
        }
    }

    // ── Cross-process string mapping helpers ─────────────────────────────────────────────────────
    // These map Learning-module int enums to the strings Python InferPayload.parse expects.
    // We cannot reference Learning.Domain enums here (module isolation), so we use int constants.
    // Mapping is frozen: Learning.SubjectCode (0=Math,1=Science,2=Arabic,3=English) and
    // Learning.KnowledgeNodeType (0=Skill,1=Concept,2=Review).

    private static string MapSubjectCodeToString(int subjectCode) => subjectCode switch
    {
        0 => "math",
        1 => "science",
        2 => "arabic",
        3 => "english",
        _ => subjectCode.ToString(),
    };

    private static string MapNodeTypeToString(int nodeType) => nodeType switch
    {
        0 => "Skill",
        1 => "Concept",
        2 => "Review",
        _ => nodeType.ToString(),
    };
}
