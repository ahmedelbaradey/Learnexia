'use client';

export const dynamic = 'force-dynamic';

/**
 * Subject detail page — `/curriculum/subjects/[id]` (P7-01-FE-3/4/5).
 *
 * Shows a subject header card (code, language, grade, active, P7-05 slot) and
 * a units table under it. Keyboard-first reorder on units (same pattern as the
 * subjects list). Four states: loading / error / not-found / results.
 *
 * Design Spec §S3.
 */

import { useState, useEffect, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  useSubjectList,
  useUnitList,
  useAdminGrades,
  useSetSubjectActive,
  useSetUnitActive,
  useReorderUnits,
  usePreviewContent,
  type SubjectDto,
  type UnitDto,
} from '@learnexia/api-client';
import {
  VERSIONED_ENTITY_TYPE,
  type LifecycleStateValue,
} from '@learnexia/shared/constants';
import { AdminShell } from '../../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../../components/AdminErrorBanner';
import { SubjectCodeBadge } from '../../../../../components/SubjectCodeBadge';
import { LanguageBadge } from '../../../../../components/LanguageBadge';
import { ActiveBadge } from '../../../../../components/ActiveBadge';
import { UnitForm } from '../../../../../components/UnitForm';
import { SubjectForm } from '../../../../../components/SubjectForm';
import { CurriculumDeleteDialog, type CurriculumDeleteTarget } from '../../../../../components/CurriculumDeleteDialog';
import { getStrings, ADMIN_LOCALE } from '../../../../../lib/strings';
import { LifecycleBadge } from '../../../../../components/LifecycleBadge';
import { CurriculumLifecycleControl } from '../../../../../components/CurriculumLifecycleControl';
import { VersionHistory } from '../../../../../components/VersionHistory';

const strings = getStrings(ADMIN_LOCALE);

// ── SubjectLifecycleBadge — fetches state via GET /Preview (DG-2) ─────────────

/**
 * DG-2: SubjectDto does NOT carry a lifecycleState field.
 * See the same pattern in subjects/page.tsx for full DG-2 explanation.
 */
function SubjectLifecycleBadge({ subjectId }: { subjectId: number }) {
  const { data, isLoading } = usePreviewContent(VERSIONED_ENTITY_TYPE.Subject, subjectId);

  if (isLoading) {
    return (
      <div
        style={{
          height: 20, width: 68, borderRadius: 9999,
          background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
          backgroundSize: '200px 100%', animation: 'lx-shimmer 1400ms linear infinite',
          display: 'inline-block',
        }}
      />
    );
  }

  return (
    <LifecycleBadge
      state={data?.currentState as LifecycleStateValue | null | undefined}
      strings={strings}
      testId={`subject-${subjectId}-lifecycle-badge`}
    />
  );
}

// ── SubjectLifecyclePanel — lifecycle control + version history ────────────────

/**
 * Mounts CurriculumLifecycleControl + VersionHistory for the subject.
 * Both share the same usePreviewContent fetch (staleTime keeps it free from cache).
 * Rendered below the subject header card as a full-width panel.
 */
function SubjectLifecyclePanel({ subjectId }: { subjectId: number }) {
  const { data } = usePreviewContent(VERSIONED_ENTITY_TYPE.Subject, subjectId);
  const currentState = data?.currentState as LifecycleStateValue | null | undefined;

  // currentState not yet known (still loading or no published version): render
  // VersionHistory only so we can still show "never published" empty state.
  return (
    <div
      data-testid={`subject-${subjectId}-lifecycle-panel`}
      style={{ display: 'flex', flexDirection: 'column', gap: 16 }}
    >
      {/* Lifecycle action control (publish / unpublish / archive / restore) */}
      {currentState != null && (
        <div
          data-testid={`subject-${subjectId}-lifecycle-control`}
          style={{
            backgroundColor: 'var(--lx-card)', borderRadius: 20,
            border: '1px solid var(--lx-border)', padding: '16px 20px',
          }}
        >
          <CurriculumLifecycleControl
            entityType={VERSIONED_ENTITY_TYPE.Subject}
            entityId={subjectId}
            currentState={currentState}
            language={data?.language ?? 0}
            strings={strings}
          />
        </div>
      )}

      {/* Version history panel */}
      <div
        data-testid={`subject-${subjectId}-version-history`}
        style={{
          backgroundColor: 'var(--lx-card)', borderRadius: 20,
          border: '1px solid var(--lx-border)', overflow: 'hidden',
        }}
      >
        <VersionHistory
          entityType={VERSIONED_ENTITY_TYPE.Subject}
          entityId={subjectId}
          strings={strings}
        />
      </div>
    </div>
  );
}

// ── UnitLifecycleBadge — fetches state via GET /Preview (DG-2) ────────────────

/**
 * DG-2: UnitDto does NOT carry a lifecycleState field.
 * Inline badge only (no per-row lifecycle control — impractical inline).
 * Full lifecycle control for units is reachable via the subject preview page
 * (/curriculum/preview/unit/{id}) or the landing page demo panel.
 */
function UnitLifecycleBadge({ unitId }: { unitId: number }) {
  const { data, isLoading } = usePreviewContent(VERSIONED_ENTITY_TYPE.Unit, unitId);

  if (isLoading) {
    return (
      <div
        style={{
          height: 20, width: 68, borderRadius: 9999,
          background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
          backgroundSize: '200px 100%', animation: 'lx-shimmer 1400ms linear infinite',
          display: 'inline-block',
        }}
      />
    );
  }

  return (
    <LifecycleBadge
      state={data?.currentState as LifecycleStateValue | null | undefined}
      strings={strings}
      testId={`unit-${unitId}-lifecycle-badge`}
    />
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────

function SkeletonCard({ height = 120 }: { height?: number }) {
  return (
    <div style={{
      height, borderRadius: 20,
      background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
      backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
    }} />
  );
}

function SkeletonRow() {
  return (
    <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
      {[60, 200, 90, 80, 100].map((w, i) => (
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

// ── Inline icons ──────────────────────────────────────────────────────────────

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
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
      <polyline points="3 6 5 6 21 6" /><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
    </svg>
  );
}
function EyeIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      <circle cx="12" cy="12" r="3" />
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
function ArrowLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="19" y1="12" x2="5" y2="12" />
      <polyline points="12 19 5 12 12 5" />
    </svg>
  );
}
function AlertCircleIcon() {
  return (
    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" />
      <line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}
function LayersIcon() {
  return (
    <svg width="36" height="36" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 2 7 12 12 22 7 12 2" />
      <polyline points="2 17 12 22 22 17" />
      <polyline points="2 12 12 17 22 12" />
    </svg>
  );
}

// ── Subject detail page ───────────────────────────────────────────────────────

export default function SubjectDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const subjectId = Number(params.id);

  // Fetch subject by listing with a tiny page — use subjects list hook with an id filter
  // (API: GET /api/learning/Subjects/List?gradeId= with no grade means all).
  // We'll use the list and find by id.
  const { data: subjectListData, isLoading: subjectLoading, isError: subjectError } = useSubjectList({
    pageNumber: 1,
  });

  // Units
  const {
    data: unitsData,
    isLoading: unitsLoading,
    isError: unitsError,
    refetch: refetchUnits,
    isFetching: unitsFetching,
  } = useUnitList({ subjectId, pageNumber: 1, pageSize: 50 });

  // Local state
  const [localUnits, setLocalUnits] = useState<UnitDto[] | null>(null);
  const [reorderDirty, setReorderDirty] = useState(false);
  const [reorderSaving, setReorderSaving] = useState(false);
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [reorderAnnouncement, setReorderAnnouncement] = useState('');

  const [unitFormOpen, setUnitFormOpen] = useState(false);
  const [editUnit, setEditUnit] = useState<UnitDto | undefined>(undefined);

  const [deleteTarget, setDeleteTarget] = useState<CurriculumDeleteTarget | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  const [togglingUnitId, setTogglingUnitId] = useState<number | null>(null);
  const [unitToggleError, setUnitToggleError] = useState<string | null>(null);
  const [deleteSuccess, setDeleteSuccess] = useState<string | null>(null);

  // Subject active toggle state
  const [togglingSubject, setTogglingSubject] = useState(false);
  const [subjectToggleError, setSubjectToggleError] = useState<string | null>(null);

  // Edit subject
  const [subjectEditOpen, setSubjectEditOpen] = useState(false);

  const setSubjectActiveHook = useSetSubjectActive();
  const setUnitActiveHook = useSetUnitActive();
  const reorderUnitsHook = useReorderUnits();

  // Grades — for displaying gradeNumber instead of gradeId (nit fix)
  const { data: gradesData } = useAdminGrades();
  const grades = gradesData?.data ?? [];

  // Sync local units from server when not dirty
  useEffect(() => {
    if (unitsData && !reorderDirty) {
      setLocalUnits(unitsData.data ?? null);
    }
  }, [unitsData, reorderDirty]);

  // Find the subject from list data
  const subject: SubjectDto | undefined = subjectListData?.data?.find((s) => s.id === subjectId);
  const subjectNotFound = !subjectLoading && !subjectError && subjectListData && !subject;

  const units = localUnits ?? unitsData?.data ?? [];

  const handleUnitMoveUp = (index: number) => {
    if (index === 0 || !localUnits) return;
    const newOrder = [...localUnits];
    const tmp = newOrder[index - 1] as UnitDto;
    newOrder[index - 1] = newOrder[index] as UnitDto;
    newOrder[index] = tmp;
    setLocalUnits(newOrder);
    setReorderDirty(true);
    setReorderAnnouncement(`${(newOrder[index - 1] as UnitDto).name} ${strings.reorderPosition} ${index} of ${newOrder.length}`);
  };

  const handleUnitMoveDown = (index: number) => {
    if (!localUnits || index >= localUnits.length - 1) return;
    const newOrder = [...localUnits];
    const tmp = newOrder[index] as UnitDto;
    newOrder[index] = newOrder[index + 1] as UnitDto;
    newOrder[index + 1] = tmp;
    setLocalUnits(newOrder);
    setReorderDirty(true);
    setReorderAnnouncement(`${(newOrder[index + 1] as UnitDto).name} ${strings.reorderPosition} ${index + 2} of ${newOrder.length}`);
  };

  const handleSaveUnitOrder = async () => {
    if (!localUnits) return;
    setReorderSaving(true);
    setReorderError(null);
    try {
      await reorderUnitsHook.mutateAsync({ unitIds: localUnits.map((u) => u.id), subjectId });
      setReorderDirty(false);
    } catch {
      setReorderError(strings.reorderErrorMsg);
    } finally {
      setReorderSaving(false);
    }
  };

  const handleToggleSubjectActive = async () => {
    if (!subject) return;
    setTogglingSubject(true);
    setSubjectToggleError(null);
    try {
      await setSubjectActiveHook.mutateAsync({ id: subject.id, isActive: !subject.isActive });
    } catch {
      setSubjectToggleError(strings.subjectToggleError);
    } finally {
      setTogglingSubject(false);
    }
  };

  const handleToggleUnitActive = async (unit: UnitDto) => {
    setTogglingUnitId(unit.id);
    setUnitToggleError(null);
    try {
      await setUnitActiveHook.mutateAsync({ id: unit.id, isActive: !unit.isActive, subjectId });
    } catch {
      setUnitToggleError(strings.unitToggleError);
    } finally {
      setTogglingUnitId(null);
    }
  };

  const openEditUnit = useCallback((unit: UnitDto) => {
    setEditUnit(unit);
    setUnitFormOpen(true);
  }, []);

  const openCreateUnit = useCallback(() => {
    setEditUnit(undefined);
    setUnitFormOpen(true);
  }, []);

  const openDeleteDialog = useCallback((target: CurriculumDeleteTarget) => {
    setDeleteTarget(target);
    setDeleteDialogOpen(true);
  }, []);

  // Shared styles
  const actionBtnStyle: React.CSSProperties = {
    height: 32, width: 32, borderRadius: 8, backgroundColor: 'transparent',
    border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
    color: 'var(--lx-fg3)', opacity: 0.7, transition: 'color 120ms, opacity 120ms',
    outline: 'none',
  };

  const thStyle: React.CSSProperties = {
    padding: '12px 16px', textAlign: 'start',
    fontSize: 11, fontWeight: 600, color: 'var(--lx-fg3)',
    textTransform: 'uppercase', letterSpacing: '0.06em', whiteSpace: 'nowrap',
  };

  return (
    <AdminShell title={subject?.name ?? strings.curriculumDetailTitle}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>

        {/* Breadcrumb */}
        <nav aria-label="breadcrumb" style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <Link
            href="/curriculum/subjects"
            aria-label={strings.subjectsDetailBreadcrumb}
            style={{
              display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 4,
              fontSize: 14, color: 'var(--lx-fg3)', textDecoration: 'none',
              transition: 'color 120ms', fontFamily: 'inherit',
            }}
          >
            <ArrowLeftIcon />
            {strings.subjectsDetailBreadcrumb}
          </Link>
          <span style={{ fontSize: 14, color: 'var(--lx-fg3)' }}>/</span>
          <span style={{ fontSize: 14, color: 'var(--lx-fg1)', fontWeight: 600 }}>
            {subject?.name ?? `#${subjectId}`}
          </span>
        </nav>

        {/* Loading state for subject header */}
        {subjectLoading && <SkeletonCard height={140} />}

        {/* Not found state */}
        {subjectNotFound && (
          <div
            data-testid="subject-not-found"
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', padding: 40,
              display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16, textAlign: 'center',
            }}
          >
            <span style={{ color: 'var(--lx-fg3)' }}><AlertCircleIcon /></span>
            <h1 style={{ margin: 0, fontSize: 20, fontWeight: 700, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
              {strings.subjectsDetailNotFound}
            </h1>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)' }}>
              {strings.subjectsDetailNotFoundBody}
            </p>
            <button
              onClick={() => router.push('/curriculum/subjects')}
              style={{
                display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8,
                height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: 'pointer', fontFamily: 'inherit',
              }}
            >
              <ArrowLeftIcon />
              {strings.subjectsDetailBackBtn}
            </button>
          </div>
        )}

        {/* Subject header card */}
        {!subjectLoading && !subjectNotFound && subject && (
          <div
            data-testid={`subject-header-${subject.id}`}
            style={{
              backgroundColor: 'var(--lx-card)', borderRadius: 20,
              border: '1px solid var(--lx-border)', padding: 24,
              display: 'flex', flexDirection: 'column', gap: 16,
            }}
          >
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'flex-start', justifyContent: 'space-between', gap: 16 }}>
              {/* Left: identity */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 10 }}>
                  <SubjectCodeBadge code={subject.subjectCode} />
                  <LanguageBadge language={subject.language} />
                  <ActiveBadge isActive={subject.isActive} />
                  {/* P7-05 lifecycle badge slot (DG-2) */}
                  <span
                    data-slot="lifecycle"
                    data-testid={`subject-${subject.id}-lifecycle-slot`}
                    style={{ minWidth: 80, display: 'inline-flex', alignItems: 'center' }}
                  >
                    <SubjectLifecycleBadge subjectId={subject.id} />
                  </span>
                </div>
                <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
                  {subject.name}
                </h1>
                <div style={{ display: 'flex', flexDirection: 'row', gap: 20 }}>
                  <span style={{ fontSize: 13, color: 'var(--lx-fg3)' }} dir="ltr">
                    {strings.subjectsDetailGradePrefix}{' '}
                    {grades.find((g) => g.id === subject.gradeId)?.gradeNumber ?? subject.gradeId}
                  </span>
                  <span style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>
                    {strings.subjectsDetailOrderPrefix} {subject.sequenceOrder}
                  </span>
                </div>
              </div>

              {/* Right: actions */}
              <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8, flexShrink: 0 }}>
                {/* Toggle active */}
                <button
                  data-testid={`subject-${subject.id}-toggle-active`}
                  aria-label={subject.isActive ? strings.subjectsDetailDeactivateBtn : strings.subjectsDetailToggleActiveBtn}
                  aria-busy={togglingSubject}
                  onClick={() => void handleToggleSubjectActive()}
                  style={{
                    height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 16,
                    border: `1px solid ${subject.isActive ? 'rgba(239,68,68,0.3)' : 'rgba(34,197,94,0.3)'}`,
                    backgroundColor: 'transparent',
                    color: subject.isActive ? '#EF4444' : '#22C55E',
                    fontSize: 13, fontWeight: 500, cursor: togglingSubject ? 'not-allowed' : 'pointer',
                    display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  }}
                >
                  {togglingSubject
                    ? '…'
                    : subject.isActive
                      ? <><EyeOffIcon /> {strings.subjectsDetailDeactivateBtn}</>
                      : <><EyeIcon /> {strings.subjectsDetailToggleActiveBtn}</>
                  }
                </button>

                {/* Edit */}
                <button
                  data-testid={`subject-${subject.id}-edit`}
                  aria-label={`Edit ${subject.name}`}
                  onClick={() => setSubjectEditOpen(true)}
                  style={{
                    height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 16,
                    border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                    color: 'var(--lx-fg2)', fontSize: 13, fontWeight: 500, cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  }}
                >
                  <PencilIcon />
                  {strings.subjectsDetailEditBtn}
                </button>

                {/* Delete */}
                <button
                  data-testid={`subject-${subject.id}-delete`}
                  aria-label={`Remove ${subject.name}`}
                  onClick={() => openDeleteDialog({ kind: 'subject', id: subject.id, name: subject.name })}
                  style={{
                    height: 36, paddingLeft: 14, paddingRight: 14, borderRadius: 16,
                    border: '1px solid rgba(239,68,68,0.2)', backgroundColor: 'transparent',
                    color: '#EF4444', fontSize: 13, fontWeight: 500, cursor: 'pointer',
                    display: 'flex', alignItems: 'center', gap: 6, fontFamily: 'inherit',
                  }}
                >
                  <TrashIcon />
                  {strings.subjectsDetailDeleteBtn}
                </button>
              </div>
            </div>

            {/* Error notices on subject */}
            {subjectToggleError && <AdminErrorBanner variant="error" message={subjectToggleError} />}
          </div>
        )}

        {/* P7-05 lifecycle control + version history panel — mounted below subject header */}
        {!subjectLoading && !subjectNotFound && subject && (
          <SubjectLifecyclePanel subjectId={subject.id} />
        )}

        {/* Subject load error */}
        {subjectError && !subjectLoading && (
          <AdminErrorBanner variant="error" message={strings.subjectsListError} />
        )}

        {/* Units section */}
        {!subjectLoading && !subjectNotFound && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {/* Units header row */}
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
              <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 12 }}>
                <h2 style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
                  {strings.unitsHeading}
                </h2>
                {unitsData && unitsData.totalCount > 0 && (
                  <span style={{
                    backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                    padding: '4px 10px', fontSize: 12, color: 'var(--lx-fg3)',
                  }}>
                    {unitsData.totalCount} {strings.unitsResultCount}
                  </span>
                )}
              </div>
              <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                {/* Save order button */}
                {reorderDirty && (
                  <button
                    data-testid="units-save-order"
                    onClick={() => void handleSaveUnitOrder()}
                    aria-busy={reorderSaving}
                    disabled={reorderSaving}
                    style={{
                      display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 6,
                      height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                      backgroundColor: 'rgba(245,158,11,0.15)', border: '1px solid rgba(245,158,11,0.3)',
                      color: '#F59E0B', fontSize: 13, fontWeight: 600,
                      cursor: reorderSaving ? 'not-allowed' : 'pointer', fontFamily: 'inherit',
                    }}
                  >
                    <SaveIcon />
                    {reorderSaving ? '…' : strings.reorderSaveBtn}
                  </button>
                )}
                {/* New unit button */}
                <button
                  data-testid="new-unit-btn"
                  onClick={openCreateUnit}
                  aria-label="Create a new unit"
                  style={{
                    display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8,
                    height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                    backgroundColor: '#4F46E5', border: 'none',
                    color: '#F8FAFC', fontSize: 14, fontWeight: 600, cursor: 'pointer',
                    fontFamily: 'inherit',
                  }}
                >
                  <PlusIcon />
                  {strings.unitsNewUnit}
                </button>
              </div>
            </div>

            {/* Aria-live for reorder */}
            <span className="sr-only" aria-live="polite">{reorderAnnouncement}</span>

            {/* Unit errors */}
            {unitToggleError && <AdminErrorBanner variant="error" message={unitToggleError} />}
            {reorderError && <AdminErrorBanner variant="error" message={reorderError} />}
            {deleteSuccess && <AdminErrorBanner variant="warning" message={deleteSuccess} />}

            {/* Units loading */}
            {unitsLoading && (
              <div
                role="status"
                aria-label={strings.unitsLoadingLabel}
                data-testid="units-loading"
                style={{
                  backgroundColor: 'var(--lx-card)', borderRadius: 20,
                  border: '1px solid var(--lx-border)', overflow: 'hidden',
                }}
              >
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                  <tbody>
                    {Array.from({ length: 4 }).map((_, i) => <SkeletonRow key={i} />)}
                  </tbody>
                </table>
              </div>
            )}

            {/* Units error */}
            {unitsError && !unitsLoading && (
              <div>
                <AdminErrorBanner variant="error" message={strings.unitsListError} data-testid="units-error-banner" />
                <button
                  onClick={() => void refetchUnits()}
                  style={{
                    marginTop: 8, padding: '8px 16px', borderRadius: 16,
                    border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                    color: 'var(--lx-fg2)', cursor: 'pointer', fontFamily: 'inherit', fontSize: 13,
                  }}
                >
                  {strings.usersListRetry}
                </button>
              </div>
            )}

            {/* Units empty */}
            {!unitsLoading && !unitsError && units.length === 0 && (
              <div
                data-testid="units-empty-state"
                style={{
                  backgroundColor: 'var(--lx-card)', borderRadius: 20,
                  border: '1px solid var(--lx-border)', padding: 40,
                  display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16, textAlign: 'center',
                }}
              >
                <span style={{ color: 'var(--lx-fg3)' }}><LayersIcon /></span>
                <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
                  {strings.unitsNoResults}
                </h2>
                <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', maxWidth: 320 }}>
                  {strings.unitsNoResultsHint}
                </p>
                <button
                  onClick={openCreateUnit}
                  style={{
                    display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8,
                    height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                    border: '1px solid rgba(79,70,229,0.4)', backgroundColor: 'transparent',
                    color: '#4F46E5', fontSize: 14, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
                  }}
                >
                  <PlusIcon />
                  {strings.unitsNewUnit}
                </button>
              </div>
            )}

            {/* Units table */}
            {!unitsLoading && !unitsError && units.length > 0 && (
              <div
                data-testid="units-table"
                style={{
                  backgroundColor: 'var(--lx-card)', borderRadius: 20,
                  border: '1px solid var(--lx-border)', overflow: 'hidden',
                  opacity: unitsFetching && !unitsLoading ? 0.6 : 1,
                  transition: 'opacity 120ms var(--lx-ease-out)',
                }}
              >
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                  <caption className="sr-only">{strings.unitsTableCaption}</caption>
                  <thead>
                    <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                      <th scope="col" style={thStyle}>{strings.unitsColOrder}</th>
                      <th scope="col" style={thStyle}>{strings.unitsColName}</th>
                      <th scope="col" style={thStyle}>{strings.unitsColActive}</th>
                      {/* P7-05 lifecycle column */}
                      <th scope="col" style={{ ...thStyle, width: 100 }} aria-label="Lifecycle status" />
                      <th scope="col" style={{ ...thStyle, width: 110 }}>
                        <span className="sr-only">Actions</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {units.map((unit, index) => {
                      const isToggling = togglingUnitId === unit.id;
                      return (
                        <tr
                          key={unit.id}
                          data-testid={`units-row-${unit.id}`}
                          style={{
                            borderBottom: '1px solid var(--lx-border)',
                            opacity: unit.isActive ? 1 : 0.55,
                            transition: 'background-color 120ms var(--lx-ease-out)',
                          }}
                        >
                          {/* Order / reorder */}
                          <td style={{ padding: '12px 16px', verticalAlign: 'middle', width: 120 }}>
                            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                              <button
                                data-testid={`unit-${unit.id}-move-up`}
                                aria-label={`${strings.reorderMoveUp} ${unit.name}`}
                                aria-disabled={index === 0}
                                onClick={() => handleUnitMoveUp(index)}
                                style={{
                                  ...actionBtnStyle, border: '1px solid var(--lx-border)',
                                  opacity: index === 0 ? 0.4 : 1,
                                  cursor: index === 0 ? 'not-allowed' : 'pointer',
                                }}
                              >
                                <ChevronUpIcon />
                              </button>
                              <button
                                data-testid={`unit-${unit.id}-move-down`}
                                aria-label={`${strings.reorderMoveDown} ${unit.name}`}
                                aria-disabled={index === units.length - 1}
                                onClick={() => handleUnitMoveDown(index)}
                                style={{
                                  ...actionBtnStyle, border: '1px solid var(--lx-border)',
                                  opacity: index === units.length - 1 ? 0.4 : 1,
                                  cursor: index === units.length - 1 ? 'not-allowed' : 'pointer',
                                }}
                              >
                                <ChevronDownIcon />
                              </button>
                              <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg3)', fontVariantNumeric: 'tabular-nums', minWidth: 20, textAlign: 'center' }}>
                                {unit.sequenceOrder}
                              </span>
                            </div>
                          </td>

                          {/* Name */}
                          <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                            <span style={{ fontSize: 14, fontWeight: 600, color: 'var(--lx-fg1)' }}>{unit.name}</span>
                          </td>

                          {/* Active */}
                          <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                            <ActiveBadge isActive={unit.isActive} />
                          </td>

                          {/* P7-05 lifecycle slot — UnitLifecycleBadge (DG-2) */}
                          <td
                            data-slot="lifecycle"
                            data-testid={`unit-${unit.id}-lifecycle-slot`}
                            style={{ padding: '12px 16px', verticalAlign: 'middle', minWidth: 100 }}
                          >
                            <UnitLifecycleBadge unitId={unit.id} />
                          </td>

                          {/* Actions */}
                          <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                            <div style={{ display: 'flex', flexDirection: 'row', gap: 4 }}>
                              {/* Edit */}
                              <button
                                data-testid={`unit-${unit.id}-edit`}
                                aria-label={`Edit ${unit.name}`}
                                onClick={() => openEditUnit(unit)}
                                style={actionBtnStyle}
                                onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                                onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
                              >
                                <PencilIcon />
                              </button>

                              {/* Toggle active */}
                              <button
                                data-testid={`unit-${unit.id}-toggle-active`}
                                aria-label={unit.isActive ? `Deactivate ${unit.name}` : `Activate ${unit.name}`}
                                aria-busy={isToggling}
                                onClick={() => void handleToggleUnitActive(unit)}
                                style={{ ...actionBtnStyle, color: unit.isActive ? '#22C55E' : 'var(--lx-fg3)' }}
                                onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                                onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
                              >
                                {isToggling ? '…' : unit.isActive ? <EyeOffIcon /> : <EyeIcon />}
                              </button>

                              {/* Delete */}
                              <button
                                data-testid={`unit-${unit.id}-delete`}
                                aria-label={`Remove ${unit.name}`}
                                onClick={() => openDeleteDialog({ kind: 'unit', id: unit.id, name: unit.name, subjectId })}
                                style={actionBtnStyle}
                                onMouseEnter={(e) => { (e.currentTarget as HTMLElement).style.color = '#EF4444'; }}
                                onMouseLeave={(e) => { (e.currentTarget as HTMLElement).style.color = 'var(--lx-fg3)'; }}
                                onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                                onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
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
        )}
      </div>

      {/* Unit form modal */}
      {subject && (
        <UnitForm
          open={unitFormOpen}
          onClose={() => setUnitFormOpen(false)}
          subjectId={subject.id}
          subjectLanguage={subject.language}
          editUnit={editUnit}
        />
      )}

      {/* Subject edit modal */}
      {subject && (
        <SubjectForm
          open={subjectEditOpen}
          onClose={() => setSubjectEditOpen(false)}
          editSubject={subject}
        />
      )}

      {/* Delete dialog */}
      <CurriculumDeleteDialog
        open={deleteDialogOpen}
        target={deleteTarget}
        onClose={() => { setDeleteDialogOpen(false); setDeleteTarget(null); }}
        onDeleted={(target) => {
          const msg = target.kind === 'unit'
            ? strings.curriculumDeleteSuccessUnit
            : strings.curriculumDeleteSuccessSubject;
          setDeleteSuccess(msg);
          setTimeout(() => setDeleteSuccess(null), 4000);
          // If subject was deleted, go back to the list
          if (target.kind === 'subject') {
            router.push('/curriculum/subjects');
          }
        }}
      />
    </AdminShell>
  );
}
