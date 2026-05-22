using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Learnexia.Modules.Identity.Domain.Entities;

public class User : IdentityUser<int>
{
    public string FullName { get; set; } = default!;
    public string PreferredLanguage { get; set; } = "ar-EG";
    public string CountryCode { get; set; } = "+20";
    
    public string? Nationality { get; set; }
    public string? PersonalPhotoPath { get; set; }
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

    // Navigation properties for the parent↔student linkage (P1-04).
    public virtual ICollection<ParentStudent> LinksAsParent { get; set; } = new List<ParentStudent>();
    public virtual ICollection<ParentStudent> LinksAsStudent { get; set; } = new List<ParentStudent>();
}
