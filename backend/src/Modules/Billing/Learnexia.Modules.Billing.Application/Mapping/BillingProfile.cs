using AutoMapper;

namespace Learnexia.Modules.Billing.Application.Mapping;

/// <summary>
/// AutoMapper profile for the Billing module.
/// CreditAccount → CreditAccountDto mapping removed (CreditAccount retired — see P10-13).
/// </summary>
public class BillingProfile : Profile
{
    public BillingProfile()
    {
        // All active mappings are handled by the handler/service layer directly (no domain→DTO map needed).
    }
}
