import Image from 'next/image';

import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './AITutorBubble.module.css';

/**
 * AITutorBubble — frosted-glass chat bubble with owl mascot avatar.
 * Source (LTR): design-system/preview/components-tutor.html
 * Source (RTL): design-system/preview/ar-tutor.html
 * RTL delta: border-end-start-radius:4px on .bubble is the ONLY structural diff
 *   — the logical property resolves bottom-left in LTR, bottom-right in RTL.
 * data-testid="ai-tutor-bubble"
 */
interface AITutorBubbleProps {
  locale: Locale;
}

export function AITutorBubble({ locale }: AITutorBubbleProps) {
  const { tutorBubble } = getCopy(locale);

  return (
    <section
      className={styles.section}
      data-testid="ai-tutor-bubble"
    >
      <div className={styles.inner}>
        <div className={styles.stage} data-dir={locale === 'ar' ? 'rtl' : 'ltr'}>
          {/* Owl mascot avatar */}
          <div className={styles.avatar}>
            <Image
              src="/assets/mascot-owl.svg"
              alt=""
              width={54}
              height={54}
              aria-hidden="true"
            />
          </div>

          {/* Frosted-glass speech bubble */}
          <div className={styles.bubble} data-testid="ai-tutor-bubble-inner">
            {/* Name label */}
            <div className={styles.name}>{tutorBubble.name}</div>

            {/* Message with inline highlighted word */}
            <div className={styles.msg}>
              {tutorBubble.msgLead}
              <span className={styles.highlight}>{tutorBubble.msgHighlight}</span>
              {tutorBubble.msgRest}
            </div>

            {/* Suggestion chips — decorative buttons */}
            <div className={styles.chips} data-testid="ai-tutor-chips">
              {tutorBubble.chips.map((chip) => (
                <button
                  key={chip}
                  type="button"
                  className={styles.chip}
                  /* Decorative — no handler */
                >
                  {chip}
                </button>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
