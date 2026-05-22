namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.AddChild;

/// <summary>
/// The child profile returned after a successful Add-Child call. Distinct from
/// <c>LinkedChildResponse</c> (the P1-04 My-Children contract) so that surfacing grade/language/
/// country here does not change that contract. <c>Language</c> echoes the stored
/// <c>PreferredLanguage</c> culture code; <c>Country</c> echoes <c>Nationality</c>.
/// </summary>
public record AddedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Grade { get; set; }
    public string Language { get; set; } = null!;
    public string Country { get; set; } = null!;
}
