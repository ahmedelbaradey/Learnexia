namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Lightweight timed-event projection surfaced on the dashboard banner (P4-11 Batch 4).
///
/// Fields:
///   <see cref="Code"/>       — stable analytics / i18n discriminator (e.g. "DOUBLE_XP_WEEKEND").
///   <see cref="NameEn"/>     — English display name for the banner.
///   <see cref="NameAr"/>     — Arabic display name for the banner.
///   <see cref="Multiplier"/> — the XP multiplier in effect (e.g. 2.0 → "x2 XP").
///   <see cref="EndUtc"/>     — countdown target for the FE timer.
///
/// StartUtc and Scope are deliberately omitted: the FE banner only needs the countdown
/// endpoint and the multiplier label. Scope is Gamification-internal.
///
/// Hand-projected from <c>ActiveTimedEventSnapshot</c> in <c>GetDashboardQueryHandler</c>;
/// no AutoMapper per CONVENTIONS.
/// </summary>
public sealed record ActiveTimedEventDto(
    string Code,
    string NameEn,
    string NameAr,
    decimal Multiplier,
    DateTime EndUtc);
