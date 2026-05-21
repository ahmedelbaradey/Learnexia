namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;

public record GetUserListResponse
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string UserName { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool RegistrationIsCompleted { get; set; }
    public bool RegistrationMessageIsSent { get; set; }
    public List<string> Roles { get; set; } = new();
    public string? PrimaryRole { get; set; }
    public DateTime LastUpdateDate { get; set; }
}
