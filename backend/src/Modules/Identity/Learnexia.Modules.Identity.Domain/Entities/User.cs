using System.ComponentModel.DataAnnotations.Schema;
using Learnexia.Modules.Identity.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Domain.Entities;

public class User : IdentityUser<int>
{
    public string FullName { get; set; } = default!;
    public string PreferredLanguage { get; set; } = "ar-EG";
    /// <summary>
    /// Medium-of-instruction language for the child's curriculum ("ar" or "en").
    /// Distinct from <see cref="PreferredLanguage"/> (the UI/UX language).
    /// Set by the parent at onboarding; immutable by the student (parent-only change in P8-04).
    /// </summary>
    public string LearningLanguage { get; set; } = "ar";
    public string CountryCode { get; set; } = "+20";
    
    public string? Nationality { get; set; }
    public int? Grade { get; set; }
    public int? Age { get; set; }
    public string? PersonalPhotoPath { get; set; }
    /// <summary>
    /// Public URL of the user's avatar image; set by the avatar-upload endpoint (BE-4).
    /// Null until the user uploads an avatar.
    /// </summary>
    public string? AvatarUrl { get; set; }
    /// <summary>
    /// UTC timestamp at which the parent accepted the platform terms at registration (BE-9, COPPA audit).
    /// Non-null means consent was recorded; null means consent was not yet captured.
    /// No separate boolean — presence of this value is the boolean.
    /// </summary>
    public DateTime? AcceptedTermsAtUtc { get; set; }
    public bool RegistrationMessageIsSent { get; set; } = false;
    public bool RegistrationIsCompleted { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime? LastFailedLoginAttempt { get; set; }

    // ── P7-07 account governance lifecycle ──────────────────────────────────
    // Distinct from IsActive (mechanical sign-in gate, kept in sync by handlers)
    // and from LockoutEnd (temporary automatic failed-login lockout, P1-13).
    // New users created via registration are Active by default (both DB DEFAULT 0
    // and this C# initializer agree — no Draft equivalent for users).

    /// <summary>
    /// Governance lifecycle state of the account.
    /// Active = 0 (default) — backfills all existing rows via DB DEFAULT 0.
    /// Handlers that set Suspended or Deleted must also set IsActive = false.
    /// </summary>
    public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;

    /// <summary>
    /// Admin-supplied reason for the last status change (suspend/delete).
    /// Nullable; max 500 chars (matches validator bound).
    /// </summary>
    public string? LastStatusReason { get; set; }

    /// <summary>
    /// UserId of the admin who performed the last status change.
    /// Plain int — no cross-module FK; the acting admin is always an Identity user.
    /// </summary>
    public int? StatusChangedBy { get; set; }

    /// <summary>
    /// UTC timestamp of the last status change.
    /// Persist as DateTime.UtcNow; apply .ToUniversalTime() at mapping boundaries
    /// (Npgsql EnableLegacyTimestampBehavior=true — per P4-11 HANDOFF gotcha).
    /// </summary>
    public DateTime? StatusChangedAtUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; } = DateTime.Now;
    public int? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public bool? IsDeleted { get; set; } = false;
    public int? DeletedBy { get; set; }

    [ForeignKey("CreatedBy")]
    public User? CreatedByUser { get; set; }

    [ForeignKey("UpdatedBy")]
    public User? UpdatedByUser { get; set; }

    [ForeignKey("DeletedBy")]
    public User? DeletedByUser { get; set; }

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    // The parent↔student linkage (ParentStudent) moved to the Parent module (schema "parent") in P2-12.
    // Identity no longer maps it; child account operations cross the IChildAccountService Shared.Contracts seam.
}
