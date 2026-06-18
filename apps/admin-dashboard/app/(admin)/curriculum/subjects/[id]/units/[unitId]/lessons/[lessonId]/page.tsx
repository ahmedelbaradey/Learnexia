'use client';

export const dynamic = 'force-dynamic';

/**
 * Lesson detail + ContentBlockEditor page.
 * /curriculum/subjects/[id]/units/[unitId]/lessons/[lessonId]
 * Design Spec §S7, §S8 — P7-02.
 *
 * Shows: lesson header card (name, badges, meta) + lifecycle slot (P7-05 placeholder)
 *        + actions (edit/toggle-active/delete) + ContentBlockEditor below.
 *
 * Four states: loading skeleton / not-found / error / results.
 */

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  useAdminLesson,
  useSetLessonActive,
  useUnitList,
  useSubjectList,
  type AdminSingleLessonResponse,
} from '@learnexia/api-client';
import { getStrings, ADMIN_LOCALE } from '../../../../../../../../../lib/strings';
import { AdminShell } from '../../../../../../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../../../../../../components/AdminErrorBanner';
import { ActiveBadge } from '../../../../../../../../../components/ActiveBadge';
import { DifficultyBadge } from '../../../../../../../../../components/DifficultyBadge';
import { LockBadge } from '../../../../../../../../../components/LockBadge';
import { InheritedLanguageBadge } from '../../../../../../../../../components/InheritedLanguageBadge';
import { LessonForm } from '../../../../../../../../../components/LessonForm';
import { LessonDeleteDialog } from '../../../../../../../../../components/LessonDeleteDialog';
import { ContentBlockEditor } from '../../../../../../../../../components/ContentBlockEditor';

const strings = getStrings(ADMIN_LOCALE);

// ── Icons ─────────────────────────────────────────────────────────────────────
function ArrowLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="19" y1="12" x2="5" y2="12" /><polyline points="12 19 5 12 12 5" />
    </svg>
  );
}
function PencilIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  );
}
function TrashIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
    </svg>
  );
}
function EyeIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" /><circle cx="12" cy="12" r="3" />
    </svg>
  );
}
function EyeOffIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  );
}
function AlertCircleIcon({ size = 40 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────
function SkeletonBlock({ height = 100, radius = 20 }: { height?: number; radius?: number }) {
  return (
    <div style={{
      height, borderRadius: radius,
      background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
      backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
    }} />
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────
export default function LessonDetailPage() {
  const params = useParams<{ id: string; unitId: string; lessonId: string }>();
  const router = useRouter();
  const subjectId = Number(params.id);
  const unitId = Number(params.unitId);
  const lessonId = Number(params.lessonId);

  const lessonsListHref = `/curriculum/subjects/${subjectId}/units/${unitId}/lessons`;

  // Fetch unit for breadcrumb name
  const { data: unitListData } = useUnitList({ subjectId, pageNumber: 1, pageSize: 50 });
  const unit = unitListData?.data?.find((u) => u.id === unitId);
  const unitName = unit?.name ?? `Unit #${unitId}`;

  // Fetch subject for language (language is on Subject, not Unit)
  const { data: subjectListData } = useSubjectList({ pageNumber: 1, pageSize: 50 });
  const subject = subjectListData?.data?.find((s) => s.id === subjectId);
  const language = subject?.language ?? 0;

  // Lesson detail
  const {
    data: lesson,
    isPending: lessonLoading,
    isError: lessonError,
    error: lessonErrObj,
  } = useAdminLesson(lessonId);

  const setActiveMutation = useSetLessonActive();

  // UI state
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [toggling, setToggling] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  const handleToggleActive = async () => {
    if (!lesson) return;
    setToggling(true);
    setToggleError(null);
    try {
      await setActiveMutation.mutateAsync({ id: lesson.id, unitId, isActive: !lesson.isActive });
      setSuccessMsg(lesson.isActive ? strings.lessonDeactivateSuccess : strings.lessonActivateSuccess);
      setTimeout(() => setSuccessMsg(null), 3000);
    } catch (err) {
      setToggleError((err as Error).message || strings.curriculumNetworkError);
    } finally {
      setToggling(false);
    }
  };

  const lessonNotFound = !lessonLoading && !lessonError && !lesson;

  return (
    <AdminShell title={lesson?.name ?? strings.lessonPageTitle}>
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
          <Link href={`/curriculum/subjects/${subjectId}`} data-testid="breadcrumb-subject"
            style={{ fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none' }}>
            {`Subject #${subjectId}`}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <Link href={lessonsListHref} data-testid="breadcrumb-unit"
            style={{ fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none' }}>
            {unitName}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <span data-testid="breadcrumb-lesson" style={{ fontSize: 14, color: 'var(--lx-fg1)', fontWeight: 600 }}>
            {lesson?.name ?? `Lesson #${lessonId}`}
          </span>
        </nav>

        {/* Loading */}
        {lessonLoading && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }} data-testid="lesson-header-loading">
            <SkeletonBlock height={160} />
          </div>
        )}

        {/* Not found */}
        {lessonNotFound && (
          <div
            data-testid="lesson-not-found"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', padding: 40,
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16, textAlign: 'center',
            }}
          >
            <span style={{ color: 'var(--lx-fg3)' }}><AlertCircleIcon /></span>
            <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: 'var(--lx-fg1)' }}>
              {strings.lessonNotFound}
            </h1>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)' }}>
              {strings.lessonNotFoundBody}
            </p>
            <button
              type="button"
              onClick={() => router.push(lessonsListHref)}
              style={{
                height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: 'pointer', fontFamily: 'inherit',
                display: 'flex', alignItems: 'center', gap: 8,
              }}
            >
              <ArrowLeftIcon />
              {strings.lessonPageTitle}
            </button>
          </div>
        )}

        {/* Error */}
        {lessonError && !lessonLoading && (
          <AdminErrorBanner variant="error" message={(lessonErrObj as Error)?.message || strings.lessonListError} />
        )}

        {/* Lesson header card */}
        {!lessonLoading && !lessonNotFound && lesson && (
          <div
            data-testid={`lesson-header-${lesson.id}`}
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', padding: 24,
              display: 'flex', flexDirection: 'column', gap: 16,
            }}
          >
            {/* Badges row */}
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
              <DifficultyBadge difficulty={lesson.difficulty} />
              <LockBadge isLocked={lesson.isLocked} />
              <ActiveBadge isActive={lesson.isActive} />
              <InheritedLanguageBadge language={language} compact />
              {/* P7-05 lifecycle slot */}
              <span
                data-slot="lifecycle"
                data-testid={`lesson-${lesson.id}-lifecycle-slot`}
              />
            </div>

            {/* Title + meta */}
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap' }}>
              <div>
                <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: 'var(--lx-fg1)' }}>
                  {lesson.name}
                </h1>
                <div style={{ display: 'flex', flexDirection: 'row', gap: 20, marginTop: 8 }}>
                  <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>
                    {lesson.estimatedMinutes > 0 ? `${lesson.estimatedMinutes} min` : '—'}
                  </span>
                  <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>
                    Order: {lesson.sequenceOrder}
                  </span>
                  <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>
                    Blocks: {lesson.contentBlocks?.length ?? 0}
                  </span>
                </div>
              </div>

              {/* Action buttons */}
              <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8, flexShrink: 0 }}>
                {/* Toggle active */}
                <button
                  type="button"
                  data-testid={`lesson-${lesson.id}-toggle-active`}
                  aria-label={lesson.isActive ? strings.lessonDetailDeactivateBtn : strings.lessonDetailActivateBtn}
                  aria-busy={toggling}
                  onClick={() => void handleToggleActive()}
                  disabled={toggling}
                  style={{
                    height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 16,
                    border: `1px solid ${lesson.isActive ? 'rgba(239,68,68,0.3)' : 'rgba(34,197,94,0.3)'}`,
                    backgroundColor: 'transparent',
                    color: lesson.isActive ? '#EF4444' : '#22C55E',
                    fontSize: 13, fontWeight: 500, cursor: toggling ? 'not-allowed' : 'pointer',
                    display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                    opacity: toggling ? 0.6 : 1,
                  }}
                >
                  {toggling ? '…' : lesson.isActive
                    ? <><EyeOffIcon /> {strings.lessonDetailDeactivateBtn}</>
                    : <><EyeIcon /> {strings.lessonDetailActivateBtn}</>
                  }
                </button>

                {/* Edit */}
                <button
                  type="button"
                  data-testid={`lesson-${lesson.id}-edit`}
                  aria-label={strings.lessonDetailEditBtn}
                  onClick={() => setEditOpen(true)}
                  style={{
                    height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 16,
                    border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                    color: 'var(--lx-fg2)', fontSize: 13, fontWeight: 500, cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  }}
                >
                  <PencilIcon />
                  {strings.lessonDetailEditBtn}
                </button>

                {/* Delete */}
                <button
                  type="button"
                  data-testid={`lesson-${lesson.id}-delete`}
                  aria-label={strings.lessonDetailDeleteBtn}
                  onClick={() => setDeleteOpen(true)}
                  style={{
                    height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 16,
                    border: '1px solid rgba(239,68,68,0.2)', backgroundColor: 'transparent',
                    color: '#EF4444', fontSize: 13, fontWeight: 500, cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  }}
                >
                  <TrashIcon />
                  {strings.lessonDetailDeleteBtn}
                </button>
              </div>
            </div>

            {/* Error notices */}
            {toggleError && <AdminErrorBanner variant="error" message={toggleError} />}
            {successMsg && <AdminErrorBanner variant="warning" message={successMsg} />}
          </div>
        )}

        {/* Questions manager link — navigates to the questions sub-page for this lesson */}
        {!lessonLoading && !lessonNotFound && lesson && (
          <Link
            href={`/curriculum/subjects/${subjectId}/units/${unitId}/lessons/${lessonId}/questions`}
            data-testid="lesson-manage-questions-link"
            style={{
              display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between',
              gap: 12, padding: '16px 24px',
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', textDecoration: 'none',
              color: 'var(--lx-fg1)', transition: 'border-color 120ms',
            }}
          >
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <span style={{ fontSize: 15, fontWeight: 600, color: 'var(--lx-fg1)' }}>
                {strings.questionPageTitle}
              </span>
              <span style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>
                {strings.questionBreadcrumbLabel}
              </span>
            </div>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
              stroke="var(--lx-fg3)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polyline points="9 18 15 12 9 6" />
            </svg>
          </Link>
        )}

        {/* ContentBlockEditor */}
        {!lessonLoading && !lessonNotFound && lesson && (
          <div
            data-testid="content-block-editor-section"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', padding: 24,
            }}
          >
            <ContentBlockEditor lessonId={lessonId} language={language} />
          </div>
        )}
      </div>

      {/* LessonForm edit modal */}
      {lesson && (
        <LessonForm
          open={editOpen}
          onClose={() => setEditOpen(false)}
          unitId={unitId}
          language={language}
          lessonCount={0}
          editLesson={lesson as AdminSingleLessonResponse}
        />
      )}

      {/* LessonDeleteDialog */}
      {lesson && (
        <LessonDeleteDialog
          open={deleteOpen}
          onClose={() => setDeleteOpen(false)}
          lessonId={lesson.id}
          lessonName={lesson.name}
          unitId={unitId}
          backHref={lessonsListHref}
        />
      )}
    </AdminShell>
  );
}
