'use client';

/**
 * ActionTypeBadge — colored chip for an admin action-type string.
 *
 * Design Spec §D. Domain-keyed colors via `getActionDomain`. Falls back to
 * the neutral/gray palette for unregistered future action types.
 *
 * READ-ONLY: no interactive states, no mutation hooks, no click handlers.
 *
 * RTL: dot is `marginInlineEnd` (logical — flips correctly in RTL). Label text
 * is the localized label from `auditActionLabels`. Layout is symmetric.
 */

import { ADMIN_LOCALE } from '../lib/strings';
import { getActionLabel, getActionDomain } from '../lib/auditActionLabels';

interface ActionTypeBadgeProps {
  action: string;
}

// ── Domain → color map (Design Spec §D.2) ────────────────────────────────────

interface DomainColors {
  dot: string;
  background: string;
  text: string;
}

function getDomainColors(domain: string): DomainColors {
  switch (domain) {
    case 'Subject':
    case 'Unit':
    case 'Lesson':
    case 'ContentBlock':
      return {
        dot: '#4F46E5',
        background: 'rgba(79,70,229,0.15)',
        text: '#4F46E5',
      };

    case 'Skill':
    case 'KnowledgeEdge':
      return {
        dot: '#6366F1',
        background: 'rgba(99,102,241,0.15)',
        text: '#6366F1',
      };

    case 'QuizQuestion':
      return {
        dot: '#4F46E5',
        background: 'rgba(79,70,229,0.12)',
        text: '#4F46E5',
      };

    case 'Content':
      return {
        dot: '#22C55E',
        background: 'rgba(34,197,94,0.15)',
        text: '#22C55E',
      };

    case 'User':
    case 'Account':
      return {
        dot: '#F59E0B',
        background: 'rgba(245,158,11,0.15)',
        text: '#F59E0B',
      };

    case 'Child':
      return {
        dot: '#A855F7',
        background: 'rgba(168,85,247,0.15)',
        text: '#A855F7',
      };

    case 'Gamification':
    case 'Badge':
    case 'Mission':
    case 'TimedEvent':
      return {
        dot: '#A855F7',
        background: 'rgba(168,85,247,0.12)',
        text: '#A855F7',
      };

    case 'GlobalSetting':
    case 'ModerationItem':
      return {
        dot: '#94A3B8',
        background: 'rgba(148,163,184,0.12)',
        text: '#94A3B8',
      };

    default:
      // Fallback: neutral gray for any unregistered future action domain.
      return {
        dot: '#94A3B8',
        background: 'rgba(148,163,184,0.10)',
        text: '#94A3B8',
      };
  }
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ActionTypeBadge({ action }: ActionTypeBadgeProps) {
  const domain = getActionDomain(action);
  const colors = getDomainColors(domain);
  const label = getActionLabel(action, ADMIN_LOCALE);

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        height: 22,
        borderRadius: 9999,
        paddingLeft: 10,
        paddingRight: 10,
        backgroundColor: colors.background,
        gap: 5,
        maxWidth: 180,
        overflow: 'hidden',
      }}
    >
      {/* Dot — logical marginInlineEnd so it flips correctly in RTL */}
      <span
        aria-hidden="true"
        style={{
          display: 'inline-block',
          width: 6,
          height: 6,
          borderRadius: 9999,
          backgroundColor: colors.dot,
          flexShrink: 0,
          marginInlineEnd: 0,
        }}
      />
      <span
        style={{
          fontSize: 12,
          fontWeight: 600,
          letterSpacing: '0.04em',
          textTransform: 'uppercase',
          color: colors.text,
          fontFamily: 'inherit',
          overflow: 'hidden',
          textOverflow: 'ellipsis',
          whiteSpace: 'nowrap',
          maxWidth: 160,
        }}
      >
        {label}
      </span>
    </span>
  );
}
