'use client';

/**
 * ContentBlockEditor — full block editor panel for a lesson.
 * Design Spec §8 — P7-02.
 *
 * Four states: loading / empty / error / results.
 * BlockTypePickerSheet trigger → BlockForm (create).
 * Per-card: move-up/move-down (local state), save-order button → useReorderContentBlocks.
 * Edit/Delete per card via BlockForm/DeleteBlockDialog.
 * No optimistic updates (except reorder local reorder state).
 * aria-live="polite" region announces reorder changes.
 */

import { useState, useCallback, useRef } from 'react';
import {
  useAdminLesson,
  useReorderContentBlocks,
  type AdminContentBlockDto,
} from '@learnexia/api-client';
import type { ContentLanguageValue, ContentBlockTypeValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';
import { BlockCard } from './BlockCard';
import { BlockTypePickerSheet } from './BlockTypePickerSheet';
import { BlockForm } from './BlockForm';
import { DeleteBlockDialog } from './DeleteBlockDialog';

const strings = getStrings(ADMIN_LOCALE);

// ── Icons ─────────────────────────────────────────────────────────────────────
function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}
function SaveIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
      <polyline points="17 21 17 13 7 13 7 21" /><polyline points="7 3 7 8 15 8" />
    </svg>
  );
}
function AlertCircleIcon() {
  return (
    <svg width="24" height="24" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="#EF4444" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function moveItem<T>(arr: T[], fromIndex: number, toIndex: number): T[] {
  const next = [...arr];
  const [item] = next.splice(fromIndex, 1);
  next.splice(toIndex, 0, item!);
  return next;
}

function arraysEqual(a: number[], b: number[]): boolean {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;
  return true;
}

// ── Component ─────────────────────────────────────────────────────────────────

export interface ContentBlockEditorProps {
  /** Lesson ID (must be > 0). */
  lessonId: number;
  /** Inherited language from parent unit → displayed by BlockPreview. */
  language: ContentLanguageValue;
}

interface EditTarget {
  block: AdminContentBlockDto;
}

export function ContentBlockEditor({ lessonId, language }: ContentBlockEditorProps) {
  const { data: lessonData, isPending, isError, error } = useAdminLesson(lessonId);
  const reorderMutation = useReorderContentBlocks();

  // Local reorder state — mirrors server state until save.
  // Derived from server data on mount / server refresh.
  // lessonData is AdminLessonDetailDto (not wrapped in .data again — hook unwraps the envelope).
  const serverBlocks: AdminContentBlockDto[] = lessonData?.contentBlocks ?? [];
  const serverIds = serverBlocks.map((b) => b.id);

  const [localOrder, setLocalOrder] = useState<number[] | null>(null);
  const prevServerIds = useRef<number[]>([]);

  // Sync local order when server data changes (on mount or after add/delete).
  if (!arraysEqual(prevServerIds.current, serverIds)) {
    prevServerIds.current = serverIds;
    setLocalOrder(null); // reset local order to server order
  }

  const effectiveOrder: number[] = localOrder ?? serverIds;
  const orderedBlocks: AdminContentBlockDto[] = effectiveOrder
    .map((id) => serverBlocks.find((b) => b.id === id))
    .filter((b): b is AdminContentBlockDto => b !== undefined);

  const isDirty = localOrder !== null && !arraysEqual(localOrder, serverIds);

  // Picker sheet state
  const pickerBtnRef = useRef<HTMLButtonElement>(null);
  const [pickerOpen, setPickerOpen] = useState(false);

  // BlockForm state (create / edit)
  const [createType, setCreateType] = useState<ContentBlockTypeValue | null>(null);
  const [editTarget, setEditTarget] = useState<EditTarget | null>(null);

  // DeleteBlockDialog state
  const [deleteTarget, setDeleteTarget] = useState<AdminContentBlockDto | null>(null);

  // Reorder error
  const [reorderError, setReorderError] = useState<string | null>(null);

  const handleSaveOrder = useCallback(async () => {
    if (!isDirty) return;
    setReorderError(null);
    try {
      await reorderMutation.mutateAsync({ lessonId, contentBlockIds: effectiveOrder });
      setLocalOrder(null); // reset so server data is authoritative
    } catch (err) {
      setReorderError((err as Error).message || strings.curriculumNetworkError);
    }
  }, [isDirty, effectiveOrder, lessonId, reorderMutation]);

  const handleMoveUp = (index: number) => {
    if (index === 0) return;
    setLocalOrder(moveItem(effectiveOrder, index, index - 1));
  };
  const handleMoveDown = (index: number) => {
    if (index === orderedBlocks.length - 1) return;
    setLocalOrder(moveItem(effectiveOrder, index, index + 1));
  };

  const handlePickType = (type: ContentBlockTypeValue) => {
    setCreateType(type);
  };

  // ── Render states ────────────────────────────────────────────────────────
  if (isPending) {
    return (
      <div data-testid="block-editor-loading" style={{ padding: '40px 0', textAlign: 'center' }}>
        <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>{strings.blockEditorLoadingLabel}</span>
      </div>
    );
  }

  if (isError) {
    return (
      <div data-testid="block-editor-error" style={{ padding: '24px 0', display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
        <AlertCircleIcon />
        <AdminErrorBanner variant="error" message={(error as Error)?.message || strings.blockEditorListError} />
      </div>
    );
  }

  // Count label: "N blocks"
  const countLabel = strings.blockEditorCountLabel.replace('{count}', String(orderedBlocks.length));

  return (
    <section data-testid="content-block-editor" aria-label={strings.blockEditorHeading}>
      {/* Section header */}
      <div style={{
        display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 12,
        marginBottom: 20, flexWrap: 'wrap',
      }}>
        <div style={{ flex: 1 }}>
          <h3 style={{ margin: 0, fontSize: 16, fontWeight: 700, color: 'var(--lx-fg1)' }}>
            {strings.blockEditorHeading}
          </h3>
          <p style={{ margin: 0, fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>
            {countLabel}
          </p>
        </div>

        {/* Save order button — visible when order is dirty */}
        {isDirty && (
          <button
            type="button"
            data-testid="block-editor-save-order"
            onClick={handleSaveOrder}
            disabled={reorderMutation.isPending}
            aria-busy={reorderMutation.isPending}
            style={{
              height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 12,
              border: '1px solid rgba(34,197,94,0.4)', backgroundColor: 'rgba(34,197,94,0.08)',
              color: '#22C55E', fontSize: 13, fontWeight: 600, cursor: reorderMutation.isPending ? 'not-allowed' : 'pointer',
              display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
              opacity: reorderMutation.isPending ? 0.6 : 1,
            }}
          >
            <SaveIcon />
            {reorderMutation.isPending ? '…' : strings.blockEditorSaveOrderBtn}
          </button>
        )}

        {/* Add block button with picker */}
        <div style={{ position: 'relative' }}>
          <button
            ref={pickerBtnRef}
            type="button"
            data-testid="block-editor-add-btn"
            aria-haspopup="menu"
            aria-expanded={pickerOpen}
            onClick={() => setPickerOpen((prev) => !prev)}
            style={{
              height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
              backgroundColor: '#4F46E5', border: 'none', color: '#F8FAFC',
              fontSize: 14, fontWeight: 600, cursor: 'pointer',
              display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
            }}
          >
            <PlusIcon />
            {strings.blockEditorAddBtn}
          </button>
          <BlockTypePickerSheet
            open={pickerOpen}
            onClose={() => { setPickerOpen(false); pickerBtnRef.current?.focus(); }}
            onPick={handlePickType}
          />
        </div>
      </div>

      {/* Reorder error */}
      {reorderError && (
        <div style={{ marginBottom: 12 }}>
          <AdminErrorBanner variant="error" message={reorderError} />
        </div>
      )}

      {/* aria-live region for reorder announcements */}
      <div aria-live="polite" aria-atomic="true" style={{ position: 'absolute', width: 1, height: 1, overflow: 'hidden', clip: 'rect(0,0,0,0)' }}>
        {isDirty ? `${strings.blockEditorHeading} reordered, ${orderedBlocks.length} blocks` : ''}
      </div>

      {/* Empty state */}
      {orderedBlocks.length === 0 && (
        <div data-testid="block-editor-empty" style={{
          padding: '48px 24px', textAlign: 'center',
          backgroundColor: 'var(--lx-card)', borderRadius: 16,
          border: '2px dashed var(--lx-border)',
        }}>
          <p style={{ margin: 0, fontSize: 15, fontWeight: 600, color: 'var(--lx-fg2)' }}>
            {strings.blockEditorEmpty}
          </p>
          <p style={{ margin: '8px 0 0', fontSize: 13, color: 'var(--lx-fg3)', maxWidth: 360, marginLeft: 'auto', marginRight: 'auto' }}>
            {strings.blockEditorEmptyHint}
          </p>
        </div>
      )}

      {/* Block list */}
      {orderedBlocks.length > 0 && (
        <ol
          data-testid="block-list"
          style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: 12 }}
        >
          {orderedBlocks.map((block, index) => (
            <BlockCard
              key={block.id}
              block={block}
              language={language}
              index={index}
              total={orderedBlocks.length}
              isSavingOrder={reorderMutation.isPending}
              onMoveUp={() => handleMoveUp(index)}
              onMoveDown={() => handleMoveDown(index)}
              onEdit={() => setEditTarget({ block })}
              onDelete={() => setDeleteTarget(block)}
            />
          ))}
        </ol>
      )}

      {/* BlockForm — create */}
      {createType !== null && (
        <BlockForm
          open
          onClose={() => setCreateType(null)}
          lessonId={lessonId}
          blockType={createType}
        />
      )}

      {/* BlockForm — edit */}
      {editTarget && (
        <BlockForm
          open
          onClose={() => setEditTarget(null)}
          lessonId={lessonId}
          blockType={editTarget.block.blockType}
          editBlock={editTarget.block}
        />
      )}

      {/* DeleteBlockDialog */}
      {deleteTarget && (
        <DeleteBlockDialog
          open
          onClose={() => setDeleteTarget(null)}
          blockId={deleteTarget.id}
          lessonId={lessonId}
        />
      )}
    </section>
  );
}
