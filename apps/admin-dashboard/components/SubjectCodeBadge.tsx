'use client';

/**
 * SubjectCodeBadge — pill badge for a SubjectCode value (Design Spec §3.1).
 *
 * Maps SubjectCodeValue (int 0–3) to a colored dot + label pill. Mirrors the
 * StatusBadge pill geometry (dot + label, color never the only signal — a11y).
 * Used in the subjects table's Subject cell and in the coverage slot header.
 */

import { SUBJECT_CODE, type SubjectCodeValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

interface CodeConfig {
  label: string;
  dot: string;
  background: string;
  text: string;
}

const CODE_CONFIG: Record<SubjectCodeValue, CodeConfig> = {
  [SUBJECT_CODE.MATH]: {
    label: strings.subjectCodeMath,
    dot: '#4F46E5',
    background: 'rgba(79,70,229,0.15)',
    text: '#6366F1',
  },
  [SUBJECT_CODE.SCIENCE]: {
    label: strings.subjectCodeScience,
    dot: '#22C55E',
    background: 'rgba(34,197,94,0.15)',
    text: '#22C55E',
  },
  [SUBJECT_CODE.ARABIC]: {
    label: strings.subjectCodeArabic,
    dot: '#F59E0B',
    background: 'rgba(245,158,11,0.15)',
    text: '#F59E0B',
  },
  [SUBJECT_CODE.ENGLISH]: {
    label: strings.subjectCodeEnglish,
    dot: '#A855F7',
    background: 'rgba(168,85,247,0.15)',
    text: '#A855F7',
  },
};

export interface SubjectCodeBadgeProps {
  code: SubjectCodeValue;
}

export function SubjectCodeBadge({ code }: SubjectCodeBadgeProps) {
  const config = CODE_CONFIG[code];
  if (!config) return null;

  return (
    <span
      style={{
        display: 'inline-flex',
        height: 22,
        borderRadius: 9999,
        paddingLeft: 10,
        paddingRight: 10,
        alignItems: 'center',
        gap: 5,
        background: config.background,
        whiteSpace: 'nowrap',
      }}
    >
      <span
        aria-hidden
        style={{
          width: 6,
          height: 6,
          borderRadius: '50%',
          backgroundColor: config.dot,
          flexShrink: 0,
        }}
      />
      <span
        style={{
          fontSize: 12,
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.04em',
          color: config.text,
          fontFamily: 'inherit',
        }}
      >
        {config.label}
      </span>
    </span>
  );
}
