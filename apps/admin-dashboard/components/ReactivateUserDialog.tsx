'use client';

/**
 * ReactivateUserDialog — Reactivate dialog for P7-07 (Batch C).
 *
 * Uses AdminConfirmDialog with variant="reactivate" (play-circle, green).
 * Reason is OPTIONAL — the Confirm button is always enabled once the dialog opens.
 * Shows prior suspension history (lastStatusReason + statusChangedAtUtc) when present.
 *
 * Props:
 *   userId             — numeric ID of the account to reactivate.
 *   userName           — display name.
 *   lastStatusReason   — prior suspension reason (shown in the prior-reason block).
 *   statusChangedAtUtc — ISO string of when the account was suspended.
 *   onClose            — called when the dialog should close.
 *
 * Design Spec Part D §D.3.
 * Backend contract: POST /api/Admin/Users/{id}/reactivate, body { reason? }.
 * Returns BaseResponse<string> — a message string; status is refetched via invalidation.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useReactivateUser } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from './AdminConfirmDialog';
import { ReasonField } from './ReasonField';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface ReactivateUserDialogProps {
  open: boolean;
  userId: number;
  userName: string;
  lastStatusReason: string | null;
  statusChangedAtUtc: string | null;
  /** Called on cancel. Does NOT fire on success. */
  onClose: () => void;
  /** Called after a successful reactivate mutation. The caller shows the banner. */
  onSuccess?: () => void;
}

// ── Date formatter (reused from detail page pattern) ──────────────────────────

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

// ── Error mapper ──────────────────────────────────────────────────────────────

function mapReactivateError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 400) {
      const msg = err.message.toLowerCase();
      if (msg.includes('active')) return strings.lifecycleErrorAlreadyActive;
      if (msg.includes('deleted')) return strings.lifecycleErrorAlreadyDeleted;
      return strings.lifecycleErrorProtected;
    }
    if (err.status === 422) return strings.lifecycleErrorValidation;
    if (err.status === 404) return strings.lifecycleErrorProtected;
  }
  return strings.lifecycleErrorNetwork;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function ReactivateUserDialog({
  open,
  userId,
  userName,
  lastStatusReason,
  statusChangedAtUtc,
  onClose,
  onSuccess,
}: ReactivateUserDialogProps) {
  const [reason, setReason] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { mutate: reactivateUser, isPending } = useReactivateUser();

  const handleClose = useCallback(() => {
    setReason('');
    setErrorMessage(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    if (isPending) return;
    setErrorMessage(null);

    reactivateUser(
      { id: userId, reason: reason.trim() || null },
      {
        onSuccess: () => {
          setReason('');
          onClose();
          onSuccess?.();
        },
        onError: (err) => {
          setErrorMessage(mapReactivateError(err));
        },
      },
    );
  }, [isPending, reactivateUser, userId, reason, onClose, onSuccess]);

  // Confirm button — standard indigo primary
  const confirmButton = (
    <button
      type="button"
      onClick={handleConfirm}
      disabled={isPending}
      aria-label={`${strings.lifecycleReactivateConfirm} ${userName}`}
      style={{
        height: 44,
        paddingInline: 20,
        borderRadius: 'var(--lx-radius-button)',
        border: 'none',
        backgroundColor: isPending ? 'rgba(79,70,229,0.4)' : '#4F46E5',
        color: '#F8FAFC',
        fontSize: 14,
        fontWeight: 600,
        cursor: isPending ? 'default' : 'pointer',
        fontFamily: 'inherit',
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out), transform 80ms',
      }}
      onMouseEnter={(e) => {
        if (!isPending) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1';
        }
      }}
      onMouseLeave={(e) => {
        if (!isPending) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5';
        }
      }}
      onMouseDown={(e) => {
        if (!isPending) {
          (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)';
        }
      }}
      onMouseUp={(e) => {
        (e.currentTarget as HTMLButtonElement).style.transform = '';
      }}
    >
      {isPending ? <SpinnerIcon /> : strings.lifecycleReactivateConfirm}
    </button>
  );

  const cancelLabel = ADMIN_LOCALE === 'ar' ? 'إلغاء' : 'Cancel';

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant="reactivate"
      title={strings.lifecycleReactivateTitle}
      subtitle={strings.lifecycleReactivateSubtitle}
      cancelLabel={cancelLabel}
      confirmButton={confirmButton}
    >
      {/* Prior suspension reason — shows only when present */}
      {lastStatusReason ? (
        <Stack
          flexDirection="column"
          gap="$1"
          padding="$3"
          borderRadius="$sm"
          style={{
            backgroundColor: 'rgba(255,255,255,0.04)',
            border: '1px solid var(--lx-border)',
          }}
        >
          <Text
            fontFamily="$body"
            fontSize={11}
            fontWeight="600"
            color="$fg3"
            style={{ textTransform: 'uppercase', letterSpacing: '0.04em' }}
          >
            {strings.lifecycleReactivatePriorLabel}
          </Text>
          <Text fontFamily="$body" fontSize={14} color="$fg2" style={{ lineHeight: 1.5, fontStyle: 'italic' }}>
            {lastStatusReason}
          </Text>
          {statusChangedAtUtc && (
            <Text fontFamily="$body" fontSize={12} color="$fg3">
              {strings.lifecycleReactivateSuspendedOn} {formatDate(statusChangedAtUtc)}
            </Text>
          )}
        </Stack>
      ) : null}

      {/* Confirmation notice */}
      <Text fontFamily="$body" fontSize={14} color="$fg2" style={{ lineHeight: 1.5 }}>
        {strings.lifecycleReactivateNotice}
      </Text>

      {/* Reason field — optional */}
      <ReasonField
        id="reactivate-reason"
        label={strings.lifecycleReactivateReasonLabel}
        required={false}
        value={reason}
        onChange={setReason}
        maxLength={500}
      />

      {/* Envelope error */}
      {errorMessage && (
        <AdminErrorBanner variant="error" message={errorMessage} />
      )}
    </AdminConfirmDialog>
  );
}

// ── Spinner ───────────────────────────────────────────────────────────────────

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
