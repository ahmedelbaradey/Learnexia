using System.IdentityModel.Tokens.Jwt;
using System.Net;
using FluentAssertions;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RefreshToken;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using Xunit;

namespace Modules.Identity.UnitTests;

/// <summary>
/// Unit tests for the P7-07 security fix — RefreshTokenCommandHandler must reject
/// suspended / deleted accounts that still hold a valid Redis refresh token.
///
/// Bucket: Refresh-token guard (security-auditor Fix #2).
/// </summary>
public sealed class RefreshTokenAccountStatusGuardTests
{
    private readonly Mock<IIdentityServiceManager> _serviceMock = new();
    private readonly Mock<IAuthenticationService> _authServiceMock = new();
    private readonly Mock<IUserManagmentService> _userMgmtMock = new();
    private readonly Mock<ISessionManagementService> _sessionManagementServiceMock = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _localizerMock = new();
    private readonly Mock<ILoggerManager> _loggerMock = new();

    private const string FakeAccessToken = "fake-access-token";
    private const string FakeRefreshToken = "fake-refresh-token";
    private const string FakeUserId = "42";
    private const string LocalizedDeactivated = "Your account is inactive. Please contact support.";

    // A valid-looking JwtSecurityToken stub — no signature needed for unit tests.
    private static readonly JwtSecurityToken FakeJwt = new();

    public RefreshTokenAccountStatusGuardTests()
    {
        // Wire service manager to sub-mocks.
        _serviceMock.Setup(s => s.AuthenticationService).Returns(_authServiceMock.Object);
        _serviceMock.Setup(s => s.UserManagmentService).Returns(_userMgmtMock.Object);

        // ReadJwtToken returns a stub token so the handler does not throw on ReadJwtToken.
        _authServiceMock
            .Setup(a => a.ReadJwtToken(FakeAccessToken))
            .Returns(FakeJwt);

        // ValidateDetails returns a non-error (userId, expiryDate) tuple so execution
        // flows past the switch block and reaches the user lookup.
        _authServiceMock
            .Setup(a => a.ValidateDetails(FakeJwt, FakeAccessToken, FakeRefreshToken))
            .ReturnsAsync((FakeUserId, (DateTime?)DateTime.UtcNow.AddHours(1)));

        // Localizer returns a recognizable string for the deactivated key.
        var localizedString = new LocalizedString(SharedResourcesKey.LoginAccountDeactivated, LocalizedDeactivated);
        _localizerMock
            .Setup(l => l[SharedResourcesKey.LoginAccountDeactivated])
            .Returns(localizedString);
    }

    private RefreshTokenCommandHandler BuildHandler() =>
        new(_serviceMock.Object, _sessionManagementServiceMock.Object, _localizerMock.Object, _loggerMock.Object);

    private static RefreshTokenCommand BuildCommand() => new()
    {
        AccessToken = FakeAccessToken,
        RefreshToken = FakeRefreshToken,
    };

    [Fact]
    public async Task Handle_SuspendedAccount_Returns401WithDeactivatedMessage()
    {
        // Arrange
        var suspendedUser = new User
        {
            IsActive = false,
            AccountStatus = AccountStatus.Suspended,
        };
        _userMgmtMock
            .Setup(u => u.FindByIdAsync(FakeUserId))
            .ReturnsAsync(suspendedUser);

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.Successed.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Message.Should().Be(LocalizedDeactivated);
    }

    [Fact]
    public async Task Handle_DeletedAccount_Returns401WithDeactivatedMessage()
    {
        // Arrange
        var deletedUser = new User
        {
            IsActive = false,
            AccountStatus = AccountStatus.Deleted,
        };
        _userMgmtMock
            .Setup(u => u.FindByIdAsync(FakeUserId))
            .ReturnsAsync(deletedUser);

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        // Assert
        result.Successed.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Message.Should().Be(LocalizedDeactivated);
    }

    [Fact]
    public async Task Handle_InactiveAccountWithActiveStatus_Returns401WithDeactivatedMessage()
    {
        // Arrange: IsActive = false but AccountStatus = Active (e.g. legacy deactivation path).
        // The guard checks BOTH IsActive and AccountStatus — either alone triggers the block.
        var inactiveUser = new User
        {
            IsActive = false,
            AccountStatus = AccountStatus.Active,
        };
        _userMgmtMock
            .Setup(u => u.FindByIdAsync(FakeUserId))
            .ReturnsAsync(inactiveUser);

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(BuildCommand(), CancellationToken.None);

        // Assert — !IsActive alone triggers the guard.
        result.Successed.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Message.Should().Be(LocalizedDeactivated);
    }
}
