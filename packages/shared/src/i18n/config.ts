/**
 * react-i18next initialization.
 *
 * Framework-agnostic init: creates (or reuses) an i18next instance bound to
 * react-i18next with the ar/en resources. The app calls `initI18n` once at
 * startup, passing the initial locale (resolved from the user's preference /
 * device). Arabic is the default per product (Arabic-first).
 */

import i18n, { type i18n as I18nInstance } from 'i18next';
import { initReactI18next } from 'react-i18next';

import { DEFAULT_LOCALE, LOCALES, type Locale } from '../constants';
import { ar, en } from './resources';

/**
 * Components access the whole tree as one flat namespace via dotted keys
 * (e.g. `t('auth.login.title')`, `t('common.appName')`), so load each language
 * under a single namespace rather than splitting per top-level group.
 */
const DEFAULT_NS = 'app';
const singleNsResources = {
  en: { [DEFAULT_NS]: en },
  ar: { [DEFAULT_NS]: ar },
} as const;

export interface InitI18nOptions {
  /** Initial locale; defaults to the product default (ar). */
  locale?: Locale;
  /** Enable i18next debug logging (dev only). */
  debug?: boolean;
}

let initialized = false;

/** Initialize the shared i18next instance. Idempotent. */
export function initI18n(options: InitI18nOptions = {}): I18nInstance {
  const { locale = DEFAULT_LOCALE, debug = false } = options;

  if (initialized) {
    if (i18n.language !== locale) {
      void i18n.changeLanguage(locale);
    }
    return i18n;
  }

  void i18n.use(initReactI18next).init({
    resources: singleNsResources,
    lng: locale,
    fallbackLng: 'en',
    supportedLngs: LOCALES,
    ns: [DEFAULT_NS],
    defaultNS: DEFAULT_NS,
    debug,
    interpolation: {
      escapeValue: false, // React already escapes.
    },
    returnNull: false,
  });

  initialized = true;
  return i18n;
}

/** The shared i18next instance (initialize via `initI18n` first). */
export { i18n };

/** Change the active language at runtime (web). For native, also flip RTL. */
export async function changeLocale(locale: Locale): Promise<void> {
  await i18n.changeLanguage(locale);
}
