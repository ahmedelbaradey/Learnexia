import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './PhoneMockup.module.css';

/**
 * Decorative phone mockup for the landing hero (right column). This is
 * marketing art, not the real app — it faithfully approximates the captured
 * in-phone content (header with child name + streak, a "Continue learning"
 * progress card, and a 2×2 subject grid) plus the floating "+50 XP" and
 * "New badge!" reward chips, all expressed through `--lx-*` design tokens.
 *
 * Purely decorative → the parent <section> is `aria-hidden`.
 *
 * IMPORTANT: This file and PhoneMockup.module.css are the live hero component.
 * Do NOT generalize this to accept children — that is ChildCardPhone's job.
 */
interface PhoneMockupProps {
  locale: Locale;
}

export function PhoneMockup({ locale }: PhoneMockupProps) {
  const { phone, chips } = getCopy(locale);

  return (
    <div className={styles.stage}>
      {/* Floating reward chips (sit outside the device frame). */}
      <span className={`${styles.chip} ${styles.chipXp}`}>{chips.xp}</span>
      <span className={`${styles.chip} ${styles.chipBadge}`}>
        <span className={styles.chipBadgeIcon}>🏆</span>
        {chips.badge}
      </span>

      <div className={styles.phone}>
        <div className={styles.screen}>
          {/* Header: child name + streak */}
          <div className={styles.header}>
            <span className={styles.childName}>{phone.childName}</span>
            <span className={styles.streak}>
              <span className={styles.streakFlame}>🔥</span>
              {phone.streak}
            </span>
          </div>

          {/* Continue-learning progress card */}
          <div className={styles.continueCard}>
            <span className={styles.continueLabel}>{phone.continueLearning}</span>
            <span className={styles.continueSubject}>{phone.continueSubject}</span>
            <span className={styles.progressTrack}>
              <span className={styles.progressFill} />
            </span>
          </div>

          {/* 2×2 subject grid */}
          <div className={styles.subjectGrid}>
            <div className={styles.subjectTile}>
              <span className={styles.subjectIcon} role="img" aria-label={phone.subjectMath}>
                🧮
              </span>
            </div>
            <div className={styles.subjectTile}>
              <span className={styles.subjectIcon} role="img" aria-label={phone.subjectScience}>
                🧪
              </span>
            </div>
            <div className={styles.subjectTile}>
              <span className={styles.subjectIcon} role="img" aria-label={phone.subjectArabic}>
                📖
              </span>
            </div>
            <div className={styles.subjectTile}>
              <span className={styles.subjectIcon} role="img" aria-label={phone.subjectEnglishLabel}>
                {phone.subjectEnglish}
              </span>
            </div>
          </div>

          {/* Decorative animated star near the bottom */}
          <span className={styles.star} role="img" aria-hidden="true">
            🌟
          </span>
        </div>
      </div>
    </div>
  );
}
