import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import frameStyles from './PhoneMockup.module.css';
import styles from './ChildCardPhone.module.css';

/**
 * ChildCardPhone — child-card content displayed inside a phone mock device frame.
 * Source (LTR): design-system/preview/mobile-child-card.html
 * Source (RTL): design-system/preview/ar-child-card.html
 *
 * Device shell: reuses frameStyles.stage / .phone / .screen from PhoneMockup.module.css
 * (read-only — PhoneMockup.tsx and its CSS are NOT modified per §4.1 constraint).
 * styles.screenChild overrides .screen background to var(--lx-bg) (flat dark canvas).
 *
 * data-testid="child-card-phone"
 */
interface ChildCardPhoneProps {
  locale: Locale;
}

export function ChildCardPhone({ locale }: ChildCardPhoneProps) {
  const { childCard } = getCopy(locale);

  return (
    <section
      className={styles.section}
      data-testid="child-card-phone"
    >
      <div className={styles.inner}>
        {/* Device shell — reused frame classes, screen background overridden */}
        <div className={frameStyles.stage}>
          <div className={frameStyles.phone}>
            <div className={`${frameStyles.screen} ${styles.screenChild}`}>
              {/* ── Child card ── */}
              <div className={styles.card}>

                {/* Row 1: avatar + name/grade/email + chevron */}
                <div className={styles.r1} data-testid="child-card-row1">
                  {/* Orange monogram avatar */}
                  <div
                    className={styles.av}
                    role="img"
                    aria-label={childCard.name}
                  >
                    {childCard.monogram}
                  </div>

                  {/* Name + grade + email group */}
                  <div className={styles.info}>
                    <div className={styles.nameRow}>
                      <span className={styles.nm}>{childCard.name}</span>
                      <span className={styles.gb}>{childCard.grade}</span>
                    </div>
                    {/*
                     * Email: direction:ltr in both locales — technical string.
                     * text-align:start ensures it aligns correctly after the ltr pin.
                     */}
                    <div
                      className={styles.em}
                      data-testid="child-card-email"
                    >
                      {childCard.email}
                    </div>
                  </div>

                  {/* Trailing chevron — glyph comes from copy (› LTR / ‹ RTL) */}
                  <span
                    className={styles.chev}
                    aria-hidden="true"
                  >
                    {childCard.chevron}
                  </span>
                </div>

                {/* Row 2: stats + status dot */}
                <div className={styles.stats} data-testid="child-card-stats">
                  <span className={`${styles.st} ${styles.stLevel}`}>
                    {childCard.statLevel}
                  </span>
                  <span className={`${styles.st} ${styles.stXp}`}>
                    {childCard.statXp}
                  </span>
                  <span className={`${styles.st} ${styles.stStreak}`}>
                    {childCard.statStreak}
                  </span>
                  {/* Status dot — floats to trailing edge via margin-inline-start:auto */}
                  <span
                    className={styles.dot}
                    data-testid="child-card-status"
                  >
                    {childCard.statusActive}
                  </span>
                </div>

                {/* Footer: language + view-progress */}
                <div className={styles.ft} data-testid="child-card-footer">
                  <span>
                    <span className={styles.ftLabel}>{childCard.langLabel}</span>
                    {' '}{childCard.langValue}
                  </span>
                  <span className={styles.vp}>{childCard.viewProgress}</span>
                </div>

              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
