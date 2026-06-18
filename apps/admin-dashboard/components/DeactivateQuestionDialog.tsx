'use client';

/**
 * DeactivateQuestionDialog — hide-from-students confirm dialog.
 * Design Spec §S7 — P7-04.
 * Wraps AdminConfirmDialog (suspend variant).
 */

import { useState } from 'react';
import { useSetQuestionActive } from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface DeactivateQuestionDialogProps {
  open: boolean;
  onClose: () => void;
  questionId: number;
  lessonId: number;
}

export function DeactivateQuestionDialog({ open, onClose, questionId, lessonId }: DeactivateQuestionDialogProps) {
  const setActiveMutation = useSetQuestionActive();
  const [mutationError, setMutationError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setMutationError(null);
    try {
      await setActiveMutation.mutateAsync({ id: questionId, lessonId, isActive: false });
      onClose();
    } catch (err) {
      setMutationError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  const confirmButton = (
    <button
      type="button"
      onClick={handleConfirm}
      disabled={setActiveMutation.isPending}
      data-testid="deactivate-question-confirm-btn"
      aria-busy={setActiveMutation.isPending}
      style={{
        height: 44, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
        backgroundColor: '#F59E0B', border: 'none',
        color: '#FFF', fontSize: 14, fontWeight: 600,
        cursor: setActiveMutation.isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: setActiveMutation.isPending ? 0.7 : 1,
      }}
    >
      {setActiveMutation.isPending ? '…' : strings.questionDeactivateConfirm}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      variant="suspend"
      title={strings.questionDeactivateTitle}
      subtitle={strings.questionDeactivateSubtitle}
      confirmButton={confirmButton}
      cancelLabel={strings.questionDeleteCancel}
      onClose={() => { setMutationError(null); onClose(); }}
      dialogTestId="deactivate-question-dialog"
    >
      <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
        {strings.questionDeactivateBody}
      </p>
      {mutationError && <AdminErrorBanner variant="error" message={mutationError} />}
    </AdminConfirmDialog>
  );
}
