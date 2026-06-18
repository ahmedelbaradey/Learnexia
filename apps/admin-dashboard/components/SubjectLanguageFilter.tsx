'use client';

/**
 * SubjectLanguageFilter — 3-tab segmented control for ar / en / all (Design Spec §2.2.2).
 *
 * Client-side only — the backend Subjects/List has no Language query param.
 * When language is "all", reorder is disabled (cross-tree reorder guard).
 *
 * Uses CONTENT_LANGUAGE int values (0=Ar, 1=En) or `null` for "all".
 */

import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { CONTENT_LANGUAGE, type ContentLanguageValue } from '@learnexia/shared/constants';

const strings = getStrings(ADMIN_LOCALE);

export type LanguageFilter = ContentLanguageValue | null;

interface Tab {
  value: LanguageFilter;
  label: string;
  testId: string;
}

const TABS: Tab[] = [
  { value: null, label: strings.subjectsFilterAllLanguages, testId: 'subjects-lang-all' },
  { value: CONTENT_LANGUAGE.Ar, label: strings.subjectsFilterLangAr, testId: 'subjects-lang-ar' },
  { value: CONTENT_LANGUAGE.En, label: strings.subjectsFilterLangEn, testId: 'subjects-lang-en' },
];

export interface SubjectLanguageFilterProps {
  value: LanguageFilter;
  onChange: (lang: LanguageFilter) => void;
}

export function SubjectLanguageFilter({ value, onChange }: SubjectLanguageFilterProps) {
  return (
    <div
      role="tablist"
      aria-label={strings.subjectsFilterAllLanguages}
      style={{
        display: 'flex',
        flexDirection: 'row',
        gap: 0,
        borderRadius: 8,
        border: '1px solid var(--lx-border)',
        overflow: 'hidden',
        backgroundColor: 'var(--lx-bg)',
      }}
    >
      {TABS.map((tab, index) => {
        const isActive = tab.value === value;
        return (
          <button
            key={String(tab.value)}
            role="tab"
            aria-selected={isActive}
            data-testid={tab.testId}
            onClick={() => onChange(tab.value)}
            style={{
              flex: 1,
              height: 40,
              paddingLeft: 12,
              paddingRight: 12,
              border: 'none',
              borderInlineEnd: index < TABS.length - 1 ? '1px solid var(--lx-border)' : 'none',
              cursor: 'pointer',
              backgroundColor: isActive ? 'rgba(79,70,229,0.18)' : 'transparent',
              color: isActive ? '#4F46E5' : '#94A3B8',
              fontSize: 13,
              fontWeight: isActive ? 600 : 500,
              fontFamily: 'inherit',
              transition: 'background-color 120ms var(--lx-ease-out)',
              outline: 'none',
            }}
            onFocus={(e) => {
              (e.currentTarget as HTMLElement).style.boxShadow = 'var(--lx-focus-ring)';
            }}
            onBlur={(e) => {
              (e.currentTarget as HTMLElement).style.boxShadow = 'none';
            }}
          >
            {tab.label}
          </button>
        );
      })}
    </div>
  );
}
