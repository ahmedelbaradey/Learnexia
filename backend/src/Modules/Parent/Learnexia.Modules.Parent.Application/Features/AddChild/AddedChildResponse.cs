namespace Learnexia.Modules.Parent.Application.Features.AddChild;

/// <summary>The child profile returned after a successful Add-Child call.</summary>
public record AddedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public int? Grade { get; set; }
    public string Language { get; set; } = null!;
    public string Country { get; set; } = null!;
}
