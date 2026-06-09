using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminSearchUsers;

public class SearchUsersQueryHandler : BaseResponseHandler, IQueryHandler<SearchUsersQuery, PaginatedResult<AdminUserListItemDto>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    private const int MaxPageSize = 100;

    public SearchUsersQueryHandler(
        IIdentityServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _publisher = publisher;
    }

    public async Task<PaginatedResult<AdminUserListItemDto>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // ── Input guards (queries are NOT auto-validated — guard in-handler) ──
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            var pageSize = request.PageSize < 1 ? 20
                : request.PageSize > MaxPageSize ? MaxPageSize
                : request.PageSize;

            // ── Base query: exclude hard-deleted (IsDeleted=true) rows ──
            // Keep as IQueryable throughout so all filters, ordering, and pagination
            // are pushed to the database (Finding #2 — no in-memory full-table load).
            var users = _service.UserManagmentService.UsersWithRoles();
            users = users.Where(u => u.IsDeleted != true);

            // ── Free-text filter: FullName OR Email pushed to DB (EF-translatable Contains) ──
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                var q = request.Q.Trim();
                users = users.Where(u =>
                    u.FullName.Contains(q) ||
                    (u.Email != null && u.Email.Contains(q)));
            }

            // ── AccountStatus filter (P7-07 upgrade from IsActive flag) ──
            // When a specific status is requested, filter to exactly that status.
            // When null, exclude Deleted accounts (admin search defaults to non-deleted).
            if (request.Status.HasValue)
            {
                var status = request.Status.Value;
                users = users.Where(u => u.AccountStatus == status);
            }
            else
            {
                users = users.Where(u => u.AccountStatus != AccountStatus.Deleted);
            }

            // ── Role filter: resolve user ids in role then apply as a DB-side Contains ──
            // GetUsersByRole is async (UserManager.GetUsersInRoleAsync), so we resolve the
            // id list once and push the filter into the IQueryable before materialisation.
            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                var usersInRole = await _service.UserManagmentService.GetUsersByRole(request.Role);
                var roleIds = usersInRole.Select(u => u.Id).ToList();
                users = users.Where(u => roleIds.Contains(u.Id));
            }

            // ── DB-side count and pagination — no in-memory materialisation ──
            // Sort is hard-coded to FullName; asc/desc inferred from request.OrderBy.
            // request.OrderBy is NOT interpolated into dynamic LINQ (Finding #5 prevention).
            var descending = !string.IsNullOrWhiteSpace(request.OrderBy) &&
                             request.OrderBy.Contains("desc", StringComparison.OrdinalIgnoreCase);

            var orderedUsers = descending
                ? users.OrderByDescending(u => u.FullName)
                : users.OrderBy(u => u.FullName);

            var total = orderedUsers.Count();

            if (total == 0)
                return PaginatedResult<AdminUserListItemDto>.EmptyCollection(
                    _localizer[SharedResourcesKey.NoRecords]);

            var paged = orderedUsers
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var dtos = paged.Select(MapToListItem).ToList();
            var result = PaginatedResult<AdminUserListItemDto>.Success(dtos, total, pageNumber, pageSize);

            // ── Audit emit: publish AdminActionPerformedEvent best-effort post-read ──
            // Finding #3/#9 — no raw search term (can be a child's name/email) in audit Details.
            // Use q-length and role/status identifiers only; no PII.
            var adminUserId = _currentUserService.UserId ?? 0;
            var qLen = string.IsNullOrWhiteSpace(request.Q) ? 0 : request.Q.Trim().Length;
            _ = PublishAuditEventAsync(adminUserId, AdminActions.UserSearched, 0,
                $"q-length={qLen};role={request.Role};status={request.Status}", CancellationToken.None);

            _logger.LogInfo($"Admin user search completed: {total} result(s), page {pageNumber}/{pageSize}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchUsersQueryHandler");
            return PaginatedResult<AdminUserListItemDto>.ServerError(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private static AdminUserListItemDto MapToListItem(User u)
        => new()
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? string.Empty,
            Role = u.Roles.Select(r => r.Name).FirstOrDefault(),
            AccountStatus = u.AccountStatus,
            CreatedAt = u.CreatedAt,
        };

    private async Task PublishAuditEventAsync(
        int adminUserId,
        string action,
        int targetId,
        string? details,
        CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    adminUserId,
                    action,
                    "User",
                    targetId,
                    details),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AdminActionPerformedEvent (best-effort, ignored)");
        }
    }
}
