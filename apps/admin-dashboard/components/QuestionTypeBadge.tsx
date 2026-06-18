'use client';

/**
 * QuestionTypeBadge — maps QuestionTypeValue (1/2/3/4) to a colored pill.
 * Design Spec §S3 — P7-04.
 * Mirrors DifficultyBadge shape.
 */

import { QUESTION_TYPE, type QuestionTypeValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

interface QuestionTypeBadgeProps {
  questionType: QuestionTypeValue;
}

const CONFIG: Record<QuestionTypeValue, { bg: string; text: string; label: string }> = {
  [QUESTION_TYPE.MCQ]:         { bg: 'rgba(99,102,241,0.15)',  text: '#6366F1', label: strings.questionTypeMcq },
  [QUESTION_TYPE.TrueFalse]:   { bg: 'rgba(20,184,166,0.15)',  text: '#14B8A6', label: strings.questionTypeTrueFalse },
  [QUESTION_TYPE.Matching]:    { bg: 'rgba(245,158,11,0.15)',  text: '#F59E0B', label: strings.questionTypeMatching },
  [QUESTION_TYPE.FillInBlank]: { bg: 'rgba(236,72,153,0.15)', text: '#EC4899', label: strings.questionTypeFillInBlank },
};

export function QuestionTypeBadge({ questionType }: QuestionTypeBadgeProps) {
  const cfg = CONFIG[questionType];
  if (!cfg) return null;

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        height: 22,
        borderRadius: 9999,
        paddingLeft: 10,
        paddingRight: 10,
        backgroundColor: cfg.bg,
        flexShrink: 0,
      }}
    >
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
