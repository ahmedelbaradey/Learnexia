import Image from 'next/image';

import styles from './SiteFooter.module.css';
import { LANDING_COPY as C } from '../../lib/copy';

/**
 * Two-column site footer — logo-mark + copyright (left), legal/support links
 * (right). Matches `design-system/preview/web-footer.html`. The "العربية"
 * link is the future language switch (AR is out of scope for now — it links to
 * the same surface as a placeholder, per the EN-only HANDOFF note).
 */
export function SiteFooter() {
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
        <a href="#top" lang="ar" dir="rtl">
          {footer.links.arabic}
        </a>
      </nav>
    </footer>
  );
}
