import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './BenefitsPanel.module.css';

/**
 * BenefitsPanel — full-width purple-gradient panel.
 * Source: design-system/preview/web-benefits-panel.html
 * RTL: logical properties only (no AR-specific preview exists).
 * data-testid="benefits-panel"
 */
interface BenefitsPanelProps {
  locale: Locale;
}

export function BenefitsPanel({ locale }: BenefitsPanelProps) {
  const { benefits } = getCopy(locale);

  return (
    <section
      className={styles.section}
      data-testid="benefits-panel"
    >
      <div className={styles.inner}>
        <span
          className={styles.glyph}
          role="img"
          aria-label={locale === 'ar' ? 'تحكم' : 'controller'}
        >
          {benefits.glyph}
        </span>

        <h2 className={styles.heading}>{benefits.heading}</h2>

        <ul className={styles.list} role="list">
          {benefits.items.map((item) => (
            <li key={item.icon} className={styles.row}>
              <span
                className={styles.tile}
                role="img"
                aria-hidden="true"
              >
                {item.icon}
              </span>
              <span>{item.text}</span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
