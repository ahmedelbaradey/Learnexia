using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.List;

public class ListQueryHandler : BaseResponseHandler, IQueryHandler<ListQuery, PaginatedResult<GetUserListResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;

    public ListQueryHandler(
        IIdentityServiceManager service,
        IMapper mapper,
        ILoggerManager logger,
        IIdentityServiceManager identityServiceManager,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService)
    {
        _mapper = mapper;
        _service = service;
        _logger = logger;
        _identityServiceManager = identityServiceManager;
        _localizer = localizer;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedResult<GetUserListResponse>> Handle(ListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var users = _service.UserManagmentService.UsersWithRoles().AsQueryable();

            int currentUserId = _currentUserService.UserId ?? 0;
            users = users.Where(u => u.Id != currentUserId && u.IsDeleted != true);

            users = await ApplyFiltersAsync(users, request);
            if (!users.Any())
                return PaginatedResult<GetUserListResponse>.EmptyCollection(_localizer[SharedResourcesKey.NoRecords]);

            if (!string.IsNullOrEmpty(request.OrderBy) && request.OrderBy.Contains("roles"))
                return await HandleRoleBasedSorting(users, request);

            var paginatedList = await _mapper.ProjectTo<GetUserListResponse>(users).ToPaginatedListAsync(request.PageNumber, request.PageSize, request.OrderBy);

            _logger.LogInfo($"Successfully retrieved {paginatedList.Data.Count} users with localized roles using optimized query");

            return paginatedList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Enhanced User List Query");
            return PaginatedResult<GetUserListResponse>.ServerError(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private async Task<IQueryable<User>> ApplyFiltersAsync(IQueryable<User> users, ListQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
            users = users.Where(u => u.UserName != null && u.UserName.Contains(request.Search));

        if (!string.IsNullOrWhiteSpace(request.Name))
            users = users.Where(u => u.FullName.Contains(request.Name));

        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value)
                users = users.Where(u => u.IsActive);
            else
                users = users.Where(u => !u.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var usersInRole = await _identityServiceManager.UserManagmentService.GetUsersByRole(request.Role);
            var userIdsInRole = usersInRole.Select(u => u.Id).ToList();
            users = users.Where(u => userIdsInRole.Contains(u.Id));
        }

        return users;
    }

    private Task<PaginatedResult<GetUserListResponse>> HandleRoleBasedSorting(IQueryable<User> users, ListQuery request)
    {
        var usersList = users.ToList();
        var userDtos = _mapper.Map<List<GetUserListResponse>>(usersList);

        userDtos = request.OrderBy!.Contains("asc")
            ? userDtos.OrderBy(u => u.PrimaryRole ?? string.Empty).ToList()
            : userDtos.OrderByDescending(u => u.PrimaryRole ?? string.Empty).ToList();

        var totalCount = userDtos.Count;
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 200 : request.PageSize;

        var pagedUsers = userDtos
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(PaginatedResult<GetUserListResponse>.Success(pagedUsers, totalCount, pageNumber, pageSize));
    }
}
