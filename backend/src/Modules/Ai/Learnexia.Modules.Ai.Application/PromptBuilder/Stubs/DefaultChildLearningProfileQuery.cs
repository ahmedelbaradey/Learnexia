using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;

/// <summary>
/// Default (stub) implementation of <see cref="IChildLearningProfileQuery"/> registered
/// by the Ai module as a safe fallback.
///
/// <para>Returns a safe default profile (Grade 4, Age 10, Language = Ar) when no
/// real implementation is available. The real implementation is wired by P3-04 from
/// the Identity/Parent module seam.</para>
///
/// <para><strong>Registration note (BE-8):</strong> registered with
/// <c>TryAddTransient</c> so that if another module has already registered a real
/// implementation for <see cref="IChildLearningProfileQuery"/>, this stub will NOT
/// override it. The calling handler is responsible for populating <see cref="PromptContext"/>
/// with profile data from the JWT/claim context before calling
/// <c>IPromptBuilder.Build</c> — this seam is the fallback only.</para>
/// </summary>
internal sealed class DefaultChildLearningProfileQuery : IChildLearningProfileQuery
{
    public Task<ChildLearningProfile> GetAsync(int studentId, CancellationToken ct = default)
        // Safe defaults — grade 4, age 10, Arabic. These are overridden by the real P3-04 impl.
        => Task.FromResult(new ChildLearningProfile(Grade: 4, Age: 10, Language: TutorLanguage.Ar));
}
