'use client';

/**
 * LeagueTierOverrideDialog — override a student's league tier (P7-13-FE).
 *
 * Mirrors GradeOverrideDialog shape (AdminConfirmDialog + ReasonField + canSubmit gate).
 * Uses AdminConfirmDialog variant="gamification-override" (indigo shield icon).
 *
 * Gate: confirm button is aria-disabled until:
 *   1. selectedTier > 0 AND selectedTier !== currentTier
 *   2. reason.trim().length > 0
 *
 * Backend: POST /api/Admin/Gamification/children/{childId}/league-tier
 *   body: { newTier, reason, confirm: true }
 *   LeagueTier: Bronze=1, Silver=2, Gold=3, Diamond=4
 *
 * Error mapping: 400/422/404/5xx/network.
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useOverrideLeagueTier, type LeagueTier } from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminConfirmDialog } from '../AdminConfirmDialog';
import { ReasonField } from '../ReasonField';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Tier options ─────────────────────────────────────────────────────────────

const TIER_OPTIONS: { value: LeagueTier; enLabel: string; arLabel: string }[] = [
  { value: 1, enLabel: 'Bronze', arLabel: 'برونز' },
  { value: 2, enLabel: 'Silver', arLabel: 'فضة' },
  { value: 3, enLabel: 'Gold', arLabel: 'ذهب' },
  { value: 4, enLabel: 'Diamond', arLabel: 'ماس' },
];

// ── Props ─────────────────────────────────────────────────────────────────────

export interface LeagueTierOverrideDialogProps {
  childId: number;
  childName: string;
  /** Current tier numeric value (from AdminLeagueSummary mapped to int). */
  currentTier: LeagueTier | null;
  open: boolean;
  onClose: () => void;
  onSuccess: (message: string) => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function LeagueTierOverrideDialog({
  childId,
  childName,
  currentTier,
  open,
  onClose,
  onSuccess,
}: LeagueTierOverrideDialogProps) {
  const isAr = ADMIN_LOCALE === 'ar';
  const [selectedTier, setSelectedTier] = useState<LeagueTier | 0>(0);
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const mutation = useOverrideLeagueTier();

  const canSubmit =
    selectedTier > 0 &&
    selectedTier !== (currentTier ?? -1) &&
    reason.trim().length > 0 &&
    reason.length <= 500;

  const handleClose = useCallback(() => {
    setSelectedTier(0);
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
        newTier: selectedTier as LeagueTier,
        reason: reason.trim(),
        confirm: true,
      });
      onSuccess(strings.gamLeagueTierSuccessBanner.replace('{name}', childName));
      handleClose();
    } catch (err) {
      if (isApiError(err)) {
        if (err.status === 404) {
          setError(strings.gamLeagueTierError404);
        } else if (err.status === 422) {
          setError(strings.gamLeagueTierError422);
        } else if (err.status === 400) {
          setError(strings.gamLeagueTierError400);
        } else {
          setError(strings.gamLeagueTierErrorNetwork);
        }
      } else {
        setError(strings.gamLeagueTierErrorNetwork);
      }
    }
  }, [canSubmit, mutation, childId, selectedTier, reason, isAr, onSuccess, handleClose]);

  const currentTierLabel = currentTier
    ? (TIER_OPTIONS.find((t) => t.value === currentTier)?.[isAr ? 'arLabel' : 'enLabel'] ?? String(currentTier))
    : strings.gamLeagueTierUnknown;

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
      title={strings.gamLeagueTierDialogTitle}
      subtitle={strings.gamLeagueTierDialogBody.replace('{name}', childName)}
      cancelLabel={strings.gamLeagueTierCancelBtn}
      dialogTestId="league-tier-override-dialog"
      onClose={handleClose}
      confirmButton={
        <button
          type="button"
          data-testid="league-tier-confirm-btn"
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
          {mutation.isPending ? '…' : strings.gamLeagueTierConfirmBtn}
        </button>
      }
    >
      <Stack flexDirection="column" gap="$4">
        {/* Current tier read-only */}
        <Stack flexDirection="column" gap="$1">
          <Text style={labelStyle}>{strings.gamLeagueTierCurrentLabel}</Text>
          <div style={{
            display: 'inline-flex', alignItems: 'center', gap: 8,
            padding: '8px 12px', backgroundColor: 'var(--lx-card-soft)',
            borderRadius: 8, border: '1px solid var(--lx-border)',
            fontSize: 14, color: 'var(--lx-fg2)', fontFamily: 'inherit',
          }}>
            <span style={{ fontVariantNumeric: 'tabular-nums' }} dir="ltr">
              {currentTierLabel}
            </span>
          </div>
        </Stack>

        {/* New tier picker */}
        <Stack flexDirection="column" gap="$1">
          <label htmlFor="league-tier-select" style={labelStyle}>
            {strings.gamLeagueTierNewLabel} <span style={{ color: '#EF4444' }}>*</span>
          </label>
          <select
            id="league-tier-select"
            data-testid="league-tier-select"
            value={selectedTier === 0 ? '' : String(selectedTier)}
            onChange={(e) => setSelectedTier(e.target.value ? Number(e.target.value) as LeagueTier : 0)}
            style={{ ...inputStyle, cursor: 'pointer' }}
            onFocus={(e) => {
              e.currentTarget.style.borderColor = 'var(--lx-primary)';
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.borderColor = 'var(--lx-border)';
              e.currentTarget.style.boxShadow = 'none';
            }}
          >
            <option value="">{strings.gamLeagueTierSelectPlaceholder}</option>
            {TIER_OPTIONS.filter((t) => t.value !== currentTier).map((t) => (
              <option key={t.value} value={String(t.value)}>
                {isAr ? t.arLabel : t.enLabel}
              </option>
            ))}
          </select>
        </Stack>

        {/* Reason */}
        <ReasonField
          id="league-tier-reason"
          value={reason}
          onChange={setReason}
          label={strings.gamLeagueTierReasonLabel}
          placeholder={strings.gamLeagueTierReasonPlaceholder}
          maxLength={500}
          required
        />

        {/* Server error */}
        {error && <AdminErrorBanner variant="error" message={error} />}
      </Stack>
    </AdminConfirmDialog>
  );
}
