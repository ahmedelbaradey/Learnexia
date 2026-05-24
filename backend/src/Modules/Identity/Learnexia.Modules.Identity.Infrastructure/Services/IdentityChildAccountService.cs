using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

/// <summary>
/// Identity-side implementation of the cross-module <see cref="IChildAccountService"/> seam
/// (Shared.Contracts). Encapsulates the child-account Identity logic that previously lived in
/// Identity's <c>AddChildCommandHandler</c>, so the Parent module can provision/read/update child
/// <c>User</c> accounts without referencing Identity. Results are SHAPED — raw Identity error
/// descriptions are never returned across the boundary (logged server-side only). Registered at the
/// Host as <c>AddScoped</c> (UserManager is scoped), mirroring the IUserLookup registration pattern.
/// </summary>
public sealed class IdentityChildAccountService : IChildAccountService
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly ILoggerManager _logger;
    private readonly IPublisher _publisher;

    public IdentityChildAccountService(
        IIdentityServiceManager identityServiceManager,
        ILoggerManager logger,
        IPublisher publisher)
    {
        _identityServiceManager = identityServiceManager;
        _logger = logger;
        _publisher = publisher;
    }

    public async Task<ChildAccountResult> CreateChildAsync(CreateChildRequest req, CancellationToken ct = default)
    {
        // Race-safe backstop duplicate-email check (UserName == Email for the child).
        var existingUser = await _identityServiceManager.UserManagmentService.FindByEmailAsync(req.Email);
        if (existingUser != null)
            return new ChildAccountResult(false, 0, ChildAccountError.DuplicateEmail);

        var child = new User
        {
            UserName = req.Email,
            Email = req.Email,
            FullName = req.FullName,
            PreferredLanguage = NormalizeLanguage(req.Language),
            Nationality = req.Country,
            Grade = req.Grade,
            RegistrationIsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = req.ActingParentId,
        };

        // Identity hashes the parent-set password and persists the child immediately (no Unit of Work).
        var result = await _identityServiceManager.UserManagmentService.CreateAsync(child, req.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError(null, $"CreateChildAsync CreateAsync failed for parent {req.ActingParentId}: {errors}");
            return new ChildAccountResult(false, 0, ChildAccountError.CreateFailed);
        }

        // Role is server-assigned — always Student. Fail closed if assignment fails: there is no Unit of
        // Work, so delete the just-created child to avoid an orphaned roleless minor account.
        var roleResult = await _identityServiceManager.UserManagmentService.AddToRoleAsync(child, Roles.Student.ToString());
        if (!roleResult.Succeeded)
        {
            var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
            _logger.LogError(null, $"CreateChildAsync AddToRoleAsync failed for parent {req.ActingParentId}, child {child.Id}: {roleErrors}");

            var deleteResult = await _identityServiceManager.UserManagmentService.DeleteAsync(child);
            if (!deleteResult.Succeeded)
            {
                var deleteErrors = string.Join(", ", deleteResult.Errors.Select(e => e.Description));
                _logger.LogError(null, $"CreateChildAsync compensating DeleteAsync failed for parent {req.ActingParentId}, child {child.Id}: {deleteErrors}");
            }

            return new ChildAccountResult(false, 0, ChildAccountError.RoleAssignFailed);
        }

        // Best-effort cross-module fan-out; a publish failure must not affect the committed child.
        await PublishUserRegisteredEventAsync(child, ct);

        _logger.LogInfo($"CreateChildAsync created child {child.Id} for parent {req.ActingParentId}.");
        return new ChildAccountResult(true, child.Id, ChildAccountError.None, ToProfile(child));
    }

    public async Task<IReadOnlyList<ChildProfile>> GetChildrenAsync(IReadOnlyList<int> childIds, CancellationToken ct = default)
    {
        if (childIds is null || childIds.Count == 0)
            return Array.Empty<ChildProfile>();

        var ids = childIds.ToHashSet();
        var users = _identityServiceManager.UserManagmentService.Users()
            .Where(u => ids.Contains(u.Id))
            .ToList();

        return users.Select(ToProfile).ToList();
    }

    public async Task<ChildAccountResult> UpdateChildAsync(UpdateChildRequest req, CancellationToken ct = default)
    {
        var child = await _identityServiceManager.UserManagmentService.FindByIdAsync(req.ChildUserId.ToString());
        if (child is null)
            return new ChildAccountResult(false, 0, ChildAccountError.NotFound);

        // Mutate ONLY the editable fields — no email/login/role/id change (no mass-assignment).
        child.FullName = req.FullName;
        child.Grade = req.Grade;
        child.PreferredLanguage = NormalizeLanguage(req.Language);
        child.Nationality = req.Country;
        child.UpdatedAt = DateTime.UtcNow;

        var result = await _identityServiceManager.UserManagmentService.UpdateAsync(child);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError(null, $"UpdateChildAsync UpdateAsync failed for child {req.ChildUserId}: {errors}");
            return new ChildAccountResult(false, child.Id, ChildAccountError.UpdateFailed);
        }

        _logger.LogInfo($"UpdateChildAsync updated child {child.Id}.");
        return new ChildAccountResult(true, child.Id, ChildAccountError.None, ToProfile(child));
    }

    public async Task<LinkableChild?> FindLinkableChildByEmailAsync(string email, CancellationToken ct = default)
    {
        var child = await _identityServiceManager.UserManagmentService.FindByEmailAsync(email);
        if (child is null)
            return null;

        var roles = await _identityServiceManager.UserManagmentService.GetUserRolesAsync(child);
        var isStudent = roles.Any(r => string.Equals(r, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase));

        return new LinkableChild(child.Id, isStudent, child.CreatedBy);
    }

    private static ChildProfile ToProfile(User user) => new(
        Id: user.Id,
        FullName: user.FullName,
        Email: user.Email ?? string.Empty,
        Grade: user.Grade,
        Language: user.PreferredLanguage,
        Country: user.Nationality ?? string.Empty);

    private static string NormalizeLanguage(string language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" => "en-US",
        "ar" => "ar-EG",
        _ => "ar-EG",
    };

    private async Task PublishUserRegisteredEventAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = new UserRegisteredIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                UserId: user.Id,
                UserName: user.UserName ?? string.Empty);

            await _publisher.Publish(integrationEvent, cancellationToken);
            _logger.LogInfo($"Published UserRegisteredIntegrationEvent for user {user.Id}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish UserRegisteredIntegrationEvent for user {user.Id}.");
        }
    }
}
