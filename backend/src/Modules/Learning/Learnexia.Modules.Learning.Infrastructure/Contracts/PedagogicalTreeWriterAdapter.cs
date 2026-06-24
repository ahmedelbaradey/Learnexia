using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.EntityFrameworkCore;
using KnowledgeNodeTypeEnum = Learnexia.Modules.Learning.Domain.Enums.KnowledgeNodeType;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IPedagogicalTreeWriter"/> inside the Learning module's Infrastructure layer.
/// Called by the Curriculum ingest-advance service (via the Shared.Contracts seam) to upsert
/// pedagogy-tree nodes without any direct project reference from Curriculum to Learning.
///
/// <para><strong>Idempotency:</strong> each EnsureXAsync mirrors the exact upsert logic from
/// <c>LearningSeeder.EnsureXAsync</c> — natural-key lookup, return existing Id on hit, create and
/// return new Id on miss.  Running ingest twice MUST NOT duplicate nodes or clobber P2-10 demo data.</para>
///
/// <para><strong>SkillKey immutability contract:</strong> if a matching Skill already has a non-null
/// SkillKey, it is left unchanged; if it has null, the supplied key is applied.  New skills always
/// get the supplied key.  This preserves the invariant documented on <c>KnowledgeNode.SkillKey</c>.</para>
///
/// <para><strong>System user id:</strong> seeder rows use <c>SystemUserId = 0</c> (same convention as
/// <c>LearningSeeder</c>); ingest-authored rows also use 0 because there is no HTTP user identity in
/// the background-service context.</para>
///
/// <para>BL-05 seam-impl.</para>
/// </summary>
public sealed class PedagogicalTreeWriterAdapter : IPedagogicalTreeWriter
{
    private readonly LearningDbContext _db;
    private readonly ILoggerManager _logger;

    private const int SystemUserId = 0;

    public PedagogicalTreeWriterAdapter(
        LearningDbContext db,
        ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── EnsureSubjectAsync ─────────────────────────────────────────────────────────────────────────

    public async Task<int> EnsureSubjectAsync(
        int gradeId,
        int subjectCode,
        int language,
        string displayName,
        CancellationToken ct = default)
    {
        // Idempotency key: (GradeId, SubjectCode, Language) — mirrors LearningSeeder.EnsureSubjectAsync.
        var existing = await _db.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.GradeId      == gradeId &&
                (int)s.SubjectCode == subjectCode &&
                (int)s.Language    == language,
                ct);

        if (existing is not null)
            return existing.Id;

        var subject = new Subject
        {
            Name           = displayName,
            GradeId        = gradeId,
            SubjectCode    = (SubjectCode)subjectCode,
            Language       = (ContentLanguage)language,
            LifecycleState = LifecycleState.Published,
            IsActive       = true,
        };
        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(SystemUserId);

        _logger.LogInfo(
            $"PedagogicalTreeWriter: created Subject id={subject.Id} gradeId={gradeId} " +
            $"subjectCode={subjectCode} language={language}.");

        return subject.Id;
    }

    // ── EnsureUnitAsync ─────────────────────────────────────────────────────────────────────────────

    public async Task<int> EnsureUnitAsync(
        string name,
        int subjectId,
        int sequenceOrder,
        CancellationToken ct = default)
    {
        // Idempotency key: (Name, SubjectId) — mirrors LearningSeeder.EnsureUnitAsync.
        var existing = await _db.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == name && u.SubjectId == subjectId, ct);

        if (existing is not null)
            return existing.Id;

        var unit = new Unit
        {
            Name           = name,
            SequenceOrder  = sequenceOrder,
            SubjectId      = subjectId,
            LifecycleState = LifecycleState.Published,
            IsActive       = true,
        };
        _db.Units.Add(unit);
        await _db.SaveChangesAsync(SystemUserId);

        _logger.LogInfo(
            $"PedagogicalTreeWriter: created Unit id={unit.Id} name='{name}' subjectId={subjectId}.");

        return unit.Id;
    }

    // ── EnsureLessonAsync ──────────────────────────────────────────────────────────────────────────

    public async Task<int> EnsureLessonAsync(
        string name,
        int unitId,
        int sequenceOrder,
        CancellationToken ct = default)
    {
        // Idempotency key: (Name, UnitId) — mirrors LearningSeeder.EnsureLessonAsync.
        var existing = await _db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Name == name && l.UnitId == unitId, ct);

        if (existing is not null)
            return existing.Id;

        var lesson = new Lesson
        {
            Name           = name,
            SequenceOrder  = sequenceOrder,
            UnitId         = unitId,
            Difficulty     = DifficultyLevel.Medium,
            IsLocked       = false,
            LifecycleState = LifecycleState.Published,
            IsActive       = true,
        };
        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync(SystemUserId);

        _logger.LogInfo(
            $"PedagogicalTreeWriter: created Lesson id={lesson.Id} name='{name}' unitId={unitId}.");

        return lesson.Id;
    }

    // ── EnsureConceptAsync ─────────────────────────────────────────────────────────────────────────

    public async Task<int> EnsureConceptAsync(
        string name,
        int subjectId,
        string? description,
        int difficulty,
        CancellationToken ct = default)
    {
        // Idempotency key: (Name, SubjectId) — mirrors LearningSeeder.EnsureConceptAsync.
        var existing = await _db.Concepts
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == name && c.SubjectId == subjectId, ct);

        if (existing is not null)
            return existing.Id;

        var concept = new Concept
        {
            Name            = name,
            Description     = description,
            DifficultyLevel = (DifficultyLevel)difficulty,
            SubjectId       = subjectId,
        };
        _db.Concepts.Add(concept);
        await _db.SaveChangesAsync(SystemUserId);

        _logger.LogInfo(
            $"PedagogicalTreeWriter: created Concept id={concept.Id} name='{name}' subjectId={subjectId}.");

        return concept.Id;
    }

    // ── EnsureSkillAsync ───────────────────────────────────────────────────────────────────────────

    public async Task<EnsureSkillResult> EnsureSkillAsync(
        string name,
        int conceptId,
        string skillKey,
        int masteryThreshold,
        int estimatedTimeMinutes,
        int subjectId,
        int gradeId,
        int difficulty,
        CancellationToken ct = default)
    {
        // ── Skill upsert (idempotency key: Name + ConceptId) ──────────────────────────────────────
        // Mirrors LearningSeeder.EnsureSkillAsync. SkillKey immutability contract:
        // - New skill → set SkillKey at creation.
        // - Existing skill, null SkillKey → apply the supplied key.
        // - Existing skill, non-null SkillKey → leave unchanged (immutability).

        var existingSkill = await _db.Skills
            .FirstOrDefaultAsync(sk => sk.Name == name && sk.ConceptId == conceptId, ct);

        int skillId;

        if (existingSkill is null)
        {
            var skill = new Skill
            {
                Name                 = name,
                ConceptId            = conceptId,
                MasteryThreshold     = masteryThreshold,
                EstimatedTimeMinutes = estimatedTimeMinutes,
                IsActive             = true,
            };
            _db.Skills.Add(skill);
            await _db.SaveChangesAsync(SystemUserId);
            skillId = skill.Id;

            _logger.LogInfo(
                $"PedagogicalTreeWriter: created Skill id={skillId} name='{name}' conceptId={conceptId} skillKey='{skillKey}'.");
        }
        else
        {
            skillId = existingSkill.Id;
        }

        // ── KnowledgeNode upsert (SkillKey-anchored idempotency) ──────────────────────────────────
        // Try to find the KnowledgeNode by SkillKey first (stable identity across re-ingest).
        // Fallback: look up by SkillId (for nodes created by the seeder before SkillKey was added).

        int knowledgeNodeId = 0;
        try
        {
            var existingNode = !string.IsNullOrWhiteSpace(skillKey)
                ? await _db.KnowledgeNodes
                    .FirstOrDefaultAsync(kn => kn.SkillKey == skillKey, ct)
                : null;

            existingNode ??= await _db.KnowledgeNodes
                .FirstOrDefaultAsync(kn => kn.SkillId == skillId, ct);

            if (existingNode is null)
            {
                var node = new KnowledgeNode
                {
                    Name       = name,
                    NodeType   = KnowledgeNodeTypeEnum.Skill,
                    SubjectId  = subjectId,
                    GradeId    = gradeId,
                    Difficulty = difficulty,
                    SkillId    = skillId,
                    SkillKey   = skillKey,
                };
                _db.KnowledgeNodes.Add(node);
                await _db.SaveChangesAsync(SystemUserId);
                knowledgeNodeId = node.Id;

                _logger.LogInfo(
                    $"PedagogicalTreeWriter: created KnowledgeNode id={knowledgeNodeId} " +
                    $"skillId={skillId} skillKey='{skillKey}'.");
            }
            else
            {
                knowledgeNodeId = existingNode.Id;

                // Apply SkillKey if the existing node has none (backfill).
                if (existingNode.SkillKey is null && !string.IsNullOrWhiteSpace(skillKey))
                {
                    existingNode.SkillKey = skillKey;
                    await _db.SaveChangesAsync(SystemUserId);

                    _logger.LogInfo(
                        $"PedagogicalTreeWriter: backfilled SkillKey='{skillKey}' on KnowledgeNode id={knowledgeNodeId}.");
                }
            }
        }
        catch (Exception ex)
        {
            // KnowledgeNode creation is best-effort at ingest time — if it fails, log and continue.
            // The Skill was successfully written; the curriculum chunk can still be linked to the skill.
            // The KG edge (BL-03) can be re-inferred once the node is corrected.
            _logger.LogError(ex,
                $"PedagogicalTreeWriter: KnowledgeNode upsert failed for skillId={skillId} " +
                $"skillKey='{skillKey}'. Chunk will be written without a KnowledgeNodeId.");
        }

        return new EnsureSkillResult(skillId, knowledgeNodeId);
    }
}
