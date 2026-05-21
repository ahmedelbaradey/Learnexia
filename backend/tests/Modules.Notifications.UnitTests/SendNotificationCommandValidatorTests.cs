using FluentAssertions;
using Learnexia.Modules.Notifications.Application.Features.SendNotification;
using Xunit;

namespace Modules.Notifications.UnitTests;

public sealed class SendNotificationCommandValidatorTests
{
    private readonly SendNotificationCommandValidator _validator = new();

    [Fact]
    public void Should_fail_when_title_is_empty()
    {
        var cmd = new SendNotificationCommand(Guid.NewGuid(), string.Empty, "body", Guid.NewGuid(), null);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_pass_for_valid_input()
    {
        var cmd = new SendNotificationCommand(Guid.NewGuid(), "Title", "body", Guid.NewGuid(), null);
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }
}
