'use client';

/**
 * ActivateQuestionDialog — show-to-students confirm dialog.
 * Design Spec §S7 — P7-04.
 * Wraps AdminConfirmDialog (reactivate variant).
 */

import { useState } from 'react';
import { useSetQuestionActive } from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface ActivateQuestionDialogProps {
  open: boolean;
  onClose: () => void;
  questionId: number;
  lessonId: number;
}

export function ActivateQuestionDialog({ open, onClose, questionId, lessonId }: ActivateQuestionDialogProps) {
  const setActiveMutation = useSetQuestionActive();
  const [mutationError, setMutationError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setMutationError(null);
    try {
      await setActiveMutation.mutateAsync({ id: questionId, lessonId, isActive: true });
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
      data-testid="activate-question-confirm-btn"
      aria-busy={setActiveMutation.isPending}
      style={{
        height: 44, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
        backgroundColor: '#22C55E', border: 'none',
        color: '#FFF', fontSize: 14, fontWeight: 600,
        cursor: setActiveMutation.isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: setActiveMutation.isPending ? 0.7 : 1,
      }}
    >
      {setActiveMutation.isPending ? '…' : strings.questionActivateConfirm}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      variant="reactivate"
      title={strings.questionActivateTitle}
      subtitle={strings.questionActivateSubtitle}
      confirmButton={confirmButton}
      cancelLabel={strings.questionDeleteCancel}
      onClose={() => { setMutationError(null); onClose(); }}
      dialogTestId="activate-question-dialog"
    >
      <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
        {strings.questionActivateBody}
      </p>
      {mutationError && <AdminErrorBanner variant="error" message={mutationError} />}
    </AdminConfirmDialog>
  );
}
