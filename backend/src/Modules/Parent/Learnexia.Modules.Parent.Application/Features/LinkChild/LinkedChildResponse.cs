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

    // Editable profile fields (P1-12-FE edit-child) — surfaced so the parent edit form can PRE-FILL the
    // child's real values instead of stubs (a blank pre-fill would let a save silently overwrite them).
    /// <summary>Grade level (1–6), or null if unset.</summary>
    public int? Grade { get; set; }

    /// <summary>UI/account language as the short code ("ar"/"en") the LanguageSelect uses — derived from the
    /// stored culture code so the edit form round-trips cleanly (BE NormalizeLanguage only accepts short codes).</summary>
    public string Language { get; set; } = null!;

    /// <summary>Country/nationality code (echoes the stored Nationality).</summary>
    public string Country { get; set; } = null!;
}
