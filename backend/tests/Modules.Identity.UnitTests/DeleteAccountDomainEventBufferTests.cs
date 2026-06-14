using FluentAssertions;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Events;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteAccount;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.DomainEvents;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using Xunit;

namespace Modules.Identity.UnitTests;

/// <summary>
/// Unit tests for the P7-07 post-commit domain-events buffer fix (Security High #1).
///
/// Verifies that <see cref="DeleteAccountCommandHandler"/>:
///   a) enqueues an <see cref="AccountDeletedDomainEvent"/> via <see cref="IIdentityDomainEventsBuffer"/>
///      on the success path (so post-commit side-effects fire only after a committed delete).
///   b) does NOT enqueue anything when an early-return guard fires before any mutation
///      (confirm=false, self-protection, already-deleted) — so a rolled-back or rejected
///      command leaves no phantom events in the buffer.
///
/// These tests exercise only the Application layer (no Infrastructure, no EF, no Redis).
/// </summary>
public sealed class DeleteAccountDomainEventBufferTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────────

    private readonly Mock<IIdentityServiceManager> _serviceMock = new();
    private readonly Mock<IUserManagmentService> _userMgmtMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly Mock<IParentChildQuery> _parentChildQueryMock = new();
    private readonly Mock<IStringLocalizer<SharedResources>> _localizerMock = new();
    private readonly Mock<IIdentityDomainEventsBuffer> _bufferMock = new();
    private readonly Mock<ILoggerManager> _loggerMock = new();

    private const int AdminId = 99;
    private const int TargetUserId = 42;

    public DeleteAccountDomainEventBufferTests()
    {
        _serviceMock.Setup(s => s.UserManagmentService).Returns(_userMgmtMock.Object);
        _currentUserMock.Setup(c => c.UserId).Returns(AdminId);

        // Localizer returns a non-empty string for any key (prevents null-ref in handler).
        _localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns<string>(k => new LocalizedString(k, k));
    }

    private DeleteAccountCommandHandler BuildHandler() => new(
        _serviceMock.Object,
        _currentUserMock.Object,
        _parentChildQueryMock.Object,
        _localizerMock.Object,
        _bufferMock.Object,
        _loggerMock.Object);

    // ── Helper: build an active, non-SuperAdmin user ──────────────────────────

    private static User BuildActiveUser() => new()
    {
        Id = TargetUserId,
        AccountStatus = AccountStatus.Active,
        IsActive = true,
    };

    // ── Happy-path: buffer receives exactly one AccountDeletedDomainEvent ─────

    [Fact]
    public async Task Handle_SuccessPath_AddsAccountDeletedDomainEventToBuffer()
    {
        // Arrange
        var user = BuildActiveUser();

        _userMgmtMock
            .Setup(u => u.FindByIdWithRolesAsync(TargetUserId.ToString()))
            .ReturnsAsync(user);

        _userMgmtMock
            .Setup(u => u.GetUserRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.Parent.ToString() });

        _userMgmtMock
            .Setup(u => u.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _parentChildQueryMock
            .Setup(p => p.GetChildIdsForParentAsync(TargetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int>());

        IDomainEvent? capturedEvent = null;
        _bufferMock
            .Setup(b => b.Add(It.IsAny<IDomainEvent>()))
            .Callback<IDomainEvent>(e => capturedEvent = e);

        var command = new DeleteAccountCommand
        {
            UserId = TargetUserId,
            Reason = "Test deletion",
            Confirm = true,
            CascadeChildren = false,
        };

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — handler succeeds and enqueues exactly one event.
        result.Successed.Should().BeTrue("successful delete must return successed=true");

        _bufferMock.Verify(b => b.Add(It.IsAny<IDomainEvent>()), Times.Once,
            "handler must enqueue exactly one domain event on the success path");

        capturedEvent.Should().NotBeNull("a domain event must have been captured");
        capturedEvent.Should().BeOfType<AccountDeletedDomainEvent>(
            "the enqueued event must be AccountDeletedDomainEvent");

        var deletedEvent = (AccountDeletedDomainEvent)capturedEvent!;
        deletedEvent.UserId.Should().Be(TargetUserId, "event must carry the target user id");
        deletedEvent.DeletedByAdminUserId.Should().Be(AdminId, "event must carry the acting admin id");
        deletedEvent.AffectedChildIds.Should().BeEmpty("no cascade was requested");
    }

    // ── Confirm=false guard: no buffer Add ────────────────────────────────────

    [Fact]
    public async Task Handle_ConfirmFalse_DoesNotAddToBuffer()
    {
        // Arrange
        var command = new DeleteAccountCommand
        {
            UserId = TargetUserId,
            Reason = "Should be rejected",
            Confirm = false,
            CascadeChildren = false,
        };

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — early-return before any mutation; buffer must NOT be touched.
        result.Successed.Should().BeFalse("confirm=false must return successed=false (424)");

        _bufferMock.Verify(b => b.Add(It.IsAny<IDomainEvent>()), Times.Never,
            "buffer must NOT receive any event when the confirm-gate fires (no mutation occurred)");
    }

    // ── Self-protection guard: no buffer Add ─────────────────────────────────

    [Fact]
    public async Task Handle_SelfDelete_DoesNotAddToBuffer()
    {
        // Arrange: admin tries to delete their own account (AdminId == TargetUserId).
        _currentUserMock.Setup(c => c.UserId).Returns(TargetUserId);

        var command = new DeleteAccountCommand
        {
            UserId = TargetUserId,
            Reason = "Self-delete attempt",
            Confirm = true,
            CascadeChildren = false,
        };

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Successed.Should().BeFalse("self-delete must be rejected");

        _bufferMock.Verify(b => b.Add(It.IsAny<IDomainEvent>()), Times.Never,
            "buffer must NOT receive any event when self-protection fires");
    }

    // ── Already-deleted guard: no buffer Add ─────────────────────────────────

    [Fact]
    public async Task Handle_AlreadyDeleted_DoesNotAddToBuffer()
    {
        // Arrange
        var deletedUser = new User
        {
            Id = TargetUserId,
            AccountStatus = AccountStatus.Deleted,
            IsActive = false,
        };

        _userMgmtMock
            .Setup(u => u.FindByIdWithRolesAsync(TargetUserId.ToString()))
            .ReturnsAsync(deletedUser);

        _userMgmtMock
            .Setup(u => u.GetUserRolesAsync(deletedUser))
            .ReturnsAsync(new List<string> { Roles.Parent.ToString() });

        var command = new DeleteAccountCommand
        {
            UserId = TargetUserId,
            Reason = "Attempt to re-delete",
            Confirm = true,
            CascadeChildren = false,
        };

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — state-machine guard fires before staging any mutation.
        result.Successed.Should().BeFalse("deleting an already-deleted account must be rejected");

        _bufferMock.Verify(b => b.Add(It.IsAny<IDomainEvent>()), Times.Never,
            "buffer must NOT receive any event when the already-deleted guard fires");
    }

    // ── Cascade path: buffer event carries child ids ──────────────────────────

    [Fact]
    public async Task Handle_CascadeChildren_EventCarriesChildIds()
    {
        // Arrange
        var user = BuildActiveUser();
        var childId = 101;

        _userMgmtMock
            .Setup(u => u.FindByIdWithRolesAsync(TargetUserId.ToString()))
            .ReturnsAsync(user);

        _userMgmtMock
            .Setup(u => u.GetUserRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.Parent.ToString() });

        _userMgmtMock
            .Setup(u => u.UpdateAsync(It.IsAny<User>()))
            .ReturnsAsync(IdentityResult.Success);

        _userMgmtMock
            .Setup(u => u.FindByIdAsync(childId.ToString()))
            .ReturnsAsync(new User { Id = childId, AccountStatus = AccountStatus.Active, IsActive = true });

        _parentChildQueryMock
            .Setup(p => p.GetChildIdsForParentAsync(TargetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<int> { childId });

        IDomainEvent? capturedEvent = null;
        _bufferMock
            .Setup(b => b.Add(It.IsAny<IDomainEvent>()))
            .Callback<IDomainEvent>(e => capturedEvent = e);

        var command = new DeleteAccountCommand
        {
            UserId = TargetUserId,
            Reason = "Cascade delete",
            Confirm = true,
            CascadeChildren = true,
        };

        var handler = BuildHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Successed.Should().BeTrue();

        var deletedEvent = capturedEvent as AccountDeletedDomainEvent;
        deletedEvent.Should().NotBeNull();
        deletedEvent!.AffectedChildIds.Should().Contain(childId,
            "event must carry the child id when CascadeChildren=true and child is successfully staged");
    }
}
