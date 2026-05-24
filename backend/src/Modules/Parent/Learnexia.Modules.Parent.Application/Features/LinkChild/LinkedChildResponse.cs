namespace Learnexia.Modules.Parent.Application.Features.LinkChild;

/// <summary>
/// Summary of a child the parent is linked to. Returned by the Link-Child command and the My-Children query.
/// </summary>
public record LinkedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
}
