'use client';

/**
 * ReviewItemDialog — parameterised review dialog for the moderation surface (P7-09).
 *
 * A single dialog parameterised by `decision` ('Approved' | 'Rejected' | 'Flagged').
 * Internally selects the appropriate AdminConfirmDialog variant, title, icon colour,
 * confirm button colour, and ReasonField required/optional state.
 *
 * Mirrors `SuspendUserDialog` in shape:
 *   - Uses AdminConfirmDialog with a moderation variant.
 *   - Uses ReasonField with maxLength={2000} (NOT the default 500).
 *   - Error stays in dialog on failure (no dismiss-on-error).
 *   - NO optimistic update — status comes from refetch after invalidation.
 *
 * Confirm button gating:
 *   - Approved / Flagged: always enabled (reason optional).
 *   - Rejected: disabled until reason.trim().length > 0.
 *
 * Post-success: mutation hook invalidates adminModeration.item(id) + adminModeration.all;
 * parent page refetches and shows a success banner. Dialog closes on success.
 *
 * Design Spec Part D.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useReviewModerationItem } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from './AdminConfirmDialog';
import { ReasonField } from './ReasonField';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import type { DialogVariant } from './AdminConfirmDialog';
import type { ModerationStatus } from '@learnexia/api-client';

const strings = getStrings(ADMIN_LOCALE);

// ── Decision derivations ───────────────────────────────────────────────────────

type ReviewDecision = Exclude<ModerationStatus, 'Pending'>;

interface DecisionConfig {
  variant: DialogVariant;
  title: string;
  subtitle: string;
  reasonLabel: string;
  confirmLabel: string;
  confirmBg: string;
  confirmTextColor: string;
  confirmHoverBg: string;
  required: boolean;
}

const DECISION_CONFIGS: Record<ReviewDecision, DecisionConfig> = {
  Approved: {
    variant: 'mod-approve',
    title: strings.modDlgApproveTitle,
    subtitle: strings.modDlgApproveSubtitle,
    reasonLabel: strings.modDlgApproveReasonLabel,
    confirmLabel: strings.modDlgApproveConfirm,
    confirmBg: '#22C55E',
    confirmTextColor: '#0F172A',
    confirmHoverBg: '#16A34A',
    required: false,
  },
  Rejected: {
    variant: 'mod-reject',
    title: strings.modDlgRejectTitle,
    subtitle: strings.modDlgRejectSubtitle,
    reasonLabel: strings.modDlgRejectReasonLabel,
    confirmLabel: strings.modDlgRejectConfirm,
    confirmBg: '#EF4444',
    confirmTextColor: '#F8FAFC',
    confirmHoverBg: '#F87171',
    required: true,
  },
  Flagged: {
    variant: 'mod-flag',
    title: strings.modDlgFlagTitle,
    subtitle: strings.modDlgFlagSubtitle,
    reasonLabel: strings.modDlgFlagReasonLabel,
    confirmLabel: strings.modDlgFlagConfirm,
    confirmBg: '#A855F7',
    confirmTextColor: '#F8FAFC',
    confirmHoverBg: '#9333EA',
    required: false,
  },
};

// ── Error mapping ─────────────────────────────────────────────────────────────

function mapReviewError(err: unknown): string {
  if (isApiError(err)) {
    if (err.status === 400) {
      const msg = (err.message ?? '').toLowerCase();
      if (msg.includes('flagged')) return strings.modErrAlreadyFlagged;
      return strings.modErrAlreadyTerminal;
    }
    if (err.status === 404) return strings.modErr404;
    if (err.status === 422) return strings.modErrValidation;
  }
  return strings.modErrNetwork;
}

// ── Spinner ────────────────────────────────────────────────────────────────────

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

// ── Props ─────────────────────────────────────────────────────────────────────

export interface ReviewItemDialogProps {
  open: boolean;
  itemId: number;
  /** Shown in dialog body as reference context; always dir="ltr". */
  contentReference: string;
  decision: ReviewDecision;
  /** Called when the dialog closes via cancel (not success). */
  onClose: () => void;
  /** Called after a successful review; dialog closes first. */
  onSuccess?: () => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function ReviewItemDialog({
  open,
  itemId,
  contentReference,
  decision,
  onClose,
  onSuccess,
}: ReviewItemDialogProps) {
  const [reason, setReason] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const config = DECISION_CONFIGS[decision];
  const { mutate: reviewItem, isPending } = useReviewModerationItem();

  const canSubmit =
    config.required
      ? reason.trim().length > 0 && reason.length <= 2000
      : reason.length <= 2000;

  const handleClose = useCallback(() => {
    setReason('');
    setErrorMessage(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(() => {
    if (!canSubmit || isPending) return;
    setErrorMessage(null);

    reviewItem(
      {
        id: itemId,
        decision,
        reason: reason.trim() || undefined,
      },
      {
        onSuccess: () => {
          setReason('');
          // Close first, then notify parent for the success banner.
          onClose();
          onSuccess?.();
        },
        onError: (err) => {
          setErrorMessage(mapReviewError(err));
        },
      },
    );
  }, [canSubmit, isPending, reviewItem, itemId, decision, reason, onClose, onSuccess]);

  const confirmButton = (
    <button
      type="button"
      data-testid="dialog-confirm-btn"
      onClick={handleConfirm}
      aria-disabled={!canSubmit || isPending}
      aria-label={config.confirmLabel}
      style={{
        height: 44,
        paddingInline: 20,
        borderRadius: 'var(--lx-radius-button)',
        border: 'none',
        backgroundColor:
          canSubmit && !isPending ? config.confirmBg : `${config.confirmBg}66`,
        color: config.confirmTextColor,
        fontSize: 14,
        fontWeight: 600,
        cursor: canSubmit && !isPending ? 'pointer' : 'default',
        fontFamily: 'inherit',
        display: 'inline-flex',
        alignItems: 'center',
        gap: 8,
        transition:
          'background-color var(--lx-dur-fast) var(--lx-ease-out), transform 80ms',
        opacity: canSubmit ? 1 : 0.5,
      }}
      onMouseEnter={(e) => {
        if (canSubmit && !isPending) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor =
            config.confirmHoverBg;
        }
      }}
      onMouseLeave={(e) => {
        (e.currentTarget as HTMLButtonElement).style.backgroundColor =
          canSubmit && !isPending ? config.confirmBg : `${config.confirmBg}66`;
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
        <SpinnerIcon color={config.confirmTextColor} />
      ) : (
        config.confirmLabel
      )}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant={config.variant}
      title={config.title}
      subtitle={config.subtitle}
      cancelLabel={strings.modDlgCancel}
      confirmButton={confirmButton}
      dialogTestId="review-item-dialog"
    >
      {/* Content reference display */}
      <Stack
        flexDirection="column"
        gap="$1"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'var(--lx-bg)',
          border: '1px solid var(--lx-border)',
        }}
      >
        <Text
          fontFamily="$body"
          fontSize={11}
          fontWeight="600"
          style={{
            color: 'var(--lx-fg3)',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
          }}
        >
          {strings.modColContentRef}
        </Text>
        <Text
          fontFamily="$body"
          fontSize={13}
          color="$fg1"
          dir="ltr"
          style={{ fontFamily: 'var(--lx-font-mono, monospace)' }}
        >
          {contentReference}
        </Text>
      </Stack>

      {/* Reason field */}
      <ReasonField
        id="review-reason"
        label={config.reasonLabel}
        required={config.required}
        value={reason}
        onChange={setReason}
        maxLength={2000}
        testId="review-reason-field"
        placeholder={
          decision === 'Rejected'
            ? ADMIN_LOCALE === 'ar'
              ? 'اذكر سبب رفض هذا العنصر…'
              : 'Describe why this item is being rejected…'
            : undefined
        }
        error={
          errorMessage === strings.modErrValidation ? errorMessage : undefined
        }
      />

      {/* Envelope error — stays open so admin can retry or cancel */}
      {errorMessage && errorMessage !== strings.modErrValidation && (
        <AdminErrorBanner variant="error" message={errorMessage} />
      )}
    </AdminConfirmDialog>
  );
}
