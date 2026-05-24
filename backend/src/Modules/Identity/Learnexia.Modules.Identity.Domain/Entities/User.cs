using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Domain.Entities;

public class User : IdentityUser<int>
{
    public string FullName { get; set; } = default!;
    public string PreferredLanguage { get; set; } = "ar-EG";
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
