'use client';

/**
 * SourceBadge — pill chip for ModerationSource (P7-09).
 *
 * Two sources: AiOutput (teal energy colour) | CurriculumUpload (indigo primary).
 *
 * Anatomy: same pill shape as StatusBadge — inline-flex, height 22px,
 * borderRadius $pill (9999px), paddingHorizontal 10px. No dot (source is binary;
 * label provides all semantic meaning). Text: 12px, weight 600, uppercase.
 *
 * Design Spec §A.2.
 *
 * Usage:
 *   <SourceBadge value="AiOutput" strings={s} />
 *   <SourceBadge value="CurriculumUpload" strings={s} />
 */

import { Stack, Text } from '@tamagui/core';
import type { ModerationSource } from '@learnexia/api-client';
import type { AdminStrings } from '../lib/strings';

// ── Source config ─────────────────────────────────────────────────────────────

interface SourceConfig {
  background: string;
  textColor: string;
  getLabel: (s: AdminStrings) => string;
}

const SOURCE_CONFIGS: Record<ModerationSource, SourceConfig> = {
  AiOutput: {
    background: 'rgba(45,212,191,0.15)',
    textColor: '#2DD4BF',
    getLabel: (s) => s.modSourceAiOutput,
  },
  CurriculumUpload: {
    background: 'rgba(79,70,229,0.15)',
    textColor: '#6366F1',
    getLabel: (s) => s.modSourceCurriculumUpload,
  },
};

const SOURCE_FALLBACK: SourceConfig = {
  background: 'rgba(255,255,255,0.08)',
  textColor: 'var(--lx-fg3)',
  getLabel: () => '—',
};

// ── Props ─────────────────────────────────────────────────────────────────────

export interface SourceBadgeProps {
  value: ModerationSource | string;
  strings: AdminStrings;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function SourceBadge({ value, strings }: SourceBadgeProps) {
  const config =
    SOURCE_CONFIGS[value as ModerationSource] ?? SOURCE_FALLBACK;
  const label = config.getLabel(strings);

  return (
    <Stack
      tag="span"
      display="inline-flex"
      flexDirection="row"
      alignItems="center"
      height={22}
      paddingHorizontal={10}
      borderRadius="$pill"
      style={{ background: config.background }}
    >
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
