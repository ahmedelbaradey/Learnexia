namespace Learnexia.Modules.Parent.Application.Features.LinkChild;

/// <summary>
/// Summary of a child the parent is linked to. Returned by the Link-Child command and the My-Children query.
/// </summary>
public record LinkedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;

    /// <summary>
    /// Medium-of-instruction language ("ar"/"en", axis B) for this child's Math &amp; Science. Surfaced so the
    /// parent UI can show the current value and offer the (destructive, fresh-start) change flow (P8-04).
    /// Distinct from the child's UI/PreferredLanguage.
    /// </summary>
    public string LearningLanguage { get; set; } = null!;
}
