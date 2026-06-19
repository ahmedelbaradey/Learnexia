'use client';

// Force dynamic rendering — this page fetches live admin data and must NOT be
// statically pre-rendered. Without this, the Next.js build fails when
// NEXT_PUBLIC_API_URL is not set (same reason as users/page.tsx).
export const dynamic = 'force-dynamic';

/**
 * Audit Log page — `/audit` (P7-12-FE).
 *
 * Server-paginated, filterable, read-only table of admin governance actions.
 * Consumes `useAuditLog` from `@learnexia/api-client`.
 *
 * CRITICAL INVARIANTS:
 * 1. STRICTLY READ-ONLY. No mutation hooks anywhere on this surface.
 *    No edit/delete/restore buttons. The audit log is immutable.
 * 2. `details` is rendered as plain text / pretty-printed JSON — NEVER HTML.
 *    See `AuditEntryDetail` for the implementation.
 * 3. No export in v1 — backend has no export endpoint. AC gap recorded in PR.
 *
 * Four states: loading-skeleton / empty / error+retry / results.
 * Filters: AdminUserId (int), ActionType (grouped select), TargetEntityType (select),
 *          DateFrom / DateTo (date inputs; DateTo < DateFrom → validation error, no call).
 * Pagination: server-side; filter change resets to page 1.
 * `placeholderData: keepPreviousData` keeps rows visible during refetch.
 *
 * Design Spec: P7-12-FE.md Parts B–G + I–K.
 * Acceptance criteria: P7-12 AC 1–9, 11.
 *
 * EXPORT DEFERRAL (AC gap):
 * Story AC "Admins can export the filtered log (CSV/JSON)" is unsatisfiable
 * in v1 — the shipped backend (`GET /api/Admin/Audit/Log`) has no export
 * endpoint. FE-4 is not built. Backend follow-up required:
 * `GET /api/Admin/Audit/Log/Export` (filtered, streaming CSV/JSON).
 * No client-side single-page CSV is provided (would export only the visible
 * page, silently misleading for a compliance export).
 */

import { useState, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

import { useAuditLog, type AuditLogDto, type AuditLogFilters } from '@learnexia/api-client';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';
import { AdminErrorBanner } from '../../../components/AdminErrorBanner';
import { ActionTypeBadge } from '../../../components/ActionTypeBadge';
import { AuditEntryDetail } from '../../../components/AuditEntryDetail';
import {
  ACTION_GROUPS,
  ACTION_LABELS,
  TARGET_TYPE_LABELS,
  getTargetTypeLabel,
} from '../../../lib/auditActionLabels';

const strings = getStrings(ADMIN_LOCALE);

// ── Inline helper constants ────────────────────────────────────────────────────

const PAGE_SIZE = 20;

// ── Skeleton row ──────────────────────────────────────────────────────────────

function AuditSkeletonRow() {
  return (
    <tr style={{ borderBottom: '1px solid var(--lx-border)', height: 52 }}>
      {[90, 140, 160, 110, 48].map((w, i) => (
        <td key={i} style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <div
            role="presentation"
            style={{
              height: 16,
              width: i === 4 ? 32 : w,
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

// ── Pagination ─────────────────────────────────────────────────────────────────

interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPrev: () => void;
  onNext: () => void;
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

function ChevronDownIcon({ rotated }: { rotated?: boolean }) {
  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      aria-hidden="true"
      style={{
        transform: rotated ? 'rotate(180deg)' : 'rotate(0deg)',
        transition: 'transform var(--lx-dur-fast) var(--lx-ease-out)',
      }}
    >
      <path d="M6 9l6 6 6-6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function AuditPagination({ currentPage, totalPages, onPrev, onNext }: PaginationProps) {
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

  // AR pagination: Eastern-Arabic numerals for page numbers (Design Spec §I)
  const formatNum = (n: number) =>
    ADMIN_LOCALE === 'ar'
      ? new Intl.NumberFormat('ar-EG').format(n)
      : String(n);

  const pageText =
    ADMIN_LOCALE === 'ar'
      ? `الصفحة ${formatNum(currentPage)} من ${formatNum(totalPages)}`
      : `Page ${currentPage} of ${totalPages}`;

  return (
    <Stack
      flexDirection="row"
      alignItems="center"
      justifyContent="center"
      gap="$3"
      padding="$4"
    >
      <button
        type="button"
        aria-label={strings.auditPrevPage}
        data-testid="audit-pagination-prev"
        disabled={isFirst}
        onClick={onPrev}
        style={btnStyle(isFirst)}
      >
        <ChevronLeftIcon />
      </button>

      <Text
        fontFamily="$body"
        fontSize={14}
        color="$fg2"
        dir="ltr"
        data-testid="audit-page-indicator"
        style={{ fontVariantNumeric: 'tabular-nums' }}
      >
        {pageText}
      </Text>

      <button
        type="button"
        aria-label={strings.auditNextPage}
        data-testid="audit-pagination-next"
        disabled={isLast}
        onClick={onNext}
        style={btnStyle(isLast)}
      >
        <ChevronRightIcon />
      </button>
    </Stack>
  );
}

// ── Native select style ────────────────────────────────────────────────────────

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

const inputStyle: React.CSSProperties = {
  height: 44,
  backgroundColor: 'var(--lx-bg)',
  borderRadius: 'var(--lx-radius-sm)',
  border: '1px solid var(--lx-border)',
  color: 'var(--lx-fg1)',
  paddingInline: 12,
  fontSize: 14,
  fontFamily: 'inherit',
  outline: 'none',
  boxSizing: 'border-box',
};

// ── Timestamp formatter ────────────────────────────────────────────────────────

function formatAuditTimestamp(iso: string): string {
  try {
    return new Intl.DateTimeFormat(ADMIN_LOCALE === 'ar' ? 'ar-EG' : 'en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

// ── Table header styles ────────────────────────────────────────────────────────

const thStyle: React.CSSProperties = {
  padding: '12px 16px',
  textAlign: 'start',
  fontSize: 11,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  fontFamily: 'inherit',
  whiteSpace: 'nowrap',
};

// ── Audit row component ────────────────────────────────────────────────────────

interface AuditRowProps {
  entry: AuditLogDto;
  isExpanded: boolean;
  onToggle: (id: number) => void;
}

function AuditRow({ entry, isExpanded, onToggle }: AuditRowProps) {
  const localizedTargetType = getTargetTypeLabel(entry.targetEntityType, ADMIN_LOCALE);

  return (
    <>
      {/* Main data row */}
      <tr
        data-testid={`audit-row-${entry.id}`}
        onClick={() => onToggle(entry.id)}
        style={{
          borderBottom: isExpanded ? 'none' : '1px solid var(--lx-border)',
          cursor: 'pointer',
          transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
          backgroundColor: isExpanded ? 'rgba(79,70,229,0.06)' : 'transparent',
        }}
        onMouseEnter={(e) => {
          if (!isExpanded) {
            (e.currentTarget as HTMLTableRowElement).style.backgroundColor =
              'var(--lx-card-soft)';
          }
        }}
        onMouseLeave={(e) => {
          (e.currentTarget as HTMLTableRowElement).style.backgroundColor = isExpanded
            ? 'rgba(79,70,229,0.06)'
            : 'transparent';
        }}
      >
        {/* Cell 1 — Admin (actor) */}
        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <span
            dir="ltr"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              height: 26,
              paddingLeft: 8,
              paddingRight: 8,
              borderRadius: 9999,
              backgroundColor: 'rgba(79,70,229,0.15)',
              color: '#6366F1',
              fontSize: 12,
              fontWeight: 600,
              fontVariantNumeric: 'tabular-nums',
              fontFamily: 'var(--lx-font-mono, monospace)',
              whiteSpace: 'nowrap',
            }}
          >
            #{entry.adminUserId}
          </span>
        </td>

        {/* Cell 2 — Action type badge */}
        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <ActionTypeBadge action={entry.action} />
        </td>

        {/* Cell 3 — Target (type + optional id) */}
        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <span
            style={{
              fontSize: 14,
              color: 'var(--lx-fg2)',
              fontFamily: 'inherit',
            }}
          >
            {localizedTargetType}
          </span>
          {entry.targetEntityId > 0 && (
            <span
              dir="ltr"
              style={{
                marginInlineStart: 6,
                fontSize: 12,
                color: 'var(--lx-fg3)',
                fontFamily: 'var(--lx-font-mono, monospace)',
                fontVariantNumeric: 'tabular-nums',
              }}
            >
              #{entry.targetEntityId}
            </span>
          )}
        </td>

        {/* Cell 4 — When (timestamp) */}
        <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
          <span
            dir="ltr"
            title={entry.occurredAtUtc}
            style={{
              fontSize: 13,
              color: 'var(--lx-fg3)',
              fontVariantNumeric: 'tabular-nums',
              whiteSpace: 'nowrap',
              fontFamily: 'inherit',
            }}
          >
            {formatAuditTimestamp(entry.occurredAtUtc)}
          </span>
        </td>

        {/* Cell 5 — Expand toggle */}
        <td
          style={{
            width: 48,
            padding: '12px 8px',
            verticalAlign: 'middle',
            textAlign: 'center',
          }}
        >
          <button
            type="button"
            aria-expanded={isExpanded}
            aria-controls={`audit-detail-${entry.id}`}
            aria-label={isExpanded ? strings.auditCollapseEntry : strings.auditExpandEntry}
            data-testid={`audit-expand-${entry.id}`}
            onClick={(e) => {
              e.stopPropagation();
              onToggle(entry.id);
            }}
            style={{
              width: 32,
              height: 32,
              borderRadius: 8,
              border: '1px solid var(--lx-border)',
              backgroundColor: 'transparent',
              color: 'var(--lx-fg3)',
              cursor: 'pointer',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
            }}
            onMouseEnter={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor =
                'var(--lx-card)';
            }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor =
                'transparent';
            }}
            onFocus={(e) => {
              (e.currentTarget as HTMLButtonElement).style.boxShadow =
                'var(--lx-focus-ring-admin)';
              (e.currentTarget as HTMLButtonElement).style.outline = 'none';
            }}
            onBlur={(e) => {
              (e.currentTarget as HTMLButtonElement).style.boxShadow = 'none';
            }}
          >
            <ChevronDownIcon rotated={isExpanded} />
          </button>
        </td>
      </tr>

      {/* Expanded detail row — conditionally rendered (not hidden with CSS) */}
      {isExpanded && (
        <tr
          id={`audit-detail-${entry.id}`}
          data-testid={`audit-detail-${entry.id}`}
        >
          <td
            colSpan={5}
            style={{
              padding: 0,
              borderBottom: '1px solid var(--lx-border)',
            }}
          >
            <AuditEntryDetail entry={entry} />
          </td>
        </tr>
      )}
    </>
  );
}

// ── Main page ──────────────────────────────────────────────────────────────────

export default function AuditPage() {
  // Filter state (all strings; AdminUserId parsed to int before sending)
  const [adminIdInput, setAdminIdInput] = useState('');
  const [actionType, setActionType] = useState('');
  const [targetType, setTargetType] = useState('');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [page, setPage] = useState(1);

  // Expanded row state — only one row expanded at a time is not required,
  // but the design implies a set. We track a Set<number> of expanded IDs.
  const [expandedIds, setExpandedIds] = useState<Set<number>>(new Set());

  const toggleExpand = useCallback((id: number) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  // Date validation: no API call when DateTo < DateFrom
  const hasDateRangeError =
    dateFrom !== '' && dateTo !== '' && dateTo < dateFrom;

  const hasActiveFilters =
    adminIdInput !== '' ||
    actionType !== '' ||
    targetType !== '' ||
    dateFrom !== '' ||
    dateTo !== '';

  const clearFilters = useCallback(() => {
    setAdminIdInput('');
    setActionType('');
    setTargetType('');
    setDateFrom('');
    setDateTo('');
    setPage(1);
    setExpandedIds(new Set());
  }, []);

  // Reset to page 1 whenever a filter changes; collapse expanded rows on filter
  // change to avoid stale detail panels for newly-filtered results.
  const handleFilterChange = useCallback(<T,>(setter: React.Dispatch<React.SetStateAction<T>>, value: T) => {
    setter(value);
    setPage(1);
    setExpandedIds(new Set());
  }, []);

  // Build the hook filters — only pass defined non-empty values
  const filters: AuditLogFilters = {
    ...(adminIdInput !== '' && !isNaN(parseInt(adminIdInput, 10))
      ? { AdminUserId: parseInt(adminIdInput, 10) }
      : {}),
    ...(actionType !== '' ? { ActionType: actionType } : {}),
    ...(targetType !== '' ? { TargetEntityType: targetType } : {}),
    // Date-only inputs: append time bounds before sending
    ...(dateFrom !== '' ? { DateFrom: `${dateFrom}T00:00:00Z` } : {}),
    ...(dateTo !== '' && !hasDateRangeError ? { DateTo: `${dateTo}T23:59:59Z` } : {}),
    PageNumber: page,
    PageSize: PAGE_SIZE,
  };

  // Disable the query when there is a date-range validation error
  const { data, isLoading, isError, refetch, isFetching } = useAuditLog(
    hasDateRangeError ? { PageNumber: page, PageSize: PAGE_SIZE } : filters,
  );

  const entries: AuditLogDto[] = hasDateRangeError ? [] : (data?.data ?? []);
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  const showSkeleton = isLoading && !hasDateRangeError;
  const showError = !isLoading && isError && !hasDateRangeError;
  const showEmpty = !isLoading && !isError && entries.length === 0 && !hasDateRangeError;
  const showResults = !isLoading && !isError && entries.length > 0 && !hasDateRangeError;

  const tableOpacity = isFetching && !isLoading ? 0.6 : 1;

  return (
    <AdminShell title={strings.pageTitleAudit}>
      <Stack flexDirection="column" gap="$6">

        {/* Page header */}
        <Stack
          flexDirection="row"
          alignItems="center"
          justifyContent="space-between"
          gap="$4"
        >
          <Text
            fontFamily="$heading"
            fontSize={24}
            fontWeight="700"
            color="$fg1"
          >
            {strings.auditPageHeading}
          </Text>

          {/* Result count pill — shown only in results state */}
          {showResults && (
            <Stack
              tag="span"
              paddingHorizontal={10}
              paddingVertical={4}
              borderRadius="$pill"
              data-testid="audit-result-count"
              style={{ backgroundColor: 'var(--lx-card-soft)' }}
            >
              <Text fontFamily="$body" fontSize={12} color="$fg3">
                {totalCount} {strings.auditResultCount}
              </Text>
            </Stack>
          )}
        </Stack>

        {/* Filters bar */}
        <Stack
          flexDirection="row"
          alignItems="center"
          gap="$4"
          flexWrap="wrap"
        >
          {/* Admin user ID (numeric) */}
          <div>
            <label htmlFor="audit-admin-id" className="sr-only">
              {strings.auditFilterAdminIdLabel}
            </label>
            <input
              id="audit-admin-id"
              type="number"
              data-testid="audit-filter-admin-id"
              value={adminIdInput}
              onChange={(e) => handleFilterChange(setAdminIdInput, e.target.value)}
              placeholder={strings.auditFilterAdminIdPlaceholder}
              min="1"
              step="1"
              style={{ ...inputStyle, width: 160 }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-primary)';
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-border)';
                e.currentTarget.style.boxShadow = 'none';
              }}
            />
          </div>

          {/* Action type (grouped select) */}
          <div>
            <label htmlFor="audit-action-type" className="sr-only">
              {strings.auditFilterActionTypeLabel}
            </label>
            <select
              id="audit-action-type"
              data-testid="audit-filter-action-type"
              value={actionType}
              onChange={(e) => handleFilterChange(setActionType, e.target.value)}
              style={{ ...selectStyle, width: 200 }}
              onFocus={(e) => {
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.boxShadow = 'none';
              }}
            >
              <option value="">{strings.auditFilterActionTypePlaceholder}</option>
              {ACTION_GROUPS.map((group) => (
                <optgroup
                  key={group.domain}
                  label={group.label[ADMIN_LOCALE]}
                >
                  {group.actions.map((action) => (
                    <option key={action} value={action}>
                      {ACTION_LABELS[action]?.[ADMIN_LOCALE] ?? action}
                    </option>
                  ))}
                </optgroup>
              ))}
            </select>
          </div>

          {/* Target entity type (flat select) */}
          <div>
            <label htmlFor="audit-target-type" className="sr-only">
              {strings.auditFilterTargetTypeLabel}
            </label>
            <select
              id="audit-target-type"
              data-testid="audit-filter-target-type"
              value={targetType}
              onChange={(e) => handleFilterChange(setTargetType, e.target.value)}
              style={{ ...selectStyle, width: 180 }}
              onFocus={(e) => {
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.boxShadow = 'none';
              }}
            >
              <option value="">{strings.auditFilterTargetTypePlaceholder}</option>
              {Object.entries(TARGET_TYPE_LABELS).map(([key, labels]) => (
                <option key={key} value={key}>
                  {labels[ADMIN_LOCALE]}
                </option>
              ))}
            </select>
          </div>

          {/* Date from */}
          <div>
            <label htmlFor="audit-date-from" className="sr-only">
              {strings.auditFilterDateFromLabel}
            </label>
            <input
              id="audit-date-from"
              type="date"
              data-testid="audit-filter-date-from"
              value={dateFrom}
              onChange={(e) => handleFilterChange(setDateFrom, e.target.value)}
              style={{ ...inputStyle, width: 160 }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-primary)';
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = hasDateRangeError
                  ? 'var(--lx-danger)'
                  : 'var(--lx-border)';
                e.currentTarget.style.boxShadow = 'none';
              }}
            />
          </div>

          {/* Date to */}
          <div>
            <label htmlFor="audit-date-to" className="sr-only">
              {strings.auditFilterDateToLabel}
            </label>
            <input
              id="audit-date-to"
              type="date"
              data-testid="audit-filter-date-to"
              value={dateTo}
              onChange={(e) => handleFilterChange(setDateTo, e.target.value)}
              style={{ ...inputStyle, width: 160 }}
              onFocus={(e) => {
                e.currentTarget.style.borderColor = 'var(--lx-primary)';
                e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
              }}
              onBlur={(e) => {
                e.currentTarget.style.borderColor = hasDateRangeError
                  ? 'var(--lx-danger)'
                  : 'var(--lx-border)';
                e.currentTarget.style.boxShadow = 'none';
              }}
            />
          </div>

          {/* Clear filters — shown when any filter is active */}
          {hasActiveFilters && (
            <button
              type="button"
              data-testid="audit-clear-filters"
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
                whiteSpace: 'nowrap',
              }}
            >
              {strings.auditClearFilters}
            </button>
          )}
        </Stack>

        {/* Date range validation note — shown below filter bar */}
        {hasDateRangeError && (
          <div
            style={{
              fontSize: 12,
              color: 'var(--lx-danger)',
              fontFamily: 'inherit',
            }}
            role="alert"
          >
            {strings.auditDateRangeError}
          </div>
        )}

        {/* Results area — aria-live for screen reader announcements */}
        <div aria-live="polite" aria-atomic="false">

          {/* State: Loading (skeleton) */}
          {showSkeleton && (
            <div
              role="status"
              aria-label={strings.auditLoadingLabel}
              data-testid="audit-loading"
              style={{
                backgroundColor: 'var(--lx-card)',
                borderRadius: 'var(--lx-radius-card)',
                overflow: 'hidden',
                border: '1px solid var(--lx-border)',
              }}
            >
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <caption className="sr-only">{strings.auditTableCaption}</caption>
                <thead>
                  <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                    {[
                      strings.auditColAdmin,
                      strings.auditColAction,
                      strings.auditColTarget,
                      strings.auditColWhen,
                    ].map((col) => (
                      <th key={col} scope="col" style={thStyle}>{col}</th>
                    ))}
                    <th scope="col" className="sr-only">{strings.auditColDetailsHeader}</th>
                  </tr>
                </thead>
                <tbody>
                  {Array.from({ length: 6 }).map((_, i) => (
                    <AuditSkeletonRow key={i} />
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {/* State: Error */}
          {showError && (
            <Stack
              flexDirection="column"
              gap="$4"
              data-testid="audit-error-banner"
            >
              <AdminErrorBanner
                variant="error"
                message={strings.auditListError}
              />
              <button
                type="button"
                data-testid="audit-retry-button"
                onClick={() => void refetch()}
                style={{
                  alignSelf: 'flex-start',
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
                {strings.auditRetry}
              </button>
            </Stack>
          )}

          {/* State: Empty */}
          {showEmpty && (
            <Stack
              alignItems="center"
              justifyContent="center"
              padding="$10"
              gap="$4"
              data-testid="audit-empty-state"
              style={{
                backgroundColor: 'var(--lx-card)',
                borderRadius: 'var(--lx-radius-card)',
                border: '1px solid var(--lx-border)',
              }}
            >
              {/* scroll-text SVG icon placeholder */}
              <span style={{ color: 'var(--lx-fg3)' }}>
                <svg
                  width="40"
                  height="40"
                  viewBox="0 0 24 24"
                  fill="none"
                  aria-hidden="true"
                  strokeWidth="1.5"
                  stroke="currentColor"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                >
                  {/* Lucide scroll-text paths */}
                  <path d="M8 21h12a2 2 0 0 0 2-2v-2H10v2a2 2 0 1 1-4 0V5a2 2 0 1 0-4 0v3h4" />
                  <path d="M19 17V5a2 2 0 0 0-2-2H4" />
                  <path d="M15 8h-5" />
                  <path d="M15 12h-5" />
                </svg>
              </span>

              <Text
                fontFamily="$heading"
                fontSize={18}
                fontWeight="600"
                color="$fg1"
              >
                {strings.auditEmptyHeading}
              </Text>

              <Text
                fontFamily="$body"
                fontSize={14}
                color="$fg3"
                style={{ textAlign: 'center', maxWidth: 320 }}
              >
                {hasActiveFilters
                  ? strings.auditEmptyBodyFiltered
                  : strings.auditEmptyBodyEmpty}
              </Text>

              {hasActiveFilters && (
                <button
                  type="button"
                  onClick={clearFilters}
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
                  {strings.auditClearFilters}
                </button>
              )}
            </Stack>
          )}

          {/* State: Results */}
          {showResults && (
            <Stack flexDirection="column" gap="$4">
              {/* SR-only result count announcement */}
              <span className="sr-only" aria-live="polite">
                {totalCount} {strings.auditResultCount}
              </span>

              {/* Table wrapper */}
              <div
                data-testid="audit-table-wrapper"
                style={{
                  backgroundColor: 'var(--lx-card)',
                  borderRadius: 'var(--lx-radius-card)',
                  overflow: 'hidden',
                  border: '1px solid var(--lx-border)',
                  opacity: tableOpacity,
                  transition: 'opacity var(--lx-dur-fast) var(--lx-ease-out)',
                }}
              >
                <table
                  data-testid="audit-table"
                  style={{ width: '100%', borderCollapse: 'collapse' }}
                >
                  <caption className="sr-only">{strings.auditTableCaption}</caption>
                  <thead>
                    <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                      <th scope="col" style={thStyle}>{strings.auditColAdmin}</th>
                      <th scope="col" style={{ ...thStyle, minWidth: 180 }}>{strings.auditColAction}</th>
                      <th scope="col" style={{ ...thStyle, minWidth: 140 }}>{strings.auditColTarget}</th>
                      <th scope="col" style={{ ...thStyle, minWidth: 160, whiteSpace: 'nowrap' }}>{strings.auditColWhen}</th>
                      <th scope="col" className="sr-only">{strings.auditColDetailsHeader}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {entries.map((entry) => (
                      <AuditRow
                        key={entry.id}
                        entry={entry}
                        isExpanded={expandedIds.has(entry.id)}
                        onToggle={toggleExpand}
                      />
                    ))}
                  </tbody>
                </table>
              </div>

              {/* Pagination — shown only when there are multiple pages */}
              {totalPages > 1 && (
                <AuditPagination
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
