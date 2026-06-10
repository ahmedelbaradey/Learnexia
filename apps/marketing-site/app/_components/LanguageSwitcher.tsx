import type { Locale } from '../../lib/copy';
import styles from './LanguageSwitcher.module.css';

interface LanguageSwitcherProps {
  currentLocale: Locale;
}

/**
 * Top-nav EN/AR two-segment pill switcher.
 *
 * Matches the nav button styling from ar-web-nav.html.
 * Each segment is a plain <a> tag (full-document navigation) so that
 * middleware re-runs on every switch and the root layout re-renders with
 * the correct lang/dir. Using Next.js <Link> would do a client-side soft
 * nav that skips root-layout re-render, leaving <html lang dir> stale.
 * The active segment gets the active-pill visual (indigo-tinted background).
 *
 * data-testid="lang-switcher" for E2E targeting.
 */
export function LanguageSwitcher({ currentLocale }: LanguageSwitcherProps) {
  return (
    <div className={styles.switcher} data-testid="lang-switcher" role="navigation" aria-label="Language">
      {/* eslint-disable-next-line @next/next/no-html-link-for-pages -- full-document nav required: middleware must re-run to set x-locale; soft-nav skips root layout re-render and leaves <html lang dir> stale */}
      <a
        href="/en"
        className={`${styles.segment} ${currentLocale === 'en' ? styles.active : ''}`}
        aria-current={currentLocale === 'en' ? 'true' : undefined}
        hrefLang="en"
      >
        EN
      </a>
      {/* eslint-disable-next-line @next/next/no-html-link-for-pages -- full-document nav required: middleware must re-run to set x-locale; soft-nav skips root layout re-render and leaves <html lang dir> stale */}
      <a
        href="/ar"
        className={`${styles.segment} ${currentLocale === 'ar' ? styles.active : ''}`}
        aria-current={currentLocale === 'ar' ? 'true' : undefined}
        hrefLang="ar"
      >
        ع
      </a>
    </div>
  );
}
