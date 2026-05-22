namespace Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;

/// <summary>
/// Summary of a child the parent is linked to. Returned by the Link-Child command and the
/// My-Children query. Fields are limited to those owned by the Identity module's <c>User</c> —
/// grade is a Curriculum-module concept and is intentionally NOT surfaced here (module isolation;
/// no cross-module FK). A grade-aware projection belongs to a Curriculum-facing endpoint later.
/// </summary>
public record LinkedChildResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
}
