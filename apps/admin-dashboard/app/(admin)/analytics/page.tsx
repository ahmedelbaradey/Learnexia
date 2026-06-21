'use client';

// Force dynamic rendering — this page fetches live admin data and must NOT be
// statically pre-rendered (same reason as audit/page.tsx).
export const dynamic = 'force-dynamic';

/**
 * Analytics page — `/analytics` (P7-10-FE).
 *
 * Platform KPI summary cards + categorical breakdown charts.
 * Date-range filter drives `usePlatformKpis`; subject/grade/language slice
 * selects are client-side only (they filter embedded breakdown arrays).
 *
 * CRITICAL INVARIANTS:
 * 1. READ-ONLY — no mutations anywhere on this surface.
 * 2. N/A state: `*NaReason` fields render "N/A — {reason}", never `0`.
 * 3. No per-child PII on screen (aggregate data only).
 * 4. No `/Analytics/trend` endpoint — breakdown bars are the approved substitute.
 *
 * Four states: loading-skeleton / empty / error+retry / results.
 * Acceptance criteria: P7-10 AC1–AC7.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { usePlatformKpis } from '@learnexia/api-client';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';
import { AdminErrorBanner } from '../../../components/AdminErrorBanner';
import { useAdminGuard } from '../../../lib/hooks/useAdminGuard';
import { AnalyticsFilters } from '../../../components/analytics/AnalyticsFilters';
import { KpiCards } from '../../../components/analytics/KpiCards';
import { KpiBreakdownCharts } from '../../../components/analytics/KpiBreakdownCharts';

const strings = getStrings(ADMIN_LOCALE);

// ── Shimmer style (mirrors AuditSkeletonRow) ──────────────────────────────────

const shimmerStyle: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1400ms linear infinite',
  borderRadius: 6,
};

// ── Skeleton component ─────────────────────────────────────────────────────────

function KpiCardsSkeleton() {
  return (
    <div
      role="status"
      aria-label={strings.analyticsLoadingLabel}
      data-testid="analytics-loading"
    >
      {/* 8 KPI card skeletons */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(4, 1fr)',
          gap: 16,
          marginBottom: 16,
        }}
      >
        {Array.from({ length: 8 }).map((_, i) => (
          <div
            key={i}
            role="presentation"
            style={{ ...shimmerStyle, height: 110, borderRadius: 20 }}
          />
        ))}
      </div>
      {/* 2 chart placeholders */}
      <div
        role="presentation"
        style={{ ...shimmerStyle, height: 260, borderRadius: 20, marginTop: 24 }}
      />
      <div
        role="presentation"
        style={{ ...shimmerStyle, height: 260, borderRadius: 20, marginTop: 16 }}
      />
    </div>
  );
}

// ── Empty state ────────────────────────────────────────────────────────────────

function EmptyState() {
  return (
    <Stack
      alignItems="center"
      justifyContent="center"
      padding={40}
      gap="$4"
      data-testid="analytics-empty"
      style={{
        backgroundColor: 'var(--lx-card)',
        borderRadius: 'var(--lx-radius-card)',
        border: '1px solid var(--lx-border)',
      }}
    >
      {/* Lucide bar-chart-2 inline SVG */}
      <span style={{ color: 'var(--lx-fg3)' }}>
        <svg
          width="40"
          height="40"
          viewBox="0 0 24 24"
          fill="none"
          aria-hidden="true"
          stroke="currentColor"
          strokeWidth="1.5"
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <line x1="18" y1="20" x2="18" y2="10" />
          <line x1="12" y1="20" x2="12" y2="4" />
          <line x1="6" y1="20" x2="6" y2="14" />
          <line x1="2" y1="20" x2="22" y2="20" />
        </svg>
      </span>
      <Text fontFamily="$heading" fontSize={18} fontWeight="600" color="$fg1">
        {strings.analyticsEmptyHeading}
      </Text>
      <Text
        fontFamily="$body"
        fontSize={14}
        color="$fg3"
        style={{ textAlign: 'center', maxWidth: 320, lineHeight: 1.5 }}
      >
        {strings.analyticsEmptyBody}
      </Text>
    </Stack>
  );
}

// ── Main page ──────────────────────────────────────────────────────────────────

export default function AnalyticsPage() {
  useAdminGuard();

  // Date range filter state (drives API re-fetch)
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  // Client-side slice state (does NOT re-fetch — filters embedded breakdown arrays)
  const [subjectSlice, setSubjectSlice] = useState<number | null>(null);
  const [gradeSlice, setGradeSlice] = useState<number | null>(null);
  const [languageSlice, setLanguageSlice] = useState<number | null>(null);

  // Date validation: no API call when To < From
  const hasDateRangeError = from !== '' && to !== '' && to < from;

  const clearFilters = useCallback(() => {
    setFrom('');
    setTo('');
    setSubjectSlice(null);
    setGradeSlice(null);
    setLanguageSlice(null);
  }, []);

  // Build API params — only send ISO strings when date is set and valid
  const kpisFrom = hasDateRangeError ? undefined : (from || undefined);
  const kpisTo = hasDateRangeError ? undefined : (to || undefined);

  const { data, isLoading, isError, refetch } = usePlatformKpis(kpisFrom, kpisTo);

  // Empty detection — all key counters are 0 and breakdown arrays are empty
  const isSemanticEmpty =
    !isLoading &&
    !isError &&
    data != null &&
    data.lessonsCompleted === 0 &&
    data.totalAttempts === 0 &&
    data.distinctActiveStudents === 0 &&
    data.bySubject.length === 0 &&
    data.byGrade.length === 0 &&
    data.byLanguage.length === 0;

  const showSkeleton = isLoading && !hasDateRangeError;
  const showError = !isLoading && isError && !hasDateRangeError;
  const showEmpty = isSemanticEmpty && !hasDateRangeError;
  const showResults = !isLoading && !isError && data != null && !isSemanticEmpty && !hasDateRangeError;

  return (
    <AdminShell title={strings.analyticsPageTitle}>
      <Stack flexDirection="column" gap="$6">

        {/* Page header */}
        <Stack
          flexDirection="row"
          alignItems="center"
          justifyContent="space-between"
          gap="$4"
        >
          <Stack flexDirection="column" gap="$1">
            <Text
              fontFamily="$heading"
              fontSize={24}
              fontWeight="700"
              color="$fg1"
              data-testid="analytics-page-heading"
            >
              {strings.analyticsPageHeading}
            </Text>
            <Text
              fontFamily="$body"
              fontSize={14}
              color="$fg3"
              style={{ marginTop: 4 }}
            >
              {strings.analyticsPageSubtitle}
            </Text>
          </Stack>
        </Stack>

        {/* Filters */}
        <AnalyticsFilters
          from={from}
          to={to}
          subjectSlice={subjectSlice}
          gradeSlice={gradeSlice}
          languageSlice={languageSlice}
          hasDateRangeError={hasDateRangeError}
          onFromChange={(v) => { setFrom(v); }}
          onToChange={(v) => { setTo(v); }}
          onSubjectSliceChange={setSubjectSlice}
          onGradeSliceChange={setGradeSlice}
          onLanguageSliceChange={setLanguageSlice}
          onClear={clearFilters}
        />

        {/* Results area — aria-live for screen reader announcements */}
        <div aria-live="polite" aria-atomic="false">

          {/* State: Loading */}
          {showSkeleton && <KpiCardsSkeleton />}

          {/* State: Error */}
          {showError && (
            <Stack flexDirection="column" gap="$4" data-testid="analytics-error-banner">
              <AdminErrorBanner variant="error" message={strings.analyticsLoadError} />
              <button
                type="button"
                data-testid="analytics-retry"
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
                {strings.analyticsRetry}
              </button>
            </Stack>
          )}

          {/* State: Empty */}
          {showEmpty && <EmptyState />}

          {/* State: Results */}
          {showResults && data && (
            <Stack flexDirection="column" gap="$6">
              <KpiCards data={data} />
              <KpiBreakdownCharts
                data={data}
                subjectSlice={subjectSlice}
                gradeSlice={gradeSlice}
                languageSlice={languageSlice}
              />
            </Stack>
          )}
        </div>
      </Stack>
    </AdminShell>
  );
}
