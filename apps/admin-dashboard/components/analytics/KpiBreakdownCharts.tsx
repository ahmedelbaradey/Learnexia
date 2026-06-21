'use client';

/**
 * KpiBreakdownCharts — categorical breakdown bars for P7-10 Platform Analytics.
 *
 * Shows By Subject / By Grade / By Language horizontal bars from the embedded
 * `bySubject`/`byGrade`/`byLanguage` arrays in `PlatformKpiSummaryDto`.
 *
 * NOTE: There is NO /Analytics/trend endpoint — this is the approved substitution
 * for the original "trend charts" FE task (re-scoped per execution plan).
 *
 * Client-side slice selects (from AnalyticsFilters) filter/highlight which bars
 * are shown without triggering a new API call.
 */

import { useState } from 'react';
import type {
  PlatformKpiSummaryDto,
  SubjectBreakdown,
  GradeBreakdown,
  LanguageBreakdown,
} from '@learnexia/api-client';
import { BarBreakdown, type BarBreakdownDataPoint } from '../charts/BarBreakdown';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Metric type for tab strip ──────────────────────────────────────────────────
type MetricKey = 'lessonsCompleted' | 'totalAttempts' | 'distinctActiveStudents';

// ── Label mappings ─────────────────────────────────────────────────────────────

const SUBJECT_LABELS: Record<number, string> = {
  1: strings.analyticsSubjectMath,
  2: strings.analyticsSubjectScience,
  3: strings.analyticsSubjectArabic,
  4: strings.analyticsSubjectEnglish,
};

const LANGUAGE_LABELS: Record<number, string> = {
  0: strings.analyticsLanguageArabic,
  1: strings.analyticsLanguageEnglish,
};

// ── Shared card styles ────────────────────────────────────────────────────────

const sectionCard: React.CSSProperties = {
  backgroundColor: 'var(--lx-card)',
  borderRadius: 'var(--lx-radius-card)',
  border: '1px solid var(--lx-border)',
  padding: 20,
  display: 'flex',
  flexDirection: 'column',
  gap: 16,
};

const sectionHeadingStyle: React.CSSProperties = {
  fontSize: 14,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  fontFamily: 'Poppins, inherit',
};

// ── Metric tab strip ──────────────────────────────────────────────────────────

interface MetricTabStripProps {
  active: MetricKey;
  onChange: (key: MetricKey) => void;
  testIdPrefix?: string;
}

function MetricTabStrip({ active, onChange, testIdPrefix = '' }: MetricTabStripProps) {
  const tabs: Array<{ key: MetricKey; label: string; testId: string }> = [
    { key: 'lessonsCompleted', label: strings.analyticsMetricLessons, testId: `${testIdPrefix}analytics-tab-lessons` },
    { key: 'totalAttempts', label: strings.analyticsMetricAttempts, testId: `${testIdPrefix}analytics-tab-attempts` },
    { key: 'distinctActiveStudents', label: strings.analyticsMetricStudents, testId: `${testIdPrefix}analytics-tab-students` },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'row', gap: 8 }}>
      {tabs.map((tab) => {
        const isActive = active === tab.key;
        return (
          <button
            key={tab.key}
            type="button"
            data-testid={tab.testId}
            onClick={() => onChange(tab.key)}
            style={{
              height: 30,
              paddingInline: 14,
              borderRadius: 9999,
              fontSize: 13,
              fontFamily: 'inherit',
              cursor: 'pointer',
              transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
              ...(isActive
                ? {
                    backgroundColor: 'rgba(79,70,229,0.18)',
                    color: 'var(--lx-primary)',
                    border: '1px solid rgba(79,70,229,0.3)',
                    fontWeight: 600,
                  }
                : {
                    backgroundColor: 'transparent',
                    color: 'var(--lx-fg3)',
                    border: '1px solid var(--lx-border)',
                    fontWeight: 400,
                  }),
            }}
          >
            {tab.label}
          </button>
        );
      })}
    </div>
  );
}

// ── Data transform helpers ────────────────────────────────────────────────────

function buildSubjectData(
  rows: SubjectBreakdown[],
  metric: MetricKey,
  languageSlice: number | null,
): BarBreakdownDataPoint[] {
  // Group by subjectCode, filter by language if slice set
  const filtered = languageSlice !== null
    ? rows.filter((r) => r.language === languageSlice)
    : rows;

  const map = new Map<number, number>();
  for (const row of filtered) {
    map.set(row.subjectCode, (map.get(row.subjectCode) ?? 0) + row[metric]);
  }

  return Array.from(map.entries()).map(([code, value]) => ({
    name: SUBJECT_LABELS[code] ?? `Subject ${code}`,
    value,
  }));
}

function buildGradeData(
  rows: GradeBreakdown[],
  metric: MetricKey,
  gradeSlice: number | null,
): BarBreakdownDataPoint[] {
  const filtered = gradeSlice !== null ? rows.filter((r) => r.gradeId === gradeSlice) : rows;
  return filtered.map((row) => ({
    name: `Grade ${row.gradeId}`,
    value: row[metric],
  }));
}

function buildLanguageData(
  rows: LanguageBreakdown[],
  metric: MetricKey,
  languageSlice: number | null,
): BarBreakdownDataPoint[] {
  const filtered = languageSlice !== null ? rows.filter((r) => r.language === languageSlice) : rows;
  return filtered.map((row) => ({
    name: LANGUAGE_LABELS[row.language] ?? `Language ${row.language}`,
    value: row[metric],
  }));
}

// ── Main component ────────────────────────────────────────────────────────────

interface KpiBreakdownChartsProps {
  data: PlatformKpiSummaryDto;
  /** Accepted for API symmetry with AnalyticsFilters but GradeBreakdown has no subjectCode field. */
  subjectSlice: number | null;
  gradeSlice: number | null;
  languageSlice: number | null;
}

export function KpiBreakdownCharts({
  data,
  subjectSlice: _subjectSlice,
  gradeSlice,
  languageSlice,
}: KpiBreakdownChartsProps) {
  const [subjectMetric, setSubjectMetric] = useState<MetricKey>('lessonsCompleted');
  const [gradeMetric, setGradeMetric] = useState<MetricKey>('lessonsCompleted');
  const [languageMetric, setLanguageMetric] = useState<MetricKey>('lessonsCompleted');

  const subjectData = buildSubjectData(data.bySubject, subjectMetric, languageSlice);
  const gradeData = buildGradeData(data.byGrade, gradeMetric, gradeSlice);
  const languageData = buildLanguageData(data.byLanguage, languageMetric, languageSlice);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>

      {/* By Subject */}
      <div style={sectionCard}>
        <span style={sectionHeadingStyle}>{strings.analyticsBySubjectHeading}</span>
        <MetricTabStrip active={subjectMetric} onChange={setSubjectMetric} testIdPrefix="subject-" />
        <BarBreakdown
          data={subjectData}
          height={220}
          barColor="#4F46E5"
          emptyLabel={strings.analyticsEmptyHeading}
          testId="analytics-chart-by-subject"
          accessibleCaption={`${strings.analyticsBySubjectHeading} breakdown`}
        />
      </div>

      {/* By Grade */}
      <div style={sectionCard}>
        <span style={sectionHeadingStyle}>{strings.analyticsByGradeHeading}</span>
        <MetricTabStrip active={gradeMetric} onChange={setGradeMetric} testIdPrefix="grade-" />
        <BarBreakdown
          data={gradeData}
          height={220}
          barColor="#22C55E"
          emptyLabel={strings.analyticsEmptyHeading}
          testId="analytics-chart-by-grade"
          accessibleCaption={`${strings.analyticsByGradeHeading} breakdown`}
        />
      </div>

      {/* By Language */}
      <div style={sectionCard}>
        <span style={sectionHeadingStyle}>{strings.analyticsByLanguageHeading}</span>
        <MetricTabStrip active={languageMetric} onChange={setLanguageMetric} testIdPrefix="language-" />
        <BarBreakdown
          data={languageData}
          height={220}
          barColor="#F59E0B"
          emptyLabel={strings.analyticsEmptyHeading}
          testId="analytics-chart-by-language"
          accessibleCaption={`${strings.analyticsByLanguageHeading} breakdown`}
        />
      </div>
    </div>
  );
}
