'use client';

/**
 * UnpublishCurriculumDialog — confirm dialog for Published → Draft transition.
 *
 * Content returns to Draft and is hidden from students.
 * Ghost-style confirm button (neutral, not destructive-red — unpublish is reversible).
 *
 * Design Spec P7-05 §Component 3b.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { useTransitionLifecycle } from '@learnexia/api-client';
import { LIFECYCLE_STATE, type VersionedEntityTypeValue } from '@learnexia/shared/constants';
import type { AdminStrings } from '../lib/strings';

function EyeOffIcon({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
      <path d="m1 1 22 22" />
    </svg>
  );
}

function SpinnerIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"
        strokeDasharray="4 2" />
    </svg>
  );
}

export interface UnpublishCurriculumDialogProps {
  open: boolean;
  entityType: VersionedEntityTypeValue;
  entityId: number;
  entityLabel: string;
  gradeId?: number;
  strings: AdminStrings;
  onClose: () => void;
  onSuccess?: () => void;
}

export function UnpublishCurriculumDialog({
  open,
  entityType,
  entityId,
  entityLabel: _entityLabel,
  gradeId,
  strings,
  onClose,
  onSuccess,
}: UnpublishCurriculumDialogProps) {
  const transition = useTransitionLifecycle();
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setError(null);
    try {
      await transition.mutateAsync({
        entityType,
        entityId,
        targetState: LIFECYCLE_STATE.Draft,
        gradeId,
      });
      onSuccess?.();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : strings.curriculumNetworkError);
    }
  };

  const isPending = transition.isPending;

  return (
    <AdminConfirmDialog
      open={open}
      onClose={onClose}
      variant="unpublish"
      title={strings.clUnpublishTitle}
      subtitle={strings.clUnpublishSubtitle}
      cancelLabel={strings.curriculumDeleteCancel}
      dialogTestId="unpublish-curriculum-dialog"
      confirmButton={
        <button
          type="button"
          data-testid="unpublish-confirm-btn"
          aria-label={strings.clUnpublishConfirm}
          disabled={isPending}
          onClick={() => void handleConfirm()}
          style={{
            height: 44,
            paddingInline: 20,
            borderRadius: 'var(--lx-radius-button)',
            border: '1px solid rgba(255,255,255,0.16)',
            backgroundColor: 'var(--lx-card-soft)',
            color: '#F8FAFC',
            fontSize: 14,
            fontWeight: 600,
            cursor: isPending ? 'not-allowed' : 'pointer',
            fontFamily: 'inherit',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            opacity: isPending ? 0.7 : 1,
            transition: 'filter 120ms var(--lx-ease-out), background-color 120ms var(--lx-ease-out)',
          }}
          onMouseEnter={(e) => {
            if (!isPending) {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'var(--lx-card)';
              (e.currentTarget as HTMLButtonElement).style.filter = 'brightness(1.05)';
            }
          }}
          onMouseLeave={(e) => {
            (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'var(--lx-card-soft)';
            (e.currentTarget as HTMLButtonElement).style.filter = 'none';
          }}
        >
          {isPending ? <SpinnerIcon /> : null}
          {strings.clUnpublishConfirm}
        </button>
      }
    >
      {/* Notice */}
      <Stack
        flexDirection="row"
        gap="$2"
        alignItems="flex-start"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(148,163,184,0.08)',
          border: '1px solid rgba(148,163,184,0.15)',
        }}
      >
        <span style={{ color: '#94A3B8', flexShrink: 0, marginTop: 1 }}>
          <EyeOffIcon size={16} />
        </span>
        <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
          {strings.clUnpublishNoticeBody}
        </Text>
      </Stack>

      {error && <AdminErrorBanner variant="error" message={error} />}
    </AdminConfirmDialog>
  );
}
