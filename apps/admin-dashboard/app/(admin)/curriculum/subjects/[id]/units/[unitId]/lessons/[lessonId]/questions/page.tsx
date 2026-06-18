'use client';

export const dynamic = 'force-dynamic';

/**
 * Questions list page — /curriculum/subjects/[id]/units/[unitId]/lessons/[lessonId]/questions
 * Design Spec §S2, §S3, §S4 — P7-04.
 *
 * Four states: loading skeleton / empty / error / results.
 * Keyboard-first reorder: move-up / move-down + Save Order button.
 * QuestionEditor modal (create / edit).
 * DeleteQuestionDialog / DeactivateQuestionDialog / ActivateQuestionDialog per row.
 * Lifecycle slot: empty placeholder (AdminQuestionDto lacks lifecycleState).
 */

import { useState, useCallback, useRef } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import {
  useAdminQuestionsByLesson,
  useReorderQuestions,
  type AdminQuestionDto,
} from '@learnexia/api-client';
import { getStrings, ADMIN_LOCALE } from '../../../../../../../../../../lib/strings';
import { AdminShell } from '../../../../../../../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../../../../../../../components/AdminErrorBanner';
import { ActiveBadge } from '../../../../../../../../../../components/ActiveBadge';
import { DifficultyBadge } from '../../../../../../../../../../components/DifficultyBadge';
import { QuestionTypeBadge } from '../../../../../../../../../../components/QuestionTypeBadge';
import { QuestionEditor } from '../../../../../../../../../../components/QuestionEditor';
import { DeleteQuestionDialog } from '../../../../../../../../../../components/DeleteQuestionDialog';
import { DeactivateQuestionDialog } from '../../../../../../../../../../components/DeactivateQuestionDialog';
import { ActivateQuestionDialog } from '../../../../../../../../../../components/ActivateQuestionDialog';

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
function ChevronUpIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="18 15 12 9 6 15" />
    </svg>
  );
}
function ChevronDownIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="6 9 12 15 18 9" />
    </svg>
  );
}
function SaveIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
      <polyline points="17 21 17 13 7 13 7 21" /><polyline points="7 3 7 8 15 8" />
    </svg>
  );
}
function PencilIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  );
}
function TrashIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
    </svg>
  );
}
function EyeIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" /><circle cx="12" cy="12" r="3" />
    </svg>
  );
}
function EyeOffIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  );
}
function ArrowLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
    </svg>
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────
function SkeletonRow() {
  return (
    <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
      {[80, 220, 100, 90, 70].map((w, i) => (
        <td key={i} style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <div style={{
            height: 14, width: w, maxWidth: '100%', borderRadius: 6,
            background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
          }} />
        </td>
      ))}
    </tr>
  );
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function moveItem<T>(arr: T[], from: number, to: number): T[] {
  const next = [...arr];
  const [el] = next.splice(from, 1);
  next.splice(to, 0, el!);
  return next;
}
function arraysEqual(a: number[], b: number[]): boolean {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;
  return true;
}

// ── Truncate question text for table display ───────────────────────────────────
function truncate(text: string, max = 80): string {
  if (text.length <= max) return text;
  return text.slice(0, max) + '…';
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function QuestionsListPage() {
  const params = useParams<{ id: string; unitId: string; lessonId: string }>();
  const subjectId = Number(params.id);
  const unitId = Number(params.unitId);
  const lessonId = Number(params.lessonId);

  // Questions list
  const {
    data: questionsData,
    isPending: questionsLoading,
    isError: questionsIsError,
    error: questionsErrObj,
    refetch: refetchQuestions,
  } = useAdminQuestionsByLesson(lessonId);

  const reorderMutation = useReorderQuestions();

  // useAdminQuestionsByLesson returns AdminQuestionDto[] directly (not a paginated wrapper)
  const serverQuestions: AdminQuestionDto[] = questionsData ?? [];
  const serverIds = serverQuestions.map((q) => q.id);

  // Local reorder state
  const [localOrder, setLocalOrder] = useState<number[] | null>(null);
  const prevServerIds = useRef<number[]>([]);
  if (!arraysEqual(prevServerIds.current, serverIds)) {
    prevServerIds.current = serverIds;
    setLocalOrder(null);
  }
  const effectiveOrder = localOrder ?? serverIds;
  const orderedQuestions = effectiveOrder
    .map((id) => serverQuestions.find((q) => q.id === id))
    .filter((q): q is AdminQuestionDto => q !== undefined);
  const isDirty = localOrder !== null && !arraysEqual(localOrder, serverIds);

  // Reorder state
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [reorderAnnouncement, setReorderAnnouncement] = useState('');
  const [orderSuccess, setOrderSuccess] = useState<string | null>(null);

  // Editor state
  const [editorOpen, setEditorOpen] = useState(false);
  const [editQuestion, setEditQuestion] = useState<AdminQuestionDto | undefined>(undefined);

  // Per-row dialog state
  const [deleteTarget, setDeleteTarget] = useState<AdminQuestionDto | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<AdminQuestionDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<AdminQuestionDto | null>(null);

  const handleMoveUp = (index: number) => {
    if (index === 0) return;
    const newIndex = index - 1;
    const next = moveItem(effectiveOrder, index, newIndex);
    setLocalOrder(next);
    // Announce 1-based new position: newIndex + 1
    setReorderAnnouncement(
      strings.questionMovedAnnouncement
        .replace('{N}', String(newIndex + 1))
        .replace('{total}', String(orderedQuestions.length))
    );
  };

  const handleMoveDown = (index: number) => {
    if (index >= orderedQuestions.length - 1) return;
    const newIndex = index + 1;
    const next = moveItem(effectiveOrder, index, newIndex);
    setLocalOrder(next);
    // Announce 1-based new position: newIndex + 1
    setReorderAnnouncement(
      strings.questionMovedAnnouncement
        .replace('{N}', String(newIndex + 1))
        .replace('{total}', String(orderedQuestions.length))
    );
  };

  const handleSaveOrder = useCallback(async () => {
    if (!isDirty) return;
    setReorderError(null);
    setOrderSuccess(null);
    try {
      await reorderMutation.mutateAsync({ lessonId, questionIds: effectiveOrder });
      setLocalOrder(null);
      setOrderSuccess(strings.questionsOrderSaved);
      setTimeout(() => setOrderSuccess(null), 3000);
    } catch (err) {
      setReorderError((err as Error).message || strings.questionsOrderError);
    }
  }, [isDirty, effectiveOrder, lessonId, reorderMutation]);

  const openCreate = () => { setEditQuestion(undefined); setEditorOpen(true); };
  const openEdit = (q: AdminQuestionDto) => { setEditQuestion(q); setEditorOpen(true); };

  // Table styles
  const thStyle: React.CSSProperties = {
    padding: '12px 16px', textAlign: 'start',
    fontSize: 11, fontWeight: 600, color: 'var(--lx-fg3)',
    textTransform: 'uppercase', letterSpacing: '0.06em', whiteSpace: 'nowrap',
  };
  const actionBtnStyle: React.CSSProperties = {
    height: 30, width: 30, borderRadius: 8, backgroundColor: 'transparent', border: 'none',
    cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
    color: 'var(--lx-fg3)',
  };

  return (
    <AdminShell title={strings.questionPageTitle}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>

        {/* Breadcrumb */}
        <nav aria-label="breadcrumb" style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8, flexWrap: 'wrap' }}>
          <Link
            href="/curriculum/subjects"
            style={{ fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none', display: 'flex', alignItems: 'center', gap: 4 }}
          >
            <ArrowLeftIcon />
            {strings.subjectsDetailBreadcrumb}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <Link
            href={`/curriculum/subjects/${subjectId}`}
            data-testid="breadcrumb-subject"
            style={{ fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none' }}
          >
            {`Subject #${subjectId}`}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <Link
            href={`/curriculum/subjects/${subjectId}/units/${unitId}/lessons`}
            data-testid="breadcrumb-unit"
            style={{ fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none' }}
          >
            {strings.lessonPageTitle}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <Link
            href={`/curriculum/subjects/${subjectId}/units/${unitId}/lessons/${lessonId}`}
            data-testid="breadcrumb-lesson"
            style={{ fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none' }}
          >
            {strings.questionLessonContextLabel} {strings.questionLessonIdLabel.replace('{id}', String(lessonId))}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <span style={{ fontSize: 14, color: 'var(--lx-fg1)', fontWeight: 600 }}>
            {strings.questionBreadcrumbLabel}
          </span>
        </nav>

        {/* Page header */}
        <div style={{
          display: 'flex', flexDirection: 'row', alignItems: 'flex-start',
          justifyContent: 'space-between', gap: 16, flexWrap: 'wrap',
        }}>
          <div>
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 4 }}>
              <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: 'var(--lx-fg1)' }}>
                {strings.questionPageTitle}
              </h1>
              {questionsData && (
                <span style={{
                  backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                  padding: '4px 10px', fontSize: 12, color: 'var(--lx-fg3)',
                }}>
                  {serverQuestions.length} {strings.questionsResultCount}
                </span>
              )}
            </div>
          </div>

          {/* Action buttons */}
          <div style={{ display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'center' }}>
            {isDirty && (
              <button
                type="button"
                data-testid="questions-save-order"
                onClick={() => void handleSaveOrder()}
                disabled={reorderMutation.isPending}
                aria-busy={reorderMutation.isPending}
                style={{
                  height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 12,
                  border: '1px solid rgba(245,158,11,0.4)', backgroundColor: 'rgba(245,158,11,0.08)',
                  color: '#F59E0B', fontSize: 13, fontWeight: 600,
                  cursor: reorderMutation.isPending ? 'not-allowed' : 'pointer',
                  display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  opacity: reorderMutation.isPending ? 0.6 : 1,
                }}
              >
                <SaveIcon />
                {reorderMutation.isPending ? '…' : strings.questionsSaveOrder}
              </button>
            )}
            <button
              type="button"
              data-testid="new-question-btn"
              onClick={openCreate}
              style={{
                height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600, cursor: 'pointer',
                display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
              }}
            >
              <PlusIcon />
              {strings.questionsNewQuestion}
            </button>
          </div>
        </div>

        {/* aria-live region for reorder */}
        <span className="sr-only" aria-live="polite">{reorderAnnouncement}</span>

        {/* Error / success banners */}
        {reorderError && <AdminErrorBanner variant="error" message={reorderError} />}
        {orderSuccess && <AdminErrorBanner variant="warning" message={orderSuccess} />}

        {/* Loading */}
        {questionsLoading && (
          <div
            role="status"
            aria-label={strings.questionsLoadingLabel}
            data-testid="questions-loading"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', overflow: 'hidden',
            }}
          >
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <tbody>
                {Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)}
              </tbody>
            </table>
          </div>
        )}

        {/* Error */}
        {questionsIsError && !questionsLoading && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }} data-testid="questions-error">
            <AdminErrorBanner
              variant="error"
              message={(questionsErrObj as Error)?.message || strings.questionsListError}
            />
            <button
              type="button"
              data-testid="questions-retry"
              onClick={() => void refetchQuestions()}
              style={{
                width: 'fit-content', padding: '8px 16px', borderRadius: 12,
                border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', cursor: 'pointer', fontFamily: 'inherit', fontSize: 13,
              }}
            >
              {strings.questionsRetry}
            </button>
          </div>
        )}

        {/* Empty state */}
        {!questionsLoading && !questionsIsError && orderedQuestions.length === 0 && (
          <div
            data-testid="questions-empty"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '2px dashed var(--lx-border)', padding: 48,
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16, textAlign: 'center',
            }}
          >
            <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)' }}>
              {strings.questionsEmpty}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', maxWidth: 320 }}>
              {strings.questionsEmptyHint}
            </p>
            <button
              type="button"
              data-testid="questions-empty-create-btn"
              onClick={openCreate}
              style={{
                height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid rgba(79,70,229,0.4)', backgroundColor: 'transparent',
                color: '#4F46E5', fontSize: 14, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
                display: 'flex', alignItems: 'center', gap: 6,
              }}
            >
              <PlusIcon />
              {strings.questionsNewQuestion}
            </button>
          </div>
        )}

        {/* Table */}
        {!questionsLoading && !questionsIsError && orderedQuestions.length > 0 && (
          <div
            data-testid="questions-table"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', overflow: 'hidden',
            }}
          >
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <caption className="sr-only">{strings.questionPageTitle}</caption>
              <thead>
                <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                  <th scope="col" style={thStyle}>{strings.questionsColOrder}</th>
                  <th scope="col" style={thStyle}>{strings.questionsColQuestion}</th>
                  <th scope="col" style={thStyle}>{strings.questionsColType}</th>
                  <th scope="col" style={thStyle}>{strings.questionsColDifficulty}</th>
                  <th scope="col" style={thStyle}>{strings.questionsColActive}</th>
                  {/* Lifecycle slot — placeholder */}
                  <th scope="col" style={{ ...thStyle, width: 1 }} aria-hidden="true" />
                  <th scope="col" style={{ ...thStyle, width: 140 }}>
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {orderedQuestions.map((question, index) => {
                  const editLabel = strings.questionEditAriaLabel.replace('{N}', String(index + 1));
                  const deleteLabel = strings.questionDeleteAriaLabel.replace('{N}', String(index + 1));
                  const activateLabel = strings.questionActivateAriaLabel;
                  const deactivateLabel = strings.questionDeactivateAriaLabel;
                  return (
                    <tr
                      key={question.id}
                      data-testid={`question-row-${question.id}`}
                      style={{
                        borderBottom: '1px solid var(--lx-border)',
                        opacity: question.isActive ? 1 : 0.6,
                      }}
                    >
                      {/* Order / reorder */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle', width: 120 }}>
                        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 4 }}>
                          <button
                            type="button"
                            data-testid={`question-${question.id}-move-up`}
                            aria-label={strings.questionMoveUpAriaLabel}
                            onClick={() => handleMoveUp(index)}
                            disabled={index === 0 || reorderMutation.isPending}
                            style={{
                              ...actionBtnStyle,
                              border: '1px solid var(--lx-border)',
                              opacity: index === 0 ? 0.4 : 1,
                              cursor: index === 0 ? 'not-allowed' : 'pointer',
                            }}
                          >
                            <ChevronUpIcon />
                          </button>
                          <button
                            type="button"
                            data-testid={`question-${question.id}-move-down`}
                            aria-label={strings.questionMoveDownAriaLabel}
                            onClick={() => handleMoveDown(index)}
                            disabled={index === orderedQuestions.length - 1 || reorderMutation.isPending}
                            style={{
                              ...actionBtnStyle,
                              border: '1px solid var(--lx-border)',
                              opacity: index === orderedQuestions.length - 1 ? 0.4 : 1,
                              cursor: index === orderedQuestions.length - 1 ? 'not-allowed' : 'pointer',
                            }}
                          >
                            <ChevronDownIcon />
                          </button>
                          <span
                            dir="ltr"
                            style={{ fontSize: 13, color: 'var(--lx-fg3)', fontVariantNumeric: 'tabular-nums', minWidth: 20, textAlign: 'center' }}
                          >
                            {question.sequenceOrder}
                          </span>
                        </div>
                      </td>

                      {/* Question text (truncated) */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <button
                          type="button"
                          aria-label={editLabel}
                          data-testid={`question-${question.id}-text-btn`}
                          onClick={() => openEdit(question)}
                          style={{
                            background: 'none', border: 'none', padding: 0, cursor: 'pointer',
                            textAlign: 'start', fontSize: 14, color: 'var(--lx-fg1)', fontFamily: 'inherit',
                            fontWeight: 500, lineHeight: 1.4,
                          }}
                          dir="auto"
                        >
                          {truncate(question.questionText)}
                        </button>
                      </td>

                      {/* Type badge */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <QuestionTypeBadge questionType={question.questionType} />
                      </td>

                      {/* Difficulty badge */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <DifficultyBadge difficulty={question.difficulty} />
                      </td>

                      {/* Active badge */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <ActiveBadge isActive={question.isActive} />
                      </td>

                      {/* Lifecycle slot — empty placeholder */}
                      <td
                        data-slot="lifecycle"
                        data-testid={`question-${question.id}-lifecycle-slot`}
                        style={{ padding: '12px 16px', verticalAlign: 'middle', width: 1 }}
                      />

                      {/* Actions */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <div style={{ display: 'flex', flexDirection: 'row', gap: 4 }}>
                          {/* Edit */}
                          <button
                            type="button"
                            data-testid={`question-${question.id}-edit`}
                            aria-label={editLabel}
                            onClick={() => openEdit(question)}
                            style={actionBtnStyle}
                          >
                            <PencilIcon />
                          </button>

                          {/* Toggle active */}
                          <button
                            type="button"
                            data-testid={`question-${question.id}-toggle-active`}
                            aria-label={question.isActive ? deactivateLabel : activateLabel}
                            onClick={() => {
                              if (question.isActive) setDeactivateTarget(question);
                              else setActivateTarget(question);
                            }}
                            style={{ ...actionBtnStyle, color: question.isActive ? '#22C55E' : 'var(--lx-fg3)' }}
                          >
                            {question.isActive ? <EyeOffIcon /> : <EyeIcon />}
                          </button>

                          {/* Delete */}
                          <button
                            type="button"
                            data-testid={`question-${question.id}-delete`}
                            aria-label={deleteLabel}
                            onClick={() => setDeleteTarget(question)}
                            style={{ ...actionBtnStyle, color: 'var(--lx-danger)' }}
                          >
                            <TrashIcon />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* QuestionEditor modal (create / edit) */}
      <QuestionEditor
        open={editorOpen}
        onClose={() => setEditorOpen(false)}
        lessonId={lessonId}
        editQuestion={editQuestion}
      />

      {/* Dialogs */}
      {deleteTarget && (
        <DeleteQuestionDialog
          open
          onClose={() => setDeleteTarget(null)}
          questionId={deleteTarget.id}
          lessonId={lessonId}
        />
      )}
      {deactivateTarget && (
        <DeactivateQuestionDialog
          open
          onClose={() => setDeactivateTarget(null)}
          questionId={deactivateTarget.id}
          lessonId={lessonId}
        />
      )}
      {activateTarget && (
        <ActivateQuestionDialog
          open
          onClose={() => setActivateTarget(null)}
          questionId={activateTarget.id}
          lessonId={lessonId}
        />
      )}
    </AdminShell>
  );
}
