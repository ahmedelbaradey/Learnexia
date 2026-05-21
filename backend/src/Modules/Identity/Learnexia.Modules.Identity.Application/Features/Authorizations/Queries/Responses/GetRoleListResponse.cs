namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;

public record GetRoleListResponse
{
    public int Id { get; set; }
    public string roleName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
