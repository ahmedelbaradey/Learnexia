'use client';

/**
 * InheritedLanguageBadge — read-only chip showing content language (AR / EN).
 * No edit affordance. Drives preview direction when language prop is used.
 * Design Spec §S7 — P7-02.
 */

import { CONTENT_LANGUAGE, type ContentLanguageValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

function GlobeIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  );
}

export interface InheritedLanguageBadgeProps {
  /** ContentLanguage int: 0 = Ar, 1 = En. */
  language: ContentLanguageValue;
  /** compact = icon + short code only; default = full "Language: Arabic/English" */
  compact?: boolean;
}

export function InheritedLanguageBadge({ language, compact = false }: InheritedLanguageBadgeProps) {
  const isAr = language === CONTENT_LANGUAGE.Ar;
  const shortCode = isAr ? 'AR' : 'EN';
  const fullName = isAr ? strings.lessonInheritedLangAr : strings.lessonInheritedLangEn;

  return (
    <span
      style={{
        display: 'inline-flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: 6,
        paddingLeft: 10,
        paddingRight: 10,
        height: 24,
        borderRadius: 9999,
        backgroundColor: 'rgba(255,255,255,0.06)',
        border: '1px solid var(--lx-border)',
        color: 'var(--lx-fg3)',
        flexShrink: 0,
      }}
    >
      <GlobeIcon />
      {compact ? (
        <span style={{ fontSize: 12, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em' }}>
          {shortCode}
        </span>
      ) : (
        <>
          <span style={{ fontSize: 11, color: 'var(--lx-fg3)' }}>
            {strings.lessonInheritedLangLabel}
          </span>
          <span style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg2)' }}>
            {fullName}
          </span>
        </>
      )}
    </span>
  );
}
