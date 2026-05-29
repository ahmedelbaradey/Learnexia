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
        // No active maps in Phase 2 — hand-projection in GetDashboardQueryHandler.
        // TODO P4-02: add XP-ledger → DashboardDto.Xp map here.
        // TODO P4-06: add Mission → DailyMissionDto map here.
        // TODO P4-07: add League → LeaguePreviewDto map here.
    }
}
