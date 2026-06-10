import Image from 'next/image';

import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './SiteFooter.module.css';

/**
 * Two-column site footer — logo-mark + copyright (start), legal/support
 * links (end). Matches `design-system/preview/web-footer.html`. The former
 * العربية stub has been removed — language switching is now handled by the
 * LanguageSwitcher in the top nav.
 */
interface SiteFooterProps {
  locale: Locale;
}

export function SiteFooter({ locale }: SiteFooterProps) {
  const C = getCopy(locale);
  const { footer } = C;

  return (
    <footer className={styles.footer}>
      <div className={styles.left}>
        <Image
          src="/assets/logo-mark.svg"
          alt={C.brandLogoAlt}
          width={28}
          height={28}
          className={styles.logoMark}
        />
        <span>{footer.rights}</span>
      </div>

      <nav className={styles.links} aria-label={C.brand}>
        <a href="#top">{footer.links.privacy}</a>
        <a href="#top">{footer.links.terms}</a>
        <a href="#top">{footer.links.support}</a>
      </nav>
    </footer>
  );
}
