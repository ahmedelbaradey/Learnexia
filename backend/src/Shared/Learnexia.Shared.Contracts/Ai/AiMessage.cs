namespace Learnexia.Shared.Contracts.Ai;

/// <summary>A single turn in a multi-turn conversation history.</summary>
public sealed record AiMessage(AiMessageRole Role, string Content);

/// <summary>Role of a message in a conversation turn.</summary>
public enum AiMessageRole
{
    User,
    Assistant,
    System,
}
