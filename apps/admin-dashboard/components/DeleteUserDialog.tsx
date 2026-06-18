'use client';

/**
 * DeleteUserDialog — Delete dialog for P7-07 (Batch C).
 *
 * Two-step gate:
 *   1. Required reason (non-empty, ≤ 500 chars).
 *   2. Typed email confirmation (case-insensitive match against userEmail).
 * Both gates must be satisfied before the destructive Confirm button is armed.
 *
 * Parent-only: when isParent = true, shows a cascade-children checkbox
 * (default: unchecked — delete does NOT cascade unless explicitly checked).
 *
 * Soft-delete semantics (design decision D4):
 *   The account row is retained, learning history preserved, PII NOT yet erased.
 *   Copy must reflect this — never claim "anonymized".
 *
 * The backend DELETE-with-body is handled by ApiClient.delete() which supports
 * a JSON body — confirmed by useUnlinkChild.ts pattern.
 *
 * Props:
 *   userId    — numeric ID.
 *   userName  — display name (aria-labels, success copy).
 *   userEmail — the expected typed confirmation value.
 *   isParent  — controls cascade-children checkbox visibility.
 *   onClose   — called on cancel or success.
 *
 * Design Spec Part D §D.4.
 * Backend contract: DELETE /api/Admin/Users/{id}, body { reason, confirm, cascadeChildren }.
 * 424 (confirm: false) is handled defensively — should not occur in normal flow.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useDeleteUser } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from './AdminConfirmDialog';
import { ReasonField } from './ReasonField';
import { TypedConfirmField } from './TypedConfirmField';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface DeleteUserDialogProps {
  open: boolean;
  userId: number;
  userName: string;
  userEmail: string;
  isParent: boolean;
  /** Called on cancel. Does NOT fire on success. */
  onClose: () => void;
  /** Called after a successful delete mutation. The caller shows the banner. */
  onSuccess?: () => void;
}

// ── Error mapper ──────────────────────────────────────────────────────────────

function mapDeleteError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 400) {
      const msg = err.message.toLowerCase();
      if (msg.includes('deleted')) return strings.lifecycleErrorAlreadyDeleted;
      return strings.lifecycleErrorProtected;
    }
    if (err.status === 422) return strings.lifecycleErrorValidation;
    if (err.status === 424) return strings.lifecycleErrorConfirmMissing;
    if (err.status === 404) return strings.lifecycleErrorProtected;
  }
  return strings.lifecycleErrorNetwork;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function DeleteUserDialog({
  open,
  userId,
  userName,
  userEmail,
  isParent,
  onClose,
  onSuccess,
}: DeleteUserDialogProps) {
  const [reason, setReason] = useState('');
  const [typedEmail, setTypedEmail] = useState('');
  const [cascadeChildren, setCascadeChildren] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const { mutate: deleteUser, isPending } = useDeleteUser();

  // Both gates must be satisfied:
  // 1. Reason non-empty + within limit
  // 2. Typed email matches (case-insensitive)
  const emailMatches = typedEmail.trim().toLowerCase() === userEmail.toLowerCase();
  const reasonValid = reason.trim().length > 0 && reason.length <= 500;
  const canSubmit = reasonValid && emailMatches;

  const handleClose = useCallback(() => {
    setReason('');
    setTypedEmail('');
    setCascadeChildren(false);
    setErrorMessage(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    if (!canSubmit || isPending) return;
    setErrorMessage(null);

    // Always send confirm: true at this point — the UI gate ensures both conditions
    // are met before this function is called.
    deleteUser(
      {
        id: userId,
        reason: reason.trim(),
        confirm: true,
        cascadeChildren: isParent ? cascadeChildren : false,
      },
      {
        onSuccess: () => {
          setReason('');
          setTypedEmail('');
          setCascadeChildren(false);
          onClose();
          onSuccess?.();
        },
        onError: (err) => {
          setErrorMessage(mapDeleteError(err));
        },
      },
    );
  }, [canSubmit, isPending, deleteUser, userId, reason, cascadeChildren, isParent, onClose, onSuccess]);

  // Destructive confirm button — red styling per Design Spec D.4
  const confirmButton = (
    <button
      type="button"
      data-testid="dialog-confirm-btn"
      onClick={handleConfirm}
      disabled={isPending}
      aria-disabled={!canSubmit || isPending}
      aria-label={`${strings.lifecycleDeleteConfirm} ${userName}`}
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
      {isPending ? <SpinnerIcon /> : strings.lifecycleDeleteConfirm}
    </button>
  );

  const cancelLabel = ADMIN_LOCALE === 'ar' ? 'إلغاء' : 'Cancel';

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant="delete"
      title={strings.lifecycleDeleteTitle}
      subtitle={strings.lifecycleDeleteSubtitle}
      cancelLabel={cancelLabel}
      confirmButton={confirmButton}
      dialogTestId="delete-dialog"
    >
      {/* Soft-delete warning notice */}
      <Stack
        flexDirection="column"
        gap="$2"
        padding="$4"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(239,68,68,0.08)',
          border: '1px solid rgba(239,68,68,0.15)',
        }}
      >
        <Text fontFamily="$body" fontSize={14} fontWeight="700" style={{ color: '#EF4444' }}>
          {strings.lifecycleDeleteNoticeHeading}
        </Text>
        <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
          {strings.lifecycleDeleteNoticeBody}
        </Text>
      </Stack>

      {/* Cascade-children checkbox — only for Parent targets */}
      {isParent && (
        <Stack flexDirection="column" gap="$2">
          <div
            style={{
              borderTop: '1px solid var(--lx-border)',
              marginBlock: 4,
            }}
          />
          <label
            style={{
              display: 'flex',
              flexDirection: 'row',
              alignItems: 'flex-start',
              gap: 12,
              cursor: 'pointer',
            }}
          >
            <input
              type="checkbox"
              data-testid="delete-cascade-checkbox"
              checked={cascadeChildren}
              onChange={(e) => setCascadeChildren(e.target.checked)}
              style={{
                width: 18,
                height: 18,
                marginTop: 2,
                accentColor: '#EF4444',
                flexShrink: 0,
              }}
            />
            <Stack flexDirection="column" gap="$1">
              <Text fontFamily="$body" fontSize={14} color="$fg2" fontWeight="500">
                {strings.lifecycleDeleteCascadeLabel}
              </Text>
              <Text fontFamily="$body" fontSize={12} color="$fg3">
                {strings.lifecycleDeleteCascadeWarning}
              </Text>
            </Stack>
          </label>
        </Stack>
      )}

      {/* Reason field — required */}
      <ReasonField
        id="delete-reason"
        label={strings.lifecycleDeleteReasonLabel}
        required
        value={reason}
        onChange={setReason}
        maxLength={500}
        error={
          errorMessage === strings.lifecycleErrorValidation ? errorMessage : undefined
        }
      />

      {/* Typed email confirmation */}
      <TypedConfirmField
        id="delete-typed-confirm"
        instructionText={strings.lifecycleDeleteTypedInstruction}
        targetValue={userEmail}
        value={typedEmail}
        onChange={setTypedEmail}
        placeholder={strings.lifecycleDeleteTypedPlaceholder}
        matchLabel={strings.lifecycleDeleteTypedMatchLabel}
        caseSensitive={false}
      />

      {/* Envelope error */}
      {errorMessage && errorMessage !== strings.lifecycleErrorValidation && (
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
