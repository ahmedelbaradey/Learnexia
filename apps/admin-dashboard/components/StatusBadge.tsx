'use client';

/**
 * StatusBadge — pill-shaped label for account status and role.
 *
 * Two variants:
 *   - "status" — maps the `accountStatus` int (0 Active / 1 Suspended / 2 Deleted)
 *                to a coloured chip.  Keyed off `ACCOUNT_STATUS` const — never raw ints.
 *   - "role"   — maps a role string ("Parent" | "Student" | "Admin") to a chip.
 *
 * Built with Tamagui `@tamagui/core` Stack/Text + design-system tokens.
 * No CSS module.  Design Spec Part A §A.1.
 *
 * Usage:
 *   <StatusBadge variant="status" value={ACCOUNT_STATUS.Active} strings={s} />
 *   <StatusBadge variant="role" value="Parent" strings={s} />
 */

import { Stack, Text } from '@tamagui/core';
import { ACCOUNT_STATUS, type AccountStatusValue } from '@learnexia/shared';
import type { AdminStrings } from '../lib/strings';

// ── Status variant ─────────────────────────────────────────────────────────────

interface StatusConfig {
  dotColor: string;
  background: string;
  textColor: string;
  getLabel: (s: AdminStrings) => string;
}

const STATUS_CONFIGS: Record<AccountStatusValue, StatusConfig> = {
  [ACCOUNT_STATUS.Active]: {
    dotColor: '#22C55E',
    background: 'rgba(34,197,94,0.15)',
    textColor: '#22C55E',
    getLabel: (s) => s.statusActive,
  },
  [ACCOUNT_STATUS.Suspended]: {
    dotColor: '#F59E0B',
    background: 'rgba(245,158,11,0.15)',
    textColor: '#F59E0B',
    getLabel: (s) => s.statusSuspended,
  },
  [ACCOUNT_STATUS.Deleted]: {
    dotColor: '#EF4444',
    background: 'rgba(239,68,68,0.12)',
    textColor: '#EF4444',
    getLabel: (s) => s.statusDeleted,
  },
};

// ── Role variant ───────────────────────────────────────────────────────────────

interface RoleConfig {
  background: string;
  textColor: string;
  getLabel: (s: AdminStrings) => string;
}

const ROLE_CONFIGS: Record<string, RoleConfig> = {
  Parent: {
    background: 'rgba(79,70,229,0.15)',
    textColor: '#6366F1',
    getLabel: (s) => s.roleParent,
  },
  Student: {
    background: 'rgba(168,85,247,0.15)',
    textColor: '#A855F7',
    getLabel: (s) => s.roleStudent,
  },
  Admin: {
    background: 'rgba(255,255,255,0.08)',
    textColor: '#94A3B8',
    getLabel: (s) => s.roleAdmin,
  },
};

const ROLE_FALLBACK_CONFIG: RoleConfig = {
  background: 'rgba(255,255,255,0.08)',
  textColor: '#94A3B8',
  getLabel: () => '—',
};

// ── Props ──────────────────────────────────────────────────────────────────────

export type StatusBadgeVariant = 'status' | 'role';

interface StatusBadgeStatusProps {
  variant: 'status';
  value: AccountStatusValue;
  strings: AdminStrings;
}

interface StatusBadgeRoleProps {
  variant: 'role';
  value: string | null;
  strings: AdminStrings;
}

export type StatusBadgeProps = StatusBadgeStatusProps | StatusBadgeRoleProps;

// ── Component ──────────────────────────────────────────────────────────────────

export function StatusBadge(props: StatusBadgeProps) {
  if (props.variant === 'status') {
    const config = STATUS_CONFIGS[props.value] ?? STATUS_CONFIGS[ACCOUNT_STATUS.Active];
    const label = config.getLabel(props.strings);

    return (
      <Stack
        tag="span"
        flexDirection="row"
        alignItems="center"
        gap={5}
        display="inline-flex"
        height={22}
        paddingHorizontal={10}
        borderRadius="$pill"
        style={{ background: config.background }}
      >
        {/* Dot — non-color signal via shape (a11y: color is never the only signal) */}
        <Stack
          aria-hidden
          width={6}
          height={6}
          borderRadius="$pill"
          style={{ backgroundColor: config.dotColor, flexShrink: 0 }}
        />
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

  // Role variant
  const role = props.value ?? '';
  const config = ROLE_CONFIGS[role] ?? ROLE_FALLBACK_CONFIG;
  const label = config.getLabel(props.strings);

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
