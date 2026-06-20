'use client';

export const dynamic = 'force-dynamic';

/**
 * Skills page — `/curriculum/skills` (P7-03-FE-1..FE-5).
 *
 * Two-column layout:
 *   - Left: skills list table (four-state: no-subject / loading / empty / error / results)
 *   - Right: SkillGraph accessible adjacency editor (four-state)
 *
 * Subject picker drives both columns. Selecting a row in the skills table
 * selects that skill's node in the graph editor (right panel reflects it).
 *
 * No dedicated [skillId] route — skill detail opens as a side panel within
 * this page (design spec §S0).
 *
 * Design Spec §S1.
 */

import { useState, useEffect, useCallback } from 'react';
import {
  useSubjectList,
  useSkillList,
  useConceptList,
  type SubjectDto,
  type SingleSkillResponse,
} from '@learnexia/api-client';
import { AdminShell } from '../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { ActiveBadge } from '../../../../components/ActiveBadge';
import { SkillForm } from '../../../../components/SkillForm';
import { SkillDeleteDialog } from '../../../../components/SkillDeleteDialog';
import { SkillGraph } from '../../../../components/SkillGraph';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Debounce hook ─────────────────────────────────────────────────────────────

function useDebounce<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}

// ── Inline Pagination (mirrors subjects page local function) ──────────────────

function Pagination({
  currentPage,
  totalPages,
  onPrev,
  onNext,
}: {
  currentPage: number;
  totalPages: number;
  onPrev: () => void;
  onNext: () => void;
}) {
  if (totalPages <= 1) return null;
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 12, marginTop: 8 }}>
      <button
        onClick={onPrev}
        disabled={currentPage <= 1}
        aria-label={strings.skillPrevPage}
        style={{
          height: 32, paddingLeft: 12, paddingRight: 12, borderRadius: 12,
          border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
          color: 'var(--lx-fg2)', cursor: currentPage <= 1 ? 'not-allowed' : 'pointer',
          opacity: currentPage <= 1 ? 0.4 : 1, fontFamily: 'inherit', fontSize: 13,
        }}
      >
        ‹
      </button>
      <span style={{ fontSize: 13, color: 'var(--lx-fg3)' }}>{currentPage} / {totalPages}</span>
      <button
        onClick={onNext}
        disabled={currentPage >= totalPages}
        aria-label={strings.skillNextPage}
        style={{
          height: 32, paddingLeft: 12, paddingRight: 12, borderRadius: 12,
          border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
          color: 'var(--lx-fg2)', cursor: currentPage >= totalPages ? 'not-allowed' : 'pointer',
          opacity: currentPage >= totalPages ? 0.4 : 1, fontFamily: 'inherit', fontSize: 13,
        }}
      >
        ›
      </button>
    </div>
  );
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────

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
      <path d="M3 6h18" /><path d="M19 6l-1 14H6L5 6" /><path d="M10 11v6" />
      <path d="M14 11v6" /><path d="M9 6V4h6v2" />
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

function LayersIcon({ size = 40 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 2 7 12 12 22 7 12 2" />
      <polyline points="2 17 12 22 22 17" />
      <polyline points="2 12 12 17 22 12" />
    </svg>
  );
}

// ── Skill table skeleton ──────────────────────────────────────────────────────

function SkillsTableSkeleton() {
  return (
    <div
      role="status"
      aria-label={`${strings.skillListHeading}…`}
      data-testid="skills-loading"
      style={{ display: 'flex', flexDirection: 'column', gap: 6 }}
    >
      {[1, 2, 3, 4, 5].map((i) => (
        <div key={i} style={{
          height: 52, borderRadius: 8,
          background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
          backgroundSize: '400px 100%',
          animation: 'lx-shimmer 1400ms linear infinite',
        }} />
      ))}
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

const SELECT_STYLE: React.CSSProperties = {
  height: 44,
  paddingLeft: 12,
  paddingRight: 12,
  backgroundColor: 'var(--lx-bg)',
  borderRadius: 8,
  border: '1px solid var(--lx-border)',
  color: 'var(--lx-fg2)',
  fontSize: 14,
  fontFamily: 'inherit',
  cursor: 'pointer',
  outline: 'none',
};

const TH_STYLE: React.CSSProperties = {
  fontSize: 11,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase' as const,
  letterSpacing: '0.06em',
  padding: '12px 16px',
  textAlign: 'start',
  whiteSpace: 'nowrap',
};

export default function SkillsPage() {
  // ── Filter state ────────────────────────────────────────────────────────────

  const [selectedSubjectId, setSelectedSubjectId] = useState<number>(0);
  const [selectedConceptId, setSelectedConceptId] = useState<string>('');
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 350);
  const [page, setPage] = useState(1);

  // Graph editor selected skill id (drives both row highlight and graph node selection)
  const [selectedSkillId, setSelectedSkillId] = useState<number | null>(null);

  // ── Dialog state ────────────────────────────────────────────────────────────

  const [formOpen, setFormOpen] = useState(false);
  const [editSkill, setEditSkill] = useState<SingleSkillResponse | null>(null);

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<SingleSkillResponse | null>(null);

  // ── Data ────────────────────────────────────────────────────────────────────

  // Subject list — all subjects, for the picker (reuses P7-01's hook)
  const subjectQuery = useSubjectList({});

  const selectedSubject: SubjectDto | null =
    subjectQuery.data?.data.find((s) => s.id === selectedSubjectId) ?? null;

  // Concept list — filter by selected subject id
  const conceptQuery = useConceptList(selectedSubjectId > 0 ? selectedSubjectId : undefined);
  const conceptsForSubject = (conceptQuery.data?.data ?? []).filter(
    (c) => !selectedSubjectId || c.subjectId == null || c.subjectId === selectedSubjectId,
  );

  // Skills list — filtered by conceptId + search
  const skillFilters = {
    conceptId: selectedConceptId ? parseInt(selectedConceptId, 10) : undefined,
    search: debouncedSearch || undefined,
    pageNumber: page,
    pageSize: 20,
  };

  const skillQuery = useSkillList(selectedSubjectId > 0 ? skillFilters : {});

  // All skills (for resolving skill metadata in graph) — no pagination, larger page
  const allSkillsQuery = useSkillList(
    selectedSubjectId > 0
      ? { conceptId: selectedConceptId ? parseInt(selectedConceptId, 10) : undefined, pageSize: 200 }
      : {},
  );

  // ── Reset page on filter change ─────────────────────────────────────────────

  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, selectedConceptId, selectedSubjectId]);

  // Clear concept filter when subject changes
  useEffect(() => {
    setSelectedConceptId('');
    setSelectedSkillId(null);
  }, [selectedSubjectId]);

  // ── Action handlers ─────────────────────────────────────────────────────────

  const handleNewSkill = useCallback(() => {
    setEditSkill(null);
    setFormOpen(true);
  }, []);

  const handleEditSkill = useCallback((skill: SingleSkillResponse) => {
    setEditSkill(skill);
    setFormOpen(true);
  }, []);

  const handleDeleteSkill = useCallback((skill: SingleSkillResponse) => {
    setDeleteTarget(skill);
    setDeleteOpen(true);
  }, []);

  const handleSelectSkillRow = useCallback(
    (skill: SingleSkillResponse) => {
      setSelectedSkillId((prev) => (prev === skill.id ? null : skill.id));
    },
    [],
  );

  // Stable reference — SkillGraph's subject-change effect lists this in its deps,
  // so an inline arrow here would change every render and make the effect clear
  // the graph node selection on every keystroke/selection (the CUR-TC-71 defect).
  const handleGraphSelectSkillId = useCallback((skillId: number | null) => {
    setSelectedSkillId(skillId);
  }, []);

  const handleDeleted = useCallback(() => {
    if (deleteTarget?.id === selectedSkillId) setSelectedSkillId(null);
  }, [deleteTarget, selectedSkillId]);

  // ── Render ──────────────────────────────────────────────────────────────────

  const skills = skillQuery.data?.data ?? [];
  const totalPages = skillQuery.data?.totalPages ?? 1;
  const totalCount = skillQuery.data?.totalCount ?? 0;
  const allSkills = allSkillsQuery.data?.data ?? [];

  const hasFilters = !!debouncedSearch || !!selectedConceptId;

  return (
    <AdminShell title={strings.skillPageTitle}>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 24 }}>

        {/* Page header */}
        <div style={{
          display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 16, flexWrap: 'wrap',
        }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <h1 style={{
              margin: 0, fontSize: 24, fontWeight: 700,
              color: 'var(--lx-fg1)', fontFamily: 'var(--lx-font-display)',
            }}>
              {strings.skillListHeading}
            </h1>
            {skills.length > 0 && (
              <span style={{
                backgroundColor: 'var(--lx-card-soft)', borderRadius: 9999,
                paddingLeft: 10, paddingRight: 10, height: 20, display: 'inline-flex',
                alignItems: 'center', fontSize: 12, color: 'var(--lx-fg3)',
              }}>
                {totalCount} {strings.skillResultCount}
              </span>
            )}
          </div>
          <button
            data-testid="new-skill-btn"
            aria-label={strings.skillNewBtnAriaLabel}
            onClick={handleNewSkill}
            style={{
              height: 40, paddingLeft: 16, paddingRight: 16,
              borderRadius: 16, border: 'none',
              backgroundColor: 'var(--lx-primary)', color: 'var(--lx-fg1)',
              fontSize: 14, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
              display: 'flex', alignItems: 'center', gap: 8,
            }}
          >
            <PlusIcon />
            {strings.skillNewBtn}
          </button>
        </div>

        {/* Controls bar */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
          {/* Subject picker */}
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <label
              htmlFor="skill-subject-picker"
              style={{
                fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)',
                textTransform: 'uppercase', letterSpacing: '0.04em',
              }}
            >
              {strings.skillSubjectPickerLabel}
            </label>
            <select
              id="skill-subject-picker"
              data-testid="skill-subject-picker"
              aria-label={strings.skillSubjectPickerLabel}
              value={selectedSubjectId}
              onChange={(e) => setSelectedSubjectId(parseInt(e.target.value, 10) || 0)}
              disabled={subjectQuery.isLoading}
              style={{ ...SELECT_STYLE, width: 240 }}
            >
              <option value={0}>
                {subjectQuery.isLoading
                  ? strings.skillSubjectPickerLoading
                  : strings.skillSubjectPickerPlaceholder}
              </option>
              {(subjectQuery.data?.data ?? []).map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
            {subjectQuery.isError && (
              <span style={{ fontSize: 12, color: 'var(--lx-danger)' }}>{strings.skillSubjectPickerError}</span>
            )}
          </div>

          {/* Concept filter */}
          {selectedSubjectId > 0 && (
            <select
              id="skill-concept-filter"
              data-testid="skill-concept-filter"
              aria-label={strings.skillConceptFilterAriaLabel}
              value={selectedConceptId}
              onChange={(e) => setSelectedConceptId(e.target.value)}
              style={{ ...SELECT_STYLE, width: 200 }}
            >
              <option value="">{strings.skillConceptFilterPlaceholder}</option>
              {conceptsForSubject.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          )}

          {/* Search */}
          <div style={{ position: 'relative', flex: 1, minWidth: 200, maxWidth: 300 }}>
            <span style={{
              position: 'absolute', insetInlineStart: 12, top: '50%', transform: 'translateY(-50%)',
              color: 'var(--lx-fg3)', display: 'flex', pointerEvents: 'none',
            }}>
              <SearchIcon />
            </span>
            <input
              id="skills-search"
              data-testid="skills-search-input"
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={strings.skillSearchPlaceholder}
              dir="auto"
              style={{
                height: 44, width: '100%',
                paddingLeft: 40, paddingRight: 16,
                backgroundColor: 'var(--lx-bg)', borderRadius: 8,
                border: '1px solid var(--lx-border)', color: 'var(--lx-fg1)',
                fontSize: 14, fontFamily: 'inherit',
                boxSizing: 'border-box', outline: 'none',
              }}
            />
          </div>

          {/* Clear filters */}
          {hasFilters && (
            <button
              onClick={() => {
                setSearch('');
                setSelectedConceptId('');
              }}
              style={{
                height: 36, paddingLeft: 12, paddingRight: 12, borderRadius: 12,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg3)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
              }}
            >
              {strings.skillClearFilters}
            </button>
          )}
        </div>

        {/* Two-column layout */}
        <div style={{
          display: 'flex', gap: 24, alignItems: 'flex-start',
          flexWrap: 'wrap',
        }}>
          {/* Left — skills table */}
          <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 16 }}>

            {/* aria-live result region */}
            <span className="sr-only" aria-live="polite" aria-atomic="true">
              {!skillQuery.isLoading && skills.length > 0
                ? `${totalCount} ${strings.skillResultCount}`
                : undefined}
            </span>

            <div data-testid="skills-results" aria-live="polite" aria-atomic="false">
              {/* No subject selected state */}
              {!selectedSubjectId ? (
                <div
                  data-testid="skills-empty-state"
                  style={{
                    backgroundColor: 'var(--lx-card)', borderRadius: 20,
                    border: '1px solid var(--lx-border)', padding: 40,
                    display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16,
                  }}
                >
                  <span style={{ color: 'var(--lx-fg3)' }}><LayersIcon size={40} /></span>
                  <h3 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)' }}>
                    {strings.skillNoSubjectSelected}
                  </h3>
                  <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', textAlign: 'center', maxWidth: 300 }}>
                    {strings.skillNoResultsSelectSubject}
                  </p>
                </div>
              ) : skillQuery.isLoading ? (
                <SkillsTableSkeleton />
              ) : skillQuery.isError ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                  <AdminErrorBanner
                    variant="error"
                    message={strings.skillListError}
                  />
                  <button
                    onClick={() => void skillQuery.refetch()}
                    style={{
                      height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                      border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                      color: 'var(--lx-fg2)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
                    }}
                  >
                    {strings.skillGraphRetry}
                  </button>
                </div>
              ) : skills.length === 0 ? (
                <div
                  data-testid="skills-empty-state"
                  style={{
                    backgroundColor: 'var(--lx-card)', borderRadius: 20,
                    border: '1px solid var(--lx-border)', padding: 40,
                    display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16,
                  }}
                >
                  <span style={{ color: 'var(--lx-fg3)' }}><LayersIcon size={40} /></span>
                  <h3 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: 'var(--lx-fg1)' }}>
                    {strings.skillNoResults}
                  </h3>
                  <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', textAlign: 'center', maxWidth: 300 }}>
                    {strings.skillNoResultsHint}
                  </p>
                  <button
                    onClick={handleNewSkill}
                    style={{
                      height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                      border: '1px solid rgba(79,70,229,0.3)', backgroundColor: 'transparent',
                      color: 'var(--lx-primary)', fontSize: 14, fontWeight: 500,
                      cursor: 'pointer', fontFamily: 'inherit',
                      display: 'flex', alignItems: 'center', gap: 8,
                    }}
                  >
                    <PlusIcon />
                    {strings.skillNewBtn}
                  </button>
                </div>
              ) : (
                /* Results table */
                <div
                  data-testid="skills-table"
                  style={{
                    backgroundColor: 'var(--lx-card)', borderRadius: 20,
                    border: '1px solid var(--lx-border)', overflow: 'hidden',
                    opacity: skillQuery.isFetching && !skillQuery.isLoading ? 0.6 : 1,
                    transition: 'opacity 120ms var(--lx-ease-out)',
                  }}
                >
                  <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                    <caption className="sr-only">{strings.skillTableCaption}</caption>
                    <thead>
                      <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                        <th style={TH_STYLE}>{strings.skillColName}</th>
                        <th style={{ ...TH_STYLE, width: 110 }}>{strings.skillColThreshold}</th>
                        <th style={{ ...TH_STYLE, width: 100 }}>{strings.skillColTime}</th>
                        <th style={{ ...TH_STYLE, width: 90 }}>{strings.skillColActive}</th>
                        <th style={{ ...TH_STYLE, width: 100 }}>
                          <span className="sr-only">{strings.skillColActionsLabel}</span>
                        </th>
                      </tr>
                    </thead>
                    <tbody>
                      {skills.map((skill) => {
                        const isSelected = selectedSkillId === skill.id;
                        return (
                          <tr
                            key={skill.id}
                            tabIndex={0}
                            role="button"
                            aria-pressed={isSelected}
                            aria-label={`${skill.name} — ${strings.skillViewGraph}`}
                            data-testid={`skills-row-${skill.id}`}
                            onClick={() => handleSelectSkillRow(skill)}
                            onKeyDown={(e) => {
                              if (e.key === 'Enter' || e.key === ' ') {
                                e.preventDefault();
                                handleSelectSkillRow(skill);
                              }
                            }}
                            style={{
                              cursor: 'pointer',
                              borderBottom: '1px solid var(--lx-border)',
                              backgroundColor: isSelected
                                ? 'rgba(79,70,229,0.18)'
                                : 'var(--lx-card)',
                              borderInlineStart: isSelected
                                ? '3px solid var(--lx-primary)'
                                : '3px solid transparent',
                              transition: 'background-color 120ms var(--lx-ease-out)',
                              outline: 'none',
                            }}
                            className="skills-table-row"
                          >
                            <td style={{ padding: '12px 16px', fontSize: 14, fontWeight: 600, color: 'var(--lx-fg1)' }}>
                              {skill.name}
                            </td>
                            <td style={{ padding: '12px 16px', fontSize: 13, color: 'var(--lx-fg3)' }}>
                              <span dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>
                                {skill.masteryThreshold}%
                              </span>
                            </td>
                            <td style={{ padding: '12px 16px', fontSize: 13, color: 'var(--lx-fg3)' }}>
                              <span dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>
                                {skill.estimatedTimeMinutes} min
                              </span>
                            </td>
                            <td style={{ padding: '12px 16px' }}>
                              {skill.isActive != null && (
                                <ActiveBadge isActive={skill.isActive} />
                              )}
                            </td>
                            <td style={{ padding: '12px 16px' }}>
                              {/* Action buttons — appear at full opacity on row hover; 0.4 at rest */}
                              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                                <button
                                  data-testid={`skill-${skill.id}-edit`}
                                  aria-label={strings.skillEditAriaLabel.replace('{name}', skill.name)}
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    handleEditSkill(skill);
                                  }}
                                  style={{
                                    height: 32, width: 32, borderRadius: 8,
                                    border: 'none', backgroundColor: 'transparent',
                                    color: 'var(--lx-fg3)', cursor: 'pointer',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    fontFamily: 'inherit',
                                  }}
                                >
                                  <PencilIcon />
                                </button>
                                <button
                                  data-testid={`skill-${skill.id}-delete`}
                                  aria-label={strings.skillDeleteAriaLabel.replace('{name}', skill.name)}
                                  onClick={(e) => {
                                    e.stopPropagation();
                                    handleDeleteSkill(skill);
                                  }}
                                  style={{
                                    height: 32, width: 32, borderRadius: 8,
                                    border: 'none', backgroundColor: 'transparent',
                                    color: 'var(--lx-fg3)', cursor: 'pointer',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    fontFamily: 'inherit',
                                  }}
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

              {/* Pagination */}
              {skills.length > 0 && (
                <Pagination
                  currentPage={page}
                  totalPages={totalPages}
                  onPrev={() => setPage((p) => Math.max(1, p - 1))}
                  onNext={() => setPage((p) => Math.min(totalPages, p + 1))}
                />
              )}
            </div>
          </div>

          {/* Right — Graph editor */}
          <div style={{
            width: 380, flexShrink: 0, display: 'flex', flexDirection: 'column', gap: 16,
          }}>
            <SkillGraph
              subjectId={selectedSubjectId}
              selectedSubject={selectedSubject}
              skills={allSkills}
              onEditSkill={handleEditSkill}
              onSelectSkillId={handleGraphSelectSkillId}
              externalSelectedSkillId={selectedSkillId}
            />
          </div>
        </div>
      </div>

      {/* SkillForm — create / edit */}
      <SkillForm
        open={formOpen}
        skill={editSkill}
        currentSubjectId={selectedSubjectId || undefined}
        onClose={() => setFormOpen(false)}
        onSuccess={() => {
          // TanStack Query invalidation already fires in the hook's onSuccess
        }}
      />

      {/* SkillDeleteDialog */}
      {deleteTarget && (
        <SkillDeleteDialog
          open={deleteOpen}
          skillId={deleteTarget.id}
          skillName={deleteTarget.name}
          subjectId={selectedSubjectId || undefined}
          onClose={() => {
            setDeleteOpen(false);
            setDeleteTarget(null);
          }}
          onDeleted={handleDeleted}
        />
      )}
    </AdminShell>
  );
}
