import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './ActivityChart.module.css';

/**
 * ActivityChart — 7-bar static XP chart.
 * Source: design-system/preview/web-activity-chart.html
 * Bar heights are pixel values (not proportional to values) per the preview.
 * Sunday (index 6) is the highlighted bar.
 * RTL: flex-direction row-reverse mirrors bar order (Sun on the left).
 * data-testid="activity-chart"
 */

/** Static display values mapping Mon→Sun (index 0–6). */
const CHART_VALUES = [45, 80, 30, 95, 50, 70, 110] as const;

/**
 * Pixel heights as transcribed from web-activity-chart.html.
 * Mon=60, Tue=100, Wed=40, Thu=115, Fri=65, Sat=90, Sun=140
 */
const BAR_HEIGHTS = [60, 100, 40, 115, 65, 90, 140] as const;

/** Sunday is index 6. */
const HIGHLIGHT_INDEX = 6;

interface ActivityChartProps {
  locale: Locale;
}

export function ActivityChart({ locale }: ActivityChartProps) {
  const { activityChart } = getCopy(locale);

  return (
    <section
      className={styles.section}
      data-testid="activity-chart"
    >
      <div className={styles.inner}>
        <div className={styles.card}>
          {/* Header row: title+subtitle left, Export CSV button right */}
          <div className={styles.header}>
            <div>
              <div className={styles.title}>{activityChart.title}</div>
              <div className={styles.subtitle}>{activityChart.subtitle}</div>
            </div>
            <button
              type="button"
              className={styles.exportBtn}
              aria-label={activityChart.exportBtn}
              /* Decorative/inert — no handler per spec */
            >
              {activityChart.exportBtn}
            </button>
          </div>

          {/* Bars track */}
          <div className={styles.bars} data-testid="activity-chart-bars">
            {CHART_VALUES.map((_, idx) => {
              const isHighlight = idx === HIGHLIGHT_INDEX;
              return (
                <div
                  key={activityChart.days[idx]}
                  className={styles.col}
                  data-testid={`activity-chart-col-${idx}`}
                >
                  {/* Bar */}
                  <div
                    className={isHighlight ? `${styles.bar} ${styles.barHi}` : styles.bar}
                    style={{ height: `${BAR_HEIGHTS[idx]}px` }}
                  >
                    {/* Value label above the bar */}
                    <span
                      className={isHighlight ? `${styles.value} ${styles.valueHi}` : styles.value}
                      aria-hidden="true"
                    >
                      {activityChart.values[idx]}
                    </span>
                  </div>
                  {/* Day label below the bar */}
                  <span
                    className={isHighlight ? `${styles.day} ${styles.dayHi}` : styles.day}
                  >
                    {activityChart.days[idx]}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      </div>
    </section>
  );
}
