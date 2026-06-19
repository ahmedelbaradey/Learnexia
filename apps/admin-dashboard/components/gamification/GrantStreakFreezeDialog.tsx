'use client';

/**
 * GrantStreakFreezeDialog — grant streak freeze tokens to a student (P7-13-FE).
 *
 * Mirrors GradeOverrideDialog shape (AdminConfirmDialog + ReasonField + canSubmit gate).
 * Uses AdminConfirmDialog variant="gamification-override" (indigo shield icon).
 *
 * Gate: confirm button is aria-disabled until:
 *   1. count ≥ 1
 *   2. reason.trim().length > 0
 *
 * Note on freeze balance: no GET endpoint for current balance — balance shown
 * as unavailable with strings.gamFreezeBalanceUnavailable.
 *
 * Backend: POST /api/Admin/Gamification/children/{childId}/streak-freeze
 *   body: { count, reason, confirm: true }
 *
 * Error mapping: 400/422/404/5xx/network.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useGrantStreakFreeze } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from '../AdminConfirmDialog';
import { ReasonField } from '../ReasonField';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Props ─────────────────────────────────────────────────────────────────────

export interface GrantStreakFreezeDialogProps {
  childId: number;
  childName: string;
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function GrantStreakFreezeDialog({
  childId,
  childName,
  open,
  onClose,
  onSuccess,
}: GrantStreakFreezeDialogProps) {
  const [count, setCount] = useState<number | ''>(1);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useGrantStreakFreeze();

  const canSubmit =
    count !== '' &&
    Number(count) >= 1 &&
    Number(count) <= 2 &&
    reason.trim().length > 0 &&
    reason.length <= 500;

  const handleClose = useCallback(() => {
    setCount(1);
    setReason('');
    setError(null);
    onClose();
  }, [onClose]);

  const handleConfirm = useCallback(async () => {
    if (!canSubmit || mutation.isPending) return;
    setError(null);
    try {
      await mutation.mutateAsync({
        childId,
        count: Number(count),
        reason: reason.trim(),
        confirm: true,
      });
      onSuccess(strings.gamFreezeSuccessBanner.replace('{name}', childName));
      handleClose();
    } catch (err) {
      if (isApiError(err)) {
        if (err.status === 404) {
          setError(strings.gamFreezeError404);
        } else if (err.status === 422) {
          setError(strings.gamFreezeError422);
        } else if (err.status === 400) {
          setError(strings.gamFreezeError400);
        } else {
          setError(strings.gamFreezeErrorNetwork);
        }
      } else {
        setError(strings.gamFreezeErrorNetwork);
      }
    }
  }, [canSubmit, mutation, childId, count, reason, onSuccess, handleClose]);

  const inputStyle: React.CSSProperties = {
    height: 44, backgroundColor: 'var(--lx-bg)', borderRadius: 8,
    border: '1px solid var(--lx-border)', paddingInline: 12, fontSize: 14,
    color: 'var(--lx-fg1)', fontFamily: 'inherit', width: '100%',
    boxSizing: 'border-box', outline: 'none',
  };

  const labelStyle: React.CSSProperties = {
    fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)',
    textTransform: 'uppercase', letterSpacing: '0.04em', fontFamily: 'inherit',
  };

  return (
    <AdminConfirmDialog
      open={open}
      variant="gamification-override"
      title={strings.gamFreezeDialogTitle}
      subtitle={strings.gamFreezeDialogBody.replace('{name}', childName)}
      cancelLabel={strings.gamFreezeCancelBtn}
      dialogTestId="grant-streak-freeze-dialog"
      onClose={handleClose}
      confirmButton={
        <button
          type="button"
          data-testid="grant-freeze-confirm-btn"
          aria-disabled={!canSubmit || mutation.isPending}
          onClick={handleConfirm}
          style={{
            height: 40, paddingInline: 20, borderRadius: 'var(--lx-radius-button)',
            backgroundColor: '#4F46E5', border: 'none',
            color: '#F8FAFC', fontSize: 14, fontWeight: 600,
            cursor: !canSubmit || mutation.isPending ? 'not-allowed' : 'pointer',
            fontFamily: 'inherit',
            opacity: !canSubmit || mutation.isPending ? 0.5 : 1,
            transition: 'opacity 120ms var(--lx-ease-out), background-color 120ms var(--lx-ease-out)',
          }}
          onMouseEnter={(e) => {
            if (canSubmit && !mutation.isPending)
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1';
          }}
          onMouseLeave={(e) => {
            (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5';
          }}
        >
          {mutation.isPending ? '…' : strings.gamFreezeConfirmBtn}
        </button>
      }
    >
      <Stack flexDirection="column" gap="$4">
        {/* Freeze balance notice (unavailable) */}
        <div style={{
          padding: '8px 12px',
          backgroundColor: 'var(--lx-card-soft)',
          borderRadius: 8,
          border: '1px solid var(--lx-border)',
          fontSize: 13, color: 'var(--lx-fg3)', fontFamily: 'inherit', lineHeight: 1.5,
        }}>
          {strings.gamFreezeBalanceUnavailable}
        </div>

        {/* Count input */}
        <Stack flexDirection="column" gap="$1">
          <label htmlFor="freeze-count" style={labelStyle}>
            {strings.gamFreezeCountLabel} <span style={{ color: '#EF4444' }}>*</span>
          </label>
          <input
            id="freeze-count"
            data-testid="freeze-count-input"
            type="number"
            value={count}
            onChange={(e) => setCount(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
            min={1} max={2} step={1} dir="ltr"
            aria-required="true"
            style={inputStyle}
            onFocus={(e) => {
              e.currentTarget.style.borderColor = 'var(--lx-primary)';
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.borderColor = 'var(--lx-border)';
              e.currentTarget.style.boxShadow = 'none';
            }}
          />
          <Text fontFamily="$body" fontSize={12} color="$fg3" style={{ marginTop: 2 }}>
            {strings.gamFreezeCountHint}
          </Text>
        </Stack>

        {/* Reason */}
        <ReasonField
          id="freeze-reason"
          value={reason}
          onChange={setReason}
          label={strings.gamFreezeReasonLabel}
          placeholder={strings.gamFreezeReasonPlaceholder}
          maxLength={500}
          required
        />

        {/* Server error */}
        {error && <AdminErrorBanner variant="error" message={error} />}
      </Stack>
    </AdminConfirmDialog>
  );
}
