'use client';

/**
 * LanguageBadge — pill badge for a ContentLanguage value (Design Spec §3.1).
 *
 * Maps ContentLanguageValue (0=Ar, 1=En) to a colored pill. Color coding:
 *   Ar → amber ($accent) — consistent with "Arabic content" throughout admin
 *   En → purple            — consistent with "English content" throughout admin
 */

import { CONTENT_LANGUAGE, type ContentLanguageValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

interface LangConfig {
  label: string;
  background: string;
  text: string;
}

const LANG_CONFIG: Record<ContentLanguageValue, LangConfig> = {
  [CONTENT_LANGUAGE.Ar]: {
    label: strings.contentLangAr,
    background: 'rgba(245,158,11,0.12)',
    text: '#F59E0B',
  },
  [CONTENT_LANGUAGE.En]: {
    label: strings.contentLangEn,
    background: 'rgba(168,85,247,0.12)',
    text: '#A855F7',
  },
};

export interface LanguageBadgeProps {
  language: ContentLanguageValue;
}

export function LanguageBadge({ language }: LanguageBadgeProps) {
  const config = LANG_CONFIG[language];
  if (!config) return null;

  return (
    <span
      style={{
        display: 'inline-flex',
        height: 20,
        borderRadius: 9999,
        paddingLeft: 8,
        paddingRight: 8,
        alignItems: 'center',
        background: config.background,
      }}
    >
      <span
        style={{
          fontSize: 11,
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
