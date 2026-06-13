using Learnexia.Modules.Curriculum.Application.Features.Retrieval.Queries.RetrieveChunks;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// <see cref="ILearningContextProvider"/> + <see cref="ICurriculumContextQuery"/> implementation
/// backed by the P3-07 pgvector RAG retrieval (<see cref="RetrieveChunksQuery"/>).
///
/// <para>Registered in <c>AddCurriculumInfrastructure</c> behind the
/// <c>AiHelper:ContextProvider = "Rag"</c> config key, overriding the Ai module's stub
/// registrations (<c>EmptyLearningContextProvider</c> / <c>EmptyCurriculumContextQuery</c>).</para>
///
/// <para><strong>Config swap (HANDOFF):</strong>
/// Set <c>AiHelper:ContextProvider = "Rag"</c> in appsettings to activate RAG retrieval.
/// When the key is absent or set to any other value, the Ai module's stubs remain active.</para>
///
/// <para><strong>ILearningContextProvider.GetContextAsync:</strong>
/// Adapts <c>(studentId, skillId, questionId?, wrongAnswer?)</c> to a <c>RetrieveChunksQuery</c>.
/// Uses <see cref="IChildLearningProfileQuery"/> to resolve grade + language from the student profile.
/// SubjectId is currently passed as 0 (all-subjects) because no cross-module SkillId→SubjectId seam
/// exists yet; when P3-09 ships the real weak-area seam, this can be tightened.</para>
///
/// <para><strong>ICurriculumContextQuery.GetChunksAsync:</strong>
/// Student-less offline path used by P3-06 batch question-generation.
/// Takes <c>(skillId, gradeId)</c> → retrieves chunks filtered by skill + grade (no SubjectId).
/// Returns <see cref="CurriculumContextChunk"/> list (the <c>ICurriculumContextQuery</c> shape).</para>
///
/// <para><strong>Empty chunks = no context (AC-4):</strong>
/// When <see cref="RetrieveChunksQuery"/> returns an empty list (similarity floor not met or
/// provider unavailable), this provider returns empty <c>Chunks</c> — callers MUST redirect
/// rather than calling the AI gateway with an empty prompt (ai-helper-mvp.md §4).</para>
///
/// <para><strong>PII rule:</strong> never populates <see cref="LearningContext"/> with
/// the student's name, email, birthdate, parent info, or any direct identifier.</para>
/// </summary>
public sealed class RagContextProvider : ILearningContextProvider, ICurriculumContextQuery
{
    private readonly IMediator _mediator;
    private readonly IChildLearningProfileQuery _profileQuery;
    private readonly ILoggerManager _logger;

    public RagContextProvider(
        IMediator mediator,
        IChildLearningProfileQuery profileQuery,
        ILoggerManager logger)
    {
        _mediator     = mediator;
        _profileQuery = profileQuery;
        _logger       = logger;
    }

    // ── ILearningContextProvider ─────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<LearningContext> GetContextAsync(
        int studentId,
        int skillId,
        int? questionId,
        string? wrongAnswer,
        CancellationToken ct = default)
    {
        try
        {
            // Resolve grade and language from the student profile seam (anonymous proxy — PII-safe).
            var profile = await _profileQuery.GetAsync(studentId, ct);
            var gradeId = profile.Grade;

            // Build query text: prefer wrong answer text (most specific for "why wrong" intent),
            // fall back to skill context text.
            var text = BuildQueryText(skillId, wrongAnswer);

            // Dispatch the RAG retrieval query (in-process via MediatR — no HTTP round-trip).
            // SubjectId = 0: no subject filter (cross-module SkillId→SubjectId seam pending P3-09).
            var result = await _mediator.Send(new RetrieveChunksQuery
            {
                Text            = text,
                GradeId         = gradeId,
                SubjectId       = 0,
                WeakAreaSkillId = skillId,
                TopK            = 5,
            }, ct);

            var chunks = result is { Successed: true, Data: not null }
                ? result.Data.Chunks
                    .Select(c => new ChunkDto(c.ChunkId.ToString(), c.Content))
                    .ToList()
                : new List<ChunkDto>();

            var language = profile.Language == TutorLanguage.En
                ? TutorLanguage.En
                : TutorLanguage.Ar;

            return new LearningContext(
                Chunks:       chunks,
                QuestionText: questionId.HasValue ? wrongAnswer : null,
                WrongAnswer:  wrongAnswer,
                SkillId:      skillId,
                QuestionId:   questionId,
                GradeId:      gradeId,
                SubjectId:    0,
                Language:     language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 RagContextProvider.GetContextAsync: unhandled error; returning empty context.");
            // Fail-soft: return empty context so callers apply the refuse-and-redirect rule (AC-4).
            return new LearningContext(
                Chunks:       Array.Empty<ChunkDto>(),
                QuestionText: null,
                WrongAnswer:  wrongAnswer,
                SkillId:      skillId,
                QuestionId:   questionId,
                GradeId:      0,
                SubjectId:    0,
                Language:     TutorLanguage.Ar);
        }
    }

    // ── ICurriculumContextQuery (offline/student-less path for P3-06) ────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CurriculumContextChunk>> GetChunksAsync(
        int skillId,
        int gradeId,
        CancellationToken ct = default)
    {
        try
        {
            var text = BuildQueryText(skillId, wrongAnswer: null);

            var result = await _mediator.Send(new RetrieveChunksQuery
            {
                Text            = text,
                GradeId         = gradeId,
                SubjectId       = 0,
                WeakAreaSkillId = skillId,
                TopK            = 5,
            }, ct);

            if (result is not { Successed: true, Data: not null })
                return Array.Empty<CurriculumContextChunk>();

            return result.Data.Chunks
                .Select(c => new CurriculumContextChunk(c.ChunkId.ToString(), c.Content))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "P3-07 RagContextProvider.GetChunksAsync: unhandled error; returning empty.");
            return Array.Empty<CurriculumContextChunk>();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a query text for the RAG embedding call.
    /// Uses wrong answer text when present (maximally specific for "why wrong" intent),
    /// otherwise constructs a generic skill-search string. Using the skill id as a number
    /// is a placeholder until a SkillName seam (P3-09) is available.
    /// </summary>
    private static string BuildQueryText(int skillId, string? wrongAnswer)
    {
        if (!string.IsNullOrWhiteSpace(wrongAnswer))
            return wrongAnswer;

        return $"skill:{skillId}";
    }
}
