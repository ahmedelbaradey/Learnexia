using AutoMapper;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Domain.Entities;

namespace Learnexia.Modules.Billing.Application.Mapping;

public class BillingProfile : Profile
{
    public BillingProfile()
    {
        // CreditAccount → DTO (read model).
        CreateMap<CreditAccount, CreditAccountDto>()
            .ForMember(d => d.TotalBalance, opt => opt.MapFrom(s => s.TotalBalance));
    }
}
