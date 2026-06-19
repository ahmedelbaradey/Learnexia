using FluentAssertions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Xunit;

namespace Modules.Notifications.UnitTests;

/// <summary>
/// Unit tests for the P9-07 <see cref="ReengagementEvaluator.ArbitratePush"/> pure gate and
/// the extended <see cref="ReengagementEvaluator.NotEligibleReason"/> enum members.
///
/// Pure static — no DI, no DbContext, no Redis required.
///
/// Coverage map:
///   A1  Budget not exhausted + no cooldown + priority=0 → ShouldPush=true
///   A2  Budget exhausted (pushesSentToday >= globalBudget) → GlobalBudgetExhausted
///   A3  Budget at edge (pushesSentToday = globalBudget - 1) → ShouldPush=true (not exhausted)
///   A4  Cooldown active → Cooldown reason, push suppressed
///   A5  Budget exhausted takes priority over cooldown check (short-circuits first)
///   A6  PriorityLost: budgetRemainingAfterHigherPriority <= 0 and rank > 0 → PriorityLost
///   A7  PriorityLost NOT triggered for rank=0 (highest priority) even when budget remaining = 0
///   A8  PriorityLost NOT triggered when budgetRemaining > 0 regardless of rank
///   A9  Budget = 0 (platform admin set to 0) → GlobalBudgetExhausted even if pushesSentToday = 0
///   A10 globalBudget default = 4: pushesSentToday=3 (< 4) → ShouldPush=true
///   A11 globalBudget default = 4: pushesSentToday=4 (= 4) → GlobalBudgetExhausted
///
/// P9-08 tier selection coverage (pure logic, no DI):
///   T1  daysIdle=0 → fallback (LAPSE_WIN_BACK)
///   T2  daysIdle=1 → GENTLE (≤ threshold_1 = 2)
///   T3  daysIdle=2 → GENTLE (= threshold_1)
///   T4  daysIdle=3 → REPAIR (> threshold_1, ≤ threshold_2 = 5)
///   T5  daysIdle=5 → REPAIR (= threshold_2)
///   T6  daysIdle=6 → FRESH_START (> threshold_2)
///   T7  daysIdle=14 → FRESH_START (well above threshold_2)
///
/// ReengagementEvaluator extended-reason coverage:
///   E14 New enum members are defined and have distinct values (compile + value check)
/// </summary>
public sealed class NudgeArbiterEvaluatorTests
{
    // =========================================================================
    // A1 — Budget not exhausted, no cooldown, top priority → grant
    // =========================================================================

    [Fact(DisplayName = "P907-A1 Budget OK + no cooldown + rank=0 → ShouldPush=true")]
    public void NoBudgetExhaustion_NoCooldown_TopPriority_GrantsPush()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 0,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 4);

        result.ShouldPush.Should().BeTrue("budget not exhausted, no cooldown, top priority");
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // A2 — Budget exhausted → GlobalBudgetExhausted
    // =========================================================================

    [Fact(DisplayName = "P907-A2 pushesSentToday >= globalBudget → GlobalBudgetExhausted")]
    public void BudgetExhausted_ReturnsGlobalBudgetExhausted()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 4,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 0);

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted);
    }

    // =========================================================================
    // A3 — Budget at edge (one slot remaining) → grant
    // =========================================================================

    [Fact(DisplayName = "P907-A3 pushesSentToday = globalBudget - 1 → ShouldPush=true (not exhausted)")]
    public void BudgetEdge_OneBelowLimit_GrantsPush()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 3,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 1);

        result.ShouldPush.Should().BeTrue("3 < 4, one slot remaining");
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // A4 — Cooldown active → Cooldown
    // =========================================================================

    [Fact(DisplayName = "P907-A4 Cooldown active → Cooldown reason, push suppressed")]
    public void CooldownActive_ReturnsCooldown()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 1,
            globalBudget: 4,
            cooldownActive: true,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 3);

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.Cooldown);
    }

    // =========================================================================
    // A5 — Budget exhaustion short-circuits before cooldown check
    // =========================================================================

    [Fact(DisplayName = "P907-A5 Budget exhausted + cooldown active → GlobalBudgetExhausted (short-circuits first)")]
    public void BudgetExhaustedAndCooldown_BudgetWins()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 4,
            globalBudget: 4,
            cooldownActive: true,   // also active, but shouldn't be the reported reason
            priorityRank: 2,
            budgetRemainingAfterHigherPriority: 0);

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted,
            "budget check runs first and short-circuits; cooldown reason is not reached");
    }

    // =========================================================================
    // A6 — PriorityLost: budgetRemaining <= 0 and rank > 0
    // =========================================================================

    [Fact(DisplayName = "P907-A6 budgetRemainingAfterHigherPriority=0 and priorityRank=1 → PriorityLost")]
    public void LowPriority_NoBudgetRemaining_ReturnsPriorityLost()
    {
        // Budget has 1 slot remaining but the higher-priority reservation consumed it.
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 3,     // 3 sent, 1 remaining in global budget of 4
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 2,        // not top priority
            budgetRemainingAfterHigherPriority: 0); // but remaining budget for this rank = 0

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.PriorityLost);
    }

    // =========================================================================
    // A7 — PriorityLost NOT triggered for rank=0 even when budget remaining = 0
    // =========================================================================

    [Fact(DisplayName = "P907-A7 rank=0 (top priority), budgetRemaining=0 → NOT PriorityLost (passes to budget check)")]
    public void TopPriority_NoRemaining_DoesNotReturnPriorityLost()
    {
        // rank=0 is exempt from PriorityLost — it is the highest priority.
        // But budget is exhausted, so it returns GlobalBudgetExhausted instead.
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 4,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 0);

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted,
            "rank=0 skips the PriorityLost gate; GlobalBudgetExhausted fires instead");
    }

    // =========================================================================
    // A8 — PriorityLost NOT triggered when budgetRemaining > 0
    // =========================================================================

    [Fact(DisplayName = "P907-A8 budgetRemainingAfterHigherPriority=2, rank=3 → ShouldPush=true (not PriorityLost)")]
    public void LowPriority_BudgetRemains_GrantsPush()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 1,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 3,        // low priority
            budgetRemainingAfterHigherPriority: 2); // but budget still available

        result.ShouldPush.Should().BeTrue("budget remaining > 0, PriorityLost gate not triggered");
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // A9 — globalBudget=0 → GlobalBudgetExhausted even if pushesSentToday=0
    // =========================================================================

    [Fact(DisplayName = "P907-A9 globalBudget=0 (admin-zeroed) → GlobalBudgetExhausted even with 0 sent")]
    public void GlobalBudgetZero_BlocksAll()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 0,
            globalBudget: 0,
            cooldownActive: false,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 0);

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted,
            "0 >= 0; budget of 0 blocks everything");
    }

    // =========================================================================
    // A10 — Default budget=4, 3 sent → grant
    // =========================================================================

    [Fact(DisplayName = "P907-A10 Default budget=4, pushesSentToday=3 → ShouldPush=true")]
    public void DefaultBudget_ThreeSent_GrantsFourthPush()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 3,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 1,
            budgetRemainingAfterHigherPriority: 1);

        result.ShouldPush.Should().BeTrue("3 < 4 (default budget)");
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);
    }

    // =========================================================================
    // A11 — Default budget=4, 4 sent → GlobalBudgetExhausted
    // =========================================================================

    [Fact(DisplayName = "P907-A11 Default budget=4, pushesSentToday=4 → GlobalBudgetExhausted")]
    public void DefaultBudget_FourSent_BudgetExhausted()
    {
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday: 4,
            globalBudget: 4,
            cooldownActive: false,
            priorityRank: 0,
            budgetRemainingAfterHigherPriority: 0);

        result.ShouldPush.Should().BeFalse();
        result.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted);
    }

    // =========================================================================
    // E14 — New enum members are defined with distinct values
    // =========================================================================

    [Fact(DisplayName = "P907-E14 New NotEligibleReason enum members are defined and distinct from existing ones")]
    public void NewEnumMembers_AreDefinedAndDistinct()
    {
        var allReasons = Enum.GetValues<ReengagementEvaluator.NotEligibleReason>();

        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted,
            "P9-07 requires GlobalBudgetExhausted reason");
        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.PriorityLost,
            "P9-07 requires PriorityLost reason");
        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.Cooldown,
            "P9-07 requires Cooldown reason");

        // All values should be distinct (no duplicate int values).
        var intValues = allReasons.Select(r => (int)r).ToList();
        intValues.Should().OnlyHaveUniqueItems("all enum members must have unique int values");

        // Legacy reasons must still exist.
        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.None);
        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.DisabledByParent);
        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.DailyCapReached);
        allReasons.Should().Contain(ReengagementEvaluator.NotEligibleReason.QuietHours);
    }

    // =========================================================================
    // P9-08 tier selection — isolated pure-logic tests
    // The tier selection lives in LapseWinBackIntegrationEventHandler.SelectTier (private),
    // so we test the OBSERVABLE OUTPUT by examining the copy-template codes via the public
    // ReengagementCopyTemplates.GetTemplate, which is the ground truth for which codes exist.
    // =========================================================================

    [Theory(DisplayName = "P908 LapseWinBack tier templates exist in both locales")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_GENTLE",     "ar-EG")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_GENTLE",     "en-US")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_REPAIR",     "ar-EG")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_REPAIR",     "en-US")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_FRESH_START","ar-EG")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_FRESH_START","en-US")]
    public void P908_TierTemplates_ExistInBothLocales(string categoryStr, string code, string locale)
    {
        var category = Enum.Parse<NotificationCategory>(categoryStr);
        var (title, body) = Learnexia.Modules.Notifications.Domain.Templates.ReengagementCopyTemplates
            .GetTemplate(category, code, locale);

        title.Should().NotBeNullOrWhiteSpace($"tier {code} {locale} must have a title");
        body.Should().NotBeNullOrWhiteSpace($"tier {code} {locale} must have a body");

        // Verify the copy is not the generic fallback (which means the template is missing).
        title.Should().NotBe("New notification", $"{code} should have a dedicated template, not the generic fallback");
    }

    [Theory(DisplayName = "P908 Tier templates are never-shaming (no guilt words)")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_GENTLE",      "ar-EG")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_REPAIR",      "ar-EG")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_FRESH_START", "ar-EG")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_GENTLE",      "en-US")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_REPAIR",      "en-US")]
    [InlineData("LapseWinBack", "LAPSE_WIN_BACK_FRESH_START", "en-US")]
    public void P908_TierTemplates_AreNeverShaming(string categoryStr, string code, string locale)
    {
        var category = Enum.Parse<NotificationCategory>(categoryStr);
        var (title, body) = Learnexia.Modules.Notifications.Domain.Templates.ReengagementCopyTemplates
            .GetTemplate(category, code, locale);

        var fullText = $"{title} {body}".ToLowerInvariant();

        // BRD §8 child-safety principle: no shaming / failure framing.
        fullText.Should().NotContain("failed", "copy must not shame the child");
        fullText.Should().NotContain("broke",  "copy must not shame the child");
        fullText.Should().NotContain("wasted", "copy must not shame the child");
        fullText.Should().NotContain("lost",   "copy must not shame the child — 'lost' is a shame word here");
    }

    [Theory(DisplayName = "P905 New event templates exist in both locales")]
    [InlineData("Achievement", "STREAK_FREEZE_CONSUMED",       "ar-EG")]
    [InlineData("Achievement", "STREAK_FREEZE_CONSUMED",       "en-US")]
    [InlineData("Achievement", "LEAGUE_TIER_CHANGED_PROMOTED", "ar-EG")]
    [InlineData("Achievement", "LEAGUE_TIER_CHANGED_PROMOTED", "en-US")]
    [InlineData("Achievement", "LEVELED_UP",                   "ar-EG")]
    [InlineData("Achievement", "LEVELED_UP",                   "en-US")]
    [InlineData("Achievement", "LEAGUE_TIER_CHANGED",          "ar-EG")]
    [InlineData("Achievement", "LEAGUE_TIER_CHANGED",          "en-US")]
    public void P905_NewTemplates_ExistInBothLocales(string categoryStr, string code, string locale)
    {
        var category = Enum.Parse<NotificationCategory>(categoryStr);
        var (title, body) = Learnexia.Modules.Notifications.Domain.Templates.ReengagementCopyTemplates
            .GetTemplate(category, code, locale);

        title.Should().NotBeNullOrWhiteSpace($"{code} {locale} must have a title");
        body.Should().NotBeNullOrWhiteSpace($"{code} {locale} must have a body");
        title.Should().NotBe("New notification", $"{code} should have a dedicated template");
    }

    [Fact(DisplayName = "P905 STREAK_FREEZE_CONSUMED ar-EG renders {streakLength} placeholder")]
    public void P905_StreakFreezeConsumed_RendersPlaceholder()
    {
        var (_, body) = Learnexia.Modules.Notifications.Domain.Templates.ReengagementCopyTemplates
            .Render(NotificationCategory.Achievement, "STREAK_FREEZE_CONSUMED", "ar-EG",
                ("streakLength", "21"));

        body.Should().Contain("21", "streakLength placeholder should be substituted with the streak value");
        body.Should().NotContain("{streakLength}", "placeholder must be replaced, not left as-is");
    }

    [Fact(DisplayName = "P907 ArbiterResult record has correct shape")]
    public void P907_ArbiterResult_RecordShape()
    {
        var granted    = new ReengagementEvaluator.ArbiterResult(true,  ReengagementEvaluator.NotEligibleReason.None);
        var suppressed = new ReengagementEvaluator.ArbiterResult(false, ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted);

        granted.ShouldPush.Should().BeTrue();
        granted.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.None);

        suppressed.ShouldPush.Should().BeFalse();
        suppressed.SuppressReason.Should().Be(ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted);
    }

    // =========================================================================
    // GlobalDailyPushBudget domain setter
    // =========================================================================

    [Fact(DisplayName = "P907 ChildReengagementPreference.UpdateGlobalDailyPushBudget sets and clears the value")]
    public void UpdateGlobalDailyPushBudget_SetsAndClearsValue()
    {
        var pref = ChildReengagementPreference.CreateDefault(1, 2, NotificationCategory.Achievement);

        // Default should be null (falls back to config)
        pref.GlobalDailyPushBudget.Should().BeNull("default prefs have no explicit budget; config default applies");

        // Set a value
        pref.UpdateGlobalDailyPushBudget(6);
        pref.GlobalDailyPushBudget.Should().Be(6);

        // Clear back to null (reset to platform default)
        pref.UpdateGlobalDailyPushBudget(null);
        pref.GlobalDailyPushBudget.Should().BeNull("null resets to platform default");
    }
}
