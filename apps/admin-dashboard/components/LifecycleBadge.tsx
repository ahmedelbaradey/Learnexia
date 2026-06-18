'use client';

/**
 * LifecycleBadge — pill for the LifecycleState axis (Draft/Published/Archived).
 *
 * Mirrors StatusBadge.tsx exactly: inline dot + uppercase label + color.
 * Three independent signals (dot shape, text content, color) so color is never
 * the only indicator (a11y requirement).
 *
 * This badge is DISTINCT from the IsActive boolean badge (ActiveBadge.tsx).
 * They represent two separate axes:
 *   - IsActive (bool)    → Active / Inactive toggle owned by P7-01..04.
 *   - LifecycleState (1/2/3) → Draft / Published / Archived owned by P7-05.
 *
 * When both appear on the same row the semantic distinction is clear via label.
 *
 * Design Spec P7-05 §Component 1.
 *
 * Props:
 *   state   — LifecycleStateValue (1/2/3). Use LIFECYCLE_STATE.* — never raw ints.
 *   strings — AdminStrings instance for the active locale.
 *
 * States:
 *   - Normal: pill as below.
 *   - Unknown/out-of-range value: neutral grey "—" pill (defensive fallback).
 */

import { Stack, Text } from '@tamagui/core';
import { LIFECYCLE_STATE, type LifecycleStateValue } from '@learnexia/shared/constants';
import type { AdminStrings } from '../lib/strings';

// ── Config ────────────────────────────────────────────────────────────────────

interface LifecycleConfig {
  dotColor: string;
  background: string;
  textColor: string;
  getLabel: (s: AdminStrings) => string;
}

const LIFECYCLE_CONFIGS: Record<LifecycleStateValue, LifecycleConfig> = {
  [LIFECYCLE_STATE.Draft]: {
    dotColor: '#94A3B8',
    background: 'rgba(148,163,184,0.12)',
    textColor: '#94A3B8',
    getLabel: (s) => s.clLifecycleDraft,
  },
  [LIFECYCLE_STATE.Published]: {
    dotColor: '#22C55E',
    background: 'rgba(34,197,94,0.15)',
    textColor: '#22C55E',
    getLabel: (s) => s.clLifecyclePublished,
  },
  [LIFECYCLE_STATE.Archived]: {
    dotColor: '#F59E0B',
    background: 'rgba(245,158,11,0.12)',
    textColor: '#F59E0B',
    getLabel: (s) => s.clLifecycleArchived,
  },
};

const FALLBACK_CONFIG: LifecycleConfig = {
  dotColor: '#94A3B8',
  background: 'rgba(148,163,184,0.08)',
  textColor: '#94A3B8',
  getLabel: () => '—',
};

// ── Props ─────────────────────────────────────────────────────────────────────

export interface LifecycleBadgeProps {
  /** LifecycleState int from the API (1/2/3). Use LIFECYCLE_STATE.* — never raw ints. */
  state: LifecycleStateValue | null | undefined;
  /** Pass the AdminStrings instance for the active locale. */
  strings: AdminStrings;
  /** Optional data-testid for targeting in tests. */
  testId?: string;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function LifecycleBadge({ state, strings, testId }: LifecycleBadgeProps) {
  const config =
    state != null && state in LIFECYCLE_CONFIGS
      ? (LIFECYCLE_CONFIGS[state as LifecycleStateValue] ?? FALLBACK_CONFIG)
      : FALLBACK_CONFIG;

  const label = config.getLabel(strings);

  return (
    <Stack
      tag="span"
      flexDirection="row"
      alignItems="center"
      gap={5}
      display="inline-flex"
      height={22}
      paddingHorizontal={10}
      borderRadius="$pill"
      style={{ background: config.background }}
      data-testid={testId}
    >
      {/* Dot — non-color signal via shape (a11y: three signals total) */}
      <Stack
        aria-hidden
        width={6}
        height={6}
        borderRadius="$pill"
        style={{ backgroundColor: config.dotColor, flexShrink: 0 }}
      />
      <Text
        fontFamily="$body"
        fontSize={12}
        fontWeight="600"
        letterSpacing={0.4}
        style={{ color: config.textColor, textTransform: 'uppercase' }}
      >
        {label}
      </Text>
    </Stack>
  );
}
