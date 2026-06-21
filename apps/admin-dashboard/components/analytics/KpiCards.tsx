'use client';

/**
 * KpiCards — KPI summary card grid for P7-10 Platform Analytics.
 *
 * Renders 16 KPI cards across 5 sections (Engagement, Gamification,
 * Subscriptions, AI Safety, Session/Retention).
 *
 * N/A state: when an `*NaReason` field is a non-null, non-empty string,
 * renders "N/A — {reason}" in amber (#F59E0B) — NEVER shows `0`.
 *
 * The `quizzesCompletedNaReason` card is always N/A (no count field exists).
 */

import { getStrings, ADMIN_LOCALE } from '../../lib/strings';
import type { PlatformKpiSummaryDto } from '@learnexia/api-client';

const strings = getStrings(ADMIN_LOCALE);

// ── Shared styles ──────────────────────────────────────────────────────────────

const cardStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-card)',
  borderRadius: 'var(--lx-radius-card)',
  border: '1px solid var(--lx-border)',
  padding: 20,
  display: 'flex',
  flexDirection: 'column',
  gap: 8,
  transition: 'border-color var(--lx-dur-fast) var(--lx-ease-out)',
};

const labelStyle: React.CSSProperties = {
  fontSize: 14,
  fontWeight: 500,
  color: 'var(--lx-fg3)',
  fontFamily: 'Poppins, inherit',
  lineHeight: 1.4,
};

const valueStyle: React.CSSProperties = {
  fontSize: 28,
  fontWeight: 800,
  color: 'var(--lx-fg1)',
  fontVariantNumeric: 'tabular-nums',
  lineHeight: 1.2,
};

const naLabelStyle: React.CSSProperties = {
  fontSize: 13,
  fontWeight: 700,
  color: '#F59E0B',
  fontVariantNumeric: 'tabular-nums',
};

const naReasonStyle: React.CSSProperties = {
  fontSize: 11,
  color: 'var(--lx-fg3)',
  lineHeight: 1.4,
  marginTop: 2,
  display: 'block',
};

// ── Icon circle ────────────────────────────────────────────────────────────────

interface IconCircleProps {
  bgColor: string;
  color: string;
  children: React.ReactNode;
}

function IconCircle({ bgColor, color, children }: IconCircleProps) {
  return (
    <div
      aria-hidden="true"
      style={{
        width: 32,
        height: 32,
        borderRadius: 9999,
        backgroundColor: bgColor,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        color,
      }}
    >
      {children}
    </div>
  );
}

// ── KPI card ───────────────────────────────────────────────────────────────────

interface KpiCardProps {
  label: string;
  value?: React.ReactNode;
  naReason?: string | null;
  iconBg: string;
  iconColor: string;
  icon: React.ReactNode;
  testId: string;
  valueTestId: string;
  children?: React.ReactNode;
}

function KpiCard({
  label,
  value,
  naReason,
  iconBg,
  iconColor,
  icon,
  testId,
  valueTestId,
  children,
}: KpiCardProps) {
  const isNa = typeof naReason === 'string' && naReason.trim().length > 0;

  return (
    <div
      role="region"
      aria-label={label}
      data-testid={testId}
      style={cardStyle}
      onMouseEnter={(e) => {
        (e.currentTarget as HTMLDivElement).style.borderColor = 'rgba(79,70,229,0.3)';
      }}
      onMouseLeave={(e) => {
        (e.currentTarget as HTMLDivElement).style.borderColor = 'var(--lx-border)';
      }}
    >
      <IconCircle bgColor={iconBg} color={iconColor}>
        {icon}
      </IconCircle>
      <span style={labelStyle}>{label}</span>
      {isNa ? (
        <>
          <span style={naLabelStyle}>{strings.analyticsNaValue}</span>
          <span style={naReasonStyle}>{naReason}</span>
        </>
      ) : (
        <span data-testid={valueTestId} style={valueStyle}>
          {value}
        </span>
      )}
      {children}
    </div>
  );
}

// ── Inline SVG icons (16×16, Lucide paths) ────────────────────────────────────

function UsersIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </svg>
  );
}

function BookOpenIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
      <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
    </svg>
  );
}

function LayersIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 2 7 12 12 22 7 12 2" />
      <polyline points="2 17 12 22 22 17" />
      <polyline points="2 12 12 17 22 12" />
    </svg>
  );
}

function TargetIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <circle cx="12" cy="12" r="6" />
      <circle cx="12" cy="12" r="2" />
    </svg>
  );
}

function ZapIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
    </svg>
  );
}

function CreditCardIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="1" y="4" width="22" height="16" rx="2" ry="2" />
      <line x1="1" y1="10" x2="23" y2="10" />
    </svg>
  );
}

function ShieldIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
    </svg>
  );
}

function ClockIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <polyline points="12 6 12 12 16 14" />
    </svg>
  );
}

// ── Subscriptions tier breakdown ──────────────────────────────────────────────

interface TierBreakdownProps {
  tiers: Array<{ planCode: string; count: number }>;
}

function TierBreakdown({ tiers }: TierBreakdownProps) {
  if (tiers.length === 0) return null;
  return (
    <div
      data-testid="analytics-subscriptions-tier"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
        marginTop: 8,
        borderTop: '1px solid var(--lx-border)',
        paddingTop: 8,
      }}
    >
      {tiers.map((t) => (
        <div key={t.planCode} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: 12, color: 'var(--lx-fg3)' }}>{t.planCode}</span>
          <span style={{ fontSize: 12, color: 'var(--lx-fg1)', fontVariantNumeric: 'tabular-nums' }}>
            {t.count}
          </span>
        </div>
      ))}
    </div>
  );
}

// ── Main KpiCards component ───────────────────────────────────────────────────

interface KpiCardsProps {
  data: PlatformKpiSummaryDto;
}

const gridStyle: React.CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(4, 1fr)',
  gap: 16,
};

// Colors
const INDIGO_BG = 'rgba(79,70,229,0.15)';
const INDIGO_COLOR = '#6366F1';
const GREEN_BG = 'rgba(34,197,94,0.15)';
const GREEN_COLOR = '#22C55E';
const AMBER_BG = 'rgba(245,158,11,0.15)';
const AMBER_COLOR = '#F59E0B';
const PURPLE_BG = 'rgba(168,85,247,0.15)';
const PURPLE_COLOR = '#A855F7';
const RED_BG = 'rgba(239,68,68,0.12)';
const RED_COLOR = '#EF4444';

export function KpiCards({ data }: KpiCardsProps) {
  return (
    <div>
      {/* Section 1 — Engagement */}
      <div style={{ ...gridStyle, marginBottom: 16 }}>
        <KpiCard
          label={strings.analyticsKpiActiveStudents}
          value={data.distinctActiveStudents.toLocaleString()}
          iconBg={INDIGO_BG}
          iconColor={INDIGO_COLOR}
          icon={<UsersIcon />}
          testId="kpi-card-active-students"
          valueTestId="kpi-value-active-students"
        />
        <KpiCard
          label={strings.analyticsKpiAnalyticsStudents}
          value={data.analyticsActiveStudents.toLocaleString()}
          iconBg={INDIGO_BG}
          iconColor={INDIGO_COLOR}
          icon={<UsersIcon />}
          testId="kpi-card-analytics-students"
          valueTestId="kpi-value-analytics-students"
        />
        <KpiCard
          label={strings.analyticsKpiLessonsCompleted}
          value={data.lessonsCompleted.toLocaleString()}
          iconBg={GREEN_BG}
          iconColor={GREEN_COLOR}
          icon={<BookOpenIcon />}
          testId="kpi-card-lessons-completed"
          valueTestId="kpi-value-lessons-completed"
        />
        <KpiCard
          label={strings.analyticsKpiTotalAttempts}
          value={data.totalAttempts.toLocaleString()}
          iconBg={GREEN_BG}
          iconColor={GREEN_COLOR}
          icon={<LayersIcon />}
          testId="kpi-card-total-attempts"
          valueTestId="kpi-value-total-attempts"
        />
      </div>

      {/* Section 2 — Gamification */}
      <div style={{ ...gridStyle, marginBottom: 16 }}>
        <KpiCard
          label={strings.analyticsKpiMissionsCompleted}
          value={data.missionsCompleted.toLocaleString()}
          iconBg={AMBER_BG}
          iconColor={AMBER_COLOR}
          icon={<TargetIcon />}
          testId="kpi-card-missions-completed"
          valueTestId="kpi-value-missions-completed"
        />
        <KpiCard
          label={strings.analyticsKpiXpEarned}
          value={data.xpEarnedInWindow.toLocaleString()}
          iconBg={AMBER_BG}
          iconColor={AMBER_COLOR}
          icon={<ZapIcon />}
          testId="kpi-card-xp-earned"
          valueTestId="kpi-value-xp-earned"
        />
        {/* Quizzes — always N/A (no count field exists) */}
        <KpiCard
          label={strings.analyticsKpiQuizzesCompleted}
          naReason={data.quizzesCompletedNaReason ?? strings.analyticsNaValue}
          iconBg={GREEN_BG}
          iconColor={GREEN_COLOR}
          icon={<LayersIcon />}
          testId="kpi-card-quizzes"
          valueTestId="kpi-value-quizzes"
        />
        {/* Subscriptions — shows count but revenue may be N/A */}
        <KpiCard
          label={strings.analyticsKpiActiveSubscriptions}
          value={data.totalActiveSubscriptions.toLocaleString()}
          iconBg={PURPLE_BG}
          iconColor={PURPLE_COLOR}
          icon={<CreditCardIcon />}
          testId="kpi-card-subscriptions"
          valueTestId="kpi-value-subscriptions"
        >
          {data.revenueNaReason && data.revenueNaReason.trim().length > 0 && (
            <span style={{ fontSize: 11, color: 'var(--lx-fg3)' }}>
              {'Revenue: N/A — '}{data.revenueNaReason}
            </span>
          )}
          <TierBreakdown tiers={data.subscriptionsByTier} />
        </KpiCard>
      </div>

      {/* Section 3 — AI Safety */}
      <div style={{ ...gridStyle, marginBottom: 16 }}>
        <KpiCard
          label={strings.analyticsKpiAiSafetyEvents}
          value={data.totalAiSafetyEvents.toLocaleString()}
          iconBg={RED_BG}
          iconColor={RED_COLOR}
          icon={<ShieldIcon />}
          testId="kpi-card-ai-safety-events"
          valueTestId="kpi-value-ai-safety-events"
        />
        <KpiCard
          label={strings.analyticsKpiAiBlocked}
          value={data.aiBlockedCount.toLocaleString()}
          iconBg={RED_BG}
          iconColor={RED_COLOR}
          icon={<ShieldIcon />}
          testId="kpi-card-ai-blocked"
          valueTestId="kpi-value-ai-blocked"
        />
        <KpiCard
          label={strings.analyticsKpiAiFlagged}
          value={data.aiFlaggedCount.toLocaleString()}
          iconBg={RED_BG}
          iconColor={RED_COLOR}
          icon={<ShieldIcon />}
          testId="kpi-card-ai-flagged"
          valueTestId="kpi-value-ai-flagged"
        />
        <KpiCard
          label={strings.analyticsKpiAiRequests}
          value={data.aiRequestVolume.toLocaleString()}
          naReason={data.aiRequestVolumeNaReason}
          iconBg={RED_BG}
          iconColor={RED_COLOR}
          icon={<ShieldIcon />}
          testId="kpi-card-ai-requests"
          valueTestId="kpi-value-ai-requests"
        />
      </div>

      {/* Section 4 — Session / Retention */}
      <div style={gridStyle}>
        <KpiCard
          label={strings.analyticsKpiTotalSessions}
          value={data.totalSessions.toLocaleString()}
          iconBg={INDIGO_BG}
          iconColor={INDIGO_COLOR}
          icon={<ClockIcon />}
          testId="kpi-card-sessions"
          valueTestId="kpi-value-sessions"
        />
        <KpiCard
          label={strings.analyticsKpiAvgSessionDuration}
          value={`${(data.avgSessionDurationSeconds / 60).toFixed(1)}`}
          naReason={data.sessionDurationNaReason}
          iconBg={INDIGO_BG}
          iconColor={INDIGO_COLOR}
          icon={<ClockIcon />}
          testId="kpi-card-avg-session"
          valueTestId="kpi-value-avg-session"
        />
        <KpiCard
          label={strings.analyticsKpiAvgActiveDays}
          value={`${data.avgActiveDaysPerStudent.toFixed(1)}`}
          naReason={data.retentionNaReason}
          iconBg={INDIGO_BG}
          iconColor={INDIGO_COLOR}
          icon={<ClockIcon />}
          testId="kpi-card-avg-active-days"
          valueTestId="kpi-value-avg-active-days"
        />
        <KpiCard
          label={strings.analyticsKpiReturningRate}
          value={`${(data.returningStudentRate * 100).toFixed(1)}%`}
          naReason={data.retentionNaReason}
          iconBg={INDIGO_BG}
          iconColor={INDIGO_COLOR}
          icon={<UsersIcon />}
          testId="kpi-card-returning-rate"
          valueTestId="kpi-value-returning-rate"
        />
      </div>
    </div>
  );
}
