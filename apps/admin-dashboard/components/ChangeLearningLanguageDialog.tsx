'use client';

/**
 * ChangeLearningLanguageDialog — ADMIN-ONLY learning-language change dialog (P7-08).
 *
 * Uses AdminConfirmDialog with variant="destructive" (alert-triangle, danger-red).
 *
 * This is DESTRUCTIVE: hard-deletes Math and Science lesson attempts.
 * Cannot be undone. Arabic and English subject data are NOT affected.
 *
 * DISTINCT from the parent-app P8-04 dialog (which uses a checkbox ack on a
 * different endpoint). Admin version uses a typed confirmation ("CONFIRM") for
 * stronger friction — admins are professionals acting on someone else's child's
 * account.
 *
 * Gate: confirm button is aria-disabled until BOTH:
 *   1. selectedLanguage is chosen AND differs from currentLanguage
 *   2. typedValue === 'CONFIRM' (case-sensitive — the exact token "CONFIRM")
 *
 * Error mapping:
 *   424 → langError424 (no confirm — defensive; should not occur via UI)
 *   422 → langError422 (unsupported language)
 *   200 same-language no-op → langErrorNoOp (close with info banner)
 *   5xx / network → langErrorNetwork
 *
 * Props:
 *   childId         — numeric child ID.
 *   childName       — display name (aria-labels and success copy).
 *   currentLanguage — the child's current learningLanguage ("ar" | "en").
 *   open            — whether the dialog is visible.
 *   onClose         — called on cancel (ESC or Cancel button).
 *   onSuccess       — called after a successful mutation; the parent shows the banner.
 *
 * Design Spec Part F §F.4.
 * Backend contract: POST /api/Admin/Users/{childId}/learning-language
 *   body: { learningLanguage: "ar"|"en", confirmFreshStart: true }
 *   response: AdminChangedLearningLanguageResponseDto { childId, newLearningLanguage }
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useAdminChangeChildLearningLanguage } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from './AdminConfirmDialog';
import { TypedConfirmField } from './TypedConfirmField';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// The exact typed confirmation token — case-sensitive per Design Spec Gap 6
const CONFIRM_TOKEN = 'CONFIRM';

// ── Language display helpers ───────────────────────────────────────────────────

function getLangDisplayName(code: 'ar' | 'en'): string {
  return code === 'ar' ? strings.langArabic : strings.langEnglish;
}

// ── Error mapper ────────────────────────────────────────────────────────────────

function mapLangError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 424) return strings.langError424;
    if (err.status === 422) return strings.langError422;
    if (err.status === 404) return strings.gradeError404;
  }
  return strings.langErrorNetwork;
}

// ── SVG icons ────────────────────────────────────────────────────────────────────

function XCircleIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
      <path d="M15 9l-6 6M9 9l6 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function CheckCircleSmallIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
      <polyline points="9,12 11,14 15,10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ArrowRightIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M5 12h14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M12 5l7 7-7 7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ArrowLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M19 12H5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M12 5l-7 7 7 7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
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

// ── Component ──────────────────────────────────────────────────────────────────

export interface ChangeLearningLanguageDialogProps {
  open: boolean;
  childId: number;
  childName: string;
  currentLanguage: 'ar' | 'en';
  /** Called on cancel (ESC or Cancel button). Does NOT fire on success. */
  onClose: () => void;
  /** Called after a successful mutation. The parent page shows the success banner. */
  onSuccess?: (newLanguage: string) => void;
}

export function ChangeLearningLanguageDialog({
  open,
  childId,
  childName,
  currentLanguage,
  onClose,
  onSuccess,
}: ChangeLearningLanguageDialogProps) {
  // Use null for "nothing selected" to avoid the '' vs 'ar'|'en' type collision
  const [selectedLanguage, setSelectedLanguage] = useState<'ar' | 'en' | null>(null);
  const [typedValue, setTypedValue] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { mutate: changeLang, isPending } = useAdminChangeChildLearningLanguage();

  // Gate: language chosen (and different from current) AND typed "CONFIRM" exactly
  const langValid = selectedLanguage !== null && selectedLanguage !== currentLanguage;
  const confirmedTyped = typedValue.trim() === CONFIRM_TOKEN;
  const canSubmit = langValid && confirmedTyped;

  const handleClose = useCallback(() => {
    setSelectedLanguage(null);
    setTypedValue('');
    setErrorMessage(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    if (!canSubmit || isPending || selectedLanguage === null) return;
    setErrorMessage(null);

    changeLang(
      {
        childId,
        learningLanguage: selectedLanguage,
        confirmFreshStart: true,
      },
      {
        onSuccess: (data) => {
          setSelectedLanguage(null);
          setTypedValue('');
          onClose();
          onSuccess?.(data.newLearningLanguage);
        },
        onError: (err) => {
          setErrorMessage(mapLangError(err));
        },
      },
    );
  }, [canSubmit, isPending, selectedLanguage, changeLang, childId, onClose, onSuccess]);

  // Destructive confirm button — same red styling as DeleteUserDialog
  const confirmButton = (
    <button
      type="button"
      data-testid="dialog-confirm-btn"
      onClick={handleConfirm}
      disabled={isPending}
      aria-disabled={!canSubmit || isPending}
      aria-label={`${strings.langDialogConfirm} — ${childName}`}
      style={{
        height: 44,
        paddingInline: 20,
        borderRadius: 'var(--lx-radius-button)',
        border: 'none',
        backgroundColor: canSubmit && !isPending ? '#EF4444' : 'rgba(239,68,68,0.4)',
        color: '#F8FAFC',
        fontSize: 14,
        fontWeight: 600,
        cursor: canSubmit && !isPending ? 'pointer' : 'default',
        fontFamily: 'inherit',
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out), transform 80ms',
        opacity: canSubmit ? 1 : 0.4,
      }}
      onMouseEnter={(e) => {
        if (canSubmit && !isPending) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#F87171';
        }
      }}
      onMouseLeave={(e) => {
        if (canSubmit && !isPending) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#EF4444';
        }
      }}
      onMouseDown={(e) => {
        if (canSubmit && !isPending) {
          (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
        }
      }}
      onMouseUp={(e) => {
        (e.currentTarget as HTMLButtonElement).style.transform = '';
      }}
    >
      {isPending ? <SpinnerIcon /> : strings.langDialogConfirm}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant="destructive"
      title={strings.langDialogTitle}
      subtitle={strings.langDialogSubtitle}
      cancelLabel={ADMIN_LOCALE === 'ar' ? strings.childEditCancel : 'Cancel'}
      confirmButton={confirmButton}
      dialogTestId="change-language-dialog"
    >
      {/* Destructive warning block */}
      <Stack
        flexDirection="column"
        gap="$3"
        padding="$4"
        borderRadius="$sm"
        data-testid="lang-loss-block"
        style={{
          backgroundColor: 'rgba(239,68,68,0.08)',
          border: '1px solid rgba(239,68,68,0.25)',
        }}
      >
        <Text fontFamily="$body" fontSize={14} fontWeight="700" style={{ color: '#EF4444' }}>
          {strings.langDialogLossTitle}
        </Text>

        {/* Loss list */}
        <Stack flexDirection="row" gap="$2" alignItems="flex-start">
          <span
            aria-hidden="true"
            style={{ color: '#EF4444', flexShrink: 0, marginTop: 2, display: 'inline-flex' }}
          >
            <XCircleIcon />
          </span>
          <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
            {strings.langDialogLossLine}
          </Text>
        </Stack>

        {/* Kept list */}
        <Stack flexDirection="row" gap="$2" alignItems="flex-start" data-testid="lang-kept-line">
          <span
            aria-hidden="true"
            style={{ color: '#22C55E', flexShrink: 0, marginTop: 2, display: 'inline-flex' }}
          >
            <CheckCircleSmallIcon />
          </span>
          <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
            {strings.langDialogKeptLine}
          </Text>
        </Stack>
      </Stack>

      {/* From → To language display */}
      <Stack
        flexDirection="row"
        alignItems="center"
        gap="$4"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'var(--lx-bg)',
          border: '1px solid var(--lx-border)',
        }}
      >
        <Text
          fontFamily="$body"
          fontSize={14}
          fontWeight="600"
          color="$fg3"
          style={{ textDecoration: 'line-through' }}
        >
          {getLangDisplayName(currentLanguage)}
        </Text>
        <span style={{ color: 'var(--lx-fg3)', display: 'inline-flex' }}>
          {/* Arrow direction is RTL-aware: arrow-right in LTR, arrow-left in RTL */}
          {ADMIN_LOCALE === 'ar' ? <ArrowLeftIcon /> : <ArrowRightIcon />}
        </span>
        <Text fontFamily="$body" fontSize={14} fontWeight="700" color="$fg1">
          {selectedLanguage !== null
            ? getLangDisplayName(selectedLanguage)
            : '—'}
        </Text>
      </Stack>

      {/* Language select */}
      <Stack flexDirection="column" gap="$1">
        <label
          htmlFor="lang-select"
          style={{
            fontSize: 12,
            fontWeight: 600,
            color: 'var(--lx-fg3)',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            fontFamily: 'inherit',
          }}
        >
          {strings.langDialogNewLabel}
          {/* Required marker */}
          <span aria-hidden="true" style={{ color: 'var(--lx-danger)', marginInlineStart: 4 }}>
            *
          </span>
        </label>
        <select
          id="lang-select"
          data-testid="lang-select"
          value={selectedLanguage ?? ''}
          onChange={(e) => setSelectedLanguage((e.target.value || null) as 'ar' | 'en' | null)}
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
          <option value="" disabled>
            {strings.gradeDialogSelectPlaceholder}
          </option>
          {/* Exclude current language — changing to same is a no-op */}
          {currentLanguage !== 'ar' && (
            <option value="ar">{strings.langArabic}</option>
          )}
          {currentLanguage !== 'en' && (
            <option value="en">{strings.langEnglish}</option>
          )}
        </select>
      </Stack>

      {/* Typed confirmation gate — must type "CONFIRM" exactly (case-sensitive) */}
      <TypedConfirmField
        id="lang-typed-confirm"
        instructionText={strings.langDialogTypedInstruction}
        targetValue={CONFIRM_TOKEN}
        value={typedValue}
        onChange={setTypedValue}
        placeholder={strings.langDialogTypedPlaceholder}
        matchLabel={strings.langDialogTypedMatchLabel}
        caseSensitive={true}
      />

      {/* Envelope error banner */}
      {errorMessage && (
        <AdminErrorBanner variant="error" message={errorMessage} />
      )}
    </AdminConfirmDialog>
  );
}
