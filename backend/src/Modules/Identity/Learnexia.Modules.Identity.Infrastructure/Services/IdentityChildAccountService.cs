using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Admin;
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
            // UI/UX language (full culture code); defaults to the chosen learning language at creation
            // so the UI starts in the same language as the curriculum — independently editable later
            // via the existing EditUserPreferredLanguage endpoint.
            PreferredLanguage = NormalizeLanguage(req.Language),
            // P8-01: medium-of-instruction language ("ar" or "en"); stored as the short code.
            // Distinct from PreferredLanguage; immutable by the student (parent-only change is P8-04).
            LearningLanguage = req.LearningLanguage,
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
        Country: user.Nationality ?? string.Empty,
        LearningLanguage: user.LearningLanguage);

    private static string NormalizeLanguage(string language) => language?.Trim().ToLowerInvariant() switch
    {
        "en" => "en-US",
        "ar" => "ar-EG",
        _ => "ar-EG",
    };

    public async Task<ChildLanguageChangeResult> ChangeLearningLanguageAsync(
        int childUserId,
        string newLearningLanguage,
        CancellationToken ct = default)
    {
        // Step 1 — Resolve child; missing → NotFound.
        var child = await _identityServiceManager.UserManagmentService.FindByIdAsync(childUserId.ToString());
        if (child is null)
            return new ChildLanguageChangeResult(false, 0, string.Empty, newLearningLanguage, ErrorCode: ChildAccountError.NotFound);

        // Step 2 — Capture old language; same-language request → no-op success (no mutation, no publish).
        var oldLanguage = child.LearningLanguage ?? string.Empty;
        if (oldLanguage == newLearningLanguage)
        {
            _logger.LogInfo($"ChangeLearningLanguageAsync: child {childUserId} already has LearningLanguage='{newLearningLanguage}'. No-op.");
            return new ChildLanguageChangeResult(true, childUserId, oldLanguage, newLearningLanguage, IsNoOp: true);
        }

        // Step 3 — Mutate and commit. Identity has no UoW; UpdateAsync commits immediately.
        child.LearningLanguage = newLearningLanguage;
        child.UpdatedAt = DateTime.UtcNow;

        var result = await _identityServiceManager.UserManagmentService.UpdateAsync(child);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError(null, $"ChangeLearningLanguageAsync UpdateAsync failed for child {childUserId}: {errors}");
            return new ChildLanguageChangeResult(false, childUserId, oldLanguage, newLearningLanguage, ErrorCode: ChildAccountError.LanguageUpdateFailed);
        }

        _logger.LogInfo($"ChangeLearningLanguageAsync: child {childUserId} LearningLanguage changed from '{oldLanguage}' to '{newLearningLanguage}'.");

        // Step 4 — Best-effort post-commit publish (mirrors PublishUserRegisteredEventAsync).
        // A publish failure must NEVER roll back the already-committed User change (ADR 0002 §3).
        await PublishLearningLanguageChangedEventAsync(childUserId, oldLanguage, newLearningLanguage, ct);

        return new ChildLanguageChangeResult(true, childUserId, oldLanguage, newLearningLanguage);
    }

    public async Task<ChildGradeChangeResult> TransitionGradeAsync(
        int childUserId,
        int newGrade,
        int actingParentId,
        CancellationToken ct = default)
    {
        // Step 1 — Resolve child; missing → NotFound.
        var child = await _identityServiceManager.UserManagmentService.FindByIdAsync(childUserId.ToString());
        if (child is null)
            return new ChildGradeChangeResult(false, 0, 0, newGrade, ErrorCode: ChildAccountError.NotFound);

        // Step 1b — Defense-in-depth: only student accounts have a grade. The parent endpoint already
        // constrains the target to a linked student (only students enter the ParentStudent table), but
        // guard the seam too so a future caller can't transition a non-student. Same NotFound result →
        // the handler maps it to the generic Forbidden (anti-enumeration). Mirrors the admin
        // OverrideChildGrade role check (Roles.Student via GetUserRolesAsync).
        var roles = await _identityServiceManager.UserManagmentService.GetUserRolesAsync(child);
        if (!roles.Any(r => string.Equals(r, Roles.Student.ToString(), StringComparison.OrdinalIgnoreCase)))
            return new ChildGradeChangeResult(false, 0, 0, newGrade, ErrorCode: ChildAccountError.NotFound);

        // Step 2 — Capture old grade; same-grade request → no-op success (no mutation, no publish).
        var oldGrade = child.Grade ?? 0;
        if (oldGrade == newGrade)
        {
            _logger.LogInfo($"TransitionGradeAsync: child {childUserId} already has Grade={newGrade}. No-op.");
            return new ChildGradeChangeResult(true, childUserId, oldGrade, newGrade, IsNoOp: true);
        }

        // Step 3 — Mutate and commit. Identity has no UoW; UpdateAsync commits immediately.
        child.Grade = newGrade;
        child.UpdatedAt = DateTime.UtcNow;
        child.UpdatedBy = actingParentId;

        var result = await _identityServiceManager.UserManagmentService.UpdateAsync(child);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogError(null, $"TransitionGradeAsync UpdateAsync failed for child {childUserId}: {errors}");
            return new ChildGradeChangeResult(false, childUserId, oldGrade, newGrade, ErrorCode: ChildAccountError.GradeUpdateFailed);
        }

        _logger.LogInfo($"TransitionGradeAsync: child {childUserId} Grade changed from {oldGrade} to {newGrade} by parent {actingParentId}.");

        // Step 4 — Best-effort post-commit publish ChildGradeChangedIntegrationEvent.
        // A publish failure must NEVER roll back the already-committed User change.
        // The field ChangedByAdminUserId carries the opaque acting-user id (here: parent).
        await PublishGradeChangedEventAsync(childUserId, oldGrade, newGrade, actingParentId, ct);

        // Step 5 — Best-effort post-commit publish AdminActionPerformedEvent (audit sink).
        await PublishGradeTransitionAuditEventAsync(actingParentId, childUserId, oldGrade, newGrade, ct);

        return new ChildGradeChangeResult(true, childUserId, oldGrade, newGrade);
    }

    private async Task PublishGradeChangedEventAsync(
        int childId, int oldGrade, int newGrade, int actingParentId, CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = new ChildGradeChangedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                ChildUserId: childId,
                OldGrade: oldGrade,
                NewGrade: newGrade,
                ChangedByAdminUserId: actingParentId);

            await _publisher.Publish(integrationEvent, cancellationToken);
            _logger.LogInfo($"Published ChildGradeChangedIntegrationEvent for child {childId} ({oldGrade} → {newGrade}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"Failed to publish ChildGradeChangedIntegrationEvent for child {childId} " +
                $"({oldGrade} → {newGrade}) — ignored (best-effort). Grade change is committed.");
        }
    }

    private async Task PublishGradeTransitionAuditEventAsync(
        int actingParentId, int childId, int oldGrade, int newGrade, CancellationToken cancellationToken)
    {
        try
        {
            var auditEvent = new AdminActionPerformedEvent(
                EventId: Guid.NewGuid(),
                OccurredAtUtc: DateTime.UtcNow,
                AdminUserId: actingParentId,
                Action: AdminActions.ChildGradeTransitioned,
                TargetEntityType: "User",
                TargetEntityId: childId,
                Details: $"oldGrade={oldGrade};newGrade={newGrade}");

            await _publisher.Publish(auditEvent, cancellationToken);
            _logger.LogInfo($"Published AdminActionPerformedEvent(ChildGradeTransitioned) for child {childId} by parent {actingParentId}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish AdminActionPerformedEvent(ChildGradeTransitioned) for child {childId} — ignored (best-effort).");
        }
    }

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

    private async Task PublishLearningLanguageChangedEventAsync(
        int studentId,
        string oldLanguage,
        string newLanguage,
        CancellationToken cancellationToken)
    {
        try
        {
            var integrationEvent = new LearningLanguageChangedIntegrationEvent(
                EventId: Guid.NewGuid(),
                OccurredOnUtc: DateTime.UtcNow,
                StudentId: studentId,
                OldLanguage: oldLanguage,
                NewLanguage: newLanguage);

            await _publisher.Publish(integrationEvent, cancellationToken);
            _logger.LogInfo($"Published LearningLanguageChangedIntegrationEvent for student {studentId} ({oldLanguage} → {newLanguage}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to publish LearningLanguageChangedIntegrationEvent for student {studentId}. Change is committed; reset may be missed.");
        }
    }
}
