import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './FeaturesSection.module.css';

/**
 * "Why Learnexia" feature grid — 3 columns at 1024+, 2 at 768, 1 at 390.
 * Matches `design-system/preview/web-feature-card.html` (LTR) and
 * `design-system/preview/ar-web-features.html` (RTL). Icon tint colour is
 * driven by a per-card `tone` token (purple / orange / green) mapped to a
 * CSS-module class, never an inline literal.
 */
const TONE_CLASS = {
  purple: styles.tonePurple,
  orange: styles.toneOrange,
  green: styles.toneGreen,
} as const;

interface FeaturesSectionProps {
  locale: Locale;
}

export function FeaturesSection({ locale }: FeaturesSectionProps) {
  const { features } = getCopy(locale);

  return (
    <section id="how-it-works" className={styles.section}>
      <div className={styles.inner}>
        <p className={styles.eyebrow}>{features.eyebrow}</p>
        <h2 className={styles.title}>{features.title}</h2>

        <div className={styles.grid}>
          {features.cards.map((card) => (
            <article key={card.title} className={styles.card}>
              <span
                className={`${styles.icon} ${TONE_CLASS[card.tone]}`}
                role="img"
                aria-hidden="true"
              >
                {card.icon}
              </span>
              <h3 className={styles.cardTitle}>{card.title}</h3>
              <p className={styles.cardBody}>{card.body}</p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
