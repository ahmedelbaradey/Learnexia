'use client';

// Force dynamic rendering — live admin data, no SSG.
export const dynamic = 'force-dynamic';

/**
 * AiSafety page — `/ai-safety` (P7-11-FE).
 *
 * Five independent panel sections, each with its own hook + 4-state rendering:
 * 1. SafetySignals — event counts + 4 breakdown bars
 * 2. SafetyTrend   — daily trend line (4 series)
 * 3. EvalResults   — eval pass/fail + breach sentinel detection
 * 4. TutorUsage    — cost KPIs + by-model/by-taskKind bars + cost trend
 * 5. FlaggedOutputsTable — paginated PII-light flagged outputs
 *
 * Shared date-range filter (From/To) drives Signals, Trend, Usage, and Flagged.
 * EvalResults takes NO params.
 *
 * CRITICAL INVARIANTS:
 * 1. READ-ONLY — no mutations anywhere on this surface.
 * 2. PII-light: studentId from FlaggedOutputDto MUST NEVER be rendered.
 * 3. Eval sentinel: `isEvalBootstrapSentinel(data)` checked before breach banner.
 * 4. Param casing: PascalCase `From`/`To` for all AiSafety endpoints.
 *
 * Acceptance criteria: P7-11 AC1–AC12.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';
import { useAdminGuard } from '../../../lib/hooks/useAdminGuard';
import { AiSafetyFilters } from '../../../components/ai-safety/AiSafetyFilters';
import { SafetySignals } from '../../../components/ai-safety/SafetySignals';
import { SafetyTrend } from '../../../components/ai-safety/SafetyTrend';
import { EvalResults } from '../../../components/ai-safety/EvalResults';
import { TutorUsage } from '../../../components/ai-safety/TutorUsage';
import { FlaggedOutputsTable } from '../../../components/ai-safety/FlaggedOutputsTable';

const strings = getStrings(ADMIN_LOCALE);

export default function AiSafetyPage() {
  useAdminGuard();

  // Date range filter state — passed as PascalCase to AI safety hooks
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');

  // Date validation: no API call when To < From
  const hasDateRangeError = from !== '' && to !== '' && to < from;

  const clearFilters = useCallback(() => {
    setFrom('');
    setTo('');
  }, []);

  // PascalCase API params (only when valid)
  const From = hasDateRangeError ? undefined : (from || undefined);
  const To = hasDateRangeError ? undefined : (to || undefined);

  return (
    <AdminShell title={strings.aiSafetyPageTitle}>
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
              data-testid="ai-safety-page-heading"
            >
              {strings.aiSafetyPageHeading}
            </Text>
            <Text
              fontFamily="$body"
              fontSize={14}
              color="$fg3"
              style={{ marginTop: 4 }}
            >
              {strings.aiSafetyPageSubtitle}
            </Text>
          </Stack>
        </Stack>

        {/* Global date-range filter */}
        <AiSafetyFilters
          from={from}
          to={to}
          hasDateRangeError={hasDateRangeError}
          onFromChange={(v) => { setFrom(v); }}
          onToChange={(v) => { setTo(v); }}
          onClear={clearFilters}
        />

        {/* Section 1 — Safety signals + breakdowns */}
        <SafetySignals From={From} To={To} />

        {/* Section 2 — Safety trend line */}
        <SafetyTrend From={From} To={To} />

        {/* Section 3 — Eval results (NO params — evals endpoint takes none) */}
        <EvalResults />

        {/* Section 4 — Tutor usage + cost */}
        <TutorUsage From={From} To={To} />

        {/* Section 5 — Flagged outputs table (paginated, PII-light) */}
        <FlaggedOutputsTable From={From} To={To} />

      </Stack>
    </AdminShell>
  );
}
