using FluentAssertions;
using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Dtos;
using Learnexia.Modules.Ai.Application.Features.AdminTutorUsage.Queries.GetTutorUsage;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Localization;
using Moq;
using Resources;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="GetTutorUsageQueryHandler"/> branch logic (P7-11 usage/cost dashboard).
///
/// Coverage:
///   TU-01  from &gt;= to → BadRequest (400) with AiSafetyInvalidDateRange key.
///   TU-02  Valid range → Success (200) with data from IAiTutorUsageService.
///   TU-03  Default date range applied when From/To are null (service called with from ≈ now-30d).
///   TU-04  Exception in service → ServerError (500).
///   TU-05  Handler does NOT inject IAiTutorUsageService via a DbContext (Option-C: service only).
/// </summary>
public sealed class GetTutorUsageQueryHandlerTests
{
    // ── Factory helper ────────────────────────────────────────────────────────────

    private static GetTutorUsageQueryHandler CreateSut(
        Mock<IAiTutorUsageService> serviceMock)
    {
        var logger    = new Mock<ILoggerManager>();
        var localizer = BuildLocalizerMock();
        return new GetTutorUsageQueryHandler(serviceMock.Object, logger.Object, localizer.Object);
    }

    private static Mock<IStringLocalizer<SharedResources>> BuildLocalizerMock()
    {
        var mock = new Mock<IStringLocalizer<SharedResources>>();
        mock.Setup(l => l[It.IsAny<string>()])
            .Returns<string>(key => new LocalizedString(key, key));
        return mock;
    }

    private static TutorUsageDto MakeZeroedSummary(DateTime from, DateTime to)
        => new TutorUsageDto(
            From: from,
            To: to,
            TotalCalls: 0,
            TotalPromptTokens: 0,
            TotalCompletionTokens: 0,
            TotalEstimatedCostUsd: 0m,
            AvgLatencyMs: 0d,
            CacheHitRate: 0d,
            ByModel: Array.Empty<TutorUsageByModelDto>(),
            ByTaskKind: Array.Empty<TutorUsageByTaskKindDto>(),
            Trend: Array.Empty<TutorUsageTrendBucketDto>());

    // ── TU-01: from >= to → BadRequest ───────────────────────────────────────────

    [Fact(DisplayName = "P711-TU-01 from >= to → BadRequest with AiSafetyInvalidDateRange key")]
    public async Task Handle_FromGeqTo_ReturnsBadRequest()
    {
        // Arrange — from == to (invalid range).
        var serviceMock = new Mock<IAiTutorUsageService>();
        var sut         = CreateSut(serviceMock);

        var now    = DateTime.UtcNow;
        var query  = new GetTutorUsageQuery { From = now, To = now.AddMinutes(-1) };

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Successed.Should().BeFalse(
            "a range where from >= to must be rejected (400)");

        result.Message.Should().Be(SharedResourcesKey.AiSafetyInvalidDateRange,
            "the error message key must be AiSafetyInvalidDateRange (reused for all AI-admin date-range endpoints)");

        // Service must NOT be called when range is invalid.
        serviceMock.Verify(
            s => s.GetUsageSummaryAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "IAiTutorUsageService must NOT be called when the date range is invalid");
    }

    // ── TU-02: Valid range → Success with DTO ─────────────────────────────────────

    [Fact(DisplayName = "P711-TU-02 Valid range → Success (200) with IAiTutorUsageService data")]
    public async Task Handle_ValidRange_ReturnsSuccess()
    {
        // Arrange
        var from    = DateTime.UtcNow.AddDays(-7);
        var to      = DateTime.UtcNow;
        var dto     = MakeZeroedSummary(from, to) with { TotalCalls = 42 };

        var serviceMock = new Mock<IAiTutorUsageService>();
        serviceMock
            .Setup(s => s.GetUsageSummaryAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var sut   = CreateSut(serviceMock);
        var query = new GetTutorUsageQuery { From = from, To = to };

        // Act
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.Successed.Should().BeTrue("valid range with data must return Success");
        result.Data.Should().NotBeNull();
        result.Data!.TotalCalls.Should().Be(42, "handler must return the exact DTO from the service");

        result.Message.Should().Be(SharedResourcesKey.AiTutorUsageRetrievedSuccessfully,
            "success message key must be AiTutorUsageRetrievedSuccessfully");
    }

    // ── TU-03: Null From/To → default range applied ──────────────────────────────

    [Fact(DisplayName = "P711-TU-03 Null From/To → service called with resolved defaults (now-30d, now)")]
    public async Task Handle_NullFromTo_DefaultRangeApplied()
    {
        // Arrange — no explicit from/to; handler must default to (now-30d, now).
        DateTime? capturedFrom = null;
        DateTime? capturedTo   = null;

        var serviceMock = new Mock<IAiTutorUsageService>();
        serviceMock
            .Setup(s => s.GetUsageSummaryAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime, CancellationToken>((f, t, _) =>
            {
                capturedFrom = f;
                capturedTo   = t;
            })
            .ReturnsAsync((DateTime f, DateTime t, CancellationToken _) => MakeZeroedSummary(f, t));

        var sut   = CreateSut(serviceMock);
        var query = new GetTutorUsageQuery(); // From = null, To = null

        // Act
        var before = DateTime.UtcNow;
        await sut.Handle(query, CancellationToken.None);
        var after  = DateTime.UtcNow;

        // Assert — service was called with sensible defaults.
        serviceMock.Verify(
            s => s.GetUsageSummaryAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "service must be called exactly once with default range");

        capturedFrom.Should().NotBeNull();
        capturedTo.Should().NotBeNull();

        // "to" should be approximately now.
        capturedTo!.Value.Should().BeCloseTo(before, TimeSpan.FromSeconds(5),
            "resolved 'to' should be approximately DateTime.UtcNow");

        // "from" should be approximately 30 days before "to".
        var expectedFrom = capturedTo.Value.AddDays(-30);
        capturedFrom!.Value.Should().BeCloseTo(expectedFrom, TimeSpan.FromSeconds(5),
            "resolved 'from' should be approximately now-30d");
    }

    // ── TU-04: Service throws → ServerError ──────────────────────────────────────

    [Fact(DisplayName = "P711-TU-04 Service exception → ServerError (500); exception not propagated")]
    public async Task Handle_ServiceThrows_ReturnsServerError()
    {
        // Arrange — service throws an unexpected exception.
        var serviceMock = new Mock<IAiTutorUsageService>();
        serviceMock
            .Setup(s => s.GetUsageSummaryAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection lost"));

        var sut   = CreateSut(serviceMock);
        var query = new GetTutorUsageQuery
        {
            From = DateTime.UtcNow.AddDays(-7),
            To   = DateTime.UtcNow,
        };

        // Act — must NOT throw; handler wraps in try/catch.
        var act    = async () => await sut.Handle(query, CancellationToken.None);
        var result = await sut.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync(
            "handler must catch all exceptions and return ServerError (convention)");

        result.Successed.Should().BeFalse("a service exception must produce a non-success result");
    }

    // ── TU-05: Handler does NOT inject DbContext or repository ────────────────────

    [Fact(DisplayName = "P711-TU-05 Handler constructor parameter types — no DbContext/IQueryable/repository (Option-C)")]
    public void Handler_ConstructorParameters_NoEfOrRepositoryTypes()
    {
        // The handler must depend ONLY on the service interface, a logger, and a localizer.
        // No DbContext, IQueryable, or repository type may appear in its constructor.
        var ctorParams = typeof(GetTutorUsageQueryHandler)
            .GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .ToList();

        // Must have IAiTutorUsageService.
        ctorParams.Should().Contain(
            typeof(IAiTutorUsageService),
            "handler must inject IAiTutorUsageService (Option-C service-only rule)");

        // Must NOT have any EF DbContext type.
        var hasDbContext = ctorParams.Any(t => t.FullName != null && t.FullName.Contains("DbContext"));
        hasDbContext.Should().BeFalse(
            "handler must NOT inject any DbContext (Option-C: Application is EF-free)");

        // Must NOT have any IQueryable type.
        var hasIQueryable = ctorParams.Any(t => t.FullName != null && t.FullName.Contains("IQueryable"));
        hasIQueryable.Should().BeFalse(
            "handler must NOT inject any IQueryable (Option-C: no EF in Application)");
    }
}
