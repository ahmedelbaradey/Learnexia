namespace Learnexia.Modules.Identity.Domain.Constants;

public static class CustomClaimTypes
{
    public const string Permission = "Permission";
    public const string FCMWebToken = "FCMWebToken";
    /// <summary>
    /// JWT claim carrying the child's medium-of-instruction language ("ar" or "en").
    /// Emitted for student accounts; absent for parents/admins (consumers fall back to "ar").
    /// Immutable by the student — parent-only change is P8-04.
    /// </summary>
    public const string LearningLanguage = "learning_language";
}
