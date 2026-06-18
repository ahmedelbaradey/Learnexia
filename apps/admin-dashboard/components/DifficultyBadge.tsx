'use client';

/**
 * DifficultyBadge — maps DifficultyLevelValue (1/2/3) to a colored pill.
 * Design Spec §S3 — P7-02.
 */

import { DIFFICULTY_LEVEL, type DifficultyLevelValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

interface DifficultyBadgeProps {
  difficulty: DifficultyLevelValue;
}

const CONFIG: Record<DifficultyLevelValue, { dot: string; bg: string; text: string; label: string }> = {
  [DIFFICULTY_LEVEL.Easy]:   { dot: '#22C55E', bg: 'rgba(34,197,94,0.15)',   text: '#22C55E', label: strings.lessonDifficultyEasy },
  [DIFFICULTY_LEVEL.Medium]: { dot: '#F59E0B', bg: 'rgba(245,158,11,0.15)',  text: '#F59E0B', label: strings.lessonDifficultyMedium },
  [DIFFICULTY_LEVEL.Hard]:   { dot: '#EF4444', bg: 'rgba(239,68,68,0.15)',   text: '#EF4444', label: strings.lessonDifficultyHard },
};

export function DifficultyBadge({ difficulty }: DifficultyBadgeProps) {
  const cfg = CONFIG[difficulty];
  if (!cfg) return null;

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 5,
        height: 22,
        borderRadius: 9999,
        paddingLeft: 10,
        paddingRight: 10,
        backgroundColor: cfg.bg,
        flexShrink: 0,
      }}
    >
      <span
        aria-hidden="true"
        style={{ width: 6, height: 6, borderRadius: '50%', backgroundColor: cfg.dot, flexShrink: 0 }}
      />
      <span
        style={{
          fontSize: 12,
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.04em',
          color: cfg.text,
        }}
      >
        {cfg.label}
      </span>
    </span>
  );
}
