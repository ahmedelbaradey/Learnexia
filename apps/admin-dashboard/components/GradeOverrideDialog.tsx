'use client';

/**
 * GradeOverrideDialog — Grade override dialog for P7-08 (Batch D).
 *
 * Uses AdminConfirmDialog with variant="grade" (graduation-cap, indigo).
 *
 * This is NON-DESTRUCTIVE: XP, level, badges, streaks and mastery are preserved.
 * The curriculum re-scopes to the new grade. Copy must reflect this.
 *
 * Gate: confirm button is aria-disabled until:
 *   1. selectedGrade > 0 AND selectedGrade !== currentGrade
 *   2. reason.trim().length > 0 (FE-enforced per D3 — backend accepts null)
 *
 * Error mapping:
 *   400 same-grade → gradeError400SameGrade (unreachable via UI but mapped defensively)
 *   400 confirm-missing → gradeError400Confirm (unreachable via UI)
 *   422 range-invalid → gradeError422
 *   404 → gradeError404
 *   5xx / network → gradeErrorNetwork
 *
 * Props:
 *   childId       — numeric child ID.
 *   childName     — display name (used in success copy and aria-labels).
 *   currentGrade  — the child's current grade (pre-populated display).
 *   open          — whether the dialog is visible.
 *   onClose       — called on cancel (ESC or Cancel button).
 *   onSuccess     — called after a successful mutation; the parent shows the banner.
 *
 * Design Spec Part F §F.3.
 * Backend contract: POST /api/Admin/Users/{childId}/grade
 *   body: { grade, reason, confirm: true }
 *   response: ChildGradeOverrideResponseDto { childUserId, oldGrade, newGrade }
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useOverrideChildGrade } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from './AdminConfirmDialog';
import { ReasonField } from './ReasonField';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Grade option labels (EN and AR full ordinals per Design Spec Gap 5) ────────

const GRADE_OPTIONS: { value: number; labelEn: string; labelKey: keyof typeof strings }[] = [
  { value: 1, labelEn: strings.gradeLabel1, labelKey: 'gradeLabel1' },
  { value: 2, labelEn: strings.gradeLabel2, labelKey: 'gradeLabel2' },
  { value: 3, labelEn: strings.gradeLabel3, labelKey: 'gradeLabel3' },
  { value: 4, labelEn: strings.gradeLabel4, labelKey: 'gradeLabel4' },
  { value: 5, labelEn: strings.gradeLabel5, labelKey: 'gradeLabel5' },
  { value: 6, labelEn: strings.gradeLabel6, labelKey: 'gradeLabel6' },
];

// ── Error mapper ────────────────────────────────────────────────────────────────

function mapGradeError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 400) {
      const msg = err.message.toLowerCase();
      if (msg.includes('already') || msg.includes('same')) return strings.gradeError400SameGrade;
      return strings.gradeError400Confirm;
    }
    if (err.status === 422) return strings.gradeError422;
    if (err.status === 404) return strings.gradeError404;
  }
  return strings.gradeErrorNetwork;
}

// ── SVG icons ────────────────────────────────────────────────────────────────────

function ArrowDownIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M12 5v14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M19 12l-7 7-7-7" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ShieldCheckIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <polyline points="9,12 11,14 15,10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
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

export interface GradeOverrideDialogProps {
  open: boolean;
  childId: number;
  childName: string;
  currentGrade: number;
  /** Called on cancel (ESC or Cancel button). Does NOT fire on success. */
  onClose: () => void;
  /** Called after a successful grade override. The parent page shows the banner. */
  onSuccess?: (newGrade: number) => void;
}

export function GradeOverrideDialog({
  open,
  childId,
  childName,
  currentGrade,
  onClose,
  onSuccess,
}: GradeOverrideDialogProps) {
  const [selectedGrade, setSelectedGrade] = useState<number>(0);
  const [reason, setReason] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { mutate: overrideGrade, isPending } = useOverrideChildGrade();

  // Gate: grade selected, different from current, reason non-empty
  const canSubmit =
    selectedGrade > 0 &&
    selectedGrade !== currentGrade &&
    reason.trim().length > 0 &&
    reason.length <= 500;

  const handleClose = useCallback(() => {
    setSelectedGrade(0);
    setReason('');
    setErrorMessage(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    if (!canSubmit || isPending) return;
    setErrorMessage(null);

    overrideGrade(
      {
        childId,
        grade: selectedGrade,
        reason: reason.trim(),
        confirm: true,
      },
      {
        onSuccess: (data) => {
          setSelectedGrade(0);
          setReason('');
          onClose();
          onSuccess?.(data.newGrade);
        },
        onError: (err) => {
          setErrorMessage(mapGradeError(err));
        },
      },
    );
  }, [canSubmit, isPending, overrideGrade, childId, selectedGrade, reason, onClose, onSuccess]);

  // Confirm button — indigo primary per Design Spec F.3
  const confirmButton = (
    <button
      type="button"
      data-testid="dialog-confirm-btn"
      onClick={handleConfirm}
      disabled={isPending}
      aria-disabled={!canSubmit || isPending}
      aria-label={`${strings.gradeDialogConfirm} — ${childName}`}
      style={{
        height: 44,
        paddingInline: 20,
        borderRadius: 'var(--lx-radius-button)',
        border: 'none',
        backgroundColor: canSubmit && !isPending ? '#4F46E5' : 'rgba(79,70,229,0.4)',
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
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1';
        }
      }}
      onMouseLeave={(e) => {
        if (canSubmit && !isPending) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5';
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
      {isPending ? <SpinnerIcon /> : strings.gradeDialogConfirm}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant="grade"
      title={strings.gradeDialogTitle}
      subtitle={strings.gradeDialogSubtitle}
      cancelLabel={ADMIN_LOCALE === 'ar' ? strings.childEditCancel : 'Cancel'}
      confirmButton={confirmButton}
      dialogTestId="grade-dialog"
    >
      {/* Current grade display */}
      <Stack
        flexDirection="row"
        alignItems="center"
        justifyContent="space-between"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'var(--lx-bg)',
          border: '1px solid var(--lx-border)',
        }}
      >
        <Text fontFamily="$body" fontSize={12} color="$fg3">
          {strings.gradeDialogCurrentLabel}
        </Text>
        <Text fontFamily="$body" fontSize={16} fontWeight="700" color="$fg1">
          {/* Grade prefix differs by locale — use the label from the options array */}
          {GRADE_OPTIONS.find((o) => o.value === currentGrade)?.labelEn ??
            `${strings.userDetailGradePrefix} ${currentGrade}`}
        </Text>
      </Stack>

      {/* Arrow indicator */}
      <Stack alignItems="center" style={{ color: 'var(--lx-fg3)' }}>
        <ArrowDownIcon />
      </Stack>

      {/* Grade select */}
      <Stack flexDirection="column" gap="$1">
        <label
          htmlFor="grade-select"
          style={{
            fontSize: 12,
            fontWeight: 600,
            color: 'var(--lx-fg3)',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            fontFamily: 'inherit',
          }}
        >
          {strings.gradeDialogNewLabel}
        </label>
        <select
          id="grade-select"
          data-testid="grade-select"
          value={selectedGrade === 0 ? '' : String(selectedGrade)}
          onChange={(e) => {
            setSelectedGrade(e.target.value ? parseInt(e.target.value, 10) : 0);
          }}
          aria-describedby="grade-preserve-notice"
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
          {GRADE_OPTIONS.map(({ value, labelEn }) => (
            <option key={value} value={String(value)}>
              {labelEn}
            </option>
          ))}
        </select>
      </Stack>

      {/* History-preserved notice — shown once a grade is selected */}
      {selectedGrade > 0 && selectedGrade !== currentGrade && (
        <Stack
          id="grade-preserve-notice"
          flexDirection="row"
          gap="$2"
          alignItems="flex-start"
          padding="$3"
          borderRadius="$sm"
          data-testid="grade-preserve-notice"
          style={{
            backgroundColor: 'rgba(34,197,94,0.08)',
            border: '1px solid rgba(34,197,94,0.15)',
          }}
        >
          <span
            aria-hidden="true"
            style={{ color: '#22C55E', flexShrink: 0, marginTop: 2, display: 'inline-flex' }}
          >
            <ShieldCheckIcon />
          </span>
          <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
            {strings.gradeDialogPreserveNotice}
          </Text>
        </Stack>
      )}

      {/* Same-grade notice — when selected grade equals current */}
      {selectedGrade > 0 && selectedGrade === currentGrade && (
        <AdminErrorBanner
          variant="warning"
          message={strings.gradeError400SameGrade}
        />
      )}

      {/* Reason field — required (FE rule per D3) */}
      <ReasonField
        id="grade-reason"
        label={strings.gradeDialogReasonLabel}
        required
        value={reason}
        onChange={setReason}
        maxLength={500}
        error={
          errorMessage === strings.gradeError422 || errorMessage === strings.gradeError400SameGrade
            ? errorMessage
            : undefined
        }
      />

      {/* Envelope error banner — for non-field errors */}
      {errorMessage &&
        errorMessage !== strings.gradeError422 &&
        errorMessage !== strings.gradeError400SameGrade && (
          <AdminErrorBanner variant="error" message={errorMessage} />
        )}
    </AdminConfirmDialog>
  );
}
