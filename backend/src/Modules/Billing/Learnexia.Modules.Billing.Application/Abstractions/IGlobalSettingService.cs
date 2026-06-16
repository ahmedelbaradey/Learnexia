using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Dtos;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for admin GlobalSetting read and write operations.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, post-commit event staging, and cache-invalidation logic lives in the
/// Infrastructure implementation. Handlers stay EF-free.</para>
///
/// <para><strong>Post-commit ordering:</strong> <see cref="UpdateAsync"/> publishes
/// <c>AdminActionPerformedEvent</c> AFTER the commit (best-effort — audit failure must never
/// roll back the setting update). Cache invalidation via <c>ISettingsCacheInvalidator</c>
/// happens inside the implementation after a successful commit.</para>
/// </summary>
public interface IGlobalSettingService
{
    /// <summary>
    /// Updates the value of a managed <c>GlobalSetting</c> row.
    ///
    /// <para>Steps (all inside the implementation):
    /// <list type="number">
    ///   <item>Load the row by <paramref name="key"/>.</item>
    ///   <item>Call <c>row.Update(newValue, updatedBy, now)</c>.</item>
    ///   <item>Save in an explicit transaction and commit.</item>
    ///   <item>Publish <c>AdminActionPerformedEvent</c> post-commit (best-effort).</item>
    ///   <item>Invalidate the in-memory cache for <paramref name="key"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="key">Setting key (must be in the managed-keys allowlist; validated upstream).</param>
    /// <param name="newValue">The new value string.</param>
    /// <param name="adminUserId">Admin user id for audit (from JWT).</param>
    /// <param name="updatedBy">Admin display name / email for audit (from JWT).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<GlobalSettingUpdateResult> UpdateAsync(
        string key,
        string newValue,
        int adminUserId,
        string updatedBy,
        CancellationToken ct);

    /// <summary>
    /// Returns all managed <c>GlobalSetting</c> rows (filtered to <c>GlobalSettingKeys.ManagedKeys</c>),
    /// ordered by key.
    /// </summary>
    Task<IReadOnlyList<GlobalSettingDto>> GetAllManagedAsync(CancellationToken ct);
}

/// <summary>Result of <see cref="IGlobalSettingService.UpdateAsync"/>.</summary>
public sealed record GlobalSettingUpdateResult
{
    public bool Succeeded { get; init; }
    public GlobalSettingUpdateFailureReason? FailureReason { get; init; }

    public static GlobalSettingUpdateResult Ok()
        => new() { Succeeded = true };

    public static GlobalSettingUpdateResult Fail(GlobalSettingUpdateFailureReason reason)
        => new() { Succeeded = false, FailureReason = reason };
}

/// <summary>Failure reasons for <see cref="IGlobalSettingService.UpdateAsync"/>.</summary>
public enum GlobalSettingUpdateFailureReason
{
    /// <summary>No row with the given key exists in the database.</summary>
    NotFound = 1,
}

/// <summary>
/// Lightweight validation seam (Option C) for cross-key GlobalSetting consistency checks.
///
/// <para>Used by <c>UpdateGlobalSettingValidator</c> to read related setting values directly
/// from DB (bypassing the possibly-stale in-memory cache during batch updates).
/// Application-layer validators inject THIS interface — not <c>IBillingDbContext</c>.</para>
/// </summary>
public interface IGlobalSettingValidationService
{
    /// <summary>
    /// Returns the raw string value of a setting row, or <c>null</c> if the row does not exist.
    /// Direct DB read — bypasses the in-memory cache.
    /// </summary>
    Task<string?> GetRawValueAsync(string key, CancellationToken ct);
}
