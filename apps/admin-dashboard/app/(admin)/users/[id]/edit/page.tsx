'use client';

// Force dynamic rendering — admin data is never static.
export const dynamic = 'force-dynamic';

/**
 * Child Profile Edit Page — `/users/[id]/edit` (P7-08-FE-1, FE-2, FE-3, FE-5).
 *
 * Access gate: Student-role ONLY. Non-Student (Parent / Admin) → redirect notice
 * shown; admin is redirected to `/users/[id]` after a brief notice.
 *
 * Three clearly-separated sections per Design Spec F.1:
 *   (a) Harmless profile fields — country + preferredLanguage (Display Language).
 *       "Save Changes" button calls PATCH /api/Admin/Users/{id}/profile.
 *       Only changed fields are sent.
 *
 *   (b) Learning Language — DESTRUCTIVE gateway, clearly separated from (a).
 *       NOT part of the PATCH — clicking "Change Learning Language" opens
 *       ChangeLearningLanguageDialog. "Save Changes" does NOT touch this field.
 *
 *   (c) Grade Override — non-destructive, opens GradeOverrideDialog.
 *       "Save Changes" does NOT touch grade.
 *
 * Security: useAdminGuard() enforces AdminOnly at the client level. The backend
 * is the real gate (class-level [Authorize(AdminOnly)] on every endpoint).
 *
 * Data flow:
 *   - Profile loaded via useAdminUserProfile (already-cached by the detail page).
 *   - Profile PATCH via useUpdateChildProfile.
 *   - Grade override via GradeOverrideDialog → useOverrideChildGrade.
 *   - Learning-language change via ChangeLearningLanguageDialog
 *       → useAdminChangeChildLearningLanguage.
 *   - All mutations refetch via query invalidation; no optimistic updates.
 *
 * Child PII: child identifiers are NOT logged or surfaced in toasts; response
 * DTOs (ids + codes only) are used for success messaging, not raw user objects.
 *
 * Design Spec Part F §F.1–F.2.
 * Acceptance criteria: P7-08 AC 1–10.
 */

import { use, useState, useCallback } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Stack, Text } from '@tamagui/core';

import { useAdminUserProfile, useUpdateChildProfile } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminShell } from '../../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../../components/AdminErrorBanner';
import { AdminLoadingSkeleton } from '../../../../../components/AdminLoadingSkeleton';
import { GradeOverrideDialog } from '../../../../../components/GradeOverrideDialog';
import { ChangeLearningLanguageDialog } from '../../../../../components/ChangeLearningLanguageDialog';
import { getStrings, ADMIN_LOCALE } from '../../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── SVG icons ────────────────────────────────────────────────────────────────────

function ChevronRightIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M9 18l6-6-6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ChevronLeftIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M15 18l-6-6 6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function AlertTriangleIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="12" y1="9" x2="12" y2="13" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="17" r="1" fill="currentColor" />
    </svg>
  );
}

function SpinnerIcon() {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      style={{ animation: 'lx-spin 700ms linear infinite' }}
    >
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" opacity="0.25" />
      <path d="M12 2a10 10 0 0 1 10 10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

// ── Grade display helper ─────────────────────────────────────────────────────────

function getGradeLabel(grade: number | null | undefined): string {
  if (grade == null) return '—';
  const labels: Record<number, string> = {
    1: strings.gradeLabel1,
    2: strings.gradeLabel2,
    3: strings.gradeLabel3,
    4: strings.gradeLabel4,
    5: strings.gradeLabel5,
    6: strings.gradeLabel6,
  };
  return labels[grade] ?? `${strings.userDetailGradePrefix} ${grade}`;
}

// ── Learning language display helper ─────────────────────────────────────────────

function getLangDisplay(code: string | null | undefined): string {
  if (!code) return '—';
  if (code === 'ar') return strings.langArabic;
  if (code === 'en') return strings.langEnglish;
  return code;
}

// ── Error mapper for profile PATCH ──────────────────────────────────────────────

function mapProfilePatchError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 422) return strings.childProfileError422; // unsupported language/country
    if (err.status === 404) return strings.gradeError404;
  }
  return strings.langErrorNetwork;
}

// ── Page component ────────────────────────────────────────────────────────────────

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function ChildEditPage({ params }: PageProps) {
  const { id: idParam } = use(params);
  const userId = parseInt(idParam, 10);
  const router = useRouter();

  // Admin guard is handled by <AdminShell> — no separate useAdminGuard() call needed here.
  const { data: profile, isLoading, isError } = useAdminUserProfile(userId);

  // Track which form fields have been changed from their loaded values
  const [country, setCountry] = useState<string>('');
  const [preferredLanguage, setPreferredLanguage] = useState<string>('');
  const [countryTouched, setCountryTouched] = useState(false);
  const [langTouched, setLangTouched] = useState(false);

  // Dialog open state
  const [gradeDialogOpen, setGradeDialogOpen] = useState(false);
  const [langDialogOpen, setLangDialogOpen] = useState(false);

  // Success / error banner above the form
  const [banner, setBanner] = useState<{
    message: string;
    variant: 'success' | 'warning' | 'error';
  } | null>(null);

  const { mutate: patchProfile, isPending: isSaving } = useUpdateChildProfile();

  // Once profile loads, seed form state (once only — if not yet touched)
  if (profile && !countryTouched && !langTouched) {
    // We do NOT pre-fill — we track only changes from the loaded baseline.
    // The loaded value is the authoritative "current" state for diff detection.
  }

  const isStudent = profile?.roles?.includes('Student') ?? false;

  // Compute what actually changed vs the loaded profile values
  const profileCountry = profile?.nationality ?? '';
  const profileLang = profile?.preferredLanguage ?? '';

  const hasCountryChanged = countryTouched && country !== profileCountry;
  const hasLangChanged = langTouched && preferredLanguage !== profileLang;
  const hasChanges = hasCountryChanged || hasLangChanged;

  const handleSave = useCallback(() => {
    if (!hasChanges || isSaving || !profile) return;
    setBanner(null);

    const body: { country?: string; preferredLanguage?: string } = {};
    if (hasCountryChanged) body.country = country;
    if (hasLangChanged) body.preferredLanguage = preferredLanguage;

    patchProfile(
      { childId: userId, ...body },
      {
        onSuccess: () => {
          // Reset touched flags — profile refetch via invalidation will bring new values
          setCountryTouched(false);
          setLangTouched(false);
          setCountry('');
          setPreferredLanguage('');
          setBanner({ message: strings.childProfileSaveSuccess, variant: 'success' });
          setTimeout(() => setBanner(null), 5000);
        },
        onError: (err) => {
          setBanner({ message: mapProfilePatchError(err), variant: 'error' });
        },
      },
    );
  }, [
    hasChanges,
    isSaving,
    profile,
    hasCountryChanged,
    hasLangChanged,
    country,
    preferredLanguage,
    userId,
    patchProfile,
  ]);

  // Loading profile
  if (isLoading) {
    return (
      <AdminShell title={strings.childEditPageTitle}>
        <AdminLoadingSkeleton label={strings.userDetailLoadingLabel} />
      </AdminShell>
    );
  }

  // Error loading profile
  if (isError || !profile) {
    return (
      <AdminShell title={strings.childEditPageTitle}>
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
      </AdminShell>
    );
  }

  // Role gate: non-Student accounts cannot be edited here
  if (!isStudent) {
    return (
      <AdminShell title={strings.childEditPageTitle}>
        <Stack flexDirection="column" gap="$4">
          <div data-testid="child-edit-not-student-warning">
            <AdminErrorBanner variant="warning" message={strings.childEditNotStudent} />
          </div>
          <button
            type="button"
            onClick={() => router.push(`/users/${userId}`)}
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
      </AdminShell>
    );
  }

  // Current values from the profile (read-only baseline for diff)
  const currentCountry = profile.nationality ?? '';
  const currentPreferredLang = profile.preferredLanguage ?? '';
  const currentLearningLang = (profile.learningLanguage ?? '') as 'ar' | 'en' | '';
  const currentGrade = profile.grade ?? 1;

  // Effective form values (fall back to profile values when not touched)
  const displayCountry = countryTouched ? country : currentCountry;
  const displayLang = langTouched ? preferredLanguage : currentPreferredLang;

  const BreadcrumbSeparator = ADMIN_LOCALE === 'ar' ? <ChevronLeftIcon /> : <ChevronRightIcon />;

  return (
    <AdminShell title={strings.childEditPageTitle}>
      {/* Dialogs — mounted here so they can access profile data */}
      <GradeOverrideDialog
        open={gradeDialogOpen}
        childId={userId}
        childName={profile.fullName}
        currentGrade={currentGrade}
        onClose={() => setGradeDialogOpen(false)}
        onSuccess={(newGrade) => {
          setGradeDialogOpen(false);
          setBanner({
            message: `${strings.gradeDialogSuccess} (${getGradeLabel(newGrade)})`,
            variant: 'success',
          });
          setTimeout(() => setBanner(null), 5000);
        }}
      />

      <ChangeLearningLanguageDialog
        open={langDialogOpen}
        childId={userId}
        childName={profile.fullName}
        currentLanguage={currentLearningLang !== '' ? currentLearningLang : 'ar'}
        onClose={() => setLangDialogOpen(false)}
        onSuccess={(newLang) => {
          setLangDialogOpen(false);
          setBanner({
            message: `${strings.langDialogSuccess} (${getLangDisplay(newLang)})`,
            variant: 'success',
          });
          // No auto-dismiss for language change — it is a significant event
        }}
      />

      <Stack flexDirection="column" gap="$6" style={{ maxWidth: 640 }}>
        {/* Breadcrumb */}
        <Stack
          flexDirection="row"
          alignItems="center"
          gap="$2"
          style={{ marginBottom: 4 }}
        >
          <Link
            href="/users"
            style={{
              fontSize: 14,
              color: 'var(--lx-fg3)',
              textDecoration: 'none',
              transition: 'color var(--lx-dur-fast) var(--lx-ease-out)',
            }}
            onMouseEnter={(e) => {
              (e.currentTarget as HTMLAnchorElement).style.color = 'var(--lx-fg1)';
            }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLAnchorElement).style.color = 'var(--lx-fg3)';
            }}
          >
            {strings.navUsers}
          </Link>
          <span style={{ color: 'var(--lx-fg3)', display: 'inline-flex' }}>
            {BreadcrumbSeparator}
          </span>
          <Link
            href={`/users/${userId}`}
            style={{
              fontSize: 14,
              color: 'var(--lx-fg3)',
              textDecoration: 'none',
              transition: 'color var(--lx-dur-fast) var(--lx-ease-out)',
            }}
            onMouseEnter={(e) => {
              (e.currentTarget as HTMLAnchorElement).style.color = 'var(--lx-fg1)';
            }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLAnchorElement).style.color = 'var(--lx-fg3)';
            }}
          >
            {profile.fullName}
          </Link>
          <span style={{ color: 'var(--lx-fg3)', display: 'inline-flex' }}>
            {BreadcrumbSeparator}
          </span>
          <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
            {strings.childEditBreadcrumbEdit}
          </Text>
        </Stack>

        {/* Banner — success or error above the form */}
        {banner && (
          <div data-testid="child-edit-banner">
            <AdminErrorBanner variant={banner.variant} message={banner.message} />
          </div>
        )}

        {/* Page heading row */}
        <Stack flexDirection="row" alignItems="center" gap="$4">
          {/* Avatar initials chip — 48px */}
          <Stack
            width={48}
            height={48}
            borderRadius="$pill"
            alignItems="center"
            justifyContent="center"
            flexShrink={0}
            style={{
              backgroundColor: 'rgba(79,70,229,0.18)',
              border: '2px solid rgba(79,70,229,0.35)',
            }}
          >
            <Text fontFamily="$heading" fontSize={18} fontWeight="700" color="$primary">
              {profile.fullName.trim().charAt(0).toUpperCase() || '?'}
            </Text>
          </Stack>
          <Stack flexDirection="column" gap="$1">
            <Text fontFamily="$heading" fontSize={20} fontWeight="700" color="$fg1">
              {profile.fullName}
            </Text>
            <Text
              fontFamily="$body"
              fontSize={13}
              color="$fg3"
              dir="ltr"
              style={{ fontFamily: 'var(--lx-font-mono, monospace)' }}
            >
              {profile.email}
            </Text>
          </Stack>
        </Stack>

        {/* Form card */}
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
          {/* ── SECTION A: Harmless profile fields (country + Display Language) ── */}

          {/* Country */}
          <Stack flexDirection="column" gap="$1">
            <label
              htmlFor="edit-country"
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: 'var(--lx-fg3)',
                textTransform: 'uppercase',
                letterSpacing: '0.04em',
                fontFamily: 'inherit',
              }}
            >
              {strings.childEditCountryLabel}
            </label>
            <input
              id="edit-country"
              type="text"
              data-testid="child-edit-country"
              value={displayCountry}
              onChange={(e) => {
                setCountry(e.target.value);
                setCountryTouched(true);
              }}
              maxLength={100}
              aria-describedby="edit-country-hint"
              style={{
                height: 44,
                width: '100%',
                backgroundColor: 'var(--lx-bg)',
                borderRadius: 'var(--lx-radius-sm)',
                border: '1px solid var(--lx-border)',
                paddingInline: 16,
                color: 'var(--lx-fg1)',
                fontSize: 14,
                fontFamily: 'inherit',
                outline: 'none',
                boxSizing: 'border-box',
                transition: 'border-color var(--lx-dur-fast) var(--lx-ease-out), box-shadow var(--lx-dur-fast) var(--lx-ease-out)',
              }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-primary)';
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-border)';
                e.currentTarget.style.boxShadow = 'none';
              }}
            />
          </Stack>

          {/* Display Language (UI) — harmless, part of profile PATCH */}
          <Stack flexDirection="column" gap="$1">
            <label
              htmlFor="edit-display-lang"
              style={{
                fontSize: 12,
                fontWeight: 600,
                color: 'var(--lx-fg3)',
                textTransform: 'uppercase',
                letterSpacing: '0.04em',
                fontFamily: 'inherit',
              }}
            >
              {strings.childEditDisplayLanguageLabel}
            </label>
            <select
              id="edit-display-lang"
              data-testid="child-edit-display-lang"
              value={displayLang}
              onChange={(e) => {
                setPreferredLanguage(e.target.value);
                setLangTouched(true);
              }}
              style={{
                height: 44,
                width: '100%',
                backgroundColor: 'var(--lx-bg)',
                borderRadius: 'var(--lx-radius-sm)',
                border: '1px solid var(--lx-border)',
                color: 'var(--lx-fg2)',
                paddingInline: 12,
                fontSize: 14,
                fontFamily: 'inherit',
                outline: 'none',
                cursor: 'pointer',
                appearance: 'auto',
              }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-primary)';
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-border)';
                e.currentTarget.style.boxShadow = 'none';
              }}
            >
              <option value="ar">{strings.childEditDisplayLanguageOptionAr}</option>
              <option value="en">{strings.childEditDisplayLanguageOptionEn}</option>
            </select>
          </Stack>

          {/* Visual separator */}
          <div style={{ borderTop: '1px solid var(--lx-border)', marginBlock: 4 }} />

          {/* ── SECTION B: Learning Language (DESTRUCTIVE — opens dialog) ──────── */}
          <Stack flexDirection="column" gap="$3">
            <Stack
              flexDirection="row"
              alignItems="center"
              justifyContent="space-between"
              gap="$3"
            >
              {/* Left: label block */}
              <Stack flex={1} flexDirection="column" gap="$1">
                <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
                  {strings.childEditLearningLanguageLabel}
                </Text>
                <Text fontFamily="$body" fontSize={12} color="$fg3">
                  {strings.childEditLearningLanguageSub}
                </Text>
              </Stack>

              {/* Right: current value pill */}
              <Stack
                paddingHorizontal="$3"
                paddingVertical="$1"
                borderRadius="$pill"
                style={{
                  backgroundColor: 'rgba(168,85,247,0.15)',
                  border: '1px solid rgba(168,85,247,0.3)',
                  color: '#A855F7',
                  fontSize: 12,
                  fontWeight: 600,
                  whiteSpace: 'nowrap',
                }}
              >
                <Text fontFamily="$body" fontSize={12} fontWeight="600" style={{ color: '#A855F7' }}>
                  {getLangDisplay(currentLearningLang)}
                </Text>
              </Stack>
            </Stack>

            {/* Destructive action note */}
            <Stack
              flexDirection="row"
              gap="$2"
              alignItems="flex-start"
              padding="$3"
              borderRadius="$sm"
              style={{
                backgroundColor: 'rgba(239,68,68,0.08)',
                border: '1px solid rgba(239,68,68,0.15)',
              }}
            >
              <span
                aria-hidden="true"
                style={{ color: '#EF4444', flexShrink: 0, marginTop: 2, display: 'inline-flex' }}
              >
                <AlertTriangleIcon />
              </span>
              <Text fontFamily="$body" fontSize={12} color="$fg2" style={{ lineHeight: 1.5 }}>
                {strings.childEditLearningLanguageWarning}
              </Text>
            </Stack>

            {/* Change Learning Language button — opens the dialog */}
            <div>
              <button
                type="button"
                data-testid="child-edit-change-lang-btn"
                onClick={() => setLangDialogOpen(true)}
                disabled={!currentLearningLang}
                style={{
                  height: 44,
                  paddingInline: 16,
                  borderRadius: 'var(--lx-radius-button)',
                  border: '1px solid rgba(239,68,68,0.3)',
                  backgroundColor: 'transparent',
                  color: '#EF4444',
                  fontSize: 14,
                  fontWeight: 500,
                  cursor: currentLearningLang ? 'pointer' : 'default',
                  fontFamily: 'inherit',
                  opacity: currentLearningLang ? 1 : 0.5,
                  transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                }}
                onMouseEnter={(e) => {
                  if (currentLearningLang) {
                    (e.currentTarget as HTMLButtonElement).style.backgroundColor =
                      'rgba(239,68,68,0.08)';
                  }
                }}
                onMouseLeave={(e) => {
                  (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
                }}
                onMouseDown={(e) => {
                  if (currentLearningLang) {
                    (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
                  }
                }}
                onMouseUp={(e) => {
                  (e.currentTarget as HTMLButtonElement).style.transform = '';
                }}
              >
                {strings.childEditChangeLearningLanguage}
              </button>
            </div>
          </Stack>

          {/* Visual separator */}
          <div style={{ borderTop: '1px solid var(--lx-border)', marginBlock: 4 }} />

          {/* ── SECTION C: Grade Override (non-destructive — opens dialog) ────── */}
          <Stack flexDirection="column" gap="$3">
            <Stack
              flexDirection="row"
              alignItems="center"
              justifyContent="space-between"
              gap="$3"
            >
              <Stack flex={1} flexDirection="column" gap="$1">
                <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
                  {strings.childEditGradeLabel}
                </Text>
                <Text fontFamily="$body" fontSize={12} color="$fg3">
                  {strings.childEditGradeNote}
                </Text>
              </Stack>

              {/* Current grade pill */}
              <Stack
                paddingHorizontal="$3"
                paddingVertical="$1"
                borderRadius="$pill"
                style={{
                  backgroundColor: 'rgba(79,70,229,0.15)',
                  border: '1px solid rgba(79,70,229,0.25)',
                  whiteSpace: 'nowrap',
                }}
              >
                <Text fontFamily="$body" fontSize={12} fontWeight="600" color="$primary">
                  {getGradeLabel(currentGrade)}
                </Text>
              </Stack>
            </Stack>

            <div>
              <button
                type="button"
                data-testid="child-edit-override-grade-btn"
                onClick={() => setGradeDialogOpen(true)}
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
                  transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                }}
                onMouseEnter={(e) => {
                  (e.currentTarget as HTMLButtonElement).style.backgroundColor =
                    'var(--lx-card-soft)';
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
                {strings.childEditOverrideGrade}
              </button>
            </div>
          </Stack>

          {/* ── Form actions row ──────────────────────────────────────────────── */}
          <Stack
            flexDirection="row"
            justifyContent="flex-end"
            alignItems="center"
            gap="$3"
            style={{ marginTop: 8 }}
          >
            {/* Cancel */}
            <Link
              href={`/users/${userId}`}
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
                textDecoration: 'none',
                transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
              }}
              onMouseEnter={(e) => {
                (e.currentTarget as HTMLAnchorElement).style.backgroundColor =
                  'var(--lx-card-soft)';
              }}
              onMouseLeave={(e) => {
                (e.currentTarget as HTMLAnchorElement).style.backgroundColor = 'transparent';
              }}
            >
              {strings.childEditCancel}
            </Link>

            {/* Save Changes — disabled when no changes, or when saving */}
            <button
              type="button"
              data-testid="child-edit-save-btn"
              onClick={handleSave}
              disabled={!hasChanges || isSaving}
              aria-disabled={!hasChanges || isSaving}
              aria-label={`${strings.childEditSaveChanges} — ${profile.fullName}`}
              style={{
                height: 44,
                paddingInline: 20,
                borderRadius: 'var(--lx-radius-button)',
                border: 'none',
                backgroundColor:
                  hasChanges && !isSaving ? '#4F46E5' : 'rgba(79,70,229,0.4)',
                color: '#F8FAFC',
                fontSize: 14,
                fontWeight: 600,
                cursor: hasChanges && !isSaving ? 'pointer' : 'default',
                fontFamily: 'inherit',
                display: 'inline-flex',
                alignItems: 'center',
                gap: 8,
                opacity: hasChanges ? 1 : 0.4,
                transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out), transform 80ms',
              }}
              onMouseEnter={(e) => {
                if (hasChanges && !isSaving) {
                  (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1';
                }
              }}
              onMouseLeave={(e) => {
                if (hasChanges && !isSaving) {
                  (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5';
                }
              }}
              onMouseDown={(e) => {
                if (hasChanges && !isSaving) {
                  (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
                }
              }}
              onMouseUp={(e) => {
                (e.currentTarget as HTMLButtonElement).style.transform = '';
              }}
            >
              {isSaving ? <SpinnerIcon /> : null}
              {strings.childEditSaveChanges}
            </button>
          </Stack>
        </Stack>
      </Stack>
    </AdminShell>
  );
}
