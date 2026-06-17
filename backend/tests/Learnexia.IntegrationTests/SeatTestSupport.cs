using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Shared test support for the P10-14 seat model.
///
/// <para>P10-14 wired a seat reservation into <c>AddChildCommandHandler</c>: the Parent add-child
/// flow now reserves a seat BEFORE creating the child, and rejects with 409 when no free seat is
/// available (story P10-14 AC: "no seat available → rejected cleanly, no child created"). A Free
/// parent has exactly 1 included seat, so any pre-existing test that adds a SECOND child (or a
/// duplicate, which still reserves a seat before the duplicate-email check fires) now hits the seat
/// gate instead of its intended assertion.</para>
///
/// <para>This helper provisions a parent with enough seats to exercise multi-child scenarios by
/// upserting a Premium (3 included seats) Active subscription directly in the Billing schema —
/// bypassing the payment-provider checkout flow, which is out of scope for the Identity/Parent
/// acceptance tests. Idempotent: if the parent already has a billing-active subscription it is
/// upgraded in place rather than duplicated (the <c>ParentUserId WHERE Status=Active</c> unique
/// index forbids a second active row).</para>
/// </summary>
public static class SeatTestSupport
{
    /// <summary>
    /// Gives the parent enough seats for multi-child tests by upserting a Premium Active
    /// subscription (3 included seats) plus <paramref name="extraSeats"/> purchased seats
    /// (capacity is capped at <c>seats.max</c>=5 by the seat service). Seeds directly in the
    /// Billing DbContext — no payment flow.
    /// </summary>
    public static async Task GrantSeatsAsync(LearnexiaWebAppFactory factory, int parentUserId, int extraSeats = 0)
    {
        using var scope = factory.Services.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var now = DateTime.UtcNow;

        var sub = await db.Subscriptions.FirstOrDefaultAsync(s =>
            s.ParentUserId == parentUserId
            && (s.Status == SubscriptionStatus.Active
                || s.Status == SubscriptionStatus.PastDue
                || s.Status == SubscriptionStatus.Dunning));

        if (sub is null)
        {
            sub = new Subscription
            {
                ParentUserId        = parentUserId,
                PlanCode            = PlanCode.Premium,
                Status              = SubscriptionStatus.Active,
                BillingPeriod       = BillingPeriod.Monthly,
                CurrentCycleStart   = now,
                CurrentCycleEnd     = now.AddMonths(1),
                PurchasedExtraSeats = extraSeats,
                CreatedAt           = now,
                CreatedBy           = parentUserId,
            };
            db.Subscriptions.Add(sub);
        }
        else
        {
            sub.PlanCode            = PlanCode.Premium;
            sub.Status              = SubscriptionStatus.Active;
            sub.PurchasedExtraSeats = Math.Max(sub.PurchasedExtraSeats, extraSeats);
        }

        await db.SaveChangesAsync(0);
    }

    /// <summary>
    /// Convenience overload: resolves the parent user id from a Learnexia JWT (the <c>Id</c> claim)
    /// then provisions seats. Used by tests that hold the parent token but not the id.
    /// </summary>
    public static Task GrantSeatsAsync(LearnexiaWebAppFactory factory, string parentJwt, int extraSeats = 0)
        => GrantSeatsAsync(factory, DecodeUserId(parentJwt), extraSeats);

    /// <summary>Decodes the integer user id from a Learnexia JWT (the <c>Id</c> claim).</summary>
    public static int DecodeUserId(string jwt)
    {
        var parts   = jwt.Split('.');
        var payload = parts[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var decoded = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json    = Encoding.UTF8.GetString(decoded);
        var doc     = JsonDocument.Parse(json).RootElement;

        foreach (var prop in doc.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
            var raw = prop.Value.ValueKind == JsonValueKind.Number
                ? prop.Value.GetInt32().ToString()
                : prop.Value.GetString() ?? "";
            if (int.TryParse(raw, out var id)) return id;
        }
        throw new InvalidOperationException($"Cannot parse user id from JWT: {json}");
    }
}
