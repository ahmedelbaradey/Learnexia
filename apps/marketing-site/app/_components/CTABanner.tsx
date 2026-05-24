import styles from './CTABanner.module.css';
import { LANDING_COPY as C } from '../../lib/copy';
import { REGISTER_URL } from '../../lib/config';

/**
 * Gradient CTA banner — indigo→purple, white "Create parent account" button.
 * Matches `design-system/preview/web-cta-banner.html`. The decorative 🌟 sits
 * behind the content (aria-hidden) and pulses via the shared `lxpulse`
 * keyframe.
 */
export function CTABanner() {
  const { cta } = C;

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
