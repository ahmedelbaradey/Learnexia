/**
 * RTL helpers.
 *
 * Direction is derived from the locale (Arabic = RTL). On the web, flipping
 * direction is instant (set `dir` on the document). On native (React Native),
 * `I18nManager.forceRTL` only takes full effect after an APP RELOAD — so the
 * language-switch UX must trigger a restart. We expose a documented hook
 * (`applyNativeRtl`) that the app wires to `I18nManager` + `react-native-restart`;
 * we do NOT import those native modules here (this package stays
 * framework-agnostic) and we do NOT auto-restart — the trigger is the app's
 * language-switch action.
 */

import { RTL_LOCALES, type Locale } from '../constants';

export type Direction = 'ltr' | 'rtl';

/** Whether a locale renders right-to-left. */
export function isRtlLocale(locale: string): boolean {
  return (RTL_LOCALES as readonly string[]).includes(locale);
}

/** Direction for a locale. */
export function directionForLocale(locale: string): Direction {
  return isRtlLocale(locale) ? 'rtl' : 'ltr';
}

/** Minimal slice of RN's `I18nManager` we depend on (structural typing). */
export interface I18nManagerLike {
  isRTL: boolean;
  allowRTL(allow: boolean): void;
  forceRTL(force: boolean): void;
}

/** Minimal slice of `react-native-restart` we depend on. */
export interface RestartLike {
  restart(): void;
}

/**
 * Apply the locale's direction on NATIVE.
 *
 * The app injects RN's `I18nManager` and (optionally) `react-native-restart`.
 * If the desired direction differs from the current one, this forces RTL and
 * returns whether a reload is required. Because `forceRTL` only applies after
 * a reload, the app should call `restart()` (or prompt the user) when this
 * returns `true`. If `restart` is provided, we trigger it for the caller.
 *
 * @returns `true` if a reload was needed (direction changed).
 */
export function applyNativeRtl(
  locale: Locale,
  i18nManager: I18nManagerLike,
  restart?: RestartLike,
): boolean {
  const wantRtl = isRtlLocale(locale);
  if (i18nManager.isRTL === wantRtl) {
    return false;
  }
  i18nManager.allowRTL(wantRtl);
  i18nManager.forceRTL(wantRtl);
  // forceRTL only takes effect after a reload.
  restart?.restart();
  return true;
}

/**
 * Apply the locale's language on WEB by setting `lang` on the document element.
 * No reload required. Safe to call when there is no DOM (no-op).
 *
 * NOTE: We deliberately do NOT set `dir` here. React Native Web handles RTL
 * layout through explicit component-level props (`flexDirection: 'row-reverse'`,
 * `alignItems: 'flex-end'`, etc.). Setting `dir="rtl"` on the HTML root causes
 * CSS to flip the flex main-axis for `row` AND for `row-reverse`, resulting in a
 * double-reversal that undoes every RTL-aware prop in the component tree (the
 * layout renders as LTR despite the RTL intent). Keeping `dir` off the root lets
 * component-level RTL props work exactly as they do on native React Native.
 * Arabic text rendering is handled by `writingDirection="rtl"` and
 * `textAlign="right"` on individual Text elements.
 */
export function applyWebDirection(locale: Locale): void {
  if (typeof globalThis === 'undefined') return;
  const doc = (globalThis as { document?: { documentElement?: { lang: string } } }).document;
  if (!doc?.documentElement) return;
  doc.documentElement.lang = locale;
}
