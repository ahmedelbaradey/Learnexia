namespace Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;

/// <summary>
/// Data carried by SubmitAnswerCommand. AttemptId is injected from the route by the controller;
/// the remaining fields come from the request body. StudentId is NEVER in this DTO — it is resolved
/// from the authenticated JWT via ICurrentUserService in the handler.
/// </summary>
public record SubmitAnswerDto
{
    /// <summary>Injected from route by the controller — NOT from the request body.</summary>
    public int AttemptId { get; set; }

    public int QuestionId { get; set; }

    /// <summary>Serialized student-supplied answer; shape matches QuizQuestion.QuestionType.</summary>
    public string AnswerPayload { get; set; } = null!;

    /// <summary>Client-reported seconds spent on this question. Advisory only (client-supplied).</summary>
    public int TimeSpentSeconds { get; set; }

    public bool HintUsed { get; set; }
}
