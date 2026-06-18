'use client';

/**
 * DeleteBlockDialog — soft-delete confirm for a single content block.
 * Design Spec §8.7 — P7-02.
 *
 * Thin wrapper around AdminConfirmDialog (delete variant).
 */

import { useState } from 'react';
import { useDeleteContentBlock } from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface DeleteBlockDialogProps {
  open: boolean;
  onClose: () => void;
  blockId: number;
  lessonId: number;
}

export function DeleteBlockDialog({ open, onClose, blockId, lessonId }: DeleteBlockDialogProps) {
  const deleteMutation = useDeleteContentBlock();
  const [mutationError, setMutationError] = useState<string | null>(null);

  const handleConfirm = async () => {
    setMutationError(null);
    try {
      await deleteMutation.mutateAsync({ id: blockId, lessonId });
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
      data-testid="delete-block-confirm-btn"
      aria-busy={deleteMutation.isPending}
      style={{
        height: 44, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
        backgroundColor: 'var(--lx-danger)', border: 'none',
        color: '#F8FAFC', fontSize: 14, fontWeight: 600,
        cursor: deleteMutation.isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: deleteMutation.isPending ? 0.7 : 1,
      }}
    >
      {deleteMutation.isPending ? '…' : strings.blockDeleteConfirmBtn}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      variant="delete"
      title={strings.blockDeleteTitle}
      subtitle={strings.blockDeleteSubtitle}
      confirmButton={confirmButton}
      cancelLabel={strings.lessonDeleteCancelBtn}
      onClose={() => { setMutationError(null); onClose(); }}
      dialogTestId="delete-block-dialog"
    >
      <div
        style={{
          padding: 12,
          backgroundColor: 'rgba(239,68,68,0.08)',
          border: '1px solid rgba(239,68,68,0.15)',
          borderRadius: 8,
        }}
      >
        <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
          {strings.blockDeleteBody}
        </p>
      </div>
      {mutationError && <AdminErrorBanner variant="error" message={mutationError} />}
    </AdminConfirmDialog>
  );
}
