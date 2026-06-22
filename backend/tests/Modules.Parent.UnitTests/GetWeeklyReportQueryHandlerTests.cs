using FluentAssertions;
using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Application.Features.Analytics.GetWeeklyReport;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using System.Net;
using Xunit;

namespace Modules.Parent.UnitTests;

/// <summary>
/// Unit tests for <see cref="GetWeeklyReportQueryHandler"/> (P5-01-BE-4 / E9).
///
/// Coverage:
///   WR-01  IDOR denied → 403.
///   WR-02  No report yet → 200 OK, ReportFound=false ("not yet generated" message).
///   WR-03  Existing report → 200 OK, ReportFound=true, fields mapped correctly.
///   WR-04  childId ≤ 0 → 400.
///   WR-05  Malformed WeakAreasJson → 200 OK, empty WeakAreas (graceful degradation).
/// </summary>
public sealed class GetWeeklyReportQueryHandlerTests
{
    private static IStringLocalizer<SharedResources> BuildLocalizer()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()]).Returns<string>(k => new LocalizedString(k, k));
        // Two-arg (formatted) indexer used by the localized recommendations: render "key:arg0" so a
        // test can assert BOTH the chosen resource key and the substituted skill name.
        mock.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()])
            .Returns((string k, object[] args) => new LocalizedString(k, $"{k}:{(args.Length > 0 ? args[0] : string.Empty)}"));
        return mock.Object;
    }

    private static GetWeeklyReportQueryHandler BuildSut(
        int parentId,
        int childId,
        bool isOwner,
        WeeklyReportReadDto? report = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(parentId);

        var parentChildQuery = new Mock<IParentChildQuery>();
        parentChildQuery
            .Setup(q => q.IsParentOfChildAsync(parentId, childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isOwner);

        var reportQuery = new Mock<IWeeklyReportQueryService>();
        reportQuery
            .Setup(q => q.GetLatestAsync(childId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        reportQuery
            .Setup(q => q.GetByWeekAsync(childId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        return new GetWeeklyReportQueryHandler(
            currentUser.Object,
            parentChildQuery.Object,
            reportQuery.Object,
            new Mock<ILoggerManager>().Object,
            BuildLocalizer());
    }

    /// <summary>WR-01 — IDOR: non-owner parent → 403.</summary>
    [Fact]
    public async Task Should_return_403_when_parent_does_not_own_child()
    {
        var handler = BuildSut(parentId: 1, childId: 10, isOwner: false);
        var result  = await handler.Handle(new GetWeeklyReportQuery(10), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.Successed.Should().BeFalse();
    }

    /// <summary>WR-02 — No report yet → 200 OK with ReportFound=false (AC3).</summary>
    [Fact]
    public async Task Should_return_200_with_no_report_state_when_no_report_exists()
    {
        // report = null → no row in DB
        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, report: null);
        var result  = await handler.Handle(new GetWeeklyReportQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        result.Data!.ReportFound.Should().BeFalse();
        result.Data.XpEarned.Should().Be(0);
        result.Data.SkillsImproved.Should().Be(0);
        result.Data.WeakAreas.Should().BeEmpty();
        result.Data.Recommendations.Should().BeEmpty();
        result.Message.Should().Be(SharedResourcesKey.WeeklyReportNotYetGenerated);
    }

    /// <summary>WR-03 — Existing report → 200 OK, ReportFound=true, fields populated.</summary>
    [Fact]
    public async Task Should_return_report_when_one_exists()
    {
        var weekStart = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc); // Monday
        var generatedAt = new DateTime(2026, 6, 15, 3, 0, 0, DateTimeKind.Utc);

        var storedReport = new WeeklyReportReadDto
        {
            Id                  = 42,
            ChildId             = 5,
            WeekStartUtc        = weekStart,
            XpEarned            = 350,
            SkillsImproved      = 2,
            WeakAreasJson       = """[{"skillId":1,"skillName":"Addition","subjectCode":0,"masteryPercent":35,"severity":2}]""",
            RecommendationsJson = """["Practice skill: Addition"]""",
            GeneratedAtUtc      = generatedAt,
        };

        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, report: storedReport);
        var result  = await handler.Handle(new GetWeeklyReportQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();

        var dto = result.Data!;
        dto.ReportFound.Should().BeTrue();
        dto.WeekStartUtc.Should().Be(weekStart);
        dto.XpEarned.Should().Be(350);
        dto.SkillsImproved.Should().Be(2);
        dto.WeakAreas.Should().HaveCount(1);
        dto.WeakAreas[0].SkillId.Should().Be(1);
        dto.WeakAreas[0].Severity.Should().Be(2);
        dto.Recommendations.Should().ContainSingle(r => r == "Practice skill: Addition");
        dto.GeneratedAtUtc.Should().Be(generatedAt);
        result.Message.Should().Be(SharedResourcesKey.WeeklyReportRetrievedSuccessfully);
    }

    /// <summary>
    /// WR-06 — New structured recommendations ({code, skillName}) are LOCALIZED at read (P6 cleanup).
    /// Each entry maps to its resource key with the skill name substituted; an unknown code falls back
    /// to the bare skill name.
    /// </summary>
    [Fact]
    public async Task Should_localize_structured_recommendation_codes()
    {
        var storedReport = new WeeklyReportReadDto
        {
            Id                  = 99,
            ChildId             = 5,
            WeekStartUtc        = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            XpEarned            = 120,
            SkillsImproved      = 1,
            WeakAreasJson       = "[]",
            RecommendationsJson =
                """[{"code":"REVIEW_CONCEPT","skillName":"Fractions"},{"code":"PRACTICE_SKILL","skillName":"Addition"},{"code":"WAT","skillName":"Geometry"}]""",
            GeneratedAtUtc      = DateTime.UtcNow,
        };

        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, report: storedReport);
        var result  = await handler.Handle(new GetWeeklyReportQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Recommendations.Should().Equal(
            $"{SharedResourcesKey.WeeklyReportRecReviewConcept}:Fractions",
            $"{SharedResourcesKey.WeeklyReportRecPracticeSkill}:Addition",
            "Geometry"); // unknown code → bare skill name
    }

    /// <summary>WR-07 — Legacy prose recommendations (pre-P6) are passed through unchanged (back-compat).</summary>
    [Fact]
    public async Task Should_passthrough_legacy_prose_recommendations()
    {
        var storedReport = new WeeklyReportReadDto
        {
            Id = 100, ChildId = 5,
            WeekStartUtc = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            XpEarned = 50, SkillsImproved = 0, WeakAreasJson = "[]",
            RecommendationsJson = """["Practice skill: Addition","Review concept for Fractions"]""",
            GeneratedAtUtc = DateTime.UtcNow,
        };

        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, report: storedReport);
        var result  = await handler.Handle(new GetWeeklyReportQuery(5), CancellationToken.None);

        result.Data!.Recommendations.Should().Equal("Practice skill: Addition", "Review concept for Fractions");
    }

    /// <summary>WR-04 — childId ≤ 0 → 400 Bad Request.</summary>
    [Fact]
    public async Task Should_return_400_when_child_id_is_zero_or_negative()
    {
        var handler = BuildSut(parentId: 1, childId: -1, isOwner: false);
        var result  = await handler.Handle(new GetWeeklyReportQuery(-1), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Successed.Should().BeFalse();
    }

    /// <summary>
    /// WR-05 — Malformed WeakAreasJson (invalid JSON) → 200 OK with empty WeakAreas
    /// (graceful degradation — never throws to the caller).
    /// </summary>
    [Fact]
    public async Task Should_return_empty_weak_areas_when_json_is_malformed()
    {
        var storedReport = new WeeklyReportReadDto
        {
            Id                  = 7,
            ChildId             = 5,
            WeekStartUtc        = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc),
            XpEarned            = 100,
            SkillsImproved      = 1,
            WeakAreasJson       = "NOT_VALID_JSON{{{{",
            RecommendationsJson = "[]",
            GeneratedAtUtc      = DateTime.UtcNow,
        };

        var handler = BuildSut(parentId: 1, childId: 5, isOwner: true, report: storedReport);
        var result  = await handler.Handle(new GetWeeklyReportQuery(5), CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Successed.Should().BeTrue();
        result.Data!.ReportFound.Should().BeTrue();
        result.Data.WeakAreas.Should().BeEmpty();     // degraded gracefully
        result.Data.XpEarned.Should().Be(100);        // other fields still populated
    }
}
