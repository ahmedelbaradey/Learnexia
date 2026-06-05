namespace Learnexia.Modules.Parent.Application.Features.ChangeChildLearningLanguage;

/// <summary>The updated learning language returned after a successful Change-Learning-Language call.</summary>
public record ChangedLearningLanguageResponse
{
    public int ChildId { get; set; }
    public string NewLearningLanguage { get; set; } = null!;
}
