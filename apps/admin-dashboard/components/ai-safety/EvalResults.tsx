'use client';

/**
 * EvalResults — AI eval panel for P7-11.
 *
 * States:
 * 1. Loading — skeleton
 * 2. Error — retry button
 * 3. Bootstrap sentinel — "no eval run yet" placeholder (NOT a breach)
 * 4. Real breach — amber banner + metric cards
 * 5. Passing — green banner + metric cards
 *
 * CRITICAL: sentinel detection MUST run before the breach banner check.
 * When `isEvalBootstrapSentinel(data)` is true, `data.breached` is `true`
 * but it is NOT a real breach — render the sentinel state only.
 *
 * Three breakdown tables: byCheck / bySubject / byLanguage (Record<string, EvalCheckBreakdownDto>).
 */

import { useEvalResults, isEvalBootstrapSentinel } from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';
import type { EvalCheckBreakdownDto } from '@learnexia/api-client';

const strings = getStrings(ADMIN_LOCALE);

// ── Styles ─────────────────────────────────────────────────────────────────────

const sectionCardStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-card)',
  borderRadius: 'var(--lx-radius-card)',
  border: '1px solid var(--lx-border)',
  padding: 20,
  display: 'flex',
  flexDirection: 'column',
  gap: 20,
};

const headingStyle: React.CSSProperties = {
  fontSize: 14,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  fontFamily: 'Poppins, inherit',
};

const metaLabelStyle: React.CSSProperties = {
  fontSize: 11,
  color: 'var(--lx-fg3)',
};

const shimmerStyle: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1400ms linear infinite',
  borderRadius: 6,
};

// ── Breakdown table ────────────────────────────────────────────────────────────

interface BreakdownTableProps {
  heading: string;
  entries: Record<string, EvalCheckBreakdownDto>;
  testId: string;
}

function BreakdownTable({ heading, entries, testId }: BreakdownTableProps) {
  const rows = Object.entries(entries);
  if (rows.length === 0) return null;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
      <span
        style={{
          fontSize: 12,
          fontWeight: 600,
          color: 'var(--lx-fg3)',
          textTransform: 'uppercase',
          letterSpacing: '0.06em',
        }}
      >
        {heading}
      </span>
      <table
        data-testid={testId}
        style={{
          width: '100%',
          borderCollapse: 'collapse',
          fontSize: 13,
        }}
      >
        <thead>
          <tr>
            <th
              style={{
                textAlign: 'left',
                padding: '6px 8px',
                fontSize: 11,
                fontWeight: 600,
                color: 'var(--lx-fg3)',
                borderBottom: '1px solid var(--lx-border)',
              }}
            >
              {strings.evalColName}
            </th>
            <th
              style={{
                textAlign: 'center',
                padding: '6px 8px',
                fontSize: 11,
                fontWeight: 600,
                color: 'var(--lx-fg3)',
                borderBottom: '1px solid var(--lx-border)',
              }}
            >
              {strings.evalColPassed}
            </th>
            <th
              style={{
                textAlign: 'center',
                padding: '6px 8px',
                fontSize: 11,
                fontWeight: 600,
                color: 'var(--lx-fg3)',
                borderBottom: '1px solid var(--lx-border)',
              }}
            >
              {strings.evalColTotal}
            </th>
            <th
              style={{
                textAlign: 'right',
                padding: '6px 8px',
                fontSize: 11,
                fontWeight: 600,
                color: 'var(--lx-fg3)',
                borderBottom: '1px solid var(--lx-border)',
              }}
            >
              {strings.evalColPassRate}
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map(([key, bd]) => (
            <tr key={key}>
              <td
                style={{
                  padding: '6px 8px',
                  color: 'var(--lx-fg1)',
                  borderBottom: '1px solid var(--lx-border)',
                }}
              >
                {key}
              </td>
              <td
                style={{
                  textAlign: 'center',
                  padding: '6px 8px',
                  color: '#22C55E',
                  fontVariantNumeric: 'tabular-nums',
                  borderBottom: '1px solid var(--lx-border)',
                }}
              >
                {bd.passed}
              </td>
              <td
                style={{
                  textAlign: 'center',
                  padding: '6px 8px',
                  color: 'var(--lx-fg2)',
                  fontVariantNumeric: 'tabular-nums',
                  borderBottom: '1px solid var(--lx-border)',
                }}
              >
                {bd.total}
              </td>
              <td
                style={{
                  textAlign: 'right',
                  padding: '6px 8px',
                  color: bd.passRate < 70 ? '#EF4444' : 'var(--lx-fg1)',
                  fontWeight: 600,
                  fontVariantNumeric: 'tabular-nums',
                  borderBottom: '1px solid var(--lx-border)',
                }}
              >
                {`${bd.passRate.toFixed(1)}%`}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Eval metric card ───────────────────────────────────────────────────────────

interface EvalMetricCardProps {
  label: string;
  value: React.ReactNode;
  valueColor?: string;
  testId: string;
}

function EvalMetricCard({ label, value, valueColor, testId }: EvalMetricCardProps) {
  return (
    <div
      data-testid={testId}
      style={{
        backgroundColor: 'var(--lx-card-soft)',
        borderRadius: 12,
        padding: '12px 16px',
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
      }}
    >
      <span style={{ fontSize: 12, fontWeight: 500, color: 'var(--lx-fg3)' }}>{label}</span>
      <span
        style={{
          fontSize: 22,
          fontWeight: 800,
          color: valueColor ?? 'var(--lx-fg1)',
          fontVariantNumeric: 'tabular-nums',
        }}
      >
        {value}
      </span>
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

export function EvalResults() {
  const { data, isLoading, isError, refetch } = useEvalResults();

  if (isLoading) {
    return (
      <div
        style={sectionCardStyle}
        role="status"
        aria-label={strings.aiSafetyLoadingLabel}
        data-testid="eval-loading"
      >
        <span style={headingStyle}>{strings.aiSafetyEvalHeading}</span>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} role="presentation" style={{ ...shimmerStyle, height: 68 }} />
          ))}
        </div>
        <div role="presentation" style={{ ...shimmerStyle, height: 180 }} />
      </div>
    );
  }

  if (isError) {
    return (
      <div style={sectionCardStyle} data-testid="eval-error">
        <span style={headingStyle}>{strings.aiSafetyEvalHeading}</span>
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

  if (!data) return null;

  // CRITICAL: Check sentinel BEFORE checking breach flag.
  // Sentinel: runId === Guid.Empty && totalCases === 0 → "no eval run yet"
  if (isEvalBootstrapSentinel(data)) {
    return (
      <div style={sectionCardStyle} data-testid="eval-sentinel">
        <span style={headingStyle}>{strings.aiSafetyEvalHeading}</span>
        <div
          style={{
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            padding: 40,
            gap: 12,
            textAlign: 'center',
            color: 'var(--lx-fg3)',
          }}
        >
          {/* Info circle SVG */}
          <svg width="36" height="36" viewBox="0 0 24 24" fill="none" aria-hidden="true"
            stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" />
            <line x1="12" y1="16" x2="12.01" y2="16" />
          </svg>
          <span style={{ fontSize: 16, fontWeight: 600, color: 'var(--lx-fg2)' }}>
            {strings.aiSafetyEvalSentinelHeading}
          </span>
          <span style={{ fontSize: 14, maxWidth: 360 }}>
            {strings.aiSafetyEvalSentinelBody}
          </span>
        </div>
      </div>
    );
  }

  return (
    <div style={sectionCardStyle} data-testid="eval-results">
      <span style={headingStyle}>{strings.aiSafetyEvalHeading}</span>

      {/* Status banner — real breach or passing */}
      {data.breached ? (
        <div
          role="alert"
          data-testid="eval-breach-banner"
          style={{
            display: 'flex',
            flexDirection: 'row',
            alignItems: 'center',
            gap: 12,
            padding: '12px 16px',
            borderRadius: 10,
            backgroundColor: 'rgba(245,158,11,0.12)',
            border: '1px solid rgba(245,158,11,0.3)',
          }}
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
            stroke="#F59E0B" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
          </svg>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <span style={{ fontSize: 14, fontWeight: 600, color: '#F59E0B' }}>
              {strings.evalBreachHeading}
            </span>
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)' }}>
              {`${strings.evalBreachBody} ${data.thresholdPercent}% — ${strings.evalCurrentRate} ${data.passRate.toFixed(1)}%`}
            </span>
          </div>

        </div>
      ) : (
        <div
          data-testid="eval-passing-banner"
          style={{
            display: 'flex',
            flexDirection: 'row',
            alignItems: 'center',
            gap: 12,
            padding: '12px 16px',
            borderRadius: 10,
            backgroundColor: 'rgba(34,197,94,0.08)',
            border: '1px solid rgba(34,197,94,0.2)',
          }}
        >
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
            stroke="#22C55E" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <polyline points="20 6 9 17 4 12" />
          </svg>
          <span style={{ fontSize: 14, fontWeight: 600, color: '#22C55E' }}>
            {strings.evalPassingHeading}
          </span>
          <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginInlineStart: 4 }}>
            {`${data.passRate.toFixed(1)}% ${strings.evalPassingRate} ${data.thresholdPercent}% ${strings.evalThreshold}`}
          </span>

        </div>
      )}

      {/* Meta info: run date, tier, note */}
      <div style={{ display: 'flex', flexDirection: 'row', flexWrap: 'wrap', gap: 16 }}>
        <span style={metaLabelStyle}>
          {strings.evalRunAt}: <strong style={{ color: 'var(--lx-fg2)' }}>{new Date(data.ranAt).toLocaleString('en-US')}</strong>
        </span>
        <span style={metaLabelStyle}>
          {strings.aiSafetyEvalTier}: <strong style={{ color: 'var(--lx-fg2)' }}>{data.tier}</strong>
        </span>
        {data.note && (
          <span style={metaLabelStyle}>
            {strings.aiSafetyEvalNote}: <em style={{ color: 'var(--lx-fg2)' }}>{data.note}</em>
          </span>
        )}
      </div>

      {/* Metric cards — 4-column grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 12 }}>
        <EvalMetricCard
          label={strings.evalMetricPassRate}
          value={`${data.passRate.toFixed(1)}%`}
          valueColor={data.breached ? '#F59E0B' : '#22C55E'}
          testId="eval-card-pass-rate"
        />
        <EvalMetricCard
          label={strings.evalMetricTotalCases}
          value={data.totalCases.toLocaleString()}
          testId="eval-card-total-cases"
        />
        <EvalMetricCard
          label={strings.evalMetricPassedCases}
          value={data.passedCases.toLocaleString()}
          valueColor="#22C55E"
          testId="eval-card-passed-cases"
        />
        <EvalMetricCard
          label={strings.evalMetricFailedCases}
          value={data.failedCases.toLocaleString()}
          valueColor={data.failedCases > 0 ? '#EF4444' : 'var(--lx-fg1)'}
          testId="eval-card-failed-cases"
        />
      </div>

      {/* Breakdown tables — by check / by subject / by language */}
      <BreakdownTable
        heading={strings.aiSafetyEvalByCheck}
        entries={data.byCheck}
        testId="eval-table-by-check"
      />
      <BreakdownTable
        heading={strings.aiSafetyEvalBySubject}
        entries={data.bySubject}
        testId="eval-table-by-subject"
      />
      <BreakdownTable
        heading={strings.aiSafetyEvalByLanguage}
        entries={data.byLanguage}
        testId="eval-table-by-language"
      />
    </div>
  );
}
