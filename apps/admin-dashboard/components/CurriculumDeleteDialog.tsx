'use client';

/**
 * CurriculumDeleteDialog — soft-delete confirm dialog for subjects and units (Design Spec §S8).
 *
 * Wraps AdminConfirmDialog (variant="delete"). Plain confirm — no reason field,
 * no typed-confirm (curriculum is non-PII, soft-delete is reversible).
 *
 * "Not empty" error handling: on Successed=false the server returns a message
 * like "Subject has units and cannot be deleted". Surface it in AdminErrorBanner
 * inside the dialog body. The row is NOT removed — the error must be legible
 * before the user closes the dialog.
 */

import { useState } from 'react';
import {
  useDeleteSubject,
  useDeleteUnit,
} from '@learnexia/api-client';
import { AdminConfirmDialog } from './AdminConfirmDialog';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export type CurriculumDeleteTarget =
  | { kind: 'subject'; id: number; name: string }
  | { kind: 'unit'; id: number; name: string; subjectId: number };

export interface CurriculumDeleteDialogProps {
  open: boolean;
  target: CurriculumDeleteTarget | null;
  onClose: () => void;
  /** Called when deletion succeeds so the parent can show a transient notice. */
  onDeleted?: (target: CurriculumDeleteTarget) => void;
}

export function CurriculumDeleteDialog({ open, target, onClose, onDeleted }: CurriculumDeleteDialogProps) {
  const [serverError, setServerError] = useState<string | null>(null);

  const deleteSubjectMutation = useDeleteSubject();
  const deleteUnitMutation = useDeleteUnit();

  const isPending = deleteSubjectMutation.isPending || deleteUnitMutation.isPending;

  const handleConfirm = async () => {
    if (!target) return;
    setServerError(null);
    try {
      if (target.kind === 'subject') {
        await deleteSubjectMutation.mutateAsync({ id: target.id });
      } else {
        await deleteUnitMutation.mutateAsync({ id: target.id, subjectId: target.subjectId });
      }
      onDeleted?.(target);
      onClose();
    } catch (err) {
      setServerError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  if (!target) return null;

  const isSubject = target.kind === 'subject';
  const title = isSubject ? strings.curriculumDeleteSubjectTitle : strings.curriculumDeleteUnitTitle;
  const bodyText = isSubject ? strings.curriculumDeleteSubjectBody : strings.curriculumDeleteUnitBody;
  const confirmLabel = isSubject ? strings.curriculumDeleteConfirmSubject : strings.curriculumDeleteConfirmUnit;

  const confirmButton = (
    <button
      onClick={() => void handleConfirm()}
      disabled={isPending}
      aria-busy={isPending}
      data-testid="curriculum-delete-confirm"
      aria-label={isSubject
        ? `Confirm removal of subject ${target.name}`
        : `Confirm removal of unit ${target.name}`}
      style={{
        height: 40, paddingLeft: 20, paddingRight: 20,
        backgroundColor: '#EF4444', borderRadius: 16, border: 'none',
        color: '#F8FAFC', fontSize: 14, fontWeight: 600,
        cursor: isPending ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: isPending ? 0.7 : 1,
      }}
    >
      {isPending ? '…' : confirmLabel}
    </button>
  );

  return (
    <AdminConfirmDialog
      open={open}
      onClose={onClose}
      variant="delete"
      title={title}
      subtitle={target.name}
      cancelLabel={strings.curriculumDeleteCancel}
      confirmButton={confirmButton}
      dialogTestId="curriculum-delete-dialog"
    >
      {/* Soft-delete notice */}
      <div style={{
        backgroundColor: 'rgba(245,158,11,0.08)', borderRadius: 8,
        border: '1px solid rgba(245,158,11,0.2)',
        padding: 16, display: 'flex', flexDirection: 'column', gap: 8,
      }}>
        <p style={{ margin: 0, fontSize: 13, color: 'var(--lx-fg2)', lineHeight: 1.5 }}>
          {bodyText}
        </p>
      </div>

      {/* "Not empty" / server error */}
      {serverError && (
        <AdminErrorBanner variant="error" message={serverError} />
      )}
    </AdminConfirmDialog>
  );
}
