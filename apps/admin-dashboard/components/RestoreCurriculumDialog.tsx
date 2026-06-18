'use client';

/**
 * RestoreCurriculumDialog — confirm dialog for Archived → Draft transition.
 *
 * Green confirm button (positive recovery action). Info notice explains that
 * the content returns to Draft and can be reviewed before re-publishing.
 *
 * Design Spec P7-05 §Component 3d.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { useTransitionLifecycle } from '@learnexia/api-client';
import { LIFECYCLE_STATE, type VersionedEntityTypeValue } from '@learnexia/shared/constants';
import type { AdminStrings } from '../lib/strings';

function RotateCcwIcon({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="1 4 1 10 7 10" />
      <path d="M3.51 15a9 9 0 1 0 .49-4.5" />
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

export interface RestoreCurriculumDialogProps {
  open: boolean;
  entityType: VersionedEntityTypeValue;
  entityId: number;
  entityLabel: string;
  gradeId?: number;
  strings: AdminStrings;
  onClose: () => void;
  onSuccess?: () => void;
}

export function RestoreCurriculumDialog({
  open,
  entityType,
  entityId,
  entityLabel: _entityLabel,
  gradeId,
  strings,
  onClose,
  onSuccess,
}: RestoreCurriculumDialogProps) {
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
      variant="restore"
      title={strings.clRestoreTitle}
      subtitle={strings.clRestoreSubtitle}
      cancelLabel={strings.curriculumDeleteCancel}
      dialogTestId="restore-curriculum-dialog"
      confirmButton={
        <button
          type="button"
          data-testid="restore-confirm-btn"
          aria-label={strings.clRestoreConfirm}
          disabled={isPending}
          onClick={() => void handleConfirm()}
          style={{
            height: 44,
            paddingInline: 20,
            borderRadius: 'var(--lx-radius-button)',
            border: 'none',
            backgroundColor: '#22C55E',
            color: '#0F172A',
            fontSize: 14,
            fontWeight: 600,
            cursor: isPending ? 'not-allowed' : 'pointer',
            fontFamily: 'inherit',
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            opacity: isPending ? 0.7 : 1,
            transition: 'filter 120ms var(--lx-ease-out)',
          }}
          onMouseEnter={(e) => {
            if (!isPending) (e.currentTarget as HTMLButtonElement).style.filter = 'brightness(1.08)';
          }}
          onMouseLeave={(e) => {
            (e.currentTarget as HTMLButtonElement).style.filter = 'none';
          }}
        >
          {isPending ? <SpinnerIcon /> : null}
          {strings.clRestoreConfirm}
        </button>
      }
    >
      {/* Info block */}
      <Stack
        flexDirection="row"
        gap="$2"
        alignItems="flex-start"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(34,197,94,0.08)',
          border: '1px solid rgba(34,197,94,0.15)',
        }}
      >
        <span style={{ color: '#22C55E', flexShrink: 0, marginTop: 1 }}>
          <RotateCcwIcon size={16} />
        </span>
        <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
          {strings.clRestoreNotice}
        </Text>
      </Stack>

      {error && <AdminErrorBanner variant="error" message={error} />}
    </AdminConfirmDialog>
  );
}
