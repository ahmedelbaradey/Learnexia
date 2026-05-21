namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;

public class SingleHolderRoleAvailabilityResponse
{
    public bool LegalCouncilHasActiveUser { get; set; }
    public bool FinanceControllerHasActiveUser { get; set; }
    public bool ComplianceLegalManagingDirectorHasActiveUser { get; set; }
    public bool HeadOfRealEstateHasActiveUser { get; set; }
}
