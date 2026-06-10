import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import { REGISTER_URL } from '../../lib/config';
import styles from './CTABanner.module.css';

/**
 * Gradient CTA banner — indigo→purple, white "Create parent account" button.
 * Matches `design-system/preview/web-cta-banner.html` (LTR) and
 * `design-system/preview/ar-web-cta.html` (RTL). The decorative 🌟 sits
 * behind the content (aria-hidden) and pulses via the shared `lxpulse`
 * keyframe.
 */
interface CTABannerProps {
  locale: Locale;
}

export function CTABanner({ locale }: CTABannerProps) {
  const { cta } = getCopy(locale);

  return (
    <section id="pricing" className={styles.section}>
      <div className={styles.banner}>
        <span className={styles.decoStar} aria-hidden="true">
          {cta.star}
        </span>

        <div className={styles.copy}>
          <h2 className={styles.title}>{cta.title}</h2>
          <p className={styles.subtitle}>{cta.subtitle}</p>
        </div>

        <a className={styles.button} href={REGISTER_URL}>
          {cta.button}
        </a>
      </div>
    </section>
  );
}
