'use client';

/**
 * LessonDeleteDialog — soft-delete confirm dialog for lessons with cascade warning.
 * Design Spec §S10 — P7-02.
 *
 * Wraps AdminConfirmDialog (delete variant) with lesson-specific copy and
 * a cascade-block warning. Uses the same confirmButton-as-ReactNode API.
 */

import { useState } from 'react';
import { useDeleteLesson } from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { useRouter } from 'next/navigation';

const strings = getStrings(ADMIN_LOCALE);

export interface LessonDeleteDialogProps {
  open: boolean;
  onClose: () => void;
  lessonId: number;
  lessonName: string;
  unitId: number;
  /** If provided, navigate here after successful delete. */
  backHref?: string;
}

export function LessonDeleteDialog({
  open, onClose, lessonId, lessonName, unitId, backHref,
}: LessonDeleteDialogProps) {
  const router = useRouter();
  const deleteMutation = useDeleteLesson();
  const [mutationError, setMutationError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setMutationError(null);
    try {
      await deleteMutation.mutateAsync({ id: lessonId, unitId });
      onClose();
      if (backHref) router.push(backHref);
    } catch (err) {
      setMutationError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  const confirmButton = (
    <button
      type="button"
      onClick={handleConfirm}
      disabled={deleteMutation.isPending}
      data-testid="delete-lesson-confirm-btn"
      aria-busy={deleteMutation.isPending}
      style={{
        height: 44, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
        backgroundColor: 'var(--lx-danger)', border: 'none',
        color: '#F8FAFC', fontSize: 14, fontWeight: 600,
        cursor: deleteMutation.isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: deleteMutation.isPending ? 0.7 : 1,
      }}
    >
      {deleteMutation.isPending ? '…' : strings.lessonDeleteConfirmBtn}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      variant="delete"
      title={strings.lessonDeleteTitle}
      subtitle={strings.lessonDeleteSubtitle.replace('{name}', lessonName)}
      confirmButton={confirmButton}
      cancelLabel={strings.lessonDeleteCancelBtn}
      onClose={() => { setMutationError(null); onClose(); }}
      dialogTestId="delete-lesson-dialog"
    >
      <div
        style={{
          padding: 16,
          backgroundColor: 'rgba(239,68,68,0.08)',
          borderRadius: 8,
          border: '1px solid rgba(239,68,68,0.15)',
          display: 'flex',
          flexDirection: 'column',
          gap: 8,
        }}
      >
        <p style={{ margin: 0, fontSize: 13, fontWeight: 700, color: 'var(--lx-danger)' }}>
          {strings.lessonDeleteCascadeHeading}
        </p>
        <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
          {strings.lessonDeleteCascadeBody}
        </p>
      </div>
      {mutationError && <AdminErrorBanner variant="error" message={mutationError} />}
    </AdminConfirmDialog>
  );
}
