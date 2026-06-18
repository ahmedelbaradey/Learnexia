'use client';

// Force dynamic rendering — this page fetches live admin data and must NOT be
// statically pre-rendered. Without this, the Next.js build fails when
// NEXT_PUBLIC_API_URL is not set (the api-client initializer throws at
// prerender time). All admin routes are behind the guard; static snapshots
// would have no useful content anyway.
export const dynamic = 'force-dynamic';

/**
 * Users list page — `/users` (P7-06-FE-1).
 *
 * Server-paginated, filterable, debounced-searchable table of user accounts.
 * Consumes `useSearchUsers` from `@learnexia/api-client`.
 *
 * Four states: loading-skeleton / empty / error+retry / results.
 * Filters: role (Parent | Student), status (Active | Suspended), free-text Q.
 * Pagination: server-side; filter/query change resets to page 1.
 * `placeholderData` keeps previous rows visible during refetch.
 *
 * Design Spec Part B §B.1–B.6.
 * Acceptance criteria: P7-06 AC 1, 2, 9, 10.
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'next/navigation';

import {
  useSearchUsers,
  type SearchUsersFilters,
  type AdminUserListItemDto,
} from '@learnexia/api-client';
import { ACCOUNT_STATUS } from '@learnexia/shared';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';
import { StatusBadge } from '../../../components/StatusBadge';
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

// ── Skeleton rows ──────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr
      style={{
        borderBottom: '1px solid var(--lx-border)',
      }}
    >
      {[200, 260, 90, 90, 110].map((w, i) => (
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

// ── Avatar initials chip ───────────────────────────────────────────────────────

function AvatarChip({ name }: { name: string }) {
  const initial = name.trim().charAt(0).toUpperCase() || '?';
  return (
    <span
      aria-hidden="true"
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: 28,
        height: 28,
        borderRadius: '50%',
        backgroundColor: 'rgba(79,70,229,0.18)',
        color: '#4F46E5',
        fontSize: 11,
        fontWeight: 700,
        marginInlineEnd: 8,
        flexShrink: 0,
      }}
    >
      {initial}
    </span>
  );
}

// ── Pagination control ─────────────────────────────────────────────────────────

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

  // Page text uses tabular nums; direction wrapper is ltr for technical string
  const pageText =
    ADMIN_LOCALE === 'ar'
      ? `الصفحة ${currentPage} من ${totalPages}`
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
        aria-label={strings.usersPrevPage}
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
        style={{ fontVariantNumeric: 'tabular-nums' }}
      >
        {pageText}
      </Text>

      <button
        type="button"
        aria-label={strings.usersNextPage}
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
  width: 140,
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

// ── Main page ──────────────────────────────────────────────────────────────────

const PAGE_SIZE = 20;

export default function UsersPage() {
  const router = useRouter();

  // Filter state
  const [role, setRole] = useState('');
  const [status, setStatus] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);

  // Debounce the free-text search 350ms
  const debouncedQuery = useDebounce(query, 350);

  // Reset to page 1 when filters or query change
  const prevFiltersRef = useRef({ role, status, debouncedQuery });
  useEffect(() => {
    const prev = prevFiltersRef.current;
    if (
      prev.role !== role ||
      prev.status !== status ||
      prev.debouncedQuery !== debouncedQuery
    ) {
      setPage(1);
      prevFiltersRef.current = { role, status, debouncedQuery };
    }
  }, [role, status, debouncedQuery]);

  const hasActiveFilters = role !== '' || status !== '' || query !== '';

  const filters: SearchUsersFilters = {
    ...(role ? { role } : {}),
    ...(status !== '' ? { status: parseInt(status, 10) as typeof ACCOUNT_STATUS[keyof typeof ACCOUNT_STATUS] } : {}),
    ...(debouncedQuery ? { q: debouncedQuery } : {}),
    pageNumber: page,
    pageSize: PAGE_SIZE,
  };

  const { data, isLoading, isError, refetch, isFetching } = useSearchUsers(filters);

  // Use keepPreviousData pattern: data stays while next page loads
  // (hook uses placeholderData via keepPreviousData)
  const users: AdminUserListItemDto[] = data?.data ?? [];
  const totalPages = data?.totalPages ?? 1;
  const totalCount = data?.totalCount ?? 0;

  const clearFilters = useCallback(() => {
    setRole('');
    setStatus('');
    setQuery('');
    setPage(1);
  }, []);

  const handleRowClick = useCallback(
    (id: number) => {
      router.push(`/users/${id}`);
    },
    [router],
  );

  // Determine display state
  const showSkeleton = isLoading;
  const showError = !isLoading && isError;
  const showEmpty = !isLoading && !isError && users.length === 0;
  const showResults = !isLoading && !isError && users.length > 0;

  const tableOpacity = isFetching && !isLoading ? 0.6 : 1;

  return (
    <AdminShell title={strings.pageTitleUsers}>
    <Stack flexDirection="column" gap="$6">
      {/* Page header */}
      <Stack flexDirection="row" alignItems="center" justifyContent="space-between" gap="$4">
        <Text fontFamily="$heading" fontSize={24} fontWeight="700" color="$fg1">
          {strings.usersListHeading}
        </Text>
        {showResults && (
          <Stack
            tag="span"
            paddingHorizontal={10}
            paddingVertical={4}
            borderRadius="$pill"
            style={{ backgroundColor: 'var(--lx-card-soft)' }}
          >
            <Text fontFamily="$body" fontSize={12} color="$fg3">
              {totalCount} {strings.usersResultCount}
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
        {/* Search input */}
        <div style={{ flex: 1, minWidth: 200, maxWidth: 340, position: 'relative' }}>
          <label htmlFor="users-search" className="sr-only">
            {strings.usersListSearchPlaceholder}
          </label>
          <span
            aria-hidden="true"
            style={{
              position: 'absolute',
              insetInlineStart: 12,
              top: '50%',
              transform: 'translateY(-50%)',
              color: 'var(--lx-fg3)',
              display: 'flex',
              alignItems: 'center',
              pointerEvents: 'none',
            }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
              <circle cx="11" cy="11" r="8" stroke="currentColor" strokeWidth="2" />
              <path d="m21 21-4.35-4.35" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </span>
          <input
            id="users-search"
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={strings.usersListSearchPlaceholder}
            style={{
              display: 'block',
              width: '100%',
              height: 44,
              backgroundColor: 'var(--lx-bg)',
              borderRadius: 'var(--lx-radius-sm)',
              border: '1px solid var(--lx-border)',
              paddingInline: '40px 16px',
              color: 'var(--lx-fg1)',
              fontSize: 14,
              fontFamily: 'inherit',
              outline: 'none',
              boxSizing: 'border-box',
            }}
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

        {/* Role filter */}
        <div>
          <label htmlFor="users-role-filter" className="sr-only">
            {strings.usersListRoleFilterLabel}
          </label>
          <select
            id="users-role-filter"
            value={role}
            onChange={(e) => setRole(e.target.value)}
            style={selectStyle}
            onFocus={(e) => {
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.boxShadow = 'none';
            }}
          >
            <option value="">{strings.usersFilterAllRoles}</option>
            <option value="Parent">{strings.usersListRoleOptionParent}</option>
            <option value="Student">{strings.usersListRoleOptionStudent}</option>
          </select>
        </div>

        {/* Status filter */}
        <div>
          <label htmlFor="users-status-filter" className="sr-only">
            {strings.usersListStatusFilterLabel}
          </label>
          <select
            id="users-status-filter"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
            style={selectStyle}
            onFocus={(e) => {
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.boxShadow = 'none';
            }}
          >
            <option value="">{strings.usersFilterAllStatuses}</option>
            <option value={String(ACCOUNT_STATUS.Active)}>{strings.usersListStatusOptionActive}</option>
            <option value={String(ACCOUNT_STATUS.Suspended)}>{strings.usersListStatusOptionSuspended}</option>
          </select>
        </div>

        {/* Clear filters */}
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
              fontWeight: 500,
              cursor: 'pointer',
              fontFamily: 'inherit',
              whiteSpace: 'nowrap',
            }}
          >
            {strings.usersClearFilters}
          </button>
        )}
      </Stack>

      {/* Results area — aria-live for screen reader announcements */}
      <div aria-live="polite" aria-atomic="false">
        {showSkeleton && (
          <div
            role="status"
            aria-label={strings.usersListLoadingLabel}
            style={{
              backgroundColor: 'var(--lx-card)',
              borderRadius: 'var(--lx-radius-card)',
              overflow: 'hidden',
              border: '1px solid var(--lx-border)',
            }}
          >
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <caption className="sr-only">{strings.usersTableCaption}</caption>
              <thead>
                <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                  {[strings.usersColName, strings.usersColEmail, strings.usersColRole, strings.usersColStatus, strings.usersColCreated].map(
                    (col) => (
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
                          fontFamily: 'inherit',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {col}
                      </th>
                    ),
                  )}
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

        {showError && (
          <Stack flexDirection="column" gap="$4">
            <AdminErrorBanner
              variant="error"
              message={strings.usersListError}
            />
            <button
              type="button"
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
              {strings.usersListRetry}
            </button>
          </Stack>
        )}

        {showEmpty && (
          <Stack
            alignItems="center"
            justifyContent="center"
            padding="$10"
            gap="$4"
            style={{
              backgroundColor: 'var(--lx-card)',
              borderRadius: 'var(--lx-radius-card)',
              border: '1px solid var(--lx-border)',
            }}
          >
            <span style={{ color: 'var(--lx-fg3)' }}>
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <circle cx="11" cy="11" r="8" stroke="currentColor" strokeWidth="1.5" />
                <path d="m21 21-4.35-4.35" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
                <path d="M8 11h6M11 8v6" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
              </svg>
            </span>
            <Text fontFamily="$heading" fontSize={18} fontWeight="600" color="$fg1">
              {strings.usersNoResults}
            </Text>
            <Text
              fontFamily="$body"
              fontSize={14}
              color="$fg3"
              style={{ textAlign: 'center', maxWidth: 320 }}
            >
              {strings.usersNoResultsHint}
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
                {strings.usersClearFilters}
              </button>
            )}
          </Stack>
        )}

        {showResults && (
          <Stack flexDirection="column" gap="$4">
            {/* Result count announcement */}
            <span className="sr-only" aria-live="polite">
              {totalCount} {strings.usersResultCount}
            </span>

            {/* Table */}
            <div
              style={{
                backgroundColor: 'var(--lx-card)',
                borderRadius: 'var(--lx-radius-card)',
                overflow: 'hidden',
                border: '1px solid var(--lx-border)',
                opacity: tableOpacity,
                transition: 'opacity var(--lx-dur-fast) var(--lx-ease-out)',
              }}
            >
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <caption className="sr-only">{strings.usersTableCaption}</caption>
                <thead>
                  <tr style={{ backgroundColor: 'var(--lx-card-soft)' }}>
                    {[
                      { key: 'name', label: strings.usersColName },
                      { key: 'email', label: strings.usersColEmail },
                      { key: 'role', label: strings.usersColRole },
                      { key: 'status', label: strings.usersColStatus },
                      { key: 'created', label: strings.usersColCreated },
                    ].map(({ key, label }) => (
                      <th
                        key={key}
                        scope="col"
                        style={{
                          padding: '12px 16px',
                          textAlign: 'start',
                          fontSize: 11,
                          fontWeight: 600,
                          color: 'var(--lx-fg3)',
                          textTransform: 'uppercase',
                          letterSpacing: '0.06em',
                          fontFamily: 'inherit',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => (
                    <UserRow
                      key={user.id}
                      user={user}
                      onClick={() => handleRowClick(user.id)}
                    />
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

// ── User row ───────────────────────────────────────────────────────────────────

interface UserRowProps {
  user: AdminUserListItemDto;
  onClick: () => void;
}

function formatDate(iso: string): string {
  try {
    return new Intl.DateTimeFormat(ADMIN_LOCALE === 'ar' ? 'ar-EG' : 'en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function UserRow({ user, onClick }: UserRowProps) {
  return (
    <tr
      tabIndex={0}
      role="button"
      aria-label={`${user.fullName} — ${strings.usersViewProfile}`}
      onClick={onClick}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onClick();
        }
      }}
      style={{
        borderBottom: '1px solid var(--lx-border)',
        cursor: 'pointer',
        transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
      }}
      onMouseEnter={(e) => {
        (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'var(--lx-card-soft)';
      }}
      onMouseLeave={(e) => {
        (e.currentTarget as HTMLTableRowElement).style.backgroundColor = '';
      }}
      onMouseDown={(e) => {
        (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'rgba(79,70,229,0.08)';
      }}
      onMouseUp={(e) => {
        (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'var(--lx-card-soft)';
      }}
    >
      {/* Name */}
      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
        <div style={{ display: 'flex', alignItems: 'center' }}>
          <AvatarChip name={user.fullName} />
          <span
            style={{
              fontSize: 14,
              fontWeight: 600,
              color: 'var(--lx-fg1)',
              fontFamily: 'inherit',
            }}
          >
            {user.fullName}
          </span>
        </div>
      </td>

      {/* Email */}
      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
        <span
          dir="ltr"
          style={{
            fontFamily: 'var(--lx-font-mono, monospace)',
            fontSize: 13,
            color: 'var(--lx-fg2)',
          }}
        >
          {user.email}
        </span>
      </td>

      {/* Role */}
      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
        <StatusBadge variant="role" value={user.role} strings={strings} />
      </td>

      {/* Status */}
      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
        <StatusBadge variant="status" value={user.accountStatus} strings={strings} />
      </td>

      {/* Created date */}
      <td style={{ padding: '12px 16px', verticalAlign: 'middle' }}>
        <span
          dir="ltr"
          style={{
            fontSize: 13,
            color: 'var(--lx-fg3)',
            fontVariantNumeric: 'tabular-nums',
          }}
        >
          {formatDate(user.createdAt)}
        </span>
      </td>
    </tr>
  );
}
