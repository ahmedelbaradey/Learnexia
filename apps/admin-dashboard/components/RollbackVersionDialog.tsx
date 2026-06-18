'use client';

/**
 * RollbackVersionDialog — confirm + required reason dialog for rollback.
 *
 * Mirrors SuspendUserDialog (AdminConfirmDialog + ReasonField + error mapping
 * + pending spinner). The reason is a FE-ONLY UX guardrail — it is NOT sent
 * to the backend (POST /Rollback has no reason field). A notice inside the
 * dialog states this explicitly. (DG-4)
 *
 * Gate condition: reason.trim().length > 0 && reason.length <= 500.
 * Confirm button is aria-disabled + opacity:0.4 until gate is met.
 *
 * Design Spec P7-05 §Component 4 — RollbackVersionDialog.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { ReasonField } from './ReasonField';
import { useRollbackToVersion } from '@learnexia/api-client';
import type { VersionedEntityTypeValue } from '@learnexia/shared/constants';
import type { AdminStrings } from '../lib/strings';

function InfoIcon({ size = 13 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  );
}

function SpinnerIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="#0F172A" strokeWidth="2" strokeLinecap="round">
      <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"
        strokeDasharray="4 2" />
    </svg>
  );
}

export interface RollbackVersionDialogProps {
  open: boolean;
  entityType: VersionedEntityTypeValue;
  entityId: number;
  versionNumber: number;
  publishedAtUtc: string;
  publishedByUserId: number;
  gradeId?: number;
  strings: AdminStrings;
  onClose: () => void;
  onSuccess?: () => void;
}

export function RollbackVersionDialog({
  open,
  entityType,
  entityId,
  versionNumber,
  publishedAtUtc,
  publishedByUserId,
  gradeId,
  strings,
  onClose,
  onSuccess,
}: RollbackVersionDialogProps) {
  const rollback = useRollbackToVersion();
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);

  const isValid = reason.trim().length > 0 && reason.length <= 500;
  const isPending = rollback.isPending;

  // Format the publish date for display — always dir="ltr" (technical string)
  const formattedDate = (() => {
    try {
      return new Intl.DateTimeFormat('en-GB', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }).format(new Date(publishedAtUtc));
    } catch {
      return publishedAtUtc;
    }
  })();

  const handleConfirm = async () => {
    if (!isValid) return;
    setError(null);
    try {
      await rollback.mutateAsync({
        entityType,
        entityId,
        versionNumber,
        _feOnlyReason: reason,
        gradeId,
      });
      setReason('');
      onSuccess?.();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : strings.curriculumNetworkError);
    }
  };

  const handleClose = () => {
    setReason('');
    setError(null);
    onClose();
  };

  return (
    <AdminConfirmDialog
      open={open}
      onClose={handleClose}
      variant="restore"
      title={strings.clRollbackTitle.replace('{N}', String(versionNumber))}
      subtitle={strings.clRollbackSubtitle.replace('{formattedDate}', formattedDate)}
      cancelLabel={strings.curriculumDeleteCancel}
      dialogTestId="rollback-version-dialog"
      confirmButton={
        <button
          type="button"
          data-testid="rollback-confirm-btn"
          aria-label={strings.clRollbackConfirm.replace('{N}', String(versionNumber))}
          aria-disabled={!isValid || isPending}
          onClick={() => {
            if (isValid && !isPending) void handleConfirm();
          }}
          style={{
            height: 44,
            paddingInline: 20,
            borderRadius: 'var(--lx-radius-button)',
            border: 'none',
            backgroundColor: '#22C55E',
            color: '#0F172A',
            fontSize: 14,
            fontWeight: 600,
            cursor: !isValid || isPending ? 'not-allowed' : 'pointer',
            fontFamily: 'inherit',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            opacity: !isValid || isPending ? 0.4 : 1,
            transition: 'filter 120ms var(--lx-ease-out), opacity 120ms',
          }}
          onMouseEnter={(e) => {
            if (isValid && !isPending) (e.currentTarget as HTMLButtonElement).style.filter = 'brightness(1.08)';
          }}
          onMouseLeave={(e) => {
            (e.currentTarget as HTMLButtonElement).style.filter = 'none';
          }}
        >
          {isPending ? <SpinnerIcon /> : null}
          {strings.clRollbackConfirm.replace('{N}', String(versionNumber))}
        </button>
      }
    >
      {/* Version details block */}
      <Stack
        flexDirection="column"
        gap="$2"
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
          style={{ color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.04em' }}
        >
          {strings.clRollbackVersionDetails}
        </Text>
        <Text fontFamily="$body" fontSize={13} color="$fg2">
          {strings.clRollbackVersionDetails}{' '}v
          <span dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>{versionNumber}</span>
          {' '}—{' '}
          <span dir="ltr">{formattedDate}</span>
          {' '}(ID{' '}
          <span dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>{publishedByUserId}</span>
          )
        </Text>
      </Stack>

      {/* Warning */}
      <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
        {strings.clRollbackWarningBody}
      </Text>

      {/* Required reason (FE-only guardrail) */}
      <ReasonField
        id="rollback-reason"
        label={strings.clRollbackReasonLabel}
        required
        value={reason}
        onChange={setReason}
        maxLength={500}
        placeholder={strings.clRollbackReasonPlaceholder}
        testId="rollback-reason-field"
      />

      {/* FE-only guardrail notice */}
      <Stack
        flexDirection="row"
        gap="$2"
        alignItems="flex-start"
        padding="$2"
        paddingHorizontal="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(148,163,184,0.06)',
          border: '1px solid rgba(148,163,184,0.12)',
        }}
      >
        <span style={{ color: '#94A3B8', flexShrink: 0, marginTop: 1 }}>
          <InfoIcon size={13} />
        </span>
        <Text
          fontFamily="$body"
          fontSize={12}
          style={{ color: '#94A3B8', lineHeight: 1.5, fontStyle: 'italic' }}
        >
          {strings.clRollbackGuardrailNotice}
        </Text>
      </Stack>

      {error && <AdminErrorBanner variant="error" message={error} />}
    </AdminConfirmDialog>
  );
}
