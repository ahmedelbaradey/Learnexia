'use client';

/**
 * ArchiveCurriculumDialog — confirm dialog for Published → Archived transition.
 *
 * Amber confirm button (caution — archiving removes from active curriculum but
 * is reversible via Restore). Warning block explains re-publish requires Restore
 * to Draft first. Student progress is preserved.
 *
 * Design Spec P7-05 §Component 3c.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { useTransitionLifecycle } from '@learnexia/api-client';
import { LIFECYCLE_STATE, type VersionedEntityTypeValue } from '@learnexia/shared/constants';
import type { AdminStrings } from '../lib/strings';

function SpinnerIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round">
      <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83"
        strokeDasharray="4 2" />
    </svg>
  );
}

export interface ArchiveCurriculumDialogProps {
  open: boolean;
  entityType: VersionedEntityTypeValue;
  entityId: number;
  entityLabel: string;
  gradeId?: number;
  strings: AdminStrings;
  onClose: () => void;
  onSuccess?: () => void;
}

export function ArchiveCurriculumDialog({
  open,
  entityType,
  entityId,
  entityLabel: _entityLabel,
  gradeId,
  strings,
  onClose,
  onSuccess,
}: ArchiveCurriculumDialogProps) {
  const transition = useTransitionLifecycle();
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setError(null);
    try {
      await transition.mutateAsync({
        entityType,
        entityId,
        targetState: LIFECYCLE_STATE.Archived,
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
      variant="archive"
      title={strings.clArchiveTitle}
      subtitle={strings.clArchiveSubtitle}
      cancelLabel={strings.curriculumDeleteCancel}
      dialogTestId="archive-curriculum-dialog"
      confirmButton={
        <button
          type="button"
          data-testid="archive-confirm-btn"
          aria-label={strings.clArchiveConfirm}
          disabled={isPending}
          onClick={() => void handleConfirm()}
          style={{
            height: 44,
            paddingInline: 20,
            borderRadius: 'var(--lx-radius-button)',
            border: 'none',
            backgroundColor: '#F59E0B',
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
          {strings.clArchiveConfirm}
        </button>
      }
    >
      {/* Warning block */}
      <Stack
        flexDirection="column"
        gap="$2"
        padding="$4"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(245,158,11,0.08)',
          border: '1px solid rgba(245,158,11,0.15)',
        }}
      >
        <Text fontFamily="$body" fontSize={13} fontWeight="700" style={{ color: '#F59E0B' }}>
          {strings.clArchiveNoticeHeading}
        </Text>
        <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
          {strings.clArchiveNoticeBody}
        </Text>
      </Stack>

      {error && <AdminErrorBanner variant="error" message={error} />}
    </AdminConfirmDialog>
  );
}
