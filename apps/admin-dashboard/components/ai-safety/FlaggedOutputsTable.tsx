'use client';

/**
 * FlaggedOutputsTable — paginated table of AI safety flagged outputs for P7-11.
 *
 * PII-LIGHT INVARIANT: `studentId` from `FlaggedOutputDto` MUST NEVER be rendered
 * on screen. Only `contentRef` (SafetyEvent.Id — an opaque integer, NOT a student
 * identifier) is shown as a reference ID.
 *
 * 7 columns: Ref# / TaskKind / Action / Reason Codes / Failed Checks / Model / Occurred.
 * 3 filter selects: Action / ReasonCode / TaskKind (all PascalCase server params).
 * Pagination mirrors the audit/moderation log pattern.
 */

import { useState, useCallback } from 'react';
import { useFlaggedOutputs } from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

const PAGE_SIZE = 25;

// ── Shared styles ──────────────────────────────────────────────────────────────

const sectionCardStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-card)',
  borderRadius: 'var(--lx-radius-card)',
  border: '1px solid var(--lx-border)',
  padding: 20,
  display: 'flex',
  flexDirection: 'column',
  gap: 20,
};

const headingStyle: React.CSSProperties = {
  fontSize: 14,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  fontFamily: 'Poppins, inherit',
};

const selectStyle: React.CSSProperties = {
  height: 36,
  paddingInline: 10,
  borderRadius: 'var(--lx-radius-sm)',
  border: '1px solid var(--lx-border)',
  backgroundColor: 'var(--lx-bg)',
  color: 'var(--lx-fg1)',
  fontSize: 13,
  fontFamily: 'inherit',
  cursor: 'pointer',
};

const thStyle: React.CSSProperties = {
  padding: '8px 10px',
  fontSize: 11,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.05em',
  textAlign: 'left',
  borderBottom: '1px solid var(--lx-border)',
  whiteSpace: 'nowrap',
};

const tdStyle: React.CSSProperties = {
  padding: '10px 10px',
  fontSize: 13,
  color: 'var(--lx-fg1)',
  borderBottom: '1px solid var(--lx-border)',
  verticalAlign: 'top',
};

const shimmerStyle: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1400ms linear infinite',
  borderRadius: 6,
};

// ── Action badge chip ─────────────────────────────────────────────────────────

const ACTION_COLORS: Record<string, { bg: string; color: string }> = {
  Blocked: { bg: 'rgba(239,68,68,0.12)', color: '#EF4444' },
  Regenerated: { bg: 'rgba(245,158,11,0.12)', color: '#F59E0B' },
  FallbackReturned: { bg: 'rgba(168,85,247,0.12)', color: '#A855F7' },
  Logged: { bg: 'rgba(34,197,94,0.08)', color: '#22C55E' },
};

function ActionBadge({ action }: { action: string }) {
  const colors = ACTION_COLORS[action] ?? { bg: 'rgba(100,116,139,0.12)', color: '#64748B' };
  return (
    <span
      style={{
        display: 'inline-block',
        paddingInline: 8,
        paddingBlock: 2,
        borderRadius: 9999,
        fontSize: 11,
        fontWeight: 600,
        backgroundColor: colors.bg,
        color: colors.color,
        whiteSpace: 'nowrap',
      }}
    >
      {action}
    </span>
  );
}

// ── Skeleton row ──────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr>
      {[60, 100, 90, 160, 140, 120, 120].map((w, i) => (
        <td key={i} style={{ ...tdStyle, paddingBlock: 14 }}>
          <div role="presentation" style={{ ...shimmerStyle, width: w, height: 14 }} />
        </td>
      ))}
    </tr>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

interface FlaggedOutputsTableProps {
  From?: string;
  To?: string;
}

export function FlaggedOutputsTable({ From, To }: FlaggedOutputsTableProps) {
  const [action, setAction] = useState('');
  const [reasonCode, setReasonCode] = useState('');
  const [taskKind, setTaskKind] = useState('');
  const [page, setPage] = useState(1);

  const clearFilters = useCallback(() => {
    setAction('');
    setReasonCode('');
    setTaskKind('');
    setPage(1);
  }, []);

  const { data, isLoading, isError, isFetching, refetch } = useFlaggedOutputs({
    Action: action || undefined,
    ReasonCode: reasonCode || undefined,
    TaskKind: taskKind || undefined,
    From,
    To,
    PageNumber: page,
    PageSize: PAGE_SIZE,
  });

  const totalCount = data?.totalCount ?? 0;
  const rows = data?.data ?? [];
  const totalPages = totalCount > 0 ? Math.ceil(totalCount / PAGE_SIZE) : 1;
  const hasFilters = action !== '' || reasonCode !== '' || taskKind !== '';

  return (
    <div style={sectionCardStyle} data-testid="flagged-table-section">
      <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
        <span style={headingStyle}>{strings.aiSafetyFlaggedHeading}</span>
        {(isLoading || isFetching) && (
          <span
            style={{ fontSize: 11, color: 'var(--lx-fg3)', fontStyle: 'italic' }}
          >
            {strings.aiSafetyLoadingLabel}
          </span>
        )}
      </div>

      {/* Filters */}
      <div style={{ display: 'flex', flexDirection: 'row', flexWrap: 'wrap', gap: 10, alignItems: 'center' }}>
        {/* Action filter */}
        <div>
          <label htmlFor="flagged-filter-action" className="sr-only">
            {strings.aiSafetyFlaggedFilterAction}
          </label>
          <select
            id="flagged-filter-action"
            data-testid="flagged-filter-action"
            value={action}
            onChange={(e) => { setAction(e.target.value); setPage(1); }}
            style={selectStyle}
          >
            <option value="">{strings.aiSafetyFlaggedAllActions}</option>
            <option value="Blocked">{strings.aiSafetyFlaggedActionBlocked}</option>
            <option value="Regenerated">{strings.aiSafetyFlaggedActionRegenerated}</option>
            <option value="FallbackReturned">{strings.aiSafetyFlaggedActionFallback}</option>
            <option value="Logged">{strings.aiSafetyFlaggedActionLogged}</option>
          </select>
        </div>

        {/* ReasonCode filter */}
        <div>
          <label htmlFor="flagged-filter-reason" className="sr-only">
            {strings.aiSafetyFlaggedFilterReason}
          </label>
          <select
            id="flagged-filter-reason"
            data-testid="flagged-filter-reason"
            value={reasonCode}
            onChange={(e) => { setReasonCode(e.target.value); setPage(1); }}
            style={selectStyle}
          >
            <option value="">{strings.aiSafetyFlaggedAllReasons}</option>
            <option value="PROFANITY">{strings.aiSafetyFlaggedReasonProfanity}</option>
            <option value="HATE_SPEECH">{strings.aiSafetyFlaggedReasonHateSpeech}</option>
            <option value="VIOLENCE">{strings.aiSafetyFlaggedReasonViolence}</option>
            <option value="ADULT_CONTENT">{strings.aiSafetyFlaggedReasonAdultContent}</option>
            <option value="PROMPT_INJECTION">{strings.aiSafetyFlaggedReasonPromptInjection}</option>
            <option value="OFF_TOPIC">{strings.aiSafetyFlaggedReasonOffTopic}</option>
          </select>
        </div>

        {/* TaskKind filter */}
        <div>
          <label htmlFor="flagged-filter-taskkind" className="sr-only">
            {strings.aiSafetyFlaggedFilterTaskKind}
          </label>
          <select
            id="flagged-filter-taskkind"
            data-testid="flagged-filter-taskkind"
            value={taskKind}
            onChange={(e) => { setTaskKind(e.target.value); setPage(1); }}
            style={selectStyle}
          >
            <option value="">{strings.aiSafetyFlaggedAllTaskKinds}</option>
            <option value="Hint">{strings.aiSafetyFlaggedTaskKindHint}</option>
            <option value="Explanation">{strings.aiSafetyFlaggedTaskKindExplanation}</option>
            <option value="Question">{strings.aiSafetyFlaggedTaskKindQuestion}</option>
            <option value="Feedback">{strings.aiSafetyFlaggedTaskKindFeedback}</option>
          </select>
        </div>

        {/* Clear */}
        {hasFilters && (
          <button
            type="button"
            data-testid="flagged-clear-filters"
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
            {strings.aiSafetyClearFilters}
          </button>
        )}
      </div>

      {/* Error */}
      {isError && (
        <div data-testid="flagged-error">
          <AdminErrorBanner variant="error" message={strings.aiSafetyFlaggedLoadError} />
          <button
            type="button"
            onClick={() => void refetch()}
            style={{
              marginTop: 8,
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
            {strings.aiSafetyRetry}
          </button>
        </div>
      )}

      {/* Table */}
      <div style={{ overflowX: 'auto' }}>
        <table
          data-testid="flagged-outputs-table"
          style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}
        >
          <caption className="sr-only">{strings.aiSafetyFlaggedTableCaption}</caption>
          <thead>
            <tr>
              <th scope="col" style={thStyle}>{strings.aiSafetyFlaggedColContentRef}</th>
              <th scope="col" style={thStyle}>{strings.aiSafetyFlaggedColTaskKind}</th>
              <th scope="col" style={thStyle}>{strings.aiSafetyFlaggedColAction}</th>
              <th scope="col" style={{ ...thStyle, maxWidth: 200 }}>{strings.aiSafetyFlaggedColReasonCodes}</th>
              <th scope="col" style={{ ...thStyle, maxWidth: 200 }}>{strings.aiSafetyFlaggedColFailedChecks}</th>
              <th scope="col" style={thStyle}>{strings.aiSafetyFlaggedColModel}</th>
              <th scope="col" style={thStyle}>{strings.aiSafetyFlaggedColOccurredAt}</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)
            ) : rows.length === 0 ? (
              <tr>
                <td
                  colSpan={7}
                  style={{
                    ...tdStyle,
                    textAlign: 'center',
                    color: 'var(--lx-fg3)',
                    paddingBlock: 32,
                  }}
                  data-testid="flagged-empty"
                >
                  {strings.aiSafetyFlaggedEmpty}
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr
                  key={row.contentRef}
                  data-testid="flagged-row"
                  style={{ transition: 'background-color var(--lx-dur-fast)' }}
                  onMouseEnter={(e) => {
                    (e.currentTarget as HTMLTableRowElement).style.backgroundColor =
                      'rgba(255,255,255,0.02)';
                  }}
                  onMouseLeave={(e) => {
                    (e.currentTarget as HTMLTableRowElement).style.backgroundColor = '';
                  }}
                >
                  {/* Ref# — contentRef (SafetyEvent.Id), NOT studentId */}
                  <td style={{ ...tdStyle, fontVariantNumeric: 'tabular-nums', fontFamily: 'monospace', fontSize: 12, color: 'var(--lx-fg3)' }}>
                    #{row.contentRef}
                  </td>
                  <td style={tdStyle}>{row.taskKind}</td>
                  <td style={tdStyle}>
                    <ActionBadge action={row.actionTaken} />
                  </td>
                  <td style={{ ...tdStyle, maxWidth: 200 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                      {row.reasonCodes.map((r) => (
                        <span
                          key={r}
                          style={{
                            fontSize: 11,
                            color: 'var(--lx-fg3)',
                            backgroundColor: 'rgba(100,116,139,0.1)',
                            borderRadius: 4,
                            paddingInline: 4,
                            paddingBlock: 1,
                            display: 'inline-block',
                          }}
                        >
                          {r}
                        </span>
                      ))}
                    </div>
                  </td>
                  <td style={{ ...tdStyle, maxWidth: 200 }}>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                      {row.failedChecks.map((c) => (
                        <span
                          key={c}
                          style={{
                            fontSize: 11,
                            color: '#F59E0B',
                            backgroundColor: 'rgba(245,158,11,0.08)',
                            borderRadius: 4,
                            paddingInline: 4,
                            paddingBlock: 1,
                            display: 'inline-block',
                          }}
                        >
                          {c}
                        </span>
                      ))}
                    </div>
                  </td>
                  <td style={{ ...tdStyle, fontSize: 12, color: 'var(--lx-fg3)' }}>{row.modelId}</td>
                  <td style={{ ...tdStyle, fontSize: 12, color: 'var(--lx-fg3)', whiteSpace: 'nowrap' }}>
                    {new Date(row.occurredAtUtc).toLocaleString('en-US')}
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {totalCount > PAGE_SIZE && (
        <div
          data-testid="flagged-pagination"
          style={{
            display: 'flex',
            flexDirection: 'row',
            alignItems: 'center',
            justifyContent: 'space-between',
            gap: 8,
          }}
        >
          <span style={{ fontSize: 12, color: 'var(--lx-fg3)' }}>
            {strings.aiSafetyFlaggedPaginationInfo
              .replace('{page}', String(page))
              .replace('{total}', String(totalPages))}
          </span>
          <div style={{ display: 'flex', flexDirection: 'row', gap: 8 }}>
            <button
              type="button"
              data-testid="flagged-prev-page"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              style={{
                height: 32,
                paddingInline: 12,
                borderRadius: 'var(--lx-radius-sm)',
                border: '1px solid var(--lx-border)',
                backgroundColor: 'transparent',
                color: page <= 1 ? 'var(--lx-fg3)' : 'var(--lx-fg1)',
                fontSize: 13,
                cursor: page <= 1 ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit',
                opacity: page <= 1 ? 0.5 : 1,
              }}
            >
              {strings.aiSafetyPrevPage}
            </button>
            <button
              type="button"
              data-testid="flagged-next-page"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              style={{
                height: 32,
                paddingInline: 12,
                borderRadius: 'var(--lx-radius-sm)',
                border: '1px solid var(--lx-border)',
                backgroundColor: 'transparent',
                color: page >= totalPages ? 'var(--lx-fg3)' : 'var(--lx-fg1)',
                fontSize: 13,
                cursor: page >= totalPages ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit',
                opacity: page >= totalPages ? 0.5 : 1,
              }}
            >
              {strings.aiSafetyNextPage}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
