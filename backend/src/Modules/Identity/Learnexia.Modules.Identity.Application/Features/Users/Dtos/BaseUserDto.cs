using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos;

public record BaseUserDto : BaseDto
{
    public string Email { get; set; } = null!;
    public string? IBAN { get; set; }
    public string? Nationality { get; set; }
    public string? PassportNo { get; set; }
    public string FullName { get; set; } = null!;
    public string UserName { get; set; } = null!;
}
