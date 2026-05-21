using FluentAssertions;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;
using Learnexia.Modules.Identity.Application.Features.Authentications.Validation;
using Xunit;

namespace Modules.Identity.UnitTests;

public sealed class RefreshTokenValidatiorTests
{
    private readonly RefreshTokenValidatior _validator = new();

    [Fact]
    public void Should_fail_when_tokens_are_empty()
    {
        var result = _validator.Validate(new RefreshTokenCommand { AccessToken = "", RefreshToken = "" });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_pass_when_tokens_present()
    {
        var result = _validator.Validate(new RefreshTokenCommand { AccessToken = "a", RefreshToken = "b" });
        result.IsValid.Should().BeTrue();
    }
}
