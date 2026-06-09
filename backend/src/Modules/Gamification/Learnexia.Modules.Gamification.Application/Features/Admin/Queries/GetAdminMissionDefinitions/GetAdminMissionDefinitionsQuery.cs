using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminMissionDefinitions;

/// <summary>Admin query: list all <c>MissionDefinition</c> rows including inactive ones.</summary>
public sealed record GetAdminMissionDefinitionsQuery : IQuery<BaseResponse<List<MissionDefinitionDto>>>;
