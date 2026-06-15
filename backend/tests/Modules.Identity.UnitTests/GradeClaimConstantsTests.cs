using FluentAssertions;
using Learnexia.Modules.Identity.Domain.Constants;
using Xunit;

namespace Modules.Identity.UnitTests;

/// <summary>
/// Regression tests for AI-DEFECT-1: the Grade and Age claim-type constants defined in
/// <see cref="CustomClaimTypes"/> must match the literal strings read by
/// <c>TryResolveProfile</c> in every AI Handler (currently the bare strings "Grade" and "Age").
///
/// If these tests fail it means <see cref="CustomClaimTypes.Grade"/> or
/// <see cref="CustomClaimTypes.Age"/> have drifted from the value the AI handlers read via
/// <c>ICurrentUserService.GetClaimValue(…)</c>, which would re-introduce AI-DEFECT-1.
///
/// Tests:
///   GCC-01  CustomClaimTypes.Grade equals the literal "Grade".
///   GCC-02  CustomClaimTypes.Age   equals the literal "Age".
///   GCC-03  CustomClaimTypes.Grade is non-null and non-empty.
///   GCC-04  CustomClaimTypes.Age   is non-null and non-empty.
/// </summary>
public sealed class GradeClaimConstantsTests
{
    /// <summary>
    /// GCC-01: The Grade claim-type constant must equal "Grade" — the string read by
    /// all AI Helper handlers in <c>TryResolveProfile</c>.
    /// </summary>
    [Fact(DisplayName = "GCC-01 CustomClaimTypes.Grade == \"Grade\" (must match AI handler's TryResolveProfile claim key)")]
    public void Grade_ClaimType_MatchesAiHandlerString()
    {
        // The AI handlers read: _currentUser.GetClaimValue("Grade")
        // The Identity JWT emitter must write the same string.
        CustomClaimTypes.Grade.Should().Be("Grade",
            because: "AI handlers in TryResolveProfile read GetClaimValue(\"Grade\"); " +
                     "the claim emitted by AuthenticationIdentityService must use the identical key " +
                     "(AI-DEFECT-1 fix — drifting this constant re-introduces the defect)");
    }

    /// <summary>
    /// GCC-02: The Age claim-type constant must equal "Age" — the string read by
    /// all AI Helper handlers in <c>TryResolveProfile</c>.
    /// </summary>
    [Fact(DisplayName = "GCC-02 CustomClaimTypes.Age == \"Age\" (must match AI handler's TryResolveProfile claim key)")]
    public void Age_ClaimType_MatchesAiHandlerString()
    {
        // The AI handlers read: _currentUser.GetClaimValue("Age")
        // The Identity JWT emitter must write the same string.
        CustomClaimTypes.Age.Should().Be("Age",
            because: "AI handlers in TryResolveProfile read GetClaimValue(\"Age\"); " +
                     "the claim emitted by AuthenticationIdentityService must use the identical key " +
                     "(AI-DEFECT-1 fix — drifting this constant re-introduces the defect)");
    }

    /// <summary>GCC-03: CustomClaimTypes.Grade is not null or whitespace.</summary>
    [Fact(DisplayName = "GCC-03 CustomClaimTypes.Grade is non-null and non-empty")]
    public void Grade_ClaimType_IsNotNullOrWhiteSpace()
    {
        CustomClaimTypes.Grade.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>GCC-04: CustomClaimTypes.Age is not null or whitespace.</summary>
    [Fact(DisplayName = "GCC-04 CustomClaimTypes.Age is non-null and non-empty")]
    public void Age_ClaimType_IsNotNullOrWhiteSpace()
    {
        CustomClaimTypes.Age.Should().NotBeNullOrWhiteSpace();
    }
}
