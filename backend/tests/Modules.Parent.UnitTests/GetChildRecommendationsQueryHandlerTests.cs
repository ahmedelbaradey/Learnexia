using FluentAssertions;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetChildRecommendations;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using System.Net;
using Xunit;

namespace Modules.Parent.UnitTests;

/// <summary>
/// Unit tests for <see cref="GetChildRecommendationsQueryHandler"/> (P5-09-BE-6 / E10).
///
/// Coverage:
///   REC-01  IDOR denied: non-owner parent → 403.
///   REC-02  No recommendations stored (cold-start): 200 OK, empty Items list.
///   REC-03  Recommendations exist: 200 OK, items returned and mapped to DTO correctly.
///   REC-04  childId ≤ 0 → 400 Bad Request.
///   REC-05  Seam fault (throws): 200 OK, graceful degradation to empty list (sentinel pattern).
///   REC-06  Null parentId (unauthenticated): 401 Unauthorized.
/// </summary>
public sealed class GetChildRecommendationsQueryHandlerTests
{
    // ── Helper — builds a passthrough string localizer ────────────────────────────────────────────

    private static IStringLocalizer<SharedResources> BuildLocalizer()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));
        return mock.Object;
    }

    // ── Helper — default recommendation items ─────────────────────────────────────────────────────

    private static IReadOnlyList<RecommendationItem> TwoItems() =>
    [
        new RecommendationItem(
            SkillId:          1,
            SubjectCode:      0,
            TitleKey:         SharedResourcesKey.RecReviewTitle,
            BodyKey:          SharedResourcesKey.RecReviewBody,
            CtaKey:           SharedResourcesKey.RecReviewCta,
            Severity:         3,
            ActionType:       RecommendationActionType.Review,
            TargetDifficulty: 2),
        new RecommendationItem(
            SkillId:          2,
            SubjectCode:      1,
            TitleKey:         SharedResourcesKey.RecPracticeTitle,
            BodyKey:          SharedResourcesKey.RecPracticeBody,
            CtaKey:           SharedResourcesKey.RecPracticeCta,
            Severity:         2,
            ActionType:       RecommendationActionType.Practice,
            TargetDifficulty: 1),
    ];

    // ── Builder — assembles the handler under test ────────────────────────────────────────────────

    private static GetChildRecommendationsQueryHandler BuildSut(
        int parentId,
        int childId,
        bool isOwner,
        IReadOnlyList<RecommendationItem>? items = null,
        bool seamThrows = false)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(parentId);

        var parentChildQuery = new Mock<IParentChildQuery>();
        parentChildQuery
            .Setup(q => q.IsParentOfChildAsync(parentId, childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isOwner);

        var recQuery = new Mock<IStudentRecommendationsQuery>();
        if (seamThrows)
        {
            recQuery
                .Setup(q => q.GetLatestAsync(childId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("seam fault"));
        }
        else
        {
            recQuery
                .Setup(q => q.GetLatestAsync(childId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(items ?? []);
        }

        return new GetChildRecommendationsQueryHandler(
            currentUser.Object,
            parentChildQuery.Object,
            recQuery.Object,
            new Mock<ILoggerManager>().Object,
            BuildLocalizer());
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>REC-01 — IDOR: non-owner parent → 403 Forbidden.</summary>
    [Fact]
    public async Task Should_return_403_when_parent_does_not_own_child()
    {
        var handler = BuildSut(parentId: 1, childId: 10, isOwner: false);

        var result = await handler.Handle(new GetChildRecommendationsQuery(10), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.Successed.Should().BeFalse();
        result.Data.Should().BeNull();
    }

    /// <summary>
    /// REC-02 — Cold-start / no recommendations yet: 200 OK, empty Items (not null), Successed true.
    /// </summary>
    [Fact]
    public async Task Should_return_empty_items_when_no_recommendations_stored()
    {
        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, items: []);

        var result = await handler.Handle(new GetChildRecommendationsQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Items.Should().BeEmpty("no recommendations → Items is empty, not null");
    }

    /// <summary>
    /// REC-03 — Recommendations exist: 200 OK with correctly mapped DTO items.
    /// </summary>
    [Fact]
    public async Task Should_return_200_with_mapped_items_when_recommendations_exist()
    {
        var items   = TwoItems();
        var handler = BuildSut(parentId: 2, childId: 7, isOwner: true, items: items);

        var result = await handler.Handle(new GetChildRecommendationsQuery(7), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();

        var dto = result.Data!;
        dto.Items.Should().HaveCount(2);

        // Verify first item mapping.
        var first = dto.Items[0];
        first.SkillId.Should().Be(1);
        first.SubjectCode.Should().Be(0);
        first.TitleKey.Should().Be(SharedResourcesKey.RecReviewTitle);
        first.BodyKey.Should().Be(SharedResourcesKey.RecReviewBody);
        first.CtaKey.Should().Be(SharedResourcesKey.RecReviewCta);
        first.Severity.Should().Be(3);
        first.ActionType.Should().Be((int)RecommendationActionType.Review);
        first.TargetDifficulty.Should().Be(2);

        // Verify second item mapping.
        var second = dto.Items[1];
        second.SkillId.Should().Be(2);
        second.ActionType.Should().Be((int)RecommendationActionType.Practice);
    }

    /// <summary>REC-04 — childId ≤ 0 → 400 Bad Request.</summary>
    [Fact]
    public async Task Should_return_400_when_child_id_is_not_positive()
    {
        var handler = BuildSut(parentId: 1, childId: 0, isOwner: false);

        var result = await handler.Handle(new GetChildRecommendationsQuery(0), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Successed.Should().BeFalse();
    }

    /// <summary>
    /// REC-05 — Seam throws: 200 OK, graceful degradation to empty Items list (sentinel pattern).
    /// A single seam failure must NOT surface as an error to the parent — mirrors the E5 pattern.
    /// </summary>
    [Fact]
    public async Task Should_return_empty_items_when_seam_throws()
    {
        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, seamThrows: true);

        var result = await handler.Handle(new GetChildRecommendationsQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK, "seam faults degrade gracefully, not 500");
        result.Successed.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    /// <summary>REC-06 — Unauthenticated (null UserId) → 401 Unauthorized.</summary>
    [Fact]
    public async Task Should_return_401_when_user_is_not_authenticated()
    {
        // Build handler with a null UserId (unauthenticated caller).
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns((int?)null);

        var parentChildQuery = new Mock<IParentChildQuery>();
        var recQuery = new Mock<IStudentRecommendationsQuery>();

        var handler = new GetChildRecommendationsQueryHandler(
            currentUser.Object,
            parentChildQuery.Object,
            recQuery.Object,
            new Mock<ILoggerManager>().Object,
            BuildLocalizer());

        var result = await handler.Handle(new GetChildRecommendationsQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.Successed.Should().BeFalse();
    }
}
