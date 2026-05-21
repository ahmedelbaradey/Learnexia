namespace Learnexia.Modules.Identity.Application.Features.Users.Dtos;

public record UserProfileResponseDto : BaseUserDto
{
    public string? CVFilePath { get; set; }
    public string? PersonalPhotoPath { get; set; }
    public string? CVFileName { get; set; }
    public string? CountryCode { get; set; }
    public string? PreviewUrl { get; set; }
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool RegistrationIsCompleted { get; set; }
    public bool LegalCouncilHasActiveUser { get; set; }
    public bool FinanceControllerHasActiveUser { get; set; }
    public bool ComplianceLegalManagingDirectorHasActiveUser { get; set; }
    public bool HeadOfRealEstateHasActiveUser { get; set; }
}
