'use client';

/**
 * PublishCurriculumDialog — confirm dialog for Draft → Published transition.
 *
 * HIGH IMPACT: publishing makes content immediately visible to students.
 * The body includes an explicit impact notice stating this consequence.
 * No gating condition beyond opening the dialog (the notice is information).
 *
 * Mirror of SuspendUserDialog — AdminConfirmDialog shell with the 'publish'
 * variant. No new design patterns.
 *
 * Design Spec P7-05 §Component 3a.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { useTransitionLifecycle } from '@learnexia/api-client';
import { LIFECYCLE_STATE, type VersionedEntityTypeValue } from '@learnexia/shared/constants';
import type { AdminStrings } from '../lib/strings';

// Alert circle inline SVG
function AlertCircleIcon({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
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

export interface PublishCurriculumDialogProps {
  open: boolean;
  entityType: VersionedEntityTypeValue;
  entityId: number;
  entityLabel: string;
  gradeId?: number;
  strings: AdminStrings;
  onClose: () => void;
  onSuccess?: () => void;
}

export function PublishCurriculumDialog({
  open,
  entityType,
  entityId,
  entityLabel: _entityLabel,
  gradeId,
  strings,
  onClose,
  onSuccess,
}: PublishCurriculumDialogProps) {
  const transition = useTransitionLifecycle();
  const [error, setError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setError(null);
    try {
      await transition.mutateAsync({
        entityType,
        entityId,
        targetState: LIFECYCLE_STATE.Published,
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
      variant="publish"
      title={strings.clPublishTitle}
      subtitle={strings.clPublishSubtitle}
      cancelLabel={strings.curriculumDeleteCancel}
      dialogTestId="publish-curriculum-dialog"
      confirmButton={
        <button
          type="button"
          data-testid="publish-confirm-btn"
          aria-label={strings.clPublishConfirm}
          disabled={isPending}
          onClick={() => void handleConfirm()}
          style={{
            height: 44,
            paddingInline: 20,
            borderRadius: 'var(--lx-radius-button)',
            border: 'none',
            backgroundColor: '#4F46E5',
            color: '#F8FAFC',
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
          {strings.clPublishConfirm}
        </button>
      }
    >
      {/* Impact notice */}
      <Stack
        flexDirection="row"
        gap="$2"
        alignItems="flex-start"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(79,70,229,0.08)',
          border: '1px solid rgba(79,70,229,0.2)',
        }}
      >
        <span style={{ color: '#4F46E5', flexShrink: 0, marginTop: 1 }}>
          <AlertCircleIcon size={16} />
        </span>
        <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
          {strings.clPublishNoticeBody}
        </Text>
      </Stack>

      {error && <AdminErrorBanner variant="error" message={error} />}
    </AdminConfirmDialog>
  );
}
