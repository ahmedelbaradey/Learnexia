namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.UpdateChild;

/// <summary>
/// The updated child profile returned after a successful Update-Child call so the frontend list can
/// refresh in place. Mirrors <c>AddedChildResponse</c>: <c>Language</c> echoes the stored
/// <c>PreferredLanguage</c> culture code; <c>Country</c> echoes <c>Nationality</c>.
/// </summary>
public record UpdatedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Grade { get; set; }
    public string Language { get; set; } = null!;
    public string Country { get; set; } = null!;
}
