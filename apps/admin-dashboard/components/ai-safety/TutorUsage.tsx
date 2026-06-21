'use client';

/**
 * TutorUsage — AI tutor usage & cost panel for P7-11.
 *
 * 5 KPI cards: total calls, total tokens (prompt+completion), estimated cost,
 * avg latency ms, cache hit rate chip.
 * By-model and by-taskKind bar breakdowns with metric tab strip.
 * Daily cost TrendLine from `trend` array.
 */

import { useState } from 'react';
import { useTutorUsage } from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { BarBreakdown } from '../charts/BarBreakdown';
import { TrendLine } from '../charts/TrendLine';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';
import type { TutorUsageByModelDto, TutorUsageByTaskKindDto } from '@learnexia/api-client';

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

const miniCardStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-card-soft)',
  borderRadius: 12,
  padding: '12px 16px',
  display: 'flex',
  flexDirection: 'column',
  gap: 4,
};

const shimmerStyle: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1400ms linear infinite',
  borderRadius: 6,
};

// ── Metric tab strip ──────────────────────────────────────────────────────────

type UsageMetricKey = 'calls' | 'totalTokens' | 'totalEstimatedCostUsd';

interface MetricTabStripProps {
  active: UsageMetricKey;
  onChange: (k: UsageMetricKey) => void;
  testIdPrefix?: string;
}

function MetricTabStrip({ active, onChange, testIdPrefix = '' }: MetricTabStripProps) {
  const tabs: Array<{ key: UsageMetricKey; label: string; testId: string }> = [
    { key: 'calls', label: strings.usageMetricCalls, testId: `${testIdPrefix}usage-tab-calls` },
    { key: 'totalTokens', label: strings.usageMetricTokens, testId: `${testIdPrefix}usage-tab-tokens` },
    { key: 'totalEstimatedCostUsd', label: strings.usageMetricCost, testId: `${testIdPrefix}usage-tab-cost` },
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

// ── KPI mini card ─────────────────────────────────────────────────────────────

interface KpiCardProps {
  label: string;
  value: React.ReactNode;
  valueColor?: string;
  testId: string;
  children?: React.ReactNode;
}

function KpiCard({ label, value, valueColor, testId, children }: KpiCardProps) {
  return (
    <div data-testid={testId} style={miniCardStyle}>
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
      {children}
    </div>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

interface TutorUsageProps {
  From?: string;
  To?: string;
}

export function TutorUsage({ From, To }: TutorUsageProps) {
  const { data, isLoading, isError, refetch } = useTutorUsage(From, To);
  const [modelMetric, setModelMetric] = useState<UsageMetricKey>('calls');
  const [taskMetric, setTaskMetric] = useState<UsageMetricKey>('calls');

  if (isLoading) {
    return (
      <div
        style={sectionCardStyle}
        role="status"
        aria-label={strings.aiSafetyLoadingLabel}
        data-testid="usage-loading"
      >
        <span style={headingStyle}>{strings.aiSafetyUsageHeading}</span>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12 }}>
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} role="presentation" style={{ ...shimmerStyle, height: 68 }} />
          ))}
        </div>
        <div role="presentation" style={{ ...shimmerStyle, height: 200 }} />
        <div role="presentation" style={{ ...shimmerStyle, height: 200 }} />
      </div>
    );
  }

  if (isError) {
    return (
      <div style={sectionCardStyle} data-testid="usage-error">
        <span style={headingStyle}>{strings.aiSafetyUsageHeading}</span>
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

  if (!data || data.totalCalls === 0) {
    return (
      <div style={sectionCardStyle} data-testid="usage-empty">
        <span style={headingStyle}>{strings.aiSafetyUsageHeading}</span>
        <div style={{ padding: 40, textAlign: 'center', color: 'var(--lx-fg3)', fontSize: 14 }}>
          {strings.analyticsEmptyHeading}
        </div>
      </div>
    );
  }

  const modelBarData = data.byModel.map((m: TutorUsageByModelDto) => ({
    name: m.modelId,
    value: modelMetric === 'calls'
      ? m.calls
      : modelMetric === 'totalTokens'
        ? m.totalTokens
        : m.totalEstimatedCostUsd,
  }));

  const taskBarData = data.byTaskKind.map((t: TutorUsageByTaskKindDto) => ({
    name: t.taskKind,
    value: taskMetric === 'calls'
      ? t.calls
      : taskMetric === 'totalTokens'
        ? t.totalTokens
        : t.totalEstimatedCostUsd,
  }));

  // Daily cost trend — `trend` array from TutorUsageDto
  const trendData = data.trend.map((d) => ({
    date: d.date,
    totalEstimatedCostUsd: d.totalEstimatedCostUsd,
    calls: d.calls,
  }));

  return (
    <div style={sectionCardStyle} data-testid="usage-results">
      <span style={headingStyle}>{strings.aiSafetyUsageHeading}</span>

      {/* 5 KPI cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 12 }}>
        <KpiCard
          label={strings.aiSafetyUsageTotalCalls}
          value={data.totalCalls.toLocaleString()}
          testId="usage-card-total-calls"
        />
        <KpiCard
          label={strings.aiSafetyUsagePromptTokens}
          value={data.totalPromptTokens.toLocaleString()}
          testId="usage-card-prompt-tokens"
        />
        <KpiCard
          label={strings.aiSafetyUsageCompletionTokens}
          value={data.totalCompletionTokens.toLocaleString()}
          testId="usage-card-completion-tokens"
        />
        <KpiCard
          label={strings.aiSafetyUsageCost}
          value={`$${data.totalEstimatedCostUsd.toFixed(2)}`}
          valueColor="#A855F7"
          testId="usage-card-cost-usd"
        />
        <KpiCard
          label={strings.aiSafetyUsageAvgLatency}
          value={`${data.avgLatencyMs.toFixed(0)} ms`}
          testId="usage-card-avg-latency"
        >
          {/* Cache hit rate chip */}
          <div
            data-testid="usage-cache-hit-chip"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              paddingInline: 6,
              paddingBlock: 2,
              borderRadius: 9999,
              backgroundColor: 'rgba(34,197,94,0.12)',
              border: '1px solid rgba(34,197,94,0.2)',
              fontSize: 10,
              fontWeight: 600,
              color: '#22C55E',
              marginTop: 4,
              alignSelf: 'flex-start',
            }}
          >
            {`${(data.cacheHitRate * 100).toFixed(1)}% ${strings.aiSafetyUsageCacheHit}`}
          </div>
        </KpiCard>
      </div>

      {/* By-model breakdown */}
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
          {strings.aiSafetyUsageByModel}
        </span>
        <MetricTabStrip active={modelMetric} onChange={setModelMetric} testIdPrefix="model-" />
        <BarBreakdown
          data={modelBarData}
          height={200}
          barColor="#6366F1"
          emptyLabel={strings.analyticsEmptyHeading}
          testId="usage-breakdown-model"
          accessibleCaption={strings.aiSafetyUsageByModel}
        />
      </div>

      {/* By-taskKind breakdown */}
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
          {strings.aiSafetyUsageByTaskKind}
        </span>
        <MetricTabStrip active={taskMetric} onChange={setTaskMetric} testIdPrefix="task-" />
        <BarBreakdown
          data={taskBarData}
          height={200}
          barColor="#22C55E"
          emptyLabel={strings.analyticsEmptyHeading}
          testId="usage-breakdown-taskkind"
          accessibleCaption={strings.aiSafetyUsageByTaskKind}
        />
      </div>

      {/* Daily cost trend */}
      {trendData.length > 0 && (
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
            {strings.aiSafetyUsageCostTrend}
          </span>
          <TrendLine
            data={trendData}
            series={[
              { key: 'totalEstimatedCostUsd', name: strings.usageMetricCost, color: '#A855F7' },
            ]}
            height={220}
            emptyLabel={strings.analyticsEmptyHeading}
            testId="usage-cost-trend-chart"
            accessibleCaption={strings.aiSafetyUsageCostTrend}
          />
        </div>
      )}
    </div>
  );
}
