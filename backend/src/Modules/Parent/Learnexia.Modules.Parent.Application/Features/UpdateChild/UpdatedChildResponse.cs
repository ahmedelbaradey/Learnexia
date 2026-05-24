namespace Learnexia.Modules.Parent.Application.Features.UpdateChild;

/// <summary>The updated child profile returned after a successful Update-Child call.</summary>
public record UpdatedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Grade { get; set; }
    public string Language { get; set; } = null!;
    public string Country { get; set; } = null!;
}
