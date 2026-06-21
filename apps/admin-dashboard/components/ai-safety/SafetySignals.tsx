'use client';

/**
 * SafetySignals — safety signal cards + breakdown bars for P7-11.
 *
 * Shows: total/blocked/regenerated/fallback counts + rates, and 4 breakdown bars
 * (Action / ReasonCode / ModelId / TaskKind).
 *
 * NOTE: `signals` endpoint has NO subject or language breakdown — SafetyEvent
 * has no such columns. Do NOT add subject/language panels here.
 */

import { useSafetySignals } from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { BarBreakdown } from '../charts/BarBreakdown';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Shared styles ──────────────────────────────────────────────────────────────

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

const miniCardStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-card-soft)',
  borderRadius: 12,
  padding: '12px 16px',
  display: 'flex',
  flexDirection: 'column',
  gap: 4,
};

const miniLabelStyle: React.CSSProperties = {
  fontSize: 12,
  fontWeight: 500,
  color: 'var(--lx-fg3)',
  lineHeight: 1.4,
  fontFamily: 'inherit',
};

const shimmerStyle: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1400ms linear infinite',
  borderRadius: 6,
};

// ── Mini signal card ──────────────────────────────────────────────────────────

interface SignalCardProps {
  label: string;
  value: React.ReactNode;
  borderLeft?: string;
  valueColor?: string;
  testId: string;
  children?: React.ReactNode;
}

function SignalCard({ label, value, borderLeft, valueColor, testId, children }: SignalCardProps) {
  return (
    <div
      data-testid={testId}
      style={{
        ...miniCardStyle,
        borderLeft: borderLeft ?? 'none',
      }}
    >
      <span style={miniLabelStyle}>{label}</span>
      <span
        style={{
          fontSize: 22,
          fontWeight: 800,
          fontVariantNumeric: 'tabular-nums',
          color: valueColor ?? 'var(--lx-fg1)',
        }}
      >
        {value}
      </span>
      {children}
    </div>
  );
}

// ── Breakdown panel heading ───────────────────────────────────────────────────

function BreakdownHeading({ children }: { children: React.ReactNode }) {
  return (
    <span
      style={{
        fontSize: 12,
        fontWeight: 600,
        color: 'var(--lx-fg3)',
        textTransform: 'uppercase',
        letterSpacing: '0.06em',
        fontFamily: 'Poppins, inherit',
      }}
    >
      {children}
    </span>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

interface SafetySignalsProps {
  From?: string;
  To?: string;
}

export function SafetySignals({ From, To }: SafetySignalsProps) {
  const { data, isLoading, isError, refetch } = useSafetySignals(From, To);

  if (isLoading) {
    return (
      <div
        style={sectionCardStyle}
        role="status"
        aria-label={strings.aiSafetyLoadingLabel}
        data-testid="signals-loading"
      >
        <span style={headingStyle}>{strings.aiSafetySignalsHeading}</span>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} role="presentation" style={{ ...shimmerStyle, height: 68 }} />
          ))}
        </div>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 16 }}>
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} role="presentation" style={{ ...shimmerStyle, height: 180 }} />
          ))}
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div style={sectionCardStyle} data-testid="signals-error">
        <span style={headingStyle}>{strings.aiSafetySignalsHeading}</span>
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

  if (!data || (data.totalEvents === 0 && data.breakdownByAction.length === 0)) {
    return (
      <div style={sectionCardStyle} data-testid="signals-empty">
        <span style={headingStyle}>{strings.aiSafetySignalsHeading}</span>
        <div style={{
          padding: 40,
          textAlign: 'center',
          color: 'var(--lx-fg3)',
          fontSize: 14,
        }}>
          {strings.analyticsEmptyHeading}
        </div>
      </div>
    );
  }

  const { blockedRate, regeneratedRate, fallbackReturnedRate } = data;

  return (
    <div style={sectionCardStyle} data-testid="signals-results">
      <span style={headingStyle}>{strings.aiSafetySignalsHeading}</span>

      {/* Signal KPI cards — 3-column grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
        <SignalCard
          label={strings.aiSafetySignalsTotalEvents}
          value={data.totalEvents.toLocaleString()}
          testId="signal-card-total"
        />
        <SignalCard
          label={strings.aiSafetySignalsBlocked}
          value={data.blockedCount.toLocaleString()}
          borderLeft="3px solid #EF4444"
          valueColor="#EF4444"
          testId="signal-card-blocked"
        />
        <SignalCard
          label={strings.aiSafetySignalsRegenerated}
          value={data.regeneratedCount.toLocaleString()}
          borderLeft="3px solid #F59E0B"
          valueColor="#F59E0B"
          testId="signal-card-regen"
        />
        <SignalCard
          label={strings.aiSafetySignalsFallback}
          value={data.fallbackReturnedCount.toLocaleString()}
          borderLeft="3px solid #A855F7"
          valueColor="#A855F7"
          testId="signal-card-fallback"
        >
          <span style={{ fontSize: 11, color: 'var(--lx-fg3)' }}>
            {`${(fallbackReturnedRate * 100).toFixed(1)}%`}
          </span>
        </SignalCard>
        <SignalCard
          label={strings.aiSafetySignalsBlockedRate}
          value={`${(blockedRate * 100).toFixed(1)}%`}
          valueColor="#EF4444"
          testId="signal-card-block-rate"
        />
        <SignalCard
          label={strings.aiSafetySignalsRegeneratedRate}
          value={`${(regeneratedRate * 100).toFixed(1)}%`}
          valueColor="#F59E0B"
          testId="signal-card-regen-rate"
        />
      </div>

      {/* Breakdown bars — 2-column grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: 16 }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <BreakdownHeading>{strings.aiSafetyBreakdownByAction}</BreakdownHeading>
          <BarBreakdown
            data={data.breakdownByAction.map((d) => ({ name: d.label, value: d.count }))}
            height={180}
            barColor="#EF4444"
            emptyLabel={strings.analyticsEmptyHeading}
            testId="signal-breakdown-action"
            accessibleCaption={strings.aiSafetyBreakdownByAction}
          />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <BreakdownHeading>{strings.aiSafetyBreakdownByReason}</BreakdownHeading>
          <BarBreakdown
            data={data.breakdownByReasonCode.map((d) => ({ name: d.label, value: d.count }))}
            height={180}
            barColor="#F59E0B"
            emptyLabel={strings.analyticsEmptyHeading}
            testId="signal-breakdown-reason"
            accessibleCaption={strings.aiSafetyBreakdownByReason}
          />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <BreakdownHeading>{strings.aiSafetyBreakdownByModel}</BreakdownHeading>
          <BarBreakdown
            data={data.breakdownByModelId.map((d) => ({ name: d.label, value: d.count }))}
            height={180}
            barColor="#6366F1"
            emptyLabel={strings.analyticsEmptyHeading}
            testId="signal-breakdown-model"
            accessibleCaption={strings.aiSafetyBreakdownByModel}
          />
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
          <BreakdownHeading>{strings.aiSafetyBreakdownByTaskKind}</BreakdownHeading>
          <BarBreakdown
            data={data.breakdownByTaskKind.map((d) => ({ name: d.label, value: d.count }))}
            height={180}
            barColor="#22C55E"
            emptyLabel={strings.analyticsEmptyHeading}
            testId="signal-breakdown-taskkind"
            accessibleCaption={strings.aiSafetyBreakdownByTaskKind}
          />
        </div>
      </div>
    </div>
  );
}
