'use client';

/**
 * SafetyTrend — daily safety event TrendLine for P7-11.
 *
 * Four series: total / blocked / regenerated / fallback.
 * Maps `bucketDate` from backend DTO to `date` key expected by TrendLine.
 */

import { useSafetyTrend } from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { TrendLine } from '../charts/TrendLine';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

const sectionCardStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-card)',
  borderRadius: 'var(--lx-radius-card)',
  border: '1px solid var(--lx-border)',
  padding: 20,
  display: 'flex',
  flexDirection: 'column',
  gap: 16,
};

const headingStyle: React.CSSProperties = {
  fontSize: 14,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  fontFamily: 'Poppins, inherit',
};

const shimmerStyle: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1400ms linear infinite',
  borderRadius: 6,
};

const SERIES = [
  { key: 'totalCount', name: 'Total', color: '#6366F1' },
  { key: 'blockedCount', name: 'Blocked', color: '#EF4444' },
  { key: 'regeneratedCount', name: 'Regenerated', color: '#F59E0B' },
  { key: 'fallbackReturnedCount', name: 'Fallback', color: '#A855F7' },
] as const;

interface SafetyTrendProps {
  From?: string;
  To?: string;
}

export function SafetyTrend({ From, To }: SafetyTrendProps) {
  const { data, isLoading, isError, refetch } = useSafetyTrend(From, To);

  if (isLoading) {
    return (
      <div
        style={sectionCardStyle}
        role="status"
        aria-label={strings.aiSafetyLoadingLabel}
        data-testid="safety-trend-loading"
      >
        <span style={headingStyle}>{strings.aiSafetyTrendHeading}</span>
        <div role="presentation" style={{ ...shimmerStyle, height: 260 }} />
      </div>
    );
  }

  if (isError) {
    return (
      <div style={sectionCardStyle} data-testid="safety-trend-error">
        <span style={headingStyle}>{strings.aiSafetyTrendHeading}</span>
        <AdminErrorBanner variant="error" message={strings.aiSafetyLoadError} />
        <button
          type="button"
          onClick={() => void refetch()}
          style={{
            alignSelf: 'flex-start',
            height: 36,
            paddingInline: 16,
            borderRadius: 'var(--lx-radius-button)',
            border: '1px solid var(--lx-border)',
            backgroundColor: 'transparent',
            color: 'var(--lx-fg2)',
            fontSize: 13,
            cursor: 'pointer',
            fontFamily: 'inherit',
          }}
        >
          {strings.aiSafetyRetry}
        </button>
      </div>
    );
  }

  if (!data || data.length === 0) {
    return (
      <div style={sectionCardStyle} data-testid="safety-trend-empty">
        <span style={headingStyle}>{strings.aiSafetyTrendHeading}</span>
        <div style={{ padding: 40, textAlign: 'center', color: 'var(--lx-fg3)', fontSize: 14 }}>
          {strings.analyticsEmptyHeading}
        </div>
      </div>
    );
  }

  // Map `bucketDate` → `date` for TrendLine (TrendLine expects `date` key in each entry)
  const chartData = data.map((b) => ({
    date: b.bucketDate,
    totalCount: b.totalCount,
    blockedCount: b.blockedCount,
    regeneratedCount: b.regeneratedCount,
    fallbackReturnedCount: b.fallbackReturnedCount,
  }));

  return (
    <div style={sectionCardStyle} data-testid="safety-trend-results">
      <span style={headingStyle}>{strings.aiSafetyTrendHeading}</span>
      <TrendLine
        data={chartData}
        series={[...SERIES]}
        height={260}
        emptyLabel={strings.analyticsEmptyHeading}
        testId="safety-trend-chart"
        accessibleCaption={strings.aiSafetyTrendHeading}
      />
    </div>
  );
}
