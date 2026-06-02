using AutoMapper;

namespace Learnexia.Modules.Learning.Application.Mapping;

/// <summary>
/// AutoMapper profile for P2-09 Dashboard feature.
/// The handler builds DashboardDto and ContinueTargetDto by hand (no direct entity→DTO mapping
/// because the aggregation spans multiple entities). This profile exists for module convention
/// consistency. Phase-4 handlers (P4-02/P4-06/P4-07) may add maps here.
/// </summary>
public class DashboardProfile : Profile
{
    public DashboardProfile()
    {
        // No active maps in this profile — Phase-3 dashboard fields (XP/streak/hearts/badges/
        // missions/leagues) are populated via cross-module Shared.Contracts query seams in
        // GetDashboardQueryHandler. AutoMapper is reserved for any future intra-module mappings.
    }
}
