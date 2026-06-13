/**
 * reportsFormat — pure helpers for the A4 Reports page + A5 attempts panel.
 *
 * Range filtering over REAL `AttemptListItemDto[]` (client-side on `startedAt`,
 * acceptable this wave per A4 spec §2.1) and locale-aware number/duration/date
 * formatting (Eastern-Arabic numerals in AR prose via `Intl`, per the align
 * spec M-25 precedent in OverviewWeb).
 *
 * Unit glyphs (h/m/s ↔ س/د/ث, % ↔ ٪) are fixed locale symbols, not free-text
 * copy — they live with the formatter rather than the i18n bundle, mirroring
 * the sanctioned `DURATION_UNITS` pattern in OverviewWeb.
 */
import type { AttemptListItemDto } from '@learnexia/api-client';

/** Fixed report-range set (A4 §2.1). Referenced by value, never raw strings. */
export const REPORT_RANGE = {
  Week: 'week',
  Month: 'month',
  All: 'all',
} as const;

export type ReportRangeValue = (typeof REPORT_RANGE)[keyof typeof REPORT_RANGE];

const DAY_MS = 86_400_000;

/** Rolling window length per range (days); `null` = unbounded (All time). */
const RANGE_WINDOW_DAYS: Record<ReportRangeValue, number | null> = {
  [REPORT_RANGE.Week]: 7,
  [REPORT_RANGE.Month]: 30,
  [REPORT_RANGE.All]: null,
};

/**
 * `startedAt` epoch ms. NSwag types it as `Date` but the runtime value is the
 * raw ISO string (no jsonParseReviver) — normalize defensively.
 */
export function attemptTimestamp(attempt: AttemptListItemDto): number {
  const raw = attempt.startedAt as unknown;
  if (raw == null) return Number.NaN;
  const date = raw instanceof Date ? raw : new Date(raw as string);
  return date.getTime();
}

export interface RangeWindows {
  /** Attempts inside the selected range (newest first). */
  current: AttemptListItemDto[];
  /** Attempts in the previous equal-length window (empty for All time). */
  previous: AttemptListItemDto[];
}

/**
 * Split attempts into the selected rolling window + the previous equal-length
 * window (for KPI deltas, A4 §2.2). Both lists sort newest-first.
 */
export function splitByRange(
  attempts: AttemptListItemDto[],
  range: ReportRangeValue,
  now: number = Date.now(),
): RangeWindows {
  const sorted = [...attempts].sort(
    (a, b) => attemptTimestamp(b) - attemptTimestamp(a),
  );
  const windowDays = RANGE_WINDOW_DAYS[range];
  if (windowDays == null) {
    return { current: sorted, previous: [] };
  }
  const windowMs = windowDays * DAY_MS;
  const currentStart = now - windowMs;
  const previousStart = now - 2 * windowMs;
  const current: AttemptListItemDto[] = [];
  const previous: AttemptListItemDto[] = [];
  for (const attempt of sorted) {
    const ts = attemptTimestamp(attempt);
    if (Number.isNaN(ts)) continue;
    if (ts >= currentStart) current.push(attempt);
    else if (ts >= previousStart) previous.push(attempt);
  }
  return { current, previous };
}

/* ------------------------------------------------------------------ */
/* Locale-aware formatting                                             */
/* ------------------------------------------------------------------ */

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

export function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** Fixed percent glyphs per locale (unit symbols, not copy). */
const PERCENT_SIGN = { en: '%', ar: '٪' } as const;

/** "84%" / "٨٤٪". */
export function formatPercent(value: number, locale: string): string {
  const sign = locale === 'ar' ? PERCENT_SIGN.ar : PERCENT_SIGN.en;
  return `${formatNumber(Math.round(value), locale)}${sign}`;
}

/**
 * Signed delta value for the "+38% vs last week" line — the sign travels with
 * the localized digits ("+٣٨"); the surrounding phrase + percent sign come
 * from the i18n key.
 */
export function formatSignedNumber(value: number, locale: string): string {
  const rounded = Math.round(value);
  const abs = formatNumber(Math.abs(rounded), locale);
  return rounded < 0 ? `-${abs}` : `+${abs}`;
}

/** Fixed duration-unit glyphs per locale (same rule as OverviewWeb M-25). */
const DURATION_UNITS = {
  en: { hour: 'h', minute: 'm', second: 's' },
  ar: { hour: 'س', minute: 'د', second: 'ث' },
} as const;

/** KPI "time learning": "3h 24m" / "٣س ٢٤د" (minutes only when < 1h). */
export function formatHoursMinutes(totalSeconds: number, locale: string): string {
  const units = locale === 'ar' ? DURATION_UNITS.ar : DURATION_UNITS.en;
  const totalMinutes = Math.floor(totalSeconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  const fmt = (n: number) => formatNumber(n, locale);
  if (hours <= 0) return `${fmt(minutes)}${units.minute}`;
  return `${fmt(hours)}${units.hour} ${fmt(minutes)}${units.minute}`;
}

/** Attempt-row duration: "3m 20s" / "٣د ٢٠ث" (seconds only when < 1m). */
export function formatMinutesSeconds(totalSeconds: number, locale: string): string {
  const units = locale === 'ar' ? DURATION_UNITS.ar : DURATION_UNITS.en;
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = Math.round(totalSeconds % 60);
  const fmt = (n: number) => formatNumber(n, locale);
  if (minutes <= 0) return `${fmt(seconds)}${units.second}`;
  return `${fmt(minutes)}${units.minute} ${fmt(seconds)}${units.second}`;
}

/** "14:05" with locale digits (time may keep numeric AR digits per A5 §1). */
export function formatTimeOfDay(timestamp: number, locale: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(timestamp));
}

/** Short date "12 Jun" / "١٢ يونيو". */
export function formatShortDate(timestamp: number, locale: string): string {
  return new Intl.DateTimeFormat(intlLocale(locale), {
    day: 'numeric',
    month: 'short',
  }).format(new Date(timestamp));
}

/** Calendar-day equality (local time). */
export function isSameDay(a: number, b: number): boolean {
  const da = new Date(a);
  const db = new Date(b);
  return (
    da.getFullYear() === db.getFullYear() &&
    da.getMonth() === db.getMonth() &&
    da.getDate() === db.getDate()
  );
}
