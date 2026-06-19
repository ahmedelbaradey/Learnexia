'use client';

export const dynamic = 'force-dynamic';

/**
 * Lessons list page — /curriculum/subjects/[id]/units/[unitId]/lessons
 * Design Spec §S5, §S6 — P7-02.
 *
 * Four states: loading skeleton / empty / error / results.
 * Keyboard-first reorder: move-up / move-down buttons + aria-live.
 * LessonForm modal (create / edit), LessonDeleteDialog (soft-delete).
 * Inherits unit language for InheritedLanguageBadge.
 */

import { useState, useCallback, useRef } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import {
  useLessonsByUnit,
  useReorderLessons,
  useSetLessonActive,
  useUnitList,
  useSubjectList,
  type AdminSingleLessonResponse,
} from '@learnexia/api-client';
import { getStrings, ADMIN_LOCALE } from '../../../../../../../../lib/strings';
import { AdminShell } from '../../../../../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../../../../../components/AdminErrorBanner';
import { ActiveBadge } from '../../../../../../../../components/ActiveBadge';
import { DifficultyBadge } from '../../../../../../../../components/DifficultyBadge';
import { LockBadge } from '../../../../../../../../components/LockBadge';
import { InheritedLanguageBadge } from '../../../../../../../../components/InheritedLanguageBadge';
import { LessonForm } from '../../../../../../../../components/LessonForm';
import { LessonDeleteDialog } from '../../../../../../../../components/LessonDeleteDialog';

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
function ExternalLinkIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
      <polyline points="15 3 21 3 21 9" /><line x1="10" y1="14" x2="21" y2="3" />
    </svg>
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────
function SkeletonRow() {
  return (
    <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
      {[80, 160, 90, 80, 70, 70].map((w, i) => (
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

// ── Page ──────────────────────────────────────────────────────────────────────
export default function LessonsListPage() {
  const params = useParams<{ id: string; unitId: string }>();
  const subjectId = Number(params.id);
  const unitId = Number(params.unitId);

  // Fetch unit for breadcrumb name
  const { data: unitListData } = useUnitList({ subjectId, pageNumber: 1, pageSize: 50 });
  const unit = unitListData?.data?.find((u) => u.id === unitId);

  // Fetch subject for language (language is on Subject, not Unit)
  const { data: subjectListData } = useSubjectList({ pageNumber: 1, pageSize: 50 });
  const subject = subjectListData?.data?.find((s) => s.id === subjectId);

  // Lessons list
  const {
    data: lessonsData,
    isPending: lessonsLoading,
    isError: lessonsError,
    error: lessonsErrObj,
    refetch: refetchLessons,
  } = useLessonsByUnit(unitId, { pageNumber: 1, pageSize: 100 });

  const reorderMutation = useReorderLessons();
  const setActiveMutation = useSetLessonActive();

  const serverLessons: AdminSingleLessonResponse[] = lessonsData?.data ?? [];
  const serverIds = serverLessons.map((l) => l.id);

  // Local reorder state
  const [localOrder, setLocalOrder] = useState<number[] | null>(null);
  const prevServerIds = useRef<number[]>([]);
  if (!arraysEqual(prevServerIds.current, serverIds)) {
    prevServerIds.current = serverIds;
    setLocalOrder(null);
  }
  const effectiveOrder = localOrder ?? serverIds;
  const orderedLessons = effectiveOrder
    .map((id) => serverLessons.find((l) => l.id === id))
    .filter((l): l is AdminSingleLessonResponse => l !== undefined);
  const isDirty = localOrder !== null && !arraysEqual(localOrder, serverIds);

  // Reorder state
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [reorderAnnouncement, setReorderAnnouncement] = useState('');

  // Form / dialog state
  const [lessonFormOpen, setLessonFormOpen] = useState(false);
  const [editLesson, setEditLesson] = useState<AdminSingleLessonResponse | undefined>(undefined);
  const [deleteTarget, setDeleteTarget] = useState<AdminSingleLessonResponse | null>(null);

  // Active toggle state
  const [togglingId, setTogglingId] = useState<number | null>(null);
  const [toggleError, setToggleError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const handleMoveUp = (index: number) => {
    if (index === 0) return;
    const next = moveItem(effectiveOrder, index, index - 1);
    setLocalOrder(next);
    const moved = orderedLessons[index];
    if (moved) setReorderAnnouncement(
      strings.lessonReorderPosition
        .replace('{name}', moved.name)
        .replace('{N}', String(index))
        .replace('{total}', String(orderedLessons.length))
    );
  };
  const handleMoveDown = (index: number) => {
    if (index >= orderedLessons.length - 1) return;
    const next = moveItem(effectiveOrder, index, index + 1);
    setLocalOrder(next);
    const moved = orderedLessons[index];
    if (moved) setReorderAnnouncement(
      strings.lessonReorderPosition
        .replace('{name}', moved.name)
        .replace('{N}', String(index + 2))
        .replace('{total}', String(orderedLessons.length))
    );
  };

  const handleSaveOrder = useCallback(async () => {
    if (!isDirty) return;
    setReorderError(null);
    try {
      await reorderMutation.mutateAsync({ unitId, lessonIds: effectiveOrder });
      setLocalOrder(null);
    } catch (err) {
      setReorderError((err as Error).message || strings.curriculumNetworkError);
    }
  }, [isDirty, effectiveOrder, unitId, reorderMutation]);

  const handleToggleActive = async (lesson: AdminSingleLessonResponse) => {
    setTogglingId(lesson.id);
    setToggleError(null);
    try {
      await setActiveMutation.mutateAsync({ id: lesson.id, unitId, isActive: !lesson.isActive });
      setSuccessMsg(lesson.isActive ? strings.lessonDeactivateSuccess : strings.lessonActivateSuccess);
      setTimeout(() => setSuccessMsg(null), 3000);
    } catch (err) {
      setToggleError((err as Error).message || strings.curriculumNetworkError);
    } finally {
      setTogglingId(null);
    }
  };

  const openCreate = () => { setEditLesson(undefined); setLessonFormOpen(true); };
  const openEdit = (lesson: AdminSingleLessonResponse) => { setEditLesson(lesson); setLessonFormOpen(true); };

  // Table styles
  const thStyle: React.CSSProperties = {
    padding: '12px 16px', textAlign: 'start',
    fontSize: 11, fontWeight: 600, color: 'var(--lx-fg3)',
    textTransform: 'uppercase', letterSpacing: '0.06em', whiteSpace: 'nowrap',
  };
  const actionBtnStyle: React.CSSProperties = {
    height: 30, width: 30, borderRadius: 8, backgroundColor: 'transparent', border: 'none',
    cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
    color: 'var(--lx-fg3)', transition: 'color 120ms',
  };

  const unitName = unit?.name ?? `Unit #${unitId}`;
  const language = subject?.language ?? 0;

  return (
    <AdminShell title={strings.lessonPageTitle}>
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
          <span data-testid="breadcrumb-unit" style={{ fontSize: 14, color: 'var(--lx-fg1)', fontWeight: 600 }}>
            {unitName}
          </span>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <span style={{ fontSize: 14, color: 'var(--lx-fg1)', fontWeight: 600 }}>{strings.lessonPageTitle}</span>
        </nav>

        {/* Page header */}
        <div style={{
          display: 'flex', flexDirection: 'row', alignItems: 'flex-start',
          justifyContent: 'space-between', gap: 16, flexWrap: 'wrap',
        }}>
          <div>
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 6 }}>
              <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: 'var(--lx-fg1)' }}>
                {strings.lessonListHeading}
              </h1>
              {lessonsData && (
                <span style={{
                  backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                  padding: '4px 10px', fontSize: 12, color: 'var(--lx-fg3)',
                }}>
                  {lessonsData.totalCount} {strings.lessonResultCount}
                </span>
              )}
            </div>
            {/* Inherited language badge */}
            <InheritedLanguageBadge language={language} compact={false} />
          </div>

          {/* Action buttons */}
          <div style={{ display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'center' }}>
            {isDirty && (
              <button
                type="button"
                data-testid="lessons-save-order"
                onClick={() => void handleSaveOrder()}
                disabled={reorderMutation.isPending}
                aria-busy={reorderMutation.isPending}
                style={{
                  height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 12,
                  border: '1px solid rgba(245,158,11,0.4)', backgroundColor: 'rgba(245,158,11,0.08)',
                  color: '#F59E0B', fontSize: 13, fontWeight: 600, cursor: reorderMutation.isPending ? 'not-allowed' : 'pointer',
                  display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  opacity: reorderMutation.isPending ? 0.6 : 1,
                }}
              >
                <SaveIcon />
                {reorderMutation.isPending ? '…' : strings.lessonReorderSaveBtn}
              </button>
            )}
            <button
              type="button"
              data-testid="new-lesson-btn"
              onClick={openCreate}
              style={{
                height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600, cursor: 'pointer',
                display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
              }}
            >
              <PlusIcon />
              {strings.lessonNewBtn}
            </button>
          </div>
        </div>

        {/* aria-live region for reorder */}
        <span className="sr-only" aria-live="polite">{reorderAnnouncement}</span>

        {/* Error / success banners */}
        {reorderError && <AdminErrorBanner variant="error" message={reorderError} />}
        {toggleError && <AdminErrorBanner variant="error" message={toggleError} />}
        {successMsg && <AdminErrorBanner variant="warning" message={successMsg} />}

        {/* Loading */}
        {lessonsLoading && (
          <div
            role="status"
            aria-label={strings.lessonListLoadingLabel}
            data-testid="lessons-loading"
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
        {lessonsError && !lessonsLoading && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }} data-testid="lessons-error">
            <AdminErrorBanner variant="error" message={(lessonsErrObj as Error)?.message || strings.lessonListError} />
            <button
              type="button"
              onClick={() => void refetchLessons()}
              style={{
                width: 'fit-content', padding: '8px 16px', borderRadius: 12,
                border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', cursor: 'pointer', fontFamily: 'inherit', fontSize: 13,
              }}
            >
              {strings.usersListRetry}
            </button>
          </div>
        )}

        {/* Empty state */}
        {!lessonsLoading && !lessonsError && orderedLessons.length === 0 && (
          <div
            data-testid="lessons-empty"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '2px dashed var(--lx-border)', padding: 48,
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16, textAlign: 'center',
            }}
          >
            <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)' }}>
              {strings.lessonNoResults}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', maxWidth: 320 }}>
              {strings.lessonNoResultsHint}
            </p>
            <button
              type="button"
              onClick={openCreate}
              style={{
                height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid rgba(79,70,229,0.4)', backgroundColor: 'transparent',
                color: '#4F46E5', fontSize: 14, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
                display: 'flex', alignItems: 'center', gap: 6,
              }}
            >
              <PlusIcon />
              {strings.lessonNewBtn}
            </button>
          </div>
        )}

        {/* Table */}
        {!lessonsLoading && !lessonsError && orderedLessons.length > 0 && (
          <div
            data-testid="lessons-table"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', overflow: 'hidden',
            }}
          >
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <caption className="sr-only">{strings.lessonTableCaption}</caption>
              <thead>
                <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                  <th scope="col" style={thStyle}>{strings.lessonColOrder}</th>
                  <th scope="col" style={thStyle}>{strings.lessonColTitle}</th>
                  <th scope="col" style={thStyle}>{strings.lessonColDifficulty}</th>
                  <th scope="col" style={thStyle}>{strings.lessonColDuration}</th>
                  <th scope="col" style={thStyle}>{strings.lessonColLock}</th>
                  <th scope="col" style={thStyle}>{strings.lessonColActive}</th>
                  {/* P7-05 lifecycle slot */}
                  <th scope="col" style={{ ...thStyle, width: 90 }} aria-label="Lifecycle" />
                  <th scope="col" style={{ ...thStyle, width: 140 }}>
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {orderedLessons.map((lesson, index) => {
                  const isToggling = togglingId === lesson.id;
                  return (
                    <tr
                      key={lesson.id}
                      data-testid={`lesson-row-${lesson.id}`}
                      style={{
                        borderBottom: '1px solid var(--lx-border)',
                        opacity: lesson.isActive ? 1 : 0.6,
                        transition: 'background-color 120ms',
                      }}
                    >
                      {/* Order / reorder */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle', width: 110 }}>
                        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 4 }}>
                          <button
                            type="button"
                            data-testid={`lesson-${lesson.id}-move-up`}
                            aria-label={strings.lessonReorderMoveUp.replace('{name}', lesson.name)}
                            onClick={() => handleMoveUp(index)}
                            disabled={index === 0 || reorderMutation.isPending}
                            style={{
                              ...actionBtnStyle, border: '1px solid var(--lx-border)',
                              opacity: index === 0 ? 0.4 : 1,
                              cursor: index === 0 ? 'not-allowed' : 'pointer',
                            }}
                          >
                            <ChevronUpIcon />
                          </button>
                          <button
                            type="button"
                            data-testid={`lesson-${lesson.id}-move-down`}
                            aria-label={strings.lessonReorderMoveDown.replace('{name}', lesson.name)}
                            onClick={() => handleMoveDown(index)}
                            disabled={index === orderedLessons.length - 1 || reorderMutation.isPending}
                            style={{
                              ...actionBtnStyle, border: '1px solid var(--lx-border)',
                              opacity: index === orderedLessons.length - 1 ? 0.4 : 1,
                              cursor: index === orderedLessons.length - 1 ? 'not-allowed' : 'pointer',
                            }}
                          >
                            <ChevronDownIcon />
                          </button>
                          <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg3)', fontVariantNumeric: 'tabular-nums', minWidth: 20, textAlign: 'center' }}>
                            {lesson.sequenceOrder}
                          </span>
                        </div>
                      </td>

                      {/* Title */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <Link
                          href={`/curriculum/subjects/${subjectId}/units/${unitId}/lessons/${lesson.id}`}
                          data-testid={`lesson-${lesson.id}-detail-link`}
                          style={{ fontSize: 14, fontWeight: 600, color: 'var(--lx-fg1)', textDecoration: 'none', display: 'flex', alignItems: 'center', gap: 4 }}
                        >
                          {lesson.name}
                          <ExternalLinkIcon />
                        </Link>
                      </td>

                      {/* Difficulty */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <DifficultyBadge difficulty={lesson.difficulty} />
                      </td>

                      {/* Duration */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg2)' }}>
                          {lesson.estimatedMinutes > 0 ? `${lesson.estimatedMinutes} min` : '—'}
                        </span>
                      </td>

                      {/* Lock */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <LockBadge isLocked={lesson.isLocked} />
                      </td>

                      {/* Active */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <ActiveBadge isActive={lesson.isActive} />
                      </td>

                      {/* P7-05 lifecycle slot — placeholder only */}
                      <td
                        data-slot="lifecycle"
                        data-testid={`lesson-${lesson.id}-lifecycle-slot`}
                        style={{ padding: '12px 16px', verticalAlign: 'middle', minWidth: 90 }}
                      />

                      {/* Actions */}
                      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                        <div style={{ display: 'flex', flexDirection: 'row', gap: 4 }}>
                          {/* Edit */}
                          <button
                            type="button"
                            data-testid={`lesson-${lesson.id}-edit`}
                            aria-label={`Edit ${lesson.name}`}
                            onClick={() => openEdit(lesson)}
                            style={actionBtnStyle}
                          >
                            <PencilIcon />
                          </button>

                          {/* Toggle active */}
                          <button
                            type="button"
                            data-testid={`lesson-${lesson.id}-toggle-active`}
                            aria-label={lesson.isActive ? strings.lessonDetailDeactivateBtn : strings.lessonDetailActivateBtn}
                            aria-busy={isToggling}
                            onClick={() => void handleToggleActive(lesson)}
                            disabled={isToggling}
                            style={{ ...actionBtnStyle, color: lesson.isActive ? '#22C55E' : 'var(--lx-fg3)' }}
                          >
                            {isToggling ? <span style={{ fontSize: 12 }}>…</span> : lesson.isActive ? <EyeOffIcon /> : <EyeIcon />}
                          </button>

                          {/* Delete */}
                          <button
                            type="button"
                            data-testid={`lesson-${lesson.id}-delete`}
                            aria-label={`Delete ${lesson.name}`}
                            onClick={() => setDeleteTarget(lesson)}
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

      {/* LessonForm modal */}
      <LessonForm
        open={lessonFormOpen}
        onClose={() => setLessonFormOpen(false)}
        unitId={unitId}
        language={language}
        lessonCount={serverLessons.length}
        editLesson={editLesson}
      />

      {/* LessonDeleteDialog */}
      {deleteTarget && (
        <LessonDeleteDialog
          open
          onClose={() => setDeleteTarget(null)}
          lessonId={deleteTarget.id}
          lessonName={deleteTarget.name}
          unitId={unitId}
        />
      )}
    </AdminShell>
  );
}
