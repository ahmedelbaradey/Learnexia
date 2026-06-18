'use client';

/**
 * SuspendUserDialog — Suspend dialog for P7-07 (Batch C).
 *
 * Uses AdminConfirmDialog with variant="suspend" (pause-circle, amber).
 * Requires a non-empty reason (≤ 500 chars) before the Confirm button is armed.
 *
 * Copy constraints:
 *   - Governance notice must distinguish this from the automatic P1-13 failed-
 *     login lockout.
 *   - Must NOT claim instant full logout — Suspend revokes refresh tokens and
 *     blocks sign-in going forward, but an already-issued access JWT survives
 *     until natural expiry (G2 residual window).
 *
 * Props:
 *   userId    — numeric ID of the account to suspend.
 *   userName  — display name (used in aria-labels and success copy).
 *   onClose   — called when the dialog should close (cancel OR success).
 *
 * Design Spec Part D §D.2.
 * Backend contract: POST /api/Admin/Users/{id}/suspend, body { reason }.
 * Returns BaseResponse<string> — a message string; status is refetched via invalidation.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useSuspendUser } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from './AdminConfirmDialog';
import { ReasonField } from './ReasonField';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface SuspendUserDialogProps {
  open: boolean;
  userId: number;
  userName: string;
  /** Called on cancel (ESC, Cancel button). Does NOT fire on success. */
  onClose: () => void;
  /** Called after a successful suspend mutation. The caller shows the banner. */
  onSuccess?: () => void;
}

// ── Governance notice icon (shield-alert, 16px) ────────────────────────────────

function ShieldAlertIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <line x1="12" y1="8" x2="12" y2="12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="16" r="1" fill="currentColor" />
    </svg>
  );
}

// ── Map API errors to admin copy strings ──────────────────────────────────────

function mapSuspendError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 400) {
      const msg = err.message.toLowerCase();
      if (msg.includes('suspended')) return strings.lifecycleErrorAlreadySuspended;
      if (msg.includes('deleted')) return strings.lifecycleErrorAlreadyDeleted;
      return strings.lifecycleErrorProtected;
    }
    if (err.status === 422) return strings.lifecycleErrorValidation;
    if (err.status === 404) return strings.lifecycleErrorProtected;
  }
  return strings.lifecycleErrorNetwork;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function SuspendUserDialog({ open, userId, userName, onClose, onSuccess }: SuspendUserDialogProps) {
  const [reason, setReason] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { mutate: suspendUser, isPending } = useSuspendUser();

  const canSubmit = reason.trim().length > 0 && reason.length <= 500;

  const handleClose = useCallback(() => {
    setReason('');
    setErrorMessage(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    if (!canSubmit || isPending) return;
    setErrorMessage(null);

    suspendUser(
      { id: userId, reason: reason.trim() },
      {
        onSuccess: () => {
          setReason('');
          // Close the dialog, then notify the parent of success.
          onClose();
          onSuccess?.();
        },
        onError: (err) => {
          setErrorMessage(mapSuspendError(err));
        },
      },
    );
  }, [canSubmit, isPending, suspendUser, userId, reason, onClose, onSuccess]);

  // Confirm button — amber styling per Design Spec D.2
  const confirmButton = (
    <button
      type="button"
      onClick={handleConfirm}
      disabled={isPending}
      aria-disabled={!canSubmit || isPending}
      aria-label={`${strings.lifecycleSuspendConfirm} ${userName}`}
      style={{
        height: 44,
        paddingInline: 20,
        borderRadius: 'var(--lx-radius-button)',
        border: 'none',
        backgroundColor: canSubmit && !isPending ? '#F59E0B' : 'rgba(245,158,11,0.4)',
        color: '#0F172A',
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
          (e.currentTarget as HTMLButtonElement).style.filter = 'brightness(1.08)';
        }
      }}
      onMouseLeave={(e) => {
        (e.currentTarget as HTMLButtonElement).style.filter = '';
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
      {isPending ? (
        <SpinnerIcon color="#0F172A" />
      ) : (
        strings.lifecycleSuspendConfirm
      )}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant="suspend"
      title={strings.lifecycleSuspendTitle}
      subtitle={strings.lifecycleSuspendSubtitle}
      cancelLabel={ADMIN_LOCALE === 'ar' ? 'إلغاء' : 'Cancel'}
      confirmButton={confirmButton}
    >
      {/* Governance notice */}
      <Stack
        flexDirection="row"
        gap="$2"
        alignItems="flex-start"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(245,158,11,0.10)',
          border: '1px solid rgba(245,158,11,0.2)',
        }}
      >
        <span
          aria-hidden="true"
          style={{ color: '#F59E0B', flexShrink: 0, marginTop: 2, display: 'inline-flex' }}
        >
          <ShieldAlertIcon />
        </span>
        <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
          {strings.lifecycleSuspendNotice}
        </Text>
      </Stack>

      {/* Reason field — required */}
      <ReasonField
        id="suspend-reason"
        label={strings.lifecycleSuspendReasonLabel}
        required
        value={reason}
        onChange={setReason}
        maxLength={500}
        error={errorMessage && errorMessage === strings.lifecycleErrorValidation ? errorMessage : undefined}
      />

      {/* Envelope error banner — stays open on error so the admin can retry or cancel */}
      {errorMessage && errorMessage !== strings.lifecycleErrorValidation && (
        <AdminErrorBanner variant="error" message={errorMessage} />
      )}
    </AdminConfirmDialog>
  );
}

// ── Spinner ───────────────────────────────────────────────────────────────────

function SpinnerIcon({ color = 'currentColor' }: { color?: string }) {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      style={{ animation: 'lx-spin 700ms linear infinite' }}
    >
      <circle cx="12" cy="12" r="10" stroke={color} strokeWidth="2" opacity="0.25" />
      <path d="M12 2a10 10 0 0 1 10 10" stroke={color} strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}
