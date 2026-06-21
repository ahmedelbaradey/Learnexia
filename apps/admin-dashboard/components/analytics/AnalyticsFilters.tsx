'use client';

/**
 * AnalyticsFilters — date-range + client-side slice selectors for P7-10.
 *
 * Date "From"/"To" drive `usePlatformKpis` (re-fetch on change).
 * Subject/grade/language slice selects are client-side only — they highlight/
 * filter breakdown chart data without triggering a new API request.
 *
 * `from`/`to` are parent state that drives the hook. Slices are passed up to
 * the parent so `KpiBreakdownCharts` can receive them.
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

const GRADES = Array.from({ length: 12 }, (_, i) => i + 1);

// Subject code → display name
const SUBJECT_OPTIONS: Array<{ code: number; label: string }> = [
  { code: 1, label: strings.analyticsSubjectMath },
  { code: 2, label: strings.analyticsSubjectScience },
  { code: 3, label: strings.analyticsSubjectArabic },
  { code: 4, label: strings.analyticsSubjectEnglish },
];

// Language code → display name
const LANGUAGE_OPTIONS: Array<{ code: number; label: string }> = [
  { code: 0, label: strings.analyticsLanguageArabic },
  { code: 1, label: strings.analyticsLanguageEnglish },
];

export interface AnalyticsFiltersProps {
  from: string;
  to: string;
  subjectSlice: number | null;
  gradeSlice: number | null;
  languageSlice: number | null;
  hasDateRangeError: boolean;
  onFromChange: (v: string) => void;
  onToChange: (v: string) => void;
  onSubjectSliceChange: (v: number | null) => void;
  onGradeSliceChange: (v: number | null) => void;
  onLanguageSliceChange: (v: number | null) => void;
  onClear: () => void;
}

export function AnalyticsFilters({
  from,
  to,
  subjectSlice,
  gradeSlice,
  languageSlice,
  hasDateRangeError,
  onFromChange,
  onToChange,
  onSubjectSliceChange,
  onGradeSliceChange,
  onLanguageSliceChange,
  onClear,
}: AnalyticsFiltersProps) {
  const hasActiveFilters =
    from !== '' || to !== '' || subjectSlice !== null || gradeSlice !== null || languageSlice !== null;

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
          <label htmlFor="analytics-date-from" className="sr-only">
            {strings.analyticsDateFrom}
          </label>
          <input
            id="analytics-date-from"
            type="date"
            data-testid="analytics-filter-date-from"
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
          <label htmlFor="analytics-date-to" className="sr-only">
            {strings.analyticsDateTo}
          </label>
          <input
            id="analytics-date-to"
            type="date"
            data-testid="analytics-filter-date-to"
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

        {/* Subject Slice (client-side only) */}
        <div>
          <label htmlFor="analytics-subject-slice" className="sr-only">
            {strings.analyticsSubjectSlice}
          </label>
          <select
            id="analytics-subject-slice"
            data-testid="analytics-filter-subject"
            value={subjectSlice ?? ''}
            onChange={(e) => {
              const v = e.target.value;
              onSubjectSliceChange(v === '' ? null : parseInt(v, 10));
            }}
            style={{ ...selectStyle, width: 150 }}
            onFocus={(e) => {
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.boxShadow = 'none';
            }}
          >
            <option value="">{strings.analyticsAllSubjects}</option>
            {SUBJECT_OPTIONS.map((opt) => (
              <option key={opt.code} value={opt.code}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Grade Slice (client-side only) */}
        <div>
          <label htmlFor="analytics-grade-slice" className="sr-only">
            {strings.analyticsGradeSlice}
          </label>
          <select
            id="analytics-grade-slice"
            data-testid="analytics-filter-grade"
            value={gradeSlice ?? ''}
            onChange={(e) => {
              const v = e.target.value;
              onGradeSliceChange(v === '' ? null : parseInt(v, 10));
            }}
            style={{ ...selectStyle, width: 130 }}
            onFocus={(e) => {
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.boxShadow = 'none';
            }}
          >
            <option value="">{strings.analyticsAllGrades}</option>
            {GRADES.map((g) => (
              <option key={g} value={g}>
                {`Grade ${g}`}
              </option>
            ))}
          </select>
        </div>

        {/* Language Slice (client-side only) */}
        <div>
          <label htmlFor="analytics-language-slice" className="sr-only">
            {strings.analyticsLanguageSlice}
          </label>
          <select
            id="analytics-language-slice"
            data-testid="analytics-filter-language"
            value={languageSlice ?? ''}
            onChange={(e) => {
              const v = e.target.value;
              onLanguageSliceChange(v === '' ? null : parseInt(v, 10));
            }}
            style={{ ...selectStyle, width: 140 }}
            onFocus={(e) => {
              e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }}
            onBlur={(e) => {
              e.currentTarget.style.boxShadow = 'none';
            }}
          >
            <option value="">{strings.analyticsAllLanguages}</option>
            {LANGUAGE_OPTIONS.map((opt) => (
              <option key={opt.code} value={opt.code}>
                {opt.label}
              </option>
            ))}
          </select>
        </div>

        {/* Clear button */}
        {hasActiveFilters && (
          <button
            type="button"
            data-testid="analytics-clear-filters"
            onClick={onClear}
            style={clearBtnStyle}
          >
            {strings.analyticsClearFilters}
          </button>
        )}
      </div>

      {/* Date range error */}
      {hasDateRangeError && (
        <div
          role="alert"
          data-testid="analytics-date-range-error"
          style={{
            fontSize: 12,
            color: 'var(--lx-danger)',
            fontFamily: 'inherit',
            marginTop: 6,
          }}
        >
          {strings.analyticsDateRangeError}
        </div>
      )}
    </div>
  );
}
