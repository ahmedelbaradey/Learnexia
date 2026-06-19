'use client';

/**
 * LockBadge — maps isLocked bool to a pill showing Locked / Unlocked.
 * Design Spec §S3 — P7-02.
 */

import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

function LockIcon() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
    </svg>
  );
}

function UnlockIcon() {
  return (
    <svg width="11" height="11" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
      <path d="M7 11V7a5 5 0 0 1 9.9-1" />
    </svg>
  );
}

interface LockBadgeProps {
  isLocked: boolean;
}

export function LockBadge({ isLocked }: LockBadgeProps) {
  const color = isLocked ? '#A855F7' : '#94A3B8';
  const bg = isLocked ? 'rgba(168,85,247,0.12)' : 'rgba(255,255,255,0.05)';

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
        backgroundColor: bg,
        color,
        flexShrink: 0,
      }}
    >
      {isLocked ? <LockIcon /> : <UnlockIcon />}
      <span
        style={{
          fontSize: 12,
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.04em',
        }}
      >
        {isLocked ? strings.lessonLocked : strings.lessonUnlocked}
      </span>
    </span>
  );
}
