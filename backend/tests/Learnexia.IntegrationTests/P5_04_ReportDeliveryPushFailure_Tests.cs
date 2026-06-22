using FluentAssertions;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// P5-04 — DEL-INT-06 (closes the previously-BLOCKED delivery case).
//
// "A push-delivery FAILURE must NOT prevent the in-app inbox row from persisting."
//
// The NudgeDispatcher writes the inbox row, attempts push, then SaveChangesAsync.
// TrySendPushAsync wraps the IPushSender call in its own try/catch (returns 0 on
// throw), so a push fault is swallowed and the inbox row still commits.
//
// Coverage technique (NO shared-mutable fake state — the earlier reason this was
// blocked): inject a dedicated, single-purpose IPushSender that ALWAYS throws,
// scoped to a per-test WithWebHostBuilder factory that reuses the shared
// Testcontainers Postgres. The shared collection's TestPushSenderImpl is left
// untouched, so no other suite is affected and there is no toggle to leak.
// =============================================================================

[Collection("IntegrationTests")]
public sealed class P5_04_ReportDeliveryPushFailure_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;

    public P5_04_ReportDeliveryPushFailure_Tests(LearnexiaWebAppFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "P504-DEL-INT-06 Push delivery throws → in-app inbox row still persisted (fail-soft)")]
    public async Task PushDeliveryFailure_DoesNotPreventInboxPersistence()
    {
        // Unique synthetic recipient/code so this cannot collide with other suites in
        // the shared "IntegrationTests" collection (the Notifications schema keys the
        // inbox row by an opaque external user id — no Identity FK).
        const int childId  = 990_604;
        const int parentId = 990_605;
        var code = "DELINT06_" + Guid.NewGuid().ToString("N")[..8];

        // Seed an ACTIVE device token so the dispatcher actually ATTEMPTS a push.
        // Without a token, TrySendPushAsync short-circuits (push skipped) and the
        // throw path is never exercised.
        await RegisterDeviceTokenDirectlyAsync(childId, $"ExponentPushToken[DELINT06_{childId}]");

        // Derived factory: swap IPushSender for one that always throws. Scoped to this
        // factory only — no mutation of the shared factory's TestPushSenderImpl.
        using var failFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPushSender>();
                services.AddScoped<IPushSender, ThrowingPushSender>();
            }));

        var message = new NudgeMessage(
            RecipientChildUserId: childId,
            ParentId: parentId,
            Category: NotificationCategory.WeeklyReport,
            Code: code,
            Title: "تقرير أسبوعي جاهز",
            Body: "تقرير طفلك الأسبوعي جاهز.",
            DataJson: null,
            ShouldPush: true,
            ShouldInApp: true);

        // Act — dispatch through the throwing push sender. Must NOT bubble (fail-soft).
        await using (var scope = failFactory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<INudgeDispatcher>();
            var dispatch = async () => await dispatcher.DispatchAsync(message);
            await dispatch.Should().NotThrowAsync(
                "the dispatcher is fail-soft: a push-delivery exception must never propagate");
        }

        // Assert — the in-app inbox row is durable despite the push throwing.
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

        var row = await db.Notifications.AsNoTracking()
            .FirstOrDefaultAsync(n => n.RecipientExternalUserId == childId && n.Code == code);

        row.Should().NotBeNull(
            "the in-app inbox row must persist even when push delivery throws (durable receipt)");
        row!.SentAtUtc.Should().NotBeNull(
            "RecordSent + SaveChangesAsync must have committed after the push fault was swallowed");
        (row.DeliveredChannels & 2).Should().Be(0,
            "the PUSH channel bit (2) must NOT be set — push delivery failed");
        (row.DeliveredChannels & 4).Should().Be(4,
            "the IN-APP channel bit (4) must be set — the inbox receipt was written");
    }

    // Mirrors P9_07_NudgeArbitration_Tests.RegisterDeviceTokenDirectlyAsync.
    private async Task RegisterDeviceTokenDirectlyAsync(int userId, string expoPushToken)
    {
        using var scope = _factory.Services.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var platform  = "ios";
        var nowUtc    = DateTime.UtcNow;
        var creatorId = 0;

        await db.Database.ExecuteSqlRawAsync(
            $@"INSERT INTO notifications.""UserDeviceTokens""
               (""UserId"", ""ExpoPushToken"", ""Platform"", ""RegisteredAtUtc"", ""LastUsedAtUtc"", ""IsActive"",
                ""CreatedAt"", ""CreatedBy"")
               VALUES ({userId}, '{expoPushToken}', '{platform}', '{nowUtc:O}', '{nowUtc:O}', true,
                       '{nowUtc:O}', {creatorId})
               ON CONFLICT DO NOTHING");
    }

    /// <summary>
    /// Single-purpose <see cref="IPushSender"/> that always throws — simulates an Expo/push
    /// outage. Stateless (no toggle), so it cannot leak across tests.
    /// </summary>
    private sealed class ThrowingPushSender : IPushSender
    {
        public Task<PushSendResult> SendAsync(
            IReadOnlyList<string> expoPushTokens,
            string title,
            string body,
            string? dataJson,
            CancellationToken ct = default)
            => throw new InvalidOperationException(
                "Simulated push delivery failure (Expo unreachable) — DEL-INT-06.");
    }
}
