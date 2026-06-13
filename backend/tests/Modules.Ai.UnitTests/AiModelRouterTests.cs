using FluentAssertions;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="AiModelRouter"/>.
///
/// Coverage:
///   U01  Every AiTaskKind routes to a non-null (providerName, modelId) via built-in defaults.
///   U02  Cheap tasks → claude-haiku-4-5.
///   U03  Mid tasks → claude-sonnet-4-6.
///   U04  Premium tasks → claude-opus-4-8.
///   U05  Config-level task override changes route without recompile.
///   U06  Config-level task:tier override takes precedence over task-only override.
///   U07  Explicit TierHint=Premium on a cheap task escalates to claude-opus-4-8.
///   U08  Explicit TierHint=Cheap on a premium task demotes to claude-haiku-4-5.
///   U09  Unknown model in config: falls back to built-in default.
///   U10  All AiTaskKind enum members are covered (drift test).
/// </summary>
public sealed class AiModelRouterTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static AiModelRouter CreateRouter(
        Action<AiGatewayOptions>? configure = null)
    {
        var opts = new AiGatewayOptions();
        configure?.Invoke(opts);
        return new AiModelRouter(Options.Create(opts));
    }

    // -------------------------------------------------------------------------
    // U01 — every AiTaskKind routes to a non-empty result with default config
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "P301-U01 Every AiTaskKind routes to a valid (provider, modelId) with no config")]
    [MemberData(nameof(AllTaskKinds))]
    public void Route_AllTaskKinds_ReturnValidResult(AiTaskKind kind)
    {
        var router = CreateRouter();
        var result = router.Route(kind, null);

        result.Should().NotBeNull("every task kind must resolve a route");
        result.ProviderName.Should().NotBeNullOrWhiteSpace("provider name must be set");
        result.ModelId.Should().NotBeNullOrWhiteSpace("model ID must be set");
    }

    public static IEnumerable<object[]> AllTaskKinds() =>
        Enum.GetValues<AiTaskKind>().Select(k => new object[] { k });

    // -------------------------------------------------------------------------
    // U02 — cheap tasks → haiku
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "P301-U02 Cheap tasks route to claude-haiku-4-5 by default")]
    [InlineData(AiTaskKind.CheckAnswer)]
    [InlineData(AiTaskKind.Classify)]
    [InlineData(AiTaskKind.ShortTask)]
    public void Route_CheapTasks_ReturnHaiku(AiTaskKind kind)
    {
        var result = CreateRouter().Route(kind, null);
        result.ModelId.Should().Be("claude-haiku-4-5",
            $"{kind} is a cheap task and should map to claude-haiku-4-5 by default");
        result.ProviderName.Should().Be("Claude");
    }

    // -------------------------------------------------------------------------
    // U03 — mid tasks → sonnet
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "P301-U03 Mid tasks route to claude-sonnet-4-6 by default")]
    [InlineData(AiTaskKind.Explain)]
    [InlineData(AiTaskKind.Hint)]
    [InlineData(AiTaskKind.Simplify)]
    [InlineData(AiTaskKind.QuestionGeneration)]
    public void Route_MidTasks_ReturnSonnet(AiTaskKind kind)
    {
        var result = CreateRouter().Route(kind, null);
        result.ModelId.Should().Be("claude-sonnet-4-6",
            $"{kind} is a mid-tier task and should map to claude-sonnet-4-6 by default");
        result.ProviderName.Should().Be("Claude");
    }

    // -------------------------------------------------------------------------
    // U04 — premium tasks → opus
    // -------------------------------------------------------------------------

    [Theory(DisplayName = "P301-U04 Premium tasks route to claude-opus-4-8 by default")]
    [InlineData(AiTaskKind.AnalyzeDiagram)]
    [InlineData(AiTaskKind.HardReasoning)]
    [InlineData(AiTaskKind.ContentQa)]
    public void Route_PremiumTasks_ReturnOpus(AiTaskKind kind)
    {
        var result = CreateRouter().Route(kind, null);
        result.ModelId.Should().Be("claude-opus-4-8",
            $"{kind} is a premium task and should map to claude-opus-4-8 by default");
        result.ProviderName.Should().Be("Claude");
    }

    // -------------------------------------------------------------------------
    // U05 — config task override changes route without recompile
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "P301-U05 Config task-level override changes Hint route without recompile")]
    public void Route_ConfigTaskOverride_ChangesRoute()
    {
        var router = CreateRouter(opts =>
        {
            opts.Models["Hint"] = new ModelConfig
            {
                Provider = "OpenAi",
                ModelId  = "gpt-4o-mini",
            };
        });

        var result = router.Route(AiTaskKind.Hint, null);

        result.ProviderName.Should().Be("OpenAi",
            "config task override should change provider to OpenAi");
        result.ModelId.Should().Be("gpt-4o-mini",
            "config task override should change model ID to gpt-4o-mini");
    }

    // -------------------------------------------------------------------------
    // U06 — task:tier override takes precedence over task-only override
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "P301-U06 Config task:tier override takes precedence over task-only override")]
    public void Route_ConfigTaskTierOverride_TakesPrecedenceOverTaskOnly()
    {
        var router = CreateRouter(opts =>
        {
            opts.Models["Hint"] = new ModelConfig
            {
                Provider = "OpenAi",
                ModelId  = "gpt-4o-mini",
            };
            opts.Models["Hint:Mid"] = new ModelConfig
            {
                Provider = "Claude",
                ModelId  = "claude-opus-4-8", // unusual but valid override
            };
        });

        var result = router.Route(AiTaskKind.Hint, AiModelTier.Mid);

        result.ProviderName.Should().Be("Claude",
            "the Hint:Mid override should win over the Hint-only override");
        result.ModelId.Should().Be("claude-opus-4-8",
            "the Hint:Mid config entry explicitly set claude-opus-4-8");
    }

    // -------------------------------------------------------------------------
    // U07 — TierHint=Premium escalates cheap task to opus
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "P301-U07 TierHint=Premium on CheckAnswer escalates to claude-opus-4-8")]
    public void Route_TierHintPremium_EscalatesToOpus()
    {
        var result = CreateRouter().Route(AiTaskKind.CheckAnswer, AiModelTier.Premium);

        result.ModelId.Should().Be("claude-opus-4-8",
            "an explicit Premium tier hint should escalate to claude-opus-4-8");
        result.ProviderName.Should().Be("Claude");
    }

    // -------------------------------------------------------------------------
    // U08 — TierHint=Cheap demotes premium task to haiku
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "P301-U08 TierHint=Cheap on HardReasoning demotes to claude-haiku-4-5")]
    public void Route_TierHintCheap_DemotesToHaiku()
    {
        var result = CreateRouter().Route(AiTaskKind.HardReasoning, AiModelTier.Cheap);

        result.ModelId.Should().Be("claude-haiku-4-5",
            "an explicit Cheap tier hint should use claude-haiku-4-5 regardless of task default");
        result.ProviderName.Should().Be("Claude");
    }

    // -------------------------------------------------------------------------
    // U09 — empty model config entry is ignored; falls back to built-in default
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "P301-U09 Config entry with empty modelId falls back to built-in default")]
    public void Route_EmptyModelConfig_FallsBackToBuiltIn()
    {
        var router = CreateRouter(opts =>
        {
            opts.Models["Hint"] = new ModelConfig
            {
                Provider = "",   // empty — should be skipped
                ModelId  = "",
            };
        });

        var result = router.Route(AiTaskKind.Hint, null);

        result.ModelId.Should().Be("claude-sonnet-4-6",
            "an empty config entry should be ignored and fall back to the built-in default");
    }

    // -------------------------------------------------------------------------
    // U10 — drift test: all enum members have an entry in the default table
    // -------------------------------------------------------------------------

    [Fact(DisplayName = "P301-U10 AiModelRouter covers all AiTaskKind enum values (drift test)")]
    public void Route_AllEnumMembers_HaveBuiltInDefault()
    {
        var router = CreateRouter(); // no config overrides
        var allKinds = Enum.GetValues<AiTaskKind>();

        foreach (var kind in allKinds)
        {
            var result = router.Route(kind, null);
            result.Should().NotBeNull($"AiTaskKind.{kind} must have a built-in default route");
            result.ModelId.Should().NotBeNullOrWhiteSpace(
                $"AiTaskKind.{kind} must resolve to a non-empty model ID");
        }
    }
}
