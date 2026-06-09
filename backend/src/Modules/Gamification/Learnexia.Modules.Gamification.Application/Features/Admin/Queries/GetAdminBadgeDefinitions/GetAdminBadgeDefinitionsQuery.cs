using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminBadgeDefinitions;

/// <summary>Admin query: list all <c>BadgeDefinition</c> rows including inactive ones.</summary>
public sealed record GetAdminBadgeDefinitionsQuery : IQuery<BaseResponse<List<BadgeDefinitionDto>>>;
