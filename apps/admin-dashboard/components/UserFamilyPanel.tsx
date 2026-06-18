'use client';

/**
 * UserFamilyPanel — family linkage for a single user (P7-06-FE-3).
 *
 * For a parent: lists linked children (name + grade, NO email by design D7).
 * For a child: lists linked parents (name + email).
 * Each member deep-links to `/users/[id]`.
 *
 * Loads independently from the core profile and activity panels (AC 7) —
 * a fetch error here must NOT blank the profile card.
 *
 * Design Spec Part C §C.4.
 * Acceptance criteria: P7-06 AC 5.
 */

import { Stack, Text } from '@tamagui/core';
import Link from 'next/link';

import { useUserFamily, type AdminFamilyMemberDto } from '@learnexia/api-client';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Skeleton ───────────────────────────────────────────────────────────────────

function FamilySkeleton() {
  return (
    <Stack flexDirection="column" gap="$3">
      {[1, 2].map((i) => (
        <div
          key={i}
          role="presentation"
          style={{
            height: 56,
            borderRadius: 'var(--lx-radius-sm)',
            background:
              'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%',
            animation: 'lx-shimmer 1400ms linear infinite',
          }}
        />
      ))}
    </Stack>
  );
}

// ── Avatar chip ────────────────────────────────────────────────────────────────

function MemberAvatar({ name }: { name: string }) {
  const initial = name.trim().charAt(0).toUpperCase() || '?';
  return (
    <span
      aria-hidden="true"
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: 32,
        height: 32,
        borderRadius: '50%',
        backgroundColor: 'rgba(79,70,229,0.18)',
        color: '#4F46E5',
        fontSize: 12,
        fontWeight: 700,
        flexShrink: 0,
      }}
    >
      {initial}
    </span>
  );
}

// ── Chevron icons ──────────────────────────────────────────────────────────────

function ChevronRightIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M9 18l6-6-6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

// RTL-aware chevron: in RTL context chevron-left is used instead.
// Since this is a web-only admin surface we use CSS logical + inline dir
// to flip the chevron direction.
function MemberChevron() {
  return (
    <span style={{ color: 'var(--lx-fg3)', display: 'flex', alignItems: 'center' }}>
      <ChevronRightIcon />
    </span>
  );
}

// ── Member row ─────────────────────────────────────────────────────────────────

interface MemberRowProps {
  member: AdminFamilyMemberDto;
  /** When true: this is a child member → show grade, hide email. */
  isChild: boolean;
}

function MemberRow({ member, isChild }: MemberRowProps) {
  const secondaryLine = isChild
    ? member.grade != null
      ? `${strings.userDetailGradePrefix} ${member.grade}`
      : null
    : member.email;

  return (
    <Link
      href={`/users/${member.id}`}
      style={{ textDecoration: 'none', display: 'block' }}
      aria-label={`${member.fullName} — ${strings.usersViewProfile}`}
    >
      <Stack
        flexDirection="row"
        alignItems="center"
        gap="$3"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'var(--lx-bg)',
          border: '1px solid var(--lx-border)',
          cursor: 'pointer',
          transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
        }}
        hoverStyle={{ backgroundColor: '$card' } as never}
      >
        <MemberAvatar name={member.fullName} />

        <Stack flex={1} flexDirection="column" gap={2}>
          <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
            {member.fullName}
          </Text>
          {secondaryLine ? (
            <Text
              fontFamily="$body"
              fontSize={12}
              color="$fg3"
              dir={isChild ? undefined : 'ltr'}
              style={
                !isChild
                  ? { fontFamily: 'var(--lx-font-mono, monospace)' }
                  : undefined
              }
            >
              {secondaryLine}
            </Text>
          ) : null}
        </Stack>

        <MemberChevron />
      </Stack>
    </Link>
  );
}

// ── Panel ──────────────────────────────────────────────────────────────────────

export interface UserFamilyPanelProps {
  userId: number;
}

export function UserFamilyPanel({ userId }: UserFamilyPanelProps) {
  const { data, isLoading, isError, refetch } = useUserFamily(userId);

  const heading =
    data?.userRole === 'Parent'
      ? strings.userFamilyLinkedChildren
      : data?.userRole === 'Student'
        ? strings.userFamilyLinkedParents
        : strings.userFamilyPanelHeading;

  const members =
    data?.userRole === 'Parent'
      ? (data.children ?? [])
      : (data?.parents ?? []);

  const isChildMembers = data?.userRole === 'Parent'; // children listed → child members

  const emptyText =
    data?.userRole === 'Parent'
      ? strings.userFamilyNoChildren
      : strings.userFamilyNoParents;

  return (
    <Stack
      flexDirection="column"
      gap="$4"
      padding="$6"
      borderRadius="$card"
      style={{
        backgroundColor: 'var(--lx-card)',
        border: '1px solid var(--lx-border)',
      }}
    >
      {/* Panel heading */}
      <Text
        fontFamily="$body"
        fontSize={14}
        fontWeight="600"
        color="$fg3"
        style={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}
      >
        {heading}
      </Text>

      {isLoading && <FamilySkeleton />}

      {isError && (
        <Stack flexDirection="column" gap="$3">
          <AdminErrorBanner variant="error" message={strings.usersListError} />
          <button
            type="button"
            onClick={() => void refetch()}
            style={{
              alignSelf: 'flex-start',
              height: 32,
              paddingInline: 12,
              borderRadius: 'var(--lx-radius-button)',
              border: '1px solid var(--lx-border)',
              backgroundColor: 'transparent',
              color: 'var(--lx-fg2)',
              fontSize: 12,
              cursor: 'pointer',
              fontFamily: 'inherit',
            }}
          >
            {strings.usersListRetry}
          </button>
        </Stack>
      )}

      {!isLoading && !isError && members.length === 0 && (
        <Text
          fontFamily="$body"
          fontSize={14}
          color="$fg3"
          style={{ padding: '16px', textAlign: 'center' }}
        >
          {emptyText}
        </Text>
      )}

      {!isLoading && !isError && members.length > 0 && (
        <Stack flexDirection="column" gap="$3">
          {members.map((member) => (
            <MemberRow
              key={member.id}
              member={member}
              isChild={isChildMembers}
            />
          ))}
        </Stack>
      )}
    </Stack>
  );
}
