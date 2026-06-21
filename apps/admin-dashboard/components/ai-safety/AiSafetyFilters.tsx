'use client';

/**
 * AiSafetyFilters — global date-range filter bar for P7-11 AI Safety dashboard.
 *
 * All five panels (Signals, Trend, Eval, Usage, Flagged) consume the From/To
 * date range. The date values are lifted to the parent page component and passed
 * down to each section hook as PascalCase `From`/`To` params (controller binding).
 *
 * Note: `evals` endpoint ignores date params — it takes no params at all.
 */

import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

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
  width: 160,
};

const clearBtnStyle: React.CSSProperties = {
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
};

export interface AiSafetyFiltersProps {
  from: string;
  to: string;
  hasDateRangeError: boolean;
  onFromChange: (v: string) => void;
  onToChange: (v: string) => void;
  onClear: () => void;
}

export function AiSafetyFilters({
  from,
  to,
  hasDateRangeError,
  onFromChange,
  onToChange,
  onClear,
}: AiSafetyFiltersProps) {
  const hasActiveFilters = from !== '' || to !== '';

  return (
    <div>
      <div
        style={{
          display: 'flex',
          flexDirection: 'row',
          flexWrap: 'wrap',
          gap: 12,
          alignItems: 'flex-end',
        }}
      >
        {/* Date From */}
        <div>
          <label htmlFor="ai-safety-date-from" className="sr-only">
            {strings.aiSafetyDateFrom}
          </label>
          <input
            id="ai-safety-date-from"
            type="date"
            data-testid="ai-safety-filter-date-from"
            value={from}
            onChange={(e) => onFromChange(e.target.value)}
            style={{
              ...inputStyle,
              borderColor: hasDateRangeError ? 'var(--lx-danger)' : 'var(--lx-border)',
            }}
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

        {/* Date To */}
        <div>
          <label htmlFor="ai-safety-date-to" className="sr-only">
            {strings.aiSafetyDateTo}
          </label>
          <input
            id="ai-safety-date-to"
            type="date"
            data-testid="ai-safety-filter-date-to"
            value={to}
            onChange={(e) => onToChange(e.target.value)}
            style={{
              ...inputStyle,
              borderColor: hasDateRangeError ? 'var(--lx-danger)' : 'var(--lx-border)',
            }}
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

        {/* Clear */}
        {hasActiveFilters && (
          <button
            type="button"
            data-testid="ai-safety-clear-filters"
            onClick={onClear}
            style={clearBtnStyle}
          >
            {strings.aiSafetyClearFilters}
          </button>
        )}
      </div>

      {/* Date range error */}
      {hasDateRangeError && (
        <div
          role="alert"
          data-testid="ai-safety-date-range-error"
          style={{
            fontSize: 12,
            color: 'var(--lx-danger)',
            fontFamily: 'inherit',
            marginTop: 6,
          }}
        >
          {strings.aiSafetyDateRangeError}
        </div>
      )}
    </div>
  );
}
