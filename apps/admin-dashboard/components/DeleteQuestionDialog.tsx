'use client';

/**
 * DeleteQuestionDialog — soft-delete confirm dialog for questions.
 * Design Spec §S7 — P7-04.
 * Wraps AdminConfirmDialog (delete variant).
 */

import { useState } from 'react';
import { useDeleteQuestion } from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface DeleteQuestionDialogProps {
  open: boolean;
  onClose: () => void;
  questionId: number;
  lessonId: number;
}

export function DeleteQuestionDialog({ open, onClose, questionId, lessonId }: DeleteQuestionDialogProps) {
  const deleteMutation = useDeleteQuestion();
  const [mutationError, setMutationError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setMutationError(null);
    try {
      await deleteMutation.mutateAsync({ id: questionId, lessonId });
      onClose();
    } catch (err) {
      setMutationError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  const confirmButton = (
    <button
      type="button"
      onClick={handleConfirm}
      disabled={deleteMutation.isPending}
      data-testid="delete-question-confirm-btn"
      aria-busy={deleteMutation.isPending}
      style={{
        height: 44, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
        backgroundColor: 'var(--lx-danger)', border: 'none',
        color: '#F8FAFC', fontSize: 14, fontWeight: 600,
        cursor: deleteMutation.isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: deleteMutation.isPending ? 0.7 : 1,
      }}
    >
      {deleteMutation.isPending ? '…' : strings.questionDeleteConfirm}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      variant="delete"
      title={strings.questionDeleteTitle}
      subtitle={strings.questionDeleteSubtitle}
      confirmButton={confirmButton}
      cancelLabel={strings.questionDeleteCancel}
      onClose={() => { setMutationError(null); onClose(); }}
      dialogTestId="delete-question-dialog"
    >
      <div
        style={{
          padding: 16,
          backgroundColor: 'rgba(239,68,68,0.08)',
          borderRadius: 8,
          border: '1px solid rgba(239,68,68,0.15)',
        }}
      >
        <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
          {strings.questionDeleteBody}
        </p>
      </div>
      {mutationError && <AdminErrorBanner variant="error" message={mutationError} />}
    </AdminConfirmDialog>
  );
}
