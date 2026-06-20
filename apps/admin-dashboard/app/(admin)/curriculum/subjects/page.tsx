'use client';

export const dynamic = 'force-dynamic';

/**
 * Subjects list page — `/curriculum/subjects` (P7-01-FE-1/2/5).
 *
 * Grade-scoped, language-scoped table of subjects.
 * Four states: loading-skeleton / empty / error+retry / results.
 * Language filter: client-side (backend has no Language param).
 * Coverage panel: shown when a grade is selected.
 *
 * Design Spec §S2.
 */

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import {
  useSubjectList,
  useAdminGrades,
  useSetSubjectActive,
  useReorderSubjects,
  usePreviewContent,
  type SubjectDto,
  type AddSubjectDto,
} from '@learnexia/api-client';
import {
  VERSIONED_ENTITY_TYPE,
  type ContentLanguageValue,
  type LifecycleStateValue,
} from '@learnexia/shared/constants';
import { AdminShell } from '../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { SubjectCodeBadge } from '../../../../components/SubjectCodeBadge';
import { LanguageBadge } from '../../../../components/LanguageBadge';
import { ActiveBadge } from '../../../../components/ActiveBadge';
import { SubjectLanguageFilter, type LanguageFilter } from '../../../../components/SubjectLanguageFilter';
import { LanguageCoveragePanel } from '../../../../components/LanguageCoveragePanel';
import { SubjectForm } from '../../../../components/SubjectForm';
import { CurriculumDeleteDialog, type CurriculumDeleteTarget } from '../../../../components/CurriculumDeleteDialog';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';
import { LifecycleBadge } from '../../../../components/LifecycleBadge';

const strings = getStrings(ADMIN_LOCALE);

// ── SubjectLifecycleBadge — fetches state via GET /Preview (DG-2: SubjectDto has no lifecycleState) ─

/**
 * DG-2: SubjectDto does NOT carry a lifecycleState field.
 * Lifecycle state is only available via GET /ContentLifecycle/Preview.
 * We fetch it per-entity using usePreviewContent(Subject, id).
 *
 * O(N) concern: Each subject row fires one GET /Preview request.
 * For admin list pages with <50 rows this is acceptable (query is staleTime:30s).
 * A dedicated SubjectListDto with lifecycleState is the proper fix — flagged as
 * DG-2 for backend schema evolution.
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

// ── Debounce ─────────────────────────────────────────────────────────────────

function useDebounce<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(id);
  }, [value, delay]);
  return debounced;
}

// ── Skeleton row ──────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
      {[180, 90, 90, 70, 90, 100, 110].map((w, i) => (
        <td key={i} style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <div style={{
            height: 16, width: w, maxWidth: '100%', borderRadius: 6,
            background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
          }} />
        </td>
      ))}
    </tr>
  );
}

// ── Lucide icons (inline SVG) ─────────────────────────────────────────────────

function PlusIcon({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}
function SearchIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="11" cy="11" r="8" /><line x1="21" y1="21" x2="16.65" y2="16.65" />
    </svg>
  );
}
function BookOpenIcon() {
  return (
    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z" />
      <path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z" />
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

// ── Pagination component ──────────────────────────────────────────────────────

function Pagination({ currentPage, totalPages, onPrev, onNext }: {
  currentPage: number; totalPages: number;
  onPrev: () => void; onNext: () => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'flex-end', gap: 8, padding: '12px 16px' }}>
      <button
        onClick={onPrev} disabled={currentPage <= 1}
        aria-label={strings.subjectsPrevPage}
        style={{
          height: 32, paddingLeft: 12, paddingRight: 12, borderRadius: 8,
          border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
          color: 'var(--lx-fg2)', cursor: currentPage <= 1 ? 'not-allowed' : 'pointer',
          opacity: currentPage <= 1 ? 0.4 : 1, fontFamily: 'inherit', fontSize: 13,
        }}
      >
        {strings.subjectsPrevPage}
      </button>
      <span style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>{currentPage} / {totalPages}</span>
      <button
        onClick={onNext} disabled={currentPage >= totalPages}
        aria-label={strings.subjectsNextPage}
        style={{
          height: 32, paddingLeft: 12, paddingRight: 12, borderRadius: 8,
          border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
          color: 'var(--lx-fg2)', cursor: currentPage >= totalPages ? 'not-allowed' : 'pointer',
          opacity: currentPage >= totalPages ? 0.4 : 1, fontFamily: 'inherit', fontSize: 13,
        }}
      >
        {strings.subjectsNextPage}
      </button>
    </div>
  );
}

// ── Main page ─────────────────────────────────────────────────────────────────

export default function SubjectsPage() {
  const router = useRouter();
  const [gradeId, setGradeId] = useState<number | undefined>(undefined);
  const [langFilter, setLangFilter] = useState<LanguageFilter>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const debouncedSearch = useDebounce(search, 350);

  // Subject form state
  const [subjectFormOpen, setSubjectFormOpen] = useState(false);
  const [editSubject, setEditSubject] = useState<SubjectDto | undefined>(undefined);
  const [subjectFormPrefill, setSubjectFormPrefill] = useState<Partial<AddSubjectDto>>({});

  // Delete dialog state
  const [deleteTarget, setDeleteTarget] = useState<CurriculumDeleteTarget | null>(null);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

  // Reorder state
  const [localOrder, setLocalOrder] = useState<SubjectDto[] | null>(null);
  const [reorderDirty, setReorderDirty] = useState(false);
  const [reorderError, setReorderError] = useState<string | null>(null);
  const [reorderSaving, setReorderSaving] = useState(false);

  // Aria-live for reorder announcements
  const [reorderAnnouncement, setReorderAnnouncement] = useState('');

  // Delete success notice
  const [deleteSuccess, setDeleteSuccess] = useState<string | null>(null);

  // Toggle active in-flight per subject
  const [togglingId, setTogglingId] = useState<number | null>(null);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const setActiveHook = useSetSubjectActive();
  const reorderSubjectsHook = useReorderSubjects();

  const { data: gradesData } = useAdminGrades();
  const grades = gradesData?.data ?? [];

  const { data, isLoading, isError, refetch, isFetching } = useSubjectList({
    gradeId,
    pageNumber: page,
    search: debouncedSearch,
  });

  // Sync local order from server data when not dirty
  useEffect(() => {
    if (data && !reorderDirty) {
      setLocalOrder(data.data ?? null);
    }
  }, [data, reorderDirty]);

  // Reset page on filter change
  useEffect(() => { setPage(1); }, [gradeId, debouncedSearch, langFilter]);

  // Apply client-side language filter
  const filteredSubjects = (localOrder ?? data?.data ?? []).filter((s) => {
    if (langFilter === null) return true;
    return s.language === langFilter;
  });

  const reorderEnabled = gradeId !== undefined && gradeId > 0 && langFilter !== null;

  /**
   * Swap two subjects within the FILTERED view only.
   *
   * `index` is a position in `filteredSubjects` (the language-filtered slice).
   * We locate each filtered item's position in `localOrder` (the full cross-
   * language list) by id, swap only those two slots, and update localOrder.
   * All subjects belonging to the OTHER language retain their original slots.
   *
   * On save we POST only the filtered subset's ids so the backend reorders
   * only the (gradeId, language) tree that is currently visible.
   */
  const handleMoveUp = (index: number) => {
    if (index === 0 || !localOrder) return;
    const itemA = filteredSubjects[index - 1];
    const itemB = filteredSubjects[index];
    if (!itemA || !itemB) return;
    const posA = localOrder.findIndex((s) => s.id === itemA.id);
    const posB = localOrder.findIndex((s) => s.id === itemB.id);
    if (posA === -1 || posB === -1) return;
    const newOrder = [...localOrder];
    newOrder[posA] = itemB;
    newOrder[posB] = itemA;
    setLocalOrder(newOrder);
    setReorderDirty(true);
    setReorderAnnouncement(
      `${itemB.name} ${strings.reorderPosition} ${index} of ${filteredSubjects.length}`,
    );
  };

  const handleMoveDown = (index: number) => {
    if (!localOrder || index >= filteredSubjects.length - 1) return;
    const itemA = filteredSubjects[index];
    const itemB = filteredSubjects[index + 1];
    if (!itemA || !itemB) return;
    const posA = localOrder.findIndex((s) => s.id === itemA.id);
    const posB = localOrder.findIndex((s) => s.id === itemB.id);
    if (posA === -1 || posB === -1) return;
    const newOrder = [...localOrder];
    newOrder[posA] = itemB;
    newOrder[posB] = itemA;
    setLocalOrder(newOrder);
    setReorderDirty(true);
    setReorderAnnouncement(
      `${itemB.name} ${strings.reorderPosition} ${index + 2} of ${filteredSubjects.length}`,
    );
  };

  /**
   * POST only the filtered subset's ids — never the cross-language localOrder.
   * The backend Subjects/Reorder endpoint reorders WITHIN a (gradeId, language)
   * tree; sending cross-tree ids corrupts both trees.
   */
  const handleSaveOrder = async () => {
    if (!localOrder) return;
    setReorderSaving(true);
    setReorderError(null);
    try {
      await reorderSubjectsHook.mutateAsync({ subjectIds: filteredSubjects.map((s) => s.id) });
      setReorderDirty(false);
    } catch {
      setReorderError(strings.reorderErrorMsg);
    } finally {
      setReorderSaving(false);
    }
  };

  const handleToggleActive = async (subject: SubjectDto) => {
    setTogglingId(subject.id);
    setToggleError(null);
    try {
      await setActiveHook.mutateAsync({ id: subject.id, isActive: !subject.isActive });
    } catch {
      setToggleError(strings.subjectToggleError);
    } finally {
      setTogglingId(null);
    }
  };

  const openCreateForm = useCallback((prefill?: Partial<AddSubjectDto>) => {
    setEditSubject(undefined);
    setSubjectFormPrefill(prefill ?? {});
    setSubjectFormOpen(true);
  }, []);

  const openEditForm = useCallback((subject: SubjectDto) => {
    setEditSubject(subject);
    setSubjectFormPrefill({});
    setSubjectFormOpen(true);
  }, []);

  const openDeleteDialog = useCallback((target: CurriculumDeleteTarget) => {
    setDeleteTarget(target);
    setDeleteDialogOpen(true);
  }, []);

  const hasAnyFilter = gradeId !== undefined || langFilter !== null || search !== '';

  const thStyle: React.CSSProperties = {
    padding: '12px 16px', textAlign: 'start',
    fontSize: 11, fontWeight: 600, color: 'var(--lx-fg3)',
    textTransform: 'uppercase', letterSpacing: '0.06em', whiteSpace: 'nowrap',
  };

  const actionBtnStyle: React.CSSProperties = {
    height: 32, width: 32, borderRadius: 8, backgroundColor: 'transparent',
    border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
    color: 'var(--lx-fg3)', opacity: 0.7, transition: 'color 120ms, opacity 120ms',
    outline: 'none',
  };

  return (
    <AdminShell title={strings.curriculumPageTitle}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>

        {/* Page header */}
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', gap: 16 }}>
          <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 12 }}>
            <h1 style={{ margin: 0, fontSize: 24, fontWeight: 700, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
              {strings.subjectsListHeading}
            </h1>
            {data && data.totalCount > 0 && (
              <span style={{
                backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                padding: '4px 10px', fontSize: 12, color: 'var(--lx-fg3)',
              }}>
                {data.totalCount} {strings.subjectsResultCount}
              </span>
            )}
          </div>
          <button
            data-testid="new-subject-btn"
            onClick={() => openCreateForm()}
            aria-label="Create a new subject"
            style={{
              display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8,
              height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
              backgroundColor: '#4F46E5', border: 'none',
              color: '#F8FAFC', fontSize: 14, fontWeight: 600, cursor: 'pointer',
              fontFamily: 'inherit',
            }}
          >
            <PlusIcon size={16} />
            {strings.subjectsNewSubject}
          </button>
        </div>

        {/* Controls bar */}
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          {/* Grade selector */}
          <div style={{ position: 'relative' }}>
            <label htmlFor="grade-filter" className="sr-only">{strings.subjectsColGrade}</label>
            <select
              id="grade-filter"
              data-testid="subjects-grade-filter"
              value={gradeId ?? ''}
              onChange={(e) => setGradeId(e.target.value === '' ? undefined : Number(e.target.value))}
              style={{
                height: 44, width: 160, backgroundColor: 'var(--lx-bg)',
                borderRadius: 8, border: '1px solid var(--lx-border)',
                color: 'var(--lx-fg2)', paddingLeft: 12, paddingRight: 12,
                fontSize: 14, fontFamily: 'inherit', cursor: 'pointer', outline: 'none',
              }}
            >
              <option value="">{strings.subjectsFilterAllGrades}</option>
              {grades.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.gradeNumber != null ? (g.name ?? `Grade ${g.gradeNumber}`) : `Grade ${g.id}`}
                </option>
              ))}
            </select>
          </div>

          {/* Language filter */}
          <SubjectLanguageFilter value={langFilter} onChange={setLangFilter} />

          {/* Search */}
          <div style={{ position: 'relative', flex: 1, minWidth: 200, maxWidth: 340 }}>
            <span style={{
              position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)',
              color: 'var(--lx-fg3)', pointerEvents: 'none',
            }}>
              <SearchIcon />
            </span>
            <input
              id="subjects-search"
              data-testid="subjects-search-input"
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={strings.subjectsListSearchPlaceholder}
              style={{
                height: 44, width: '100%', boxSizing: 'border-box',
                backgroundColor: 'var(--lx-bg)', borderRadius: 8, border: '1px solid var(--lx-border)',
                paddingLeft: 40, paddingRight: 16, fontSize: 14, color: 'var(--lx-fg1)',
                fontFamily: 'inherit', outline: 'none',
              }}
            />
          </div>

          {/* Clear filters */}
          {hasAnyFilter && (
            <button
              onClick={() => { setGradeId(undefined); setLangFilter(null); setSearch(''); }}
              style={{
                height: 40, paddingLeft: 12, paddingRight: 12, borderRadius: 8,
                border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                color: 'var(--lx-fg3)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
              }}
            >
              {strings.subjectsClearFilters}
            </button>
          )}

          {/* Save order button (appears when reorder is dirty) */}
          {reorderDirty && (
            <button
              data-testid="subjects-save-order"
              onClick={() => void handleSaveOrder()}
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
        </div>

        {/* Coverage panel (only when a grade is selected) */}
        {gradeId !== undefined && gradeId > 0 && (
          <LanguageCoveragePanel
            gradeId={gradeId}
            gradeNumber={grades.find((g) => g.id === gradeId)?.gradeNumber ?? gradeId}
            onCreateClick={(prefill) => openCreateForm({ ...prefill, gradeId })}
          />
        )}

        {/* Reorder disabled notice */}
        {langFilter === null && reorderDirty === false && (
          <span style={{ fontSize: 12, color: 'var(--lx-fg3)' }}>
            {strings.reorderDisabledHint}
          </span>
        )}

        {/* Aria-live for reorder announcements */}
        <span className="sr-only" aria-live="polite">{reorderAnnouncement}</span>

        {/* Delete success / toggle error notices */}
        {deleteSuccess && (
          <AdminErrorBanner variant="warning" message={deleteSuccess} />
        )}
        {toggleError && (
          <AdminErrorBanner variant="error" message={toggleError} />
        )}
        {reorderError && (
          <AdminErrorBanner variant="error" message={reorderError} />
        )}

        {/* Results area */}
        <div aria-live="polite">
          {/* SR-only count */}
          <span className="sr-only">
            {data?.totalCount ?? 0} {strings.subjectsResultCount}
          </span>

          {/* Loading state */}
          {isLoading && (
            <div
              role="status"
              aria-label={strings.subjectsListLoadingLabel}
              data-testid="subjects-loading"
              style={{
                backgroundColor: 'var(--lx-card)', borderRadius: 20,
                border: '1px solid var(--lx-border)', overflow: 'hidden',
              }}
            >
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <tbody>
                  {Array.from({ length: 6 }).map((_, i) => <SkeletonRow key={i} />)}
                </tbody>
              </table>
            </div>
          )}

          {/* Error state */}
          {isError && !isLoading && (
            <div>
              <AdminErrorBanner variant="error" message={strings.subjectsListError} testId="subjects-error-banner" />
              <button
                onClick={() => void refetch()}
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

          {/* Empty state */}
          {!isLoading && !isError && filteredSubjects.length === 0 && (
            <div
              data-testid="subjects-empty-state"
              style={{
                backgroundColor: 'var(--lx-card)', borderRadius: 20,
                border: '1px solid var(--lx-border)',
                padding: 40, display: 'flex', flexDirection: 'column',
                alignItems: 'center', gap: 16, textAlign: 'center',
              }}
            >
              <span style={{ color: 'var(--lx-fg3)' }}><BookOpenIcon /></span>
              <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
                {strings.subjectsNoResults}
              </h2>
              <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', maxWidth: 320 }}>
                {strings.subjectsNoResultsHint}
              </p>
              <button
                onClick={() => openCreateForm()}
                style={{
                  display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8,
                  height: 40, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                  border: '1px solid rgba(79,70,229,0.4)', backgroundColor: 'transparent',
                  color: '#4F46E5', fontSize: 14, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
                }}
              >
                <PlusIcon />
                {strings.subjectsNewSubject}
              </button>
            </div>
          )}

          {/* Results table */}
          {!isLoading && !isError && filteredSubjects.length > 0 && (
            <div
              data-testid="subjects-table"
              style={{
                backgroundColor: 'var(--lx-card)', borderRadius: 20,
                border: '1px solid var(--lx-border)', overflow: 'hidden',
                opacity: isFetching && !isLoading ? 0.6 : 1,
                transition: 'opacity 120ms var(--lx-ease-out)',
              }}
            >
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <caption className="sr-only">{strings.subjectsTableCaption}</caption>
                <thead>
                  <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                    <th scope="col" style={thStyle}>{strings.subjectsColSubject}</th>
                    <th scope="col" style={thStyle}>{strings.subjectsColLanguage}</th>
                    <th scope="col" style={thStyle}>{strings.subjectsColGrade}</th>
                    <th scope="col" style={thStyle}>{strings.subjectsColOrder}</th>
                    <th scope="col" style={thStyle}>{strings.subjectsColActive}</th>
                    {/* P7-05 lifecycle column header */}
                    <th scope="col" style={{ ...thStyle, width: 100 }} aria-label="Lifecycle status" />
                    <th scope="col" style={{ ...thStyle, width: 120 }}>
                      <span className="sr-only">Actions</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {filteredSubjects.map((subject, index) => {
                    const isToggling = togglingId === subject.id;
                    return (
                      <tr
                        key={subject.id}
                        data-testid={`subjects-row-${subject.id}`}
                        tabIndex={0}
                        role="button"
                        aria-label={`${subject.name} — ${strings.subjectsViewDetail}`}
                        onClick={() => router.push(`/curriculum/subjects/${subject.id}`)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter' || e.key === ' ') {
                            e.preventDefault();
                            router.push(`/curriculum/subjects/${subject.id}`);
                          }
                        }}
                        style={{
                          borderBottom: '1px solid var(--lx-border)',
                          cursor: 'pointer',
                          opacity: subject.isActive ? 1 : 0.55,
                          transition: 'background-color 120ms var(--lx-ease-out)',
                        }}
                      >
                        {/* Subject column */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                            <SubjectCodeBadge code={subject.subjectCode} />
                            <span style={{ fontSize: 14, fontWeight: 600, color: 'var(--lx-fg1)' }}>{subject.name}</span>
                          </div>
                        </td>

                        {/* Language column */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <LanguageBadge language={subject.language} />
                        </td>

                        {/* Grade column — resolve gradeNumber from grades list (nit fix) */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle', fontSize: 14, color: 'var(--lx-fg2)' }}>
                          <span dir="ltr">
                            {strings.subjectsDetailGradePrefix}{' '}
                            {grades.find((g) => g.id === subject.gradeId)?.gradeNumber ?? subject.gradeId}
                          </span>
                        </td>

                        {/* Order column */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
                            {/* Reorder controls — only when enabled */}
                            <button
                              data-testid={`subject-${subject.id}-move-up`}
                              aria-label={`${strings.reorderMoveUp} ${subject.name}`}
                              aria-disabled={!reorderEnabled || index === 0}
                              onClick={(e) => {
                                e.stopPropagation();
                                if (reorderEnabled) handleMoveUp(index);
                              }}
                              style={{
                                ...actionBtnStyle,
                                border: '1px solid var(--lx-border)',
                                opacity: !reorderEnabled || index === 0 ? 0.4 : 1,
                                cursor: !reorderEnabled || index === 0 ? 'not-allowed' : 'pointer',
                              }}
                            >
                              <ChevronUpIcon />
                            </button>
                            <button
                              data-testid={`subject-${subject.id}-move-down`}
                              aria-label={`${strings.reorderMoveDown} ${subject.name}`}
                              aria-disabled={!reorderEnabled || index === filteredSubjects.length - 1}
                              onClick={(e) => {
                                e.stopPropagation();
                                if (reorderEnabled) handleMoveDown(index);
                              }}
                              style={{
                                ...actionBtnStyle,
                                border: '1px solid var(--lx-border)',
                                opacity: !reorderEnabled || index === filteredSubjects.length - 1 ? 0.4 : 1,
                                cursor: !reorderEnabled || index === filteredSubjects.length - 1 ? 'not-allowed' : 'pointer',
                              }}
                            >
                              <ChevronDownIcon />
                            </button>
                            <span dir="ltr" style={{ fontSize: 13, color: 'var(--lx-fg3)', fontVariantNumeric: 'tabular-nums', minWidth: 24, textAlign: 'center' }}>
                              {subject.sequenceOrder}
                            </span>
                          </div>
                        </td>

                        {/* Active column */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle', opacity: 1 }}>
                          <ActiveBadge isActive={subject.isActive} />
                        </td>

                        {/* P7-05 Lifecycle slot — SubjectLifecycleBadge fetches state via GET /Preview (DG-2) */}
                        <td
                          data-slot="lifecycle"
                          data-testid={`subject-${subject.id}-lifecycle-slot`}
                          style={{ padding: '12px 16px', verticalAlign: 'middle', minWidth: 100 }}
                        >
                          <SubjectLifecycleBadge subjectId={subject.id} />
                        </td>

                        {/* Actions column */}
                        <td
                          style={{ padding: '12px 16px', verticalAlign: 'middle' }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <div style={{ display: 'flex', flexDirection: 'row', gap: 4 }}>
                            {/* Edit */}
                            <button
                              data-testid={`subject-${subject.id}-edit`}
                              aria-label={`Edit ${subject.name}`}
                              onClick={(e) => { e.stopPropagation(); openEditForm(subject); }}
                              style={actionBtnStyle}
                              onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                              onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
                            >
                              <PencilIcon />
                            </button>

                            {/* Toggle active */}
                            <button
                              data-testid={`subject-${subject.id}-toggle-active`}
                              aria-label={subject.isActive ? `Deactivate ${subject.name}` : `Activate ${subject.name}`}
                              aria-busy={isToggling}
                              onClick={(e) => { e.stopPropagation(); void handleToggleActive(subject); }}
                              style={{ ...actionBtnStyle, color: subject.isActive ? '#22C55E' : 'var(--lx-fg3)' }}
                              onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                              onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
                            >
                              {isToggling ? '…' : subject.isActive ? <EyeOffIcon /> : <EyeIcon />}
                            </button>

                            {/* Delete */}
                            <button
                              data-testid={`subject-${subject.id}-delete`}
                              aria-label={`Remove ${subject.name}`}
                              onClick={(e) => {
                                e.stopPropagation();
                                openDeleteDialog({ kind: 'subject', id: subject.id, name: subject.name });
                              }}
                              style={{ ...actionBtnStyle }}
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

              <Pagination
                currentPage={page}
                totalPages={data?.totalPages ?? 1}
                onPrev={() => setPage((p) => Math.max(1, p - 1))}
                onNext={() => setPage((p) => p + 1)}
              />
            </div>
          )}
        </div>
      </div>

      {/* Subject form modal */}
      <SubjectForm
        open={subjectFormOpen}
        onClose={() => setSubjectFormOpen(false)}
        editSubject={editSubject}
        prefillGradeId={subjectFormPrefill.gradeId as number | undefined}
        prefillSubjectCode={subjectFormPrefill.subjectCode as import('@learnexia/shared/constants').SubjectCodeValue | undefined}
        prefillLanguage={subjectFormPrefill.language as ContentLanguageValue | undefined}
      />

      {/* Delete dialog */}
      <CurriculumDeleteDialog
        open={deleteDialogOpen}
        target={deleteTarget}
        onClose={() => { setDeleteDialogOpen(false); setDeleteTarget(null); }}
        onDeleted={(target) => {
          const msg = target.kind === 'subject'
            ? strings.curriculumDeleteSuccessSubject
            : strings.curriculumDeleteSuccessUnit;
          setDeleteSuccess(msg);
          setTimeout(() => setDeleteSuccess(null), 4000);
        }}
      />
    </AdminShell>
  );
}
