using FluentAssertions;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Learnexia.IntegrationTests;

// =============================================================================
// P10-06-BE-3 — live payment-provider slot (Paymob).
//
// The live adapter is a STUB until real Paymob API calls + keys exist. These tests
// pin the SAFETY contract of the seam:
//   1. Default (no/Fake config) resolves FakePaymentProvider for dev/staging.
//   2. When the PaymobPaymentProvider is the active IPaymentProvider, the app BOOTS
//      (no secrets needed) but EVERY payment operation throws a clear "not configured"
//      error — it must NEVER silently mock-approve (which is exactly what a silent
//      fall-back to FakePaymentProvider under a "Paymob" config would do).
//
// The config selection itself (Billing:PaymentProvider:Provider = "Fake" -> Fake;
// any other value -> PaymobPaymentProvider, NOT a Fake fall-back) is a 3-line if/else
// in Billing Infrastructure DependencyInjection, read at startup and verified by review.
// Overriding that startup-read key from the WebApplicationFactory is unreliable (it is
// read before the factory's ConfigureAppConfiguration applies — same reason the harness
// swaps the DbContext via ConfigureServices, not config), so TC2 exercises the stub by
// putting it in the seam the same way DI does.
// =============================================================================

[Collection("IntegrationTests")]
public sealed class P10_PaymobStub_Tests : IAsyncLifetime
{
    private readonly LearnexiaWebAppFactory _factory;

    public P10_PaymobStub_Tests(LearnexiaWebAppFactory factory) => _factory = factory;

    public async Task InitializeAsync() => await _factory.ApplyMigrationsAndSeedAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "P10-PaymobStub-01 Default provider resolves FakePaymentProvider (dev/staging mock)")]
    public void DefaultProvider_IsFake()
    {
        using var scope = _factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();

        provider.Should().BeOfType<FakePaymentProvider>(
            "the default (and explicit \"Fake\") provider is the dev/staging mock");
    }

    [Fact(DisplayName = "P10-PaymobStub-02 Paymob stub boots but every payment op fails loud (no silent mock-approval)")]
    public async Task PaymobStub_BootsButFailsLoud_OnEveryOperation()
    {
        // Put the stub in the seam exactly as the "Paymob" config branch does. Building the host
        // + resolving proves the app BOOTS with the Paymob adapter present (no secrets needed).
        using var paymobFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPaymentProvider>();
                services.AddScoped<IPaymentProvider, PaymobPaymentProvider>();
            }));

        using var scope = paymobFactory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPaymentProvider>();

        provider.Should().BeOfType<PaymobPaymentProvider>(
            "the Paymob slot must be a dedicated stub type, never a silent Fake fall-back");

        var payment = new Payment
        {
            ParentUserId   = 1,
            Amount         = 199m,
            Currency       = PaymentCurrency.EGP,
            Status         = PaymentStatus.Initiated,
            Kind           = PaymentKind.Subscription,
            IdempotencyKey = "paymob-stub-test",
        };

        // Checkout must NOT mint a session like the Fake mock — it must fail loud.
        var checkout = async () => await provider.CreateCheckoutSessionAsync(payment, default);
        (await checkout.Should().ThrowAsync<InvalidOperationException>(
            "the stub must fail loud, not mint a checkout session"))
            .WithMessage("*not implemented*");

        // Signature verification must NOT silently return true (that would admit an unverified webhook).
        var verify = () => provider.VerifyWebhookSignature([1, 2, 3], "sig");
        verify.Should().Throw<InvalidOperationException>(
            "a non-configured provider must reject webhooks, never accept them");

        var refund = async () => await provider.InitiateRefundAsync("ref", 10m, default);
        await refund.Should().ThrowAsync<InvalidOperationException>();

        var cancel = async () => await provider.CancelRecurringAsync("ref", default);
        await cancel.Should().ThrowAsync<InvalidOperationException>();
    }
}
