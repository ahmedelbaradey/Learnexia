'use client';

/**
 * ActiveBadge — pill badge for curriculum IsActive (bool) state (Design Spec §3.2).
 *
 * Distinct from StatusBadge (which maps the AccountStatus enum 0/1/2).
 * This is a simple active/inactive bool for subjects and units.
 */

import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface ActiveBadgeProps {
  isActive: boolean;
}

export function ActiveBadge({ isActive }: ActiveBadgeProps) {
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
        background: isActive ? 'rgba(34,197,94,0.15)' : 'rgba(255,255,255,0.06)',
        whiteSpace: 'nowrap',
      }}
    >
      <span
        aria-hidden
        style={{
          width: 6,
          height: 6,
          borderRadius: '50%',
          backgroundColor: isActive ? '#22C55E' : '#94A3B8',
          flexShrink: 0,
        }}
      />
      <span
        style={{
          fontSize: 12,
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.04em',
          color: isActive ? '#22C55E' : '#94A3B8',
          fontFamily: 'inherit',
        }}
      >
        {isActive ? strings.subjectActiveBadge : strings.subjectInactiveBadge}
      </span>
    </span>
  );
}
