'use client';

// Force dynamic rendering — live admin data; must not be statically pre-rendered.
export const dynamic = 'force-dynamic';

/**
 * Moderation Queue list page — `/moderation` (P7-09-FE-1, FE-2).
 *
 * Server-paginated, filterable, debounced-searchable table of moderation items.
 * Consumes `useModerationQueue` from `@learnexia/api-client`.
 *
 * Four states: loading-skeleton / empty / error+retry / results.
 * Filters: status / source / subject / grade / date-from / date-to / search.
 * Pagination: server-side; any filter change resets to page 1.
 * `placeholderData: keepPreviousData` keeps rows visible during refetch.
 *
 * Enum wire form: INTEGERS — ModerationStatus: Pending=0 Approved=1 Rejected=2 Flagged=3
 *                             ModerationSource: AiOutput=0 CurriculumUpload=1
 *
 * Design Spec Part B §B.1–B.5. Acceptance criteria: P7-09 AC 1, 2, 3, 8, 9.
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'next/navigation';

import {
  useModerationQueue,
  type ModerationQueueFilters,
  type ModerationItemDto,
} from '@learnexia/api-client';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';
import { StatusBadge } from '../../../components/StatusBadge';
import { SourceBadge } from '../../../components/SourceBadge';
import { AdminErrorBanner } from '../../../components/AdminErrorBanner';

const strings = getStrings(ADMIN_LOCALE);

// ── Debounce hook ──────────────────────────────────────────────────────────────

function useDebounce<T>(value: T, delay: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(id);
  }, [value, delay]);
  return debounced;
}

// ── Icons ─────────────────────────────────────────────────────────────────────

function SearchXIcon() {
  return (
    <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="11" cy="11" r="8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="m21 21-4.35-4.35" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="m8 8 6 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="m14 8-6 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function ChevronLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M15 18l-6-6 6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ChevronRightIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M9 18l6-6-6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

// ── Skeleton row ───────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
      {[120, 200, 100, 80, 90, 100].map((w, i) => (
        <td key={i} style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <div
            role="presentation"
            style={{
              height: 16,
              width: w,
              maxWidth: '100%',
              borderRadius: 6,
              background:
                'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
              backgroundSize: '400px 100%',
              animation: 'lx-shimmer 1400ms linear infinite',
            }}
          />
        </td>
      ))}
    </tr>
  );
}

// ── Pagination ────────────────────────────────────────────────────────────────

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPrev: () => void;
  onNext: () => void;
}

function Pagination({ currentPage, totalPages, onPrev, onNext }: PaginationProps) {
  const isFirst = currentPage <= 1;
  const isLast = currentPage >= totalPages;

  const btnStyle = (disabled: boolean): React.CSSProperties => ({
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    height: 36,
    width: 36,
    borderRadius: 'var(--lx-radius-button)',
    border: '1px solid var(--lx-border)',
    backgroundColor: 'transparent',
    color: disabled ? 'var(--lx-fg3)' : 'var(--lx-fg2)',
    cursor: disabled ? 'not-allowed' : 'pointer',
    opacity: disabled ? 0.5 : 1,
    transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
  });

  const pageText =
    ADMIN_LOCALE === 'ar'
      ? `الصفحة ${currentPage} من ${totalPages}`
      : `Page ${currentPage} of ${totalPages}`;

  return (
    <Stack flexDirection="row" alignItems="center" justifyContent="center" gap="$3" padding="$4">
      <button
        type="button"
        aria-label={strings.modPrevPage}
        data-testid="mod-pagination-prev"
        disabled={isFirst}
        onClick={onPrev}
        style={btnStyle(isFirst)}
      >
        {ADMIN_LOCALE === 'ar' ? <ChevronRightIcon /> : <ChevronLeftIcon />}
      </button>

      <Text
        fontFamily="$body"
        fontSize={14}
        color="$fg2"
        dir="ltr"
        data-testid="mod-page-indicator"
        style={{ fontVariantNumeric: 'tabular-nums' }}
      >
        {pageText}
      </Text>

      <button
        type="button"
        aria-label={strings.modNextPage}
        data-testid="mod-pagination-next"
        disabled={isLast}
        onClick={onNext}
        style={btnStyle(isLast)}
      >
        {ADMIN_LOCALE === 'ar' ? <ChevronLeftIcon /> : <ChevronRightIcon />}
      </button>
    </Stack>
  );
}

// ── Select style ───────────────────────────────────────────────────────────────

const selectStyle: React.CSSProperties = {
  height: 44,
  backgroundColor: 'var(--lx-bg)',
  borderRadius: 'var(--lx-radius-sm)',
  border: '1px solid var(--lx-border)',
  color: 'var(--lx-fg2)',
  paddingInline: 12,
  fontSize: 14,
  fontFamily: 'inherit',
  outline: 'none',
  cursor: 'pointer',
  appearance: 'auto',
};

// ── Date format helper ─────────────────────────────────────────────────────────

function formatDetectedAt(iso: string): string {
  try {
    const d = new Date(iso);
    if (ADMIN_LOCALE === 'ar') {
      return d.toLocaleString('ar-EG', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      });
    }
    return d.toLocaleString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

// ── Subject/Grade cell ────────────────────────────────────────────────────────

function subjectGradeDisplay(
  subjectCode: string | null,
  grade: number | null,
): string {
  if (subjectCode && grade !== null) {
    return `${subjectCode} · ${grade}`;
  }
  if (subjectCode) return subjectCode;
  if (grade !== null) return String(grade);
  return '—';
}

// ── Constants ──────────────────────────────────────────────────────────────────

const PAGE_SIZE = 20;

// Integer wire values for enum filters
const STATUS_INT: Record<string, number> = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
  Flagged: 3,
};

const SOURCE_INT: Record<string, number> = {
  AiOutput: 0,
  CurriculumUpload: 1,
};

// ── Main page ──────────────────────────────────────────────────────────────────

export default function ModerationPage() {
  const router = useRouter();

  // Filter state (string form for select controls, converted to int for API)
  const [statusStr, setStatusStr] = useState('');
  const [sourceStr, setSourceStr] = useState('');
  const [subjectCode, setSubjectCode] = useState('');
  const [gradeStr, setGradeStr] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const debouncedSearch = useDebounce(search, 350);

  // Reset to page 1 when any filter/search changes
  const prevRef = useRef({ statusStr, sourceStr, subjectCode, gradeStr, dateFrom, dateTo, debouncedSearch });
  useEffect(() => {
    const prev = prevRef.current;
    if (
      prev.statusStr !== statusStr ||
      prev.sourceStr !== sourceStr ||
      prev.subjectCode !== subjectCode ||
      prev.gradeStr !== gradeStr ||
      prev.dateFrom !== dateFrom ||
      prev.dateTo !== dateTo ||
      prev.debouncedSearch !== debouncedSearch
    ) {
      setPage(1);
      prevRef.current = { statusStr, sourceStr, subjectCode, gradeStr, dateFrom, dateTo, debouncedSearch };
    }
  }, [statusStr, sourceStr, subjectCode, gradeStr, dateFrom, dateTo, debouncedSearch]);

  const hasActiveFilters =
    statusStr !== '' ||
    sourceStr !== '' ||
    subjectCode !== '' ||
    gradeStr !== '' ||
    dateFrom !== '' ||
    dateTo !== '' ||
    search !== '';

  const filters: ModerationQueueFilters = {
    ...(statusStr !== '' ? { Status: STATUS_INT[statusStr] } : {}),
    ...(sourceStr !== '' ? { Source: SOURCE_INT[sourceStr] } : {}),
    ...(subjectCode !== '' ? { SubjectCode: subjectCode } : {}),
    ...(gradeStr !== '' ? { Grade: parseInt(gradeStr, 10) } : {}),
    ...(dateFrom !== '' ? { DateFrom: dateFrom } : {}),
    ...(dateTo !== '' ? { DateTo: dateTo } : {}),
    ...(debouncedSearch !== '' ? { Search: debouncedSearch } : {}),
    PageNumber: page,
    PageSize: PAGE_SIZE,
  };

  const { data, isLoading, isError, refetch, isFetching } =
    useModerationQueue(filters);

  const items: ModerationItemDto[] = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  const clearFilters = useCallback(() => {
    setStatusStr('');
    setSourceStr('');
    setSubjectCode('');
    setGradeStr('');
    setDateFrom('');
    setDateTo('');
    setSearch('');
    setPage(1);
  }, []);

  const handleRowClick = useCallback(
    (id: number) => {
      router.push(`/moderation/${id}`);
    },
    [router],
  );

  const handleRowKeyDown = useCallback(
    (e: React.KeyboardEvent, id: number) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        router.push(`/moderation/${id}`);
      }
    },
    [router],
  );

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <AdminShell title={strings.modPageTitleQueue}>
      <Stack flexDirection="column" gap="$6">
        {/* Page header */}
        <Stack
          flexDirection="row"
          alignItems="center"
          justifyContent="space-between"
          gap="$4"
          flexWrap="wrap"
        >
          <Text
            fontFamily="$heading"
            fontSize={24}
            fontWeight="700"
            color="$fg1"
            style={{ lineHeight: 1.3 }}
          >
            {strings.modListHeading}
          </Text>

          {!isLoading && !isError && totalCount > 0 && (
            <Stack
              tag="span"
              paddingHorizontal={10}
              paddingVertical={4}
              borderRadius="$pill"
              style={{ backgroundColor: 'var(--lx-card-soft)' }}
            >
              <Text
                fontFamily="$body"
                fontSize={12}
                color="$fg3"
                dir="ltr"
                style={{ fontVariantNumeric: 'tabular-nums' }}
              >
                {totalCount} {strings.modResultCount}
              </Text>
            </Stack>
          )}
        </Stack>

        {/* Filter bar */}
        <Stack
          flexDirection="row"
          flexWrap="wrap"
          gap="$3"
          alignItems="flex-end"
        >
          {/* Search */}
          <div style={{ position: 'relative', flex: 1, minWidth: 200, maxWidth: 340 }}>
            <label
              htmlFor="mod-search"
              className="sr-only"
            >
              {strings.modSearchPlaceholder}
            </label>
            <input
              id="mod-search"
              type="search"
              data-testid="mod-search-input"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={strings.modSearchPlaceholder}
              dir="ltr"
              style={{
                ...selectStyle,
                width: '100%',
                paddingInlineStart: 36,
              }}
            />
            <span
              aria-hidden="true"
              style={{
                position: 'absolute',
                insetInlineStart: 12,
                top: '50%',
                transform: 'translateY(-50%)',
                color: 'var(--lx-fg3)',
                pointerEvents: 'none',
              }}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
                <circle cx="11" cy="11" r="8" stroke="currentColor" strokeWidth="2" />
                <path d="m21 21-4.35-4.35" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
            </span>
          </div>

          {/* Status filter */}
          <div>
            <label htmlFor="mod-status" className="sr-only">
              {ADMIN_LOCALE === 'ar' ? 'تصفية حسب الحالة' : 'Filter by status'}
            </label>
            <select
              id="mod-status"
              data-testid="mod-status-filter"
              value={statusStr}
              onChange={(e) => setStatusStr(e.target.value)}
              style={{ ...selectStyle, width: 140 }}
            >
              <option value="">{strings.modFilterAllStatuses}</option>
              <option value="Pending">{strings.modStatusPending}</option>
              <option value="Approved">{strings.modStatusApproved}</option>
              <option value="Rejected">{strings.modStatusRejected}</option>
              <option value="Flagged">{strings.modStatusFlagged}</option>
            </select>
          </div>

          {/* Source filter */}
          <div>
            <label htmlFor="mod-source" className="sr-only">
              {ADMIN_LOCALE === 'ar' ? 'تصفية حسب المصدر' : 'Filter by source'}
            </label>
            <select
              id="mod-source"
              data-testid="mod-source-filter"
              value={sourceStr}
              onChange={(e) => setSourceStr(e.target.value)}
              style={{ ...selectStyle, width: 160 }}
            >
              <option value="">{strings.modFilterAllSources}</option>
              <option value="AiOutput">{strings.modSourceAiOutput}</option>
              <option value="CurriculumUpload">{strings.modSourceCurriculumUpload}</option>
            </select>
          </div>

          {/* Subject filter */}
          <div>
            <label htmlFor="mod-subject" className="sr-only">
              {ADMIN_LOCALE === 'ar' ? 'تصفية حسب المادة' : 'Filter by subject'}
            </label>
            <select
              id="mod-subject"
              data-testid="mod-subject-filter"
              value={subjectCode}
              onChange={(e) => setSubjectCode(e.target.value)}
              style={{ ...selectStyle, width: 150 }}
            >
              <option value="">{strings.modFilterAllSubjects}</option>
              <option value="Math">{strings.modSubjectMath}</option>
              <option value="Science">{strings.modSubjectScience}</option>
              <option value="Arabic">{strings.modSubjectArabic}</option>
              <option value="English">{strings.modSubjectEnglish}</option>
            </select>
          </div>

          {/* Grade filter */}
          <div>
            <label htmlFor="mod-grade" className="sr-only">
              {ADMIN_LOCALE === 'ar' ? 'تصفية حسب الصف' : 'Filter by grade'}
            </label>
            <select
              id="mod-grade"
              data-testid="mod-grade-filter"
              value={gradeStr}
              onChange={(e) => setGradeStr(e.target.value)}
              style={{ ...selectStyle, width: 130 }}
            >
              <option value="">{strings.modFilterAllGrades}</option>
              {Array.from({ length: 12 }, (_, i) => i + 1).map((g) => (
                <option key={g} value={String(g)}>
                  {ADMIN_LOCALE === 'ar' ? `الصف ${g}` : `Grade ${g}`}
                </option>
              ))}
            </select>
          </div>

          {/* Date From */}
          <div>
            <label htmlFor="mod-date-from" className="sr-only">
              {ADMIN_LOCALE === 'ar' ? 'اكتُشف بعد' : 'Detected after'}
            </label>
            <input
              id="mod-date-from"
              type="date"
              data-testid="mod-date-from"
              // State holds the full ISO bound for the query; a date input only accepts
              // YYYY-MM-DD as its value, so display just the date portion.
              value={dateFrom.slice(0, 10)}
              onChange={(e) => setDateFrom(e.target.value ? `${e.target.value}T00:00:00Z` : '')}
              dir="ltr"
              style={{ ...selectStyle, width: 150 }}
            />
          </div>

          {/* Date To */}
          <div>
            <label htmlFor="mod-date-to" className="sr-only">
              {ADMIN_LOCALE === 'ar' ? 'اكتُشف قبل' : 'Detected before'}
            </label>
            <input
              id="mod-date-to"
              type="date"
              data-testid="mod-date-to"
              // State holds the full ISO bound for the query; a date input only accepts
              // YYYY-MM-DD as its value, so display just the date portion.
              value={dateTo.slice(0, 10)}
              onChange={(e) => setDateTo(e.target.value ? `${e.target.value}T23:59:59Z` : '')}
              dir="ltr"
              style={{ ...selectStyle, width: 150 }}
            />
          </div>

          {/* Clear filters */}
          {hasActiveFilters && (
            <button
              type="button"
              data-testid="mod-clear-filters"
              onClick={clearFilters}
              style={{
                height: 36,
                paddingInline: 12,
                borderRadius: 'var(--lx-radius-button)',
                border: '1px solid var(--lx-border)',
                backgroundColor: 'transparent',
                color: 'var(--lx-fg2)',
                fontSize: 13,
                fontWeight: 500,
                cursor: 'pointer',
                fontFamily: 'inherit',
              }}
            >
              {strings.modClearFilters}
            </button>
          )}
        </Stack>

        {/* Results area */}
        <div
          aria-live="polite"
          aria-atomic="false"
        >
          {/* SR-only count announcement */}
          <span className="sr-only">
            {!isLoading && !isError && items.length > 0
              ? `${totalCount} ${strings.modResultCount}`
              : null}
          </span>

          {/* Loading state */}
          {isLoading && (
            <div
              role="status"
              aria-label={strings.modListLoadingLabel}
              data-testid="mod-loading"
              style={{
                backgroundColor: 'var(--lx-card)',
                borderRadius: 'var(--lx-radius-card)',
                overflow: 'hidden',
                border: '1px solid var(--lx-border)',
              }}
            >
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead>
                  <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                    {[
                      strings.modColSource,
                      strings.modColContentRef,
                      strings.modColSubjectGrade,
                      strings.modColTaskKind,
                      strings.modColStatus,
                      strings.modColDetected,
                    ].map((col) => (
                      <th
                        key={col}
                        scope="col"
                        style={{
                          padding: '12px 16px',
                          textAlign: 'start',
                          fontSize: 11,
                          fontWeight: 600,
                          color: 'var(--lx-fg3)',
                          textTransform: 'uppercase',
                          letterSpacing: '0.06em',
                        }}
                      >
                        {col}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {Array.from({ length: 6 }).map((_, i) => (
                    <SkeletonRow key={i} />
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* Error state */}
          {!isLoading && isError && (
            <Stack flexDirection="column" gap="$4" data-testid="mod-error-banner">
              <AdminErrorBanner variant="error" message={strings.modListError} />
              <button
                type="button"
                data-testid="mod-retry-btn"
                onClick={() => void refetch()}
                style={{
                  height: 36,
                  paddingInline: 16,
                  borderRadius: 'var(--lx-radius-button)',
                  border: '1px solid var(--lx-border)',
                  backgroundColor: 'transparent',
                  color: 'var(--lx-fg2)',
                  fontSize: 13,
                  cursor: 'pointer',
                  fontFamily: 'inherit',
                }}
              >
                {strings.modListRetry}
              </button>
            </Stack>
          )}

          {/* Empty state */}
          {!isLoading && !isError && items.length === 0 && (
            <Stack
              data-testid="mod-empty-state"
              alignItems="center"
              padding={40}
              gap="$4"
              style={{
                backgroundColor: 'var(--lx-card)',
                borderRadius: 'var(--lx-radius-card)',
                border: '1px solid var(--lx-border)',
              }}
            >
              <span style={{ color: 'var(--lx-fg3)' }}>
                <SearchXIcon />
              </span>
              <Text
                fontFamily="$heading"
                fontSize={18}
                fontWeight="600"
                color="$fg1"
                style={{ lineHeight: 1.3, textAlign: 'center' }}
              >
                {hasActiveFilters
                  ? strings.modEmptyFiltered
                  : strings.modEmptyNoFilters}
              </Text>
              <Text
                fontFamily="$body"
                fontSize={14}
                color="$fg3"
                style={{ lineHeight: 1.5, textAlign: 'center', maxWidth: 320 }}
              >
                {hasActiveFilters
                  ? strings.modEmptyFilteredBody
                  : strings.modEmptyNoFiltersBody}
              </Text>
              {hasActiveFilters && (
                <button
                  type="button"
                  onClick={clearFilters}
                  style={{
                    height: 36,
                    paddingInline: 12,
                    borderRadius: 'var(--lx-radius-button)',
                    border: '1px solid var(--lx-border)',
                    backgroundColor: 'transparent',
                    color: 'var(--lx-fg2)',
                    fontSize: 13,
                    cursor: 'pointer',
                    fontFamily: 'inherit',
                  }}
                >
                  {strings.modClearFilters}
                </button>
              )}
            </Stack>
          )}

          {/* Results table */}
          {!isLoading && !isError && items.length > 0 && (
            <Stack flexDirection="column" gap="$4">
              <div
                data-testid="mod-table"
                style={{
                  backgroundColor: 'var(--lx-card)',
                  borderRadius: 'var(--lx-radius-card)',
                  overflow: 'hidden',
                  border: '1px solid var(--lx-border)',
                  opacity: isFetching && !isLoading ? 0.6 : 1,
                  transition: 'opacity 120ms var(--lx-ease-out)',
                }}
              >
                <table
                  style={{ width: '100%', borderCollapse: 'collapse' }}
                >
                  <caption className="sr-only">{strings.modTableCaption}</caption>
                  <thead>
                    <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                      {[
                        { key: 'source', label: strings.modColSource, width: 160 },
                        { key: 'contentRef', label: strings.modColContentRef, width: undefined },
                        { key: 'subjectGrade', label: strings.modColSubjectGrade, width: 140 },
                        { key: 'taskKind', label: strings.modColTaskKind, width: 120 },
                        { key: 'status', label: strings.modColStatus, width: 120 },
                        { key: 'detected', label: strings.modColDetected, width: 140 },
                      ].map(({ key, label, width }) => (
                        <th
                          key={key}
                          scope="col"
                          data-testid={`mod-col-${key}`}
                          style={{
                            padding: '12px 16px',
                            textAlign: 'start',
                            fontSize: 11,
                            fontWeight: 600,
                            color: 'var(--lx-fg3)',
                            textTransform: 'uppercase',
                            letterSpacing: '0.06em',
                            width: width ?? 'auto',
                          }}
                        >
                          {label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {items.map((item) => (
                      <tr
                        key={item.id}
                        data-testid={`mod-row-${item.id}`}
                        tabIndex={0}
                        role="button"
                        aria-label={`${item.contentReference} — ${strings.modViewDetail}`}
                        onClick={() => handleRowClick(item.id)}
                        onKeyDown={(e) => handleRowKeyDown(e, item.id)}
                        style={{
                          borderBottom: '1px solid var(--lx-border)',
                          cursor: 'pointer',
                          transition: 'background-color 120ms var(--lx-ease-out)',
                        }}
                        onMouseEnter={(e) => {
                          (e.currentTarget as HTMLTableRowElement).style.backgroundColor =
                            'var(--lx-card-soft)';
                        }}
                        onMouseLeave={(e) => {
                          (e.currentTarget as HTMLTableRowElement).style.backgroundColor =
                            '';
                        }}
                      >
                        {/* Source */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <SourceBadge value={item.source} strings={strings} />
                        </td>

                        {/* Content ref */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <span
                            dir="ltr"
                            style={{
                              fontFamily: 'var(--lx-font-mono)',
                              fontSize: 13,
                              color: 'var(--lx-fg2)',
                              display: 'block',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis',
                              whiteSpace: 'nowrap',
                              maxWidth: 280,
                            }}
                          >
                            {item.contentReference}
                          </span>
                        </td>

                        {/* Subject / Grade */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle', color: 'var(--lx-fg2)', fontSize: 14 }}>
                          {subjectGradeDisplay(item.subjectCode, item.grade)}
                        </td>

                        {/* Task kind */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle', color: 'var(--lx-fg3)', fontSize: 14 }}>
                          {item.taskKind ?? '—'}
                        </td>

                        {/* Status */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <StatusBadge
                            variant="moderation"
                            value={item.status}
                            strings={strings}
                          />
                        </td>

                        {/* Detected at */}
                        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
                          <span
                            dir="ltr"
                            style={{
                              fontSize: 13,
                              color: 'var(--lx-fg3)',
                              fontVariantNumeric: 'tabular-nums',
                            }}
                          >
                            {formatDetectedAt(item.detectedAt)}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <Pagination
                  currentPage={page}
                  totalPages={totalPages}
                  onPrev={() => setPage((p) => Math.max(1, p - 1))}
                  onNext={() => setPage((p) => Math.min(totalPages, p + 1))}
                />
              )}
            </Stack>
          )}
        </div>
      </Stack>
    </AdminShell>
  );
}
