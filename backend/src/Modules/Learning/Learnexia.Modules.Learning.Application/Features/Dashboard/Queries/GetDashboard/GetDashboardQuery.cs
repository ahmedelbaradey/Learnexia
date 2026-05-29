using Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Queries.GetDashboard;

/// <summary>
/// Returns the home dashboard for the JWT-resolved student.
/// Parameterless from the caller's point of view — the handler reads _currentUser.UserId.
/// No ValidationBehavior applied (CLAUDE.md rule 4 — queries are not auto-validated).
/// </summary>
public record GetDashboardQuery : IQuery<BaseResponse<DashboardDto>>;
