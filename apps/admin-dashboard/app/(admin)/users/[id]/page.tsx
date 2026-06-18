'use client';

// Force dynamic rendering — same reason as users/page.tsx.
// Admin data is never static; force-dynamic prevents the static-prerender
// from hitting the api-client initializer without NEXT_PUBLIC_API_URL.
export const dynamic = 'force-dynamic';

/**
 * User detail page — `/users/[id]` (P7-06-FE-2, FE-3, FE-4).
 *
 * Read-only admin-inspect view for a single user account.
 * Renders:
 *   - Detail header (avatar, name, email, role + status badges, status reason)
 *   - Profile fields card (incl. child-only block with TWO DISTINCT language rows)
 *   - UserFamilyPanel (loads independently)
 *   - UserActivityPanel (loads independently)
 *
 * INSERTION SEAMS (pre-cut for parallel batches):
 *   {/* P7-07 actions slot *\/}  — LifecycleActionsMenu goes here (Batch C)
 *   {/* P7-08 child-edit slot *\/} — Edit Profile entry button goes here (Batch D)
 *
 * Wraps its own AdminShell with a dynamic title (user's fullName or fallback)
 * so the TopBar shows the user's name instead of the generic "Users" title.
 *
 * Design Spec Part C §C.1–C.6.
 * Acceptance criteria: P7-06 AC 3, 4, 5, 6, 7, 8.
 */

import { use, useState } from 'react';
import Link from 'next/link';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'next/navigation';

import { useAdminUserProfile } from '@learnexia/api-client';
import { ACCOUNT_STATUS } from '@learnexia/shared';

import { AdminShell } from '../../../../components/AdminShell';
import { StatusBadge } from '../../../../components/StatusBadge';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { UserFamilyPanel } from '../../../../components/UserFamilyPanel';
import { UserActivityPanel } from '../../../../components/UserActivityPanel';
import { SuspendUserDialog } from '../../../../components/SuspendUserDialog';
import { ReactivateUserDialog } from '../../../../components/ReactivateUserDialog';
import { DeleteUserDialog } from '../../../../components/DeleteUserDialog';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Skeleton for the header + profile card ────────────────────────────────────

function DetailSkeleton() {
  function SkeletonBlock({ w, h }: { w: number | string; h: number }) {
    return (
      <div
        role="presentation"
        style={{
          height: h,
          width: w,
          maxWidth: '100%',
          borderRadius: 6,
          background:
            'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
          backgroundSize: '400px 100%',
          animation: 'lx-shimmer 1400ms linear infinite',
        }}
      />
    );
  }

  return (
    <Stack
      role="status"
      aria-label={strings.userDetailLoadingLabel}
      flexDirection="column"
      gap="$6"
    >
      {/* Header skeleton */}
      <Stack
        flexDirection="row"
        gap="$6"
        alignItems="flex-start"
        padding="$6"
        borderRadius="$card"
        style={{ backgroundColor: 'var(--lx-card)', border: '1px solid var(--lx-border)' }}
      >
        <SkeletonBlock w={64} h={64} />
        <Stack flex={1} flexDirection="column" gap="$2">
          <SkeletonBlock w={200} h={22} />
          <SkeletonBlock w={160} h={14} />
          <Stack flexDirection="row" gap="$2" marginTop="$1">
            <SkeletonBlock w={70} h={22} />
            <SkeletonBlock w={80} h={22} />
          </Stack>
        </Stack>
      </Stack>

      {/* Profile card skeleton */}
      <Stack
        padding="$6"
        borderRadius="$card"
        style={{ backgroundColor: 'var(--lx-card)', border: '1px solid var(--lx-border)' }}
      >
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          {Array.from({ length: 6 }).map((_, i) => (
            <Stack key={i} flexDirection="column" gap="$1">
              <SkeletonBlock w={80} h={12} />
              <SkeletonBlock w="100%" h={16} />
            </Stack>
          ))}
        </div>
      </Stack>
    </Stack>
  );
}

// ── "User not found" state ────────────────────────────────────────────────────

function NotFoundState({ onBack }: { onBack: () => void }) {
  return (
    <Stack
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      padding="$10"
      gap="$4"
      borderRadius="$card"
      style={{
        backgroundColor: 'var(--lx-card)',
        border: '1px solid var(--lx-border)',
      }}
    >
      <span style={{ color: 'var(--lx-fg3)' }}>
        <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
          <circle cx="12" cy="7" r="4" stroke="currentColor" strokeWidth="1.5" />
          <path d="m14 14 4 4m0-4-4 4" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
        </svg>
      </span>
      <Text fontFamily="$heading" fontSize={18} fontWeight="600" color="$fg1">
        {strings.userDetailNotFound}
      </Text>
      <Text
        fontFamily="$body"
        fontSize={14}
        color="$fg3"
        style={{ textAlign: 'center', maxWidth: 320 }}
      >
        {strings.userDetailNotFoundBody}
      </Text>
      <button
        type="button"
        onClick={onBack}
        style={{
          height: 36,
          paddingInline: 16,
          borderRadius: 'var(--lx-radius-button)',
          border: '1px solid var(--lx-border)',
          backgroundColor: 'transparent',
          color: 'var(--lx-fg2)',
          fontSize: 13,
          cursor: 'pointer',
          fontFamily: 'inherit',
        }}
      >
        {strings.userDetailBackToUsers}
      </button>
    </Stack>
  );
}

// ── Field row ─────────────────────────────────────────────────────────────────

interface FieldRowProps {
  label: string;
  children: React.ReactNode;
}

function FieldRow({ label, children }: FieldRowProps) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text
        fontFamily="$body"
        fontSize={12}
        fontWeight="500"
        color="$fg3"
      >
        {label}
      </Text>
      <div>{children}</div>
    </Stack>
  );
}

// ── Date formatter ─────────────────────────────────────────────────────────────

function formatDate(iso: string | null): string {
  if (!iso) return '—';
  try {
    return new Intl.DateTimeFormat(ADMIN_LOCALE === 'ar' ? 'ar-EG' : 'en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

// ── Language display (Gap 3 — Intl.DisplayNames for culture codes) ────────────

function formatLanguageName(code: string): string {
  if (!code) return code;
  // Normalize short codes that aren't full culture codes
  // "ar" → "ar", "en" → "en"; "ar-EG" stays as-is.
  try {
    const names = new Intl.DisplayNames(['en'], { type: 'language' });
    return names.of(code) ?? code;
  } catch {
    return code;
  }
}

// ── Learning language display (ar/en short codes → human names) ────────────────

function formatLearningLanguage(code: string, s: typeof strings): string {
  if (code === 'ar') return s.langArabic;
  if (code === 'en') return s.langEnglish;
  return code;
}

// ── Lifecycle icons ───────────────────────────────────────────────────────────

function PauseCircleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <line x1="10" y1="15" x2="10" y2="9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <line x1="14" y1="15" x2="14" y2="9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function PlayCircleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <polygon points="10,8 16,12 10,16" fill="currentColor" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M3 6h18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M19 6l-1 14H6L5 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M10 11v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M14 11v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M9 6V4h6v2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <rect x="4" y="10" width="16" height="11" rx="2" stroke="currentColor" strokeWidth="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" stroke="currentColor" strokeWidth="2" />
    </svg>
  );
}

/** Lucide pencil — 14px, used for the P7-08 Edit Profile entry button. */
function PencilIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path
        d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

// ── Page component ────────────────────────────────────────────────────────────

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function UserDetailPage({ params }: PageProps) {
  const { id: idParam } = use(params);
  const userId = parseInt(idParam, 10);
  const router = useRouter();

  const { data: profile, isLoading, isError } = useAdminUserProfile(userId);

  // ── P7-07 lifecycle dialog state ──────────────────────────────────────────
  const [suspendOpen, setSuspendOpen] = useState(false);
  const [reactivateOpen, setReactivateOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  // Success banner shown above detail header after a successful lifecycle action
  const [successBanner, setSuccessBanner] = useState<{
    message: string;
    variant: 'success' | 'warning' | 'error';
  } | null>(null);

  // Dynamic title for the TopBar: user name once loaded, fallback while loading
  const pageTitle = profile?.fullName ?? strings.pageTitleUserDetail;

  const isStudent = profile?.roles?.includes('Student') ?? false;
  const isParent = profile?.roles?.includes('Parent') ?? false;

  return (
    <AdminShell title={pageTitle}>
      <Stack flexDirection="column" gap="$6">
        {/* P7-07 success banner — shown after lifecycle actions */}
        {successBanner && (
          <div data-testid="user-detail-success-banner">
            <AdminErrorBanner variant={successBanner.variant} message={successBanner.message} />
          </div>
        )}

        {/* Loading state */}
        {isLoading && <DetailSkeleton />}

        {/* Error state (non-404) */}
        {isError && !isLoading && (
          <Stack flexDirection="column" gap="$4">
            <AdminErrorBanner
              variant="error"
              message={strings.userDetailNotFound}
            />
            <button
              type="button"
              onClick={() => router.push('/users')}
              style={{
                alignSelf: 'flex-start',
                height: 36,
                paddingInline: 16,
                borderRadius: 'var(--lx-radius-button)',
                border: '1px solid var(--lx-border)',
                backgroundColor: 'transparent',
                color: 'var(--lx-fg2)',
                fontSize: 13,
                cursor: 'pointer',
                fontFamily: 'inherit',
              }}
            >
              {strings.userDetailBackToUsers}
            </button>
          </Stack>
        )}

        {/* Not found state — 404 or invalid id (id <= 0 is disabled, won't fire) */}
        {!isLoading && !isError && !profile && (
          <NotFoundState onBack={() => router.push('/users')} />
        )}

        {/* Profile loaded */}
        {!isLoading && !isError && profile && (
          <>
            {/* ── Detail header card ─────────────────────────────────────────── */}
            <Stack
              flexDirection="row"
              alignItems="flex-start"
              gap="$6"
              padding="$6"
              borderRadius="$card"
              data-testid="user-detail-header"
              style={{
                backgroundColor: 'var(--lx-card)',
                border: '1px solid var(--lx-border)',
              }}
            >
              {/* Avatar */}
              <Stack
                width={64}
                height={64}
                borderRadius="$pill"
                alignItems="center"
                justifyContent="center"
                flexShrink={0}
                style={{
                  backgroundColor: 'rgba(79,70,229,0.18)',
                  border: '2px solid rgba(79,70,229,0.35)',
                  overflow: 'hidden',
                }}
              >
                {profile.avatarUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={profile.avatarUrl}
                    alt={profile.fullName}
                    width={64}
                    height={64}
                    style={{ objectFit: 'cover', width: '100%', height: '100%' }}
                    onError={(e) => {
                      // Fallback to initials on load error
                      (e.currentTarget as HTMLImageElement).style.display = 'none';
                    }}
                  />
                ) : null}
                {!profile.avatarUrl && (
                  <Text fontFamily="$heading" fontSize={24} fontWeight="700" color="$primary">
                    {profile.fullName.trim().charAt(0).toUpperCase() || '?'}
                  </Text>
                )}
              </Stack>

              {/* Identity block */}
              <Stack flex={1} flexDirection="column" gap="$2">
                <Text fontFamily="$heading" fontSize={22} fontWeight="700" color="$fg1">
                  {profile.fullName}
                </Text>
                <Text
                  fontFamily="$body"
                  fontSize={14}
                  color="$fg3"
                  dir="ltr"
                  style={{ fontFamily: 'var(--lx-font-mono, monospace)' }}
                >
                  {profile.email}
                </Text>

                {/* Badges row */}
                <Stack flexDirection="row" gap="$2" flexWrap="wrap" marginTop="$1">
                  <span data-testid="user-detail-role-badge">
                    <StatusBadge variant="role" value={profile.role} strings={strings} />
                  </span>
                  <span data-testid="user-detail-status-badge">
                    <StatusBadge variant="status" value={profile.accountStatus} strings={strings} />
                  </span>
                </Stack>

                {/* Status reason — only when present */}
                {profile.lastStatusReason ? (
                  <Text
                    fontFamily="$body"
                    fontSize={12}
                    color="$fg3"
                    style={{ fontStyle: 'italic' }}
                  >
                    {strings.userDetailStatusReason}: {profile.lastStatusReason}
                    {profile.statusChangedAtUtc
                      ? ` — ${formatDate(profile.statusChangedAtUtc)}`
                      : null}
                  </Text>
                ) : null}
              </Stack>

              {/* P7-07 LifecycleActionsMenu — wired to pre-cut header seam.
                  DO NOT touch the P7-08 child-edit slot below this section. */}
              <Stack marginInlineStart="auto" alignItems="flex-end" gap="$2">
                {profile.accountStatus === ACCOUNT_STATUS.Deleted ? (
                  // Terminal state — no actions available
                  <Stack
                    flexDirection="row"
                    alignItems="center"
                    gap="$2"
                    padding="$2"
                    paddingHorizontal="$3"
                    borderRadius="$sm"
                    data-testid="lifecycle-terminal-notice"
                    style={{
                      backgroundColor: 'rgba(239,68,68,0.08)',
                      border: '1px solid rgba(239,68,68,0.15)',
                    }}
                  >
                    <span style={{ color: '#EF4444', display: 'inline-flex' }}>
                      <LockIcon />
                    </span>
                    <Text fontFamily="$body" fontSize={12} color="$fg3">
                      {strings.lifecycleDeletedTerminalNotice}
                    </Text>
                  </Stack>
                ) : (
                  <Stack flexDirection="row" gap="$3" alignItems="center">
                    {/* Suspend button — Active only */}
                    {profile.accountStatus === ACCOUNT_STATUS.Active && (
                      <button
                        type="button"
                        data-testid="lifecycle-suspend-btn"
                        onClick={() => setSuspendOpen(true)}
                        aria-label={`${strings.lifecycleSuspendButton} — ${profile.fullName}`}
                        style={{
                          height: 36,
                          paddingInline: 16,
                          borderRadius: 'var(--lx-radius-button)',
                          border: '1px solid rgba(255,255,255,0.16)',
                          backgroundColor: 'transparent',
                          color: 'var(--lx-fg2)',
                          fontSize: 14,
                          fontWeight: 500,
                          cursor: 'pointer',
                          fontFamily: 'inherit',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 6,
                          transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                        }}
                        onMouseEnter={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'var(--lx-card-soft)';
                        }}
                        onMouseLeave={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
                        }}
                        onMouseDown={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
                        }}
                        onMouseUp={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.transform = '';
                        }}
                      >
                        <PauseCircleIcon />
                        {strings.lifecycleSuspendButton}
                      </button>
                    )}

                    {/* Reactivate button — Suspended only */}
                    {profile.accountStatus === ACCOUNT_STATUS.Suspended && (
                      <button
                        type="button"
                        data-testid="lifecycle-reactivate-btn"
                        onClick={() => setReactivateOpen(true)}
                        aria-label={`${strings.lifecycleReactivateButton} — ${profile.fullName}`}
                        style={{
                          height: 36,
                          paddingInline: 16,
                          borderRadius: 'var(--lx-radius-button)',
                          border: '1px solid rgba(34,197,94,0.3)',
                          backgroundColor: 'transparent',
                          color: '#22C55E',
                          fontSize: 14,
                          fontWeight: 500,
                          cursor: 'pointer',
                          fontFamily: 'inherit',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 6,
                          transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                        }}
                        onMouseEnter={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(34,197,94,0.08)';
                        }}
                        onMouseLeave={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
                        }}
                        onMouseDown={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
                        }}
                        onMouseUp={(e) => {
                          (e.currentTarget as HTMLButtonElement).style.transform = '';
                        }}
                      >
                        <PlayCircleIcon />
                        {strings.lifecycleReactivateButton}
                      </button>
                    )}

                    {/* Delete button — Active and Suspended (danger styling) */}
                    <button
                      type="button"
                      data-testid="lifecycle-delete-btn"
                      onClick={() => setDeleteOpen(true)}
                      aria-label={`${strings.lifecycleDeleteButton} — ${profile.fullName}`}
                      style={{
                        height: 36,
                        paddingInline: 16,
                        borderRadius: 'var(--lx-radius-button)',
                        border: '1px solid rgba(239,68,68,0.3)',
                        backgroundColor: 'transparent',
                        color: '#EF4444',
                        fontSize: 14,
                        fontWeight: 500,
                        cursor: 'pointer',
                        fontFamily: 'inherit',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: 6,
                        transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                      }}
                      onMouseEnter={(e) => {
                        (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(239,68,68,0.08)';
                        (e.currentTarget as HTMLButtonElement).style.borderColor = '#EF4444';
                      }}
                      onMouseLeave={(e) => {
                        (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
                        (e.currentTarget as HTMLButtonElement).style.borderColor = 'rgba(239,68,68,0.3)';
                      }}
                      onMouseDown={(e) => {
                        (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
                      }}
                      onMouseUp={(e) => {
                        (e.currentTarget as HTMLButtonElement).style.transform = '';
                      }}
                    >
                      <TrashIcon />
                      {strings.lifecycleDeleteButton}
                    </button>
                  </Stack>
                )}
              </Stack>
            </Stack>

            {/* ── Two-column body ────────────────────────────────────────────── */}
            <Stack
              flexDirection="row"
              gap="$6"
              alignItems="flex-start"
              $sm={{ flexDirection: 'column' }}
            >
              {/* Primary column */}
              <Stack flex={2} minWidth={0} flexDirection="column" gap="$6">
                {/* Profile fields card */}
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
                  <Text
                    fontFamily="$body"
                    fontSize={14}
                    fontWeight="600"
                    color="$fg3"
                    style={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}
                  >
                    {strings.userDetailProfileCard}
                  </Text>

                  {/* Standard fields grid */}
                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    <FieldRow label={strings.usersColName}>
                      <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                        {profile.fullName}
                      </Text>
                    </FieldRow>

                    <FieldRow label={strings.usersColEmail}>
                      <Text
                        fontFamily="$body"
                        fontSize={14}
                        fontWeight="500"
                        color="$fg1"
                        dir="ltr"
                        style={{ fontFamily: 'var(--lx-font-mono, monospace)' }}
                      >
                        {profile.email}
                      </Text>
                    </FieldRow>

                    <FieldRow label={strings.userDetailCreatedAt}>
                      <Text
                        fontFamily="$body"
                        fontSize={14}
                        fontWeight="500"
                        color="$fg1"
                        dir="ltr"
                      >
                        {formatDate(profile.createdAt)}
                      </Text>
                    </FieldRow>

                    <FieldRow label={strings.userDetailSignInLabel}>
                      <Text
                        fontFamily="$body"
                        fontSize={14}
                        fontWeight="500"
                        color="$fg3"
                        style={{ fontStyle: 'italic' }}
                      >
                        {strings.userDetailSignInNotTracked}
                      </Text>
                    </FieldRow>

                    <FieldRow label={strings.userDetailStatusLabel}>
                      <StatusBadge
                        variant="status"
                        value={profile.accountStatus}
                        strings={strings}
                      />
                    </FieldRow>

                    <FieldRow label={strings.userDetailStatusReason}>
                      <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                        {profile.lastStatusReason ?? '—'}
                      </Text>
                    </FieldRow>

                    <FieldRow label={strings.userDetailStatusChangedAt}>
                      <Text
                        fontFamily="$body"
                        fontSize={14}
                        fontWeight="500"
                        color="$fg1"
                        dir="ltr"
                      >
                        {formatDate(profile.statusChangedAtUtc)}
                      </Text>
                    </FieldRow>
                  </div>

                  {/* Child-only section — only for Student role accounts */}
                  {isStudent && (
                    <Stack
                      flexDirection="column"
                      gap="$4"
                      paddingTop="$4"
                      marginTop="$4"
                      style={{ borderTop: '1px solid var(--lx-border)' }}
                    >
                      <Text
                        fontFamily="$body"
                        fontSize={12}
                        fontWeight="600"
                        color="$primary"
                        style={{
                          textTransform: 'uppercase',
                          letterSpacing: '0.06em',
                        }}
                      >
                        {strings.userDetailStudentDetails}
                      </Text>

                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                        <FieldRow label={strings.userDetailGrade}>
                          <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                            {profile.grade != null
                              ? `${strings.userDetailGradePrefix} ${profile.grade}`
                              : '—'}
                          </Text>
                        </FieldRow>

                        <FieldRow label={strings.userDetailCountry}>
                          <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                            {profile.nationality ?? '—'}
                          </Text>
                        </FieldRow>
                      </div>

                      {/* ── Language row A: Preferred / Display language (INDIGO) ── */}
                      {/* CRITICAL: these two rows must NEVER be merged — P7-06 AC 4 */}
                      <Stack
                        flexDirection="column"
                        gap="$1"
                        padding="$3"
                        borderRadius="$sm"
                        data-testid="user-detail-lang-preferred"
                        style={{
                          backgroundColor: 'rgba(79,70,229,0.08)',
                          border: '1px solid rgba(79,70,229,0.15)',
                          // Full-width in grid context (spans both columns)
                          gridColumn: '1 / -1',
                        }}
                      >
                        <Text
                          fontFamily="$body"
                          fontSize={11}
                          fontWeight="600"
                          color="$primary"
                          style={{
                            textTransform: 'uppercase',
                            letterSpacing: '0.04em',
                            marginBottom: 4,
                          }}
                        >
                          {strings.userDetailPreferredLanguageLabel}
                        </Text>
                        <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
                          {formatLanguageName(profile.preferredLanguage)}
                        </Text>
                        <Text fontFamily="$body" fontSize={12} color="$fg3" style={{ marginTop: 2 }}>
                          {strings.userDetailPreferredLanguageHint}
                        </Text>
                      </Stack>

                      {/* ── Language row B: Learning language (PURPLE) ─────────── */}
                      {/* DISTINCT from row A — Math & Science medium of instruction */}
                      <Stack
                        flexDirection="column"
                        gap="$1"
                        padding="$3"
                        borderRadius="$sm"
                        data-testid="user-detail-lang-learning"
                        style={{
                          backgroundColor: 'rgba(168,85,247,0.08)',
                          border: '1px solid rgba(168,85,247,0.15)',
                          gridColumn: '1 / -1',
                        }}
                      >
                        <Text
                          fontFamily="$body"
                          fontSize={11}
                          fontWeight="600"
                          style={{
                            color: '#A855F7',
                            textTransform: 'uppercase',
                            letterSpacing: '0.04em',
                            marginBottom: 4,
                          }}
                        >
                          {strings.userDetailLearningLanguageLabel}
                        </Text>
                        <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
                          {formatLearningLanguage(profile.learningLanguage, strings)}
                        </Text>
                        <Text fontFamily="$body" fontSize={12} color="$fg3" style={{ marginTop: 2 }}>
                          {strings.userDetailLearningLanguageHint}
                        </Text>
                      </Stack>

                      {/* P7-08 child-edit slot — Edit Profile entry (Batch D owns this region) */}
                      <Stack flexDirection="row" marginTop="$2">
                        <Link
                          href={`/users/${userId}/edit`}
                          data-testid="child-edit-entry-btn"
                          style={{
                            height: 36,
                            paddingInline: 14,
                            borderRadius: 'var(--lx-radius-button)',
                            border: '1px solid var(--lx-border)',
                            backgroundColor: 'transparent',
                            color: 'var(--lx-fg2)',
                            fontSize: 13,
                            fontWeight: 500,
                            cursor: 'pointer',
                            fontFamily: 'inherit',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 6,
                            textDecoration: 'none',
                            transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                          }}
                          onMouseEnter={(e) => {
                            (e.currentTarget as HTMLAnchorElement).style.backgroundColor =
                              'var(--lx-card-soft)';
                          }}
                          onMouseLeave={(e) => {
                            (e.currentTarget as HTMLAnchorElement).style.backgroundColor =
                              'transparent';
                          }}
                        >
                          <PencilIcon />
                          {strings.childEditEditProfileButton}
                        </Link>
                      </Stack>
                    </Stack>
                  )}
                </Stack>

                {/* Family panel — loads independently */}
                <div data-testid="user-family-panel">
                  <UserFamilyPanel userId={userId} />
                </div>

                {/* Activity panel — loads independently */}
                <div data-testid="user-activity-panel">
                  <UserActivityPanel userId={userId} />
                </div>
              </Stack>

              {/* Secondary column — Actions card */}
              <Stack
                flex={1}
                flexDirection="column"
                gap="$4"
                style={{
                  minWidth: 220,
                  maxWidth: 280,
                }}
                $sm={{ minWidth: 0, maxWidth: 'unset', width: '100%' } as never}
              >
                <Stack
                  flexDirection="column"
                  gap="$3"
                  padding="$4"
                  borderRadius="$card"
                  style={{
                    backgroundColor: 'var(--lx-card)',
                    border: '1px solid var(--lx-border)',
                  }}
                >
                  <Text
                    fontFamily="$body"
                    fontSize={12}
                    fontWeight="600"
                    color="$fg3"
                    style={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}
                  >
                    {strings.lifecycleActionsHeading}
                  </Text>

                  {/* P7-07 actions (secondary card — mirrors the header menu for discoverability) */}
                  {profile.accountStatus === ACCOUNT_STATUS.Deleted ? (
                    <Text fontFamily="$body" fontSize={13} color="$fg3" style={{ fontStyle: 'italic' }}>
                      {strings.lifecycleDeletedTerminalNotice}
                    </Text>
                  ) : (
                    <Stack flexDirection="column" gap="$2">
                      {profile.accountStatus === ACCOUNT_STATUS.Active && (
                        <button
                          type="button"
                          onClick={() => setSuspendOpen(true)}
                          style={{
                            height: 36,
                            width: '100%',
                            borderRadius: 'var(--lx-radius-button)',
                            border: '1px solid rgba(255,255,255,0.08)',
                            backgroundColor: 'transparent',
                            color: 'var(--lx-fg2)',
                            fontSize: 13,
                            cursor: 'pointer',
                            fontFamily: 'inherit',
                            textAlign: 'start',
                            paddingInline: 12,
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 8,
                          }}
                        >
                          <PauseCircleIcon />
                          {strings.lifecycleSuspendButton}
                        </button>
                      )}
                      {profile.accountStatus === ACCOUNT_STATUS.Suspended && (
                        <button
                          type="button"
                          onClick={() => setReactivateOpen(true)}
                          style={{
                            height: 36,
                            width: '100%',
                            borderRadius: 'var(--lx-radius-button)',
                            border: '1px solid rgba(34,197,94,0.2)',
                            backgroundColor: 'transparent',
                            color: '#22C55E',
                            fontSize: 13,
                            cursor: 'pointer',
                            fontFamily: 'inherit',
                            textAlign: 'start',
                            paddingInline: 12,
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: 8,
                          }}
                        >
                          <PlayCircleIcon />
                          {strings.lifecycleReactivateButton}
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => setDeleteOpen(true)}
                        style={{
                          height: 36,
                          width: '100%',
                          borderRadius: 'var(--lx-radius-button)',
                          border: '1px solid rgba(239,68,68,0.2)',
                          backgroundColor: 'transparent',
                          color: '#EF4444',
                          fontSize: 13,
                          cursor: 'pointer',
                          fontFamily: 'inherit',
                          textAlign: 'start',
                          paddingInline: 12,
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: 8,
                        }}
                      >
                        <TrashIcon />
                        {strings.lifecycleDeleteButton}
                      </button>
                    </Stack>
                  )}

                  {/* P7-08 child-edit slot — DO NOT TOUCH (Batch D owns this region) */}
                </Stack>
              </Stack>
            </Stack>
            {/* ── P7-07 Lifecycle dialogs ────────────────────────────── */}
            {/* Mounted at root of the loaded-profile block so they have   */}
            {/* access to profile data (fullName, email, role).            */}
            {/* Suspend dialog */}
            <SuspendUserDialog
              open={suspendOpen}
              userId={userId}
              userName={profile.fullName}
              onClose={() => setSuspendOpen(false)}
              onSuccess={() => {
                setSuspendOpen(false);
                setSuccessBanner({
                  message: `${profile.fullName} — ${strings.lifecycleSuccessSuspended}`,
                  variant: 'warning',
                });
                setTimeout(() => setSuccessBanner(null), 5000);
              }}
            />

            {/* Reactivate dialog */}
            <ReactivateUserDialog
              open={reactivateOpen}
              userId={userId}
              userName={profile.fullName}
              lastStatusReason={profile.lastStatusReason}
              statusChangedAtUtc={profile.statusChangedAtUtc}
              onClose={() => setReactivateOpen(false)}
              onSuccess={() => {
                setReactivateOpen(false);
                setSuccessBanner({
                  message: `${profile.fullName} — ${strings.lifecycleSuccessReactivated}`,
                  variant: 'success',
                });
                setTimeout(() => setSuccessBanner(null), 5000);
              }}
            />

            {/* Delete dialog */}
            <DeleteUserDialog
              open={deleteOpen}
              userId={userId}
              userName={profile.fullName}
              userEmail={profile.email}
              isParent={isParent}
              onClose={() => setDeleteOpen(false)}
              onSuccess={() => {
                setDeleteOpen(false);
                setSuccessBanner({
                  message: `${profile.fullName} — ${strings.lifecycleSuccessDeleted}`,
                  variant: 'error',
                });
                // No auto-dismiss for deletion — it's a terminal state
              }}
            />
          </>
        )}
      </Stack>
    </AdminShell>
  );
}
