using System.Text.Json;
using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Attempts.Commands.SubmitAnswer;
using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Mapping;

/// <summary>
/// AutoMapper profile for quiz entities in the Learning module.
/// CorrectAnswer is explicitly IGNORED when mapping QuizQuestion → QuizQuestionDto
/// so it is never exposed to the client.
///
/// P7-04: Added QuizQuestion → AdminQuestionDto mapping which INCLUDES CorrectAnswer.
/// This map is used ONLY by AdminOnly-gated handlers (GetQuestionByIdQueryHandler,
/// ListQuestionsForLessonQueryHandler). The student-facing map (QuizQuestionDto) is unchanged.
///
/// P7-04 READ-PATH FIX (DEF-P7-04-READ-PATH): CorrectAnswer is stored in the DB as a
/// JSON-encoded scalar for non-Matching types (e.g. "\"Paris\""). Before returning it in
/// AdminQuestionDto, we decode it back to the raw scalar ("Paris") so that the admin
/// GET → Edit → PUT round-trip is symmetric: GET returns "Paris", Edit handler re-encodes
/// "Paris" → "\"Paris\"" (single-encode), grading decodes once → correct.
/// Matching is left AS-IS because its CorrectAnswer is a raw JSON object
/// ({"pairs":[...]}) — not a JSON-encoded scalar — and must not be deserialized as a string.
/// </summary>
public class QuizProfile : Profile
{
    public QuizProfile()
    {
        // QuizQuestion → client DTO: CorrectAnswer is intentionally excluded (server-side only).
        // SECURITY: ForSourceMember below is a deliberate control — correct answers must NEVER reach
        // the client. Removing or bypassing this call is a security defect.
        CreateMap<QuizQuestion, QuizQuestionDto>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.QuestionType, opt => opt.MapFrom(src => src.QuestionType))
            .ForMember(dest => dest.QuestionText, opt => opt.MapFrom(src => src.QuestionText))
            .ForMember(dest => dest.Options, opt => opt.MapFrom(src => src.Options))
            .ForSourceMember(src => src.CorrectAnswer, opt => opt.DoNotValidate());

        // P7-04: QuizQuestion → AdminQuestionDto — INCLUDES CorrectAnswer for admin authoring.
        // SECURITY: This map is used ONLY in AdminOnly-gated handlers. Never use it on student routes.
        //
        // DEF-P7-04-READ-PATH fix: Decode CorrectAnswer for non-Matching types so the admin read
        // returns the raw scalar (e.g. "Paris") rather than the JSON-encoded form ("\"Paris\"").
        // Matching CorrectAnswer is a JSON object and is left unchanged.
        // Guard: if the stored value is not a valid JSON-encoded string (defensive), return as-is.
        CreateMap<QuizQuestion, AdminQuestionDto>()
            .ForMember(dest => dest.CorrectAnswer, opt => opt.MapFrom(src =>
                src.QuestionType != QuestionType.Matching
                    ? DecodeJsonScalar(src.CorrectAnswer)
                    : src.CorrectAnswer));

        // SubmitAnswerCommand → StudentAnswer: AnswerPayload, TimeSpentSeconds, HintUsed, AttemptId,
        // QuestionId all map by name convention. IsCorrect is computed server-side in the handler
        // and must not be overwritten by AutoMapper (it is set explicitly after mapping).
        CreateMap<SubmitAnswerCommand, StudentAnswer>()
            .ForMember(dest => dest.IsCorrect, opt => opt.Ignore());

        // Attempt → AttemptSummaryDto: AttemptId maps from Id; Status is stringified by the handler
        // (not by AutoMapper) so handlers always control the string representation.
        // TotalAnswers and CorrectAnswers are NOT on the Attempt entity — handlers fill them explicitly
        // from the freshly-loaded StudentAnswer list after recompute.
        CreateMap<Attempt, AttemptSummaryDto>()
            .ForMember(dest => dest.AttemptId, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.TotalAnswers, opt => opt.Ignore())
            .ForMember(dest => dest.CorrectAnswers, opt => opt.Ignore());
        // Handler fills TotalAnswers + CorrectAnswers explicitly after aggregate recompute.

        // Attempt → AttemptListItemDto: Status enum → string; Id maps by name convention.
        // SECURITY: AttemptListItemDto has no CorrectAnswer field — correct answers are NEVER exposed.
        CreateMap<Attempt, AttemptListItemDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
    }

    /// <summary>
    /// Decodes a JSON-encoded scalar string literal stored in the jsonb CorrectAnswer column.
    /// Mirrors the unwrap logic in <c>AnswerComparator.NormalizeJsonScalar</c> (Domain layer).
    /// Examples: <c>"\"Paris\""</c> → <c>"Paris"</c>; <c>"\"true\""</c> → <c>"true"</c>.
    ///
    /// Used only for non-Matching types (MCQ, TrueFalse, FillInBlank) where CorrectAnswer
    /// is stored as a JSON-string literal wrapping a raw scalar.
    ///
    /// Defensive: if the value is null/empty, or is not a valid JSON-encoded string (i.e. does
    /// not start and end with <c>"</c>), it is returned unchanged rather than throwing.
    /// </summary>
    private static string DecodeJsonScalar(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize<string>(trimmed) ?? trimmed;
            }
            catch (JsonException)
            {
                return trimmed;
            }
        }

        return trimmed;
    }
}
