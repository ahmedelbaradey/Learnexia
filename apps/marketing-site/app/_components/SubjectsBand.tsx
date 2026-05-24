import styles from './SubjectsBand.module.css';
import { LANDING_COPY as C } from '../../lib/copy';

/**
 * Subjects band — 4-column grid (Math / Science / Arabic / English only; no
 * Social Studies per product override). Matches
 * `design-system/preview/web-subject-band.html`. Each tile's accent colour is
 * keyed off a `tone` token mapped to a CSS-module class.
 */
const TONE_CLASS = {
  indigo: styles.toneIndigo,
  green: styles.toneGreen,
  orange: styles.toneOrange,
  purple: styles.tonePurple,
} as const;

export function SubjectsBand() {
  const { subjects } = C;

  return (
    <section id="subjects" className={styles.section}>
      <div className={styles.inner}>
        <h2 className={styles.title}>{subjects.title}</h2>

        <div className={styles.grid}>
          {subjects.items.map((subject) => (
            <article
              key={subject.name}
              className={`${styles.card} ${TONE_CLASS[subject.tone]}`}
            >
              <span className={styles.icon} role="img" aria-hidden="true">
                {subject.icon}
              </span>
              <h3 className={styles.name}>{subject.name}</h3>
              <p className={styles.topics}>{subject.topics}</p>
              <span className={styles.grade}>{subjects.grade}</span>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}
