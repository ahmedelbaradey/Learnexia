using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IKnowledgeNodeReader"/> inside the Learning module's Infrastructure layer.
/// Called by the Curriculum module (via the Shared.Contracts seam) to read KnowledgeNode rows
/// without any direct project reference from Curriculum to Learning.
///
/// <para><strong>Usage (BL-03):</strong>
/// <list type="bullet">
///   <item><see cref="GetNodesForSubjectAsync"/>: feeds <c>BuildKnowledgeGraphSuggestionsCommand</c>.
///         Joins KnowledgeNode → Subject via <c>SubjectId</c> to filter on SubjectCode + GradeId.</item>
///   <item><see cref="GetNodesBySkillKeysAsync"/>: feeds <c>EdgeInferenceAdvanceService</c> to
///         resolve Python-emitted SkillKeys → KnowledgeNode.Ids. Returns only nodes whose SkillKey
///         is in the provided set.</item>
/// </list>
/// </para>
///
/// <para>BL-03 seam-impl.</para>
/// </summary>
public sealed class KnowledgeNodeReaderAdapter : IKnowledgeNodeReader
{
    private readonly LearningDbContext _db;
    private readonly ILoggerManager _logger;

    public KnowledgeNodeReaderAdapter(
        LearningDbContext db,
        ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeNodeSummaryDto>> GetNodesForSubjectAsync(
        int subjectCode,
        int gradeId,
        CancellationToken ct = default)
    {
        // KnowledgeNode.SubjectId is an FK to Subject.Id (not SubjectCode directly).
        // We must join to Subject to filter on (SubjectCode, GradeId).
        // AsNoTracking — read-only feed for inference, no change tracking needed.
        var subjectCodeEnum = (SubjectCode)subjectCode;

        var rows = await _db.KnowledgeNodes
            .AsNoTracking()
            .Join(
                _db.Subjects.AsNoTracking(),
                node => node.SubjectId,
                subject => subject.Id,
                (node, subject) => new { node, subject })
            .Join(
                _db.Grades.AsNoTracking(),
                x => x.node.GradeId,
                grade => grade.Id,
                (x, grade) => new { x.node, x.subject, grade })
            .Where(x => x.subject.SubjectCode == subjectCodeEnum && x.node.GradeId == gradeId)
            .Select(x => new KnowledgeNodeSummaryDto(
                x.node.Id,
                x.node.SkillKey,
                x.node.Name,
                (int)x.node.NodeType,
                (int)x.subject.SubjectCode,
                x.node.GradeId,
                x.grade.Number,
                x.node.Difficulty))
            .ToListAsync(ct);

        _logger.LogInfo(
            $"KnowledgeNodeReaderAdapter: GetNodesForSubjectAsync subjectCode={subjectCode} " +
            $"gradeId={gradeId} → {rows.Count} nodes.");

        return rows;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeNodeSummaryDto>> GetNodesBySkillKeysAsync(
        IEnumerable<string> skillKeys,
        CancellationToken ct = default)
    {
        var keyList = skillKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keyList.Count == 0)
            return Array.Empty<KnowledgeNodeSummaryDto>();

        var rows = await _db.KnowledgeNodes
            .AsNoTracking()
            .Join(
                _db.Subjects.AsNoTracking(),
                node => node.SubjectId,
                subject => subject.Id,
                (node, subject) => new { node, subject })
            .Join(
                _db.Grades.AsNoTracking(),
                x => x.node.GradeId,
                grade => grade.Id,
                (x, grade) => new { x.node, x.subject, grade })
            .Where(x => x.node.SkillKey != null && keyList.Contains(x.node.SkillKey))
            .Select(x => new KnowledgeNodeSummaryDto(
                x.node.Id,
                x.node.SkillKey,
                x.node.Name,
                (int)x.node.NodeType,
                (int)x.subject.SubjectCode,
                x.node.GradeId,
                x.grade.Number,
                x.node.Difficulty))
            .ToListAsync(ct);

        _logger.LogInfo(
            $"KnowledgeNodeReaderAdapter: GetNodesBySkillKeysAsync keys={keyList.Count} " +
            $"→ {rows.Count} resolved.");

        return rows;
    }
}
