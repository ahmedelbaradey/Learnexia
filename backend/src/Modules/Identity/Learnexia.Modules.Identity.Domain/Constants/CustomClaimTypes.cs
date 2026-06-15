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

    /// <summary>
    /// JWT claim carrying the student's current school grade (integer, e.g. "5").
    /// Emitted only for student accounts (only when <c>User.Grade</c> is non-null).
    /// Used by AI Helper handlers to key the response cache's age-band dimension and
    /// to build grade-appropriate prompts. Grade changes (P7-08 override) take effect
    /// on the next token refresh — live invalidation is not required.
    /// The claim key must match the string constant read by <c>TryResolveProfile</c>
    /// in every AI handler (AI-DEFECT-1 fix).
    /// </summary>
    public const string Grade = "Grade";

    /// <summary>
    /// JWT claim carrying the student's age in years (integer, e.g. "10").
    /// Emitted only for student accounts (only when <c>User.Age</c> is non-null).
    /// Used by AI Helper handlers for prompt age-contextualisation.
    /// </summary>
    public const string Age = "Age";
}
