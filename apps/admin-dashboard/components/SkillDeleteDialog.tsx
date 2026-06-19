'use client';

/**
 * SkillDeleteDialog — soft-delete confirm dialog for skills (P7-03 Design Spec §S3).
 *
 * Wraps AdminConfirmDialog (variant="delete"). Plain confirm — no reason field,
 * no typed-confirm (curriculum metadata is non-PII, soft-delete).
 *
 * On confirm: calls useDeleteSkill(id). On success: invalidation is handled by
 * the hook; parent's onDeleted callback is invoked.
 *
 * On ApiEnvelopeError: surfaces err.message in AdminErrorBanner inside the dialog.
 */

import { useState } from 'react';
import { useDeleteSkill } from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export interface SkillDeleteDialogProps {
  open: boolean;
  skillId: number;
  skillName: string;
  /** Optional: current subject id — passed to useDeleteSkill for graph invalidation. */
  subjectId?: number;
  onClose: () => void;
  /** Called after successful deletion — parent should clear selectedSkillId if needed. */
  onDeleted?: () => void;
}

export function SkillDeleteDialog({
  open,
  skillId,
  skillName,
  subjectId,
  onClose,
  onDeleted,
}: SkillDeleteDialogProps) {
  const [serverError, setServerError] = useState<string | null>(null);
  const deleteMutation = useDeleteSkill();

  const handleConfirm = async () => {
    setServerError(null);
    try {
      await deleteMutation.mutateAsync({ id: skillId, subjectId });
      onDeleted?.();
      onClose();
    } catch (err) {
      setServerError((err as Error).message || strings.skillGraphErrGeneric);
    }
  };

  const confirmButton = (
    <button
      onClick={() => void handleConfirm()}
      disabled={deleteMutation.isPending}
      aria-busy={deleteMutation.isPending}
      data-testid="skill-delete-confirm"
      style={{
        height: 40,
        paddingLeft: 20,
        paddingRight: 20,
        backgroundColor: '#EF4444',
        borderRadius: 16,
        border: 'none',
        color: '#F8FAFC',
        fontSize: 14,
        fontWeight: 600,
        cursor: deleteMutation.isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit',
        opacity: deleteMutation.isPending ? 0.7 : 1,
      }}
    >
      {deleteMutation.isPending ? '…' : strings.skillDeleteConfirm}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      onClose={onClose}
      variant="delete"
      title={strings.skillDeleteTitle}
      subtitle={skillName}
      cancelLabel={strings.skillDeleteCancel}
      confirmButton={confirmButton}
      dialogTestId="skill-delete-dialog"
    >
      <div style={{
        backgroundColor: 'rgba(239,68,68,0.06)',
        borderRadius: 8,
        border: '1px solid rgba(239,68,68,0.15)',
        padding: 16,
      }}>
        <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
          {strings.skillDeleteBody}
        </p>
      </div>
      {serverError && (
        <AdminErrorBanner variant="error" message={serverError} />
      )}
    </AdminConfirmDialog>
  );
}
