'use client';

/**
 * PublicationCoverage — grade-scoped publication status matrix.
 *
 * Shows which subjects are published for each language (AR + EN) in the
 * selected grade, using a semantic <table> per SubjectCode. Each cell shows:
 *   - LifecycleBadge (Published / Draft / Archived) if the subject exists
 *   - "Not Created" chip if the slot is absent
 *   - Warning chip (⚠) with aria-label if not published
 *   - Full success chip if all slots are Published
 *
 * The grade selector drives `usePublicationCoverage(gradeId)`.
 * No optimistic mutations. Invalidate-and-refetch only.
 *
 * Design Spec P7-05 §Component 6.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import {
  usePublicationCoverage,
  type PublicationCoverageSlotDto,
} from '@learnexia/api-client';
import {
  LIFECYCLE_STATE,
  SUBJECT_CODE,
  CONTENT_LANGUAGE,
  type LifecycleStateValue,
} from '@learnexia/shared/constants';
import { LifecycleBadge } from './LifecycleBadge';
import { AdminErrorBanner } from './AdminErrorBanner';
import type { AdminStrings } from '../lib/strings';

// ── Grade selector options (1-6) ──────────────────────────────────────────────

const GRADE_OPTIONS = [1, 2, 3, 4, 5, 6] as const;

// ── Subject rows (4 subjects, no Social Studies) ──────────────────────────────

const SUBJECT_ROWS: { code: number; label: string }[] = [
  { code: SUBJECT_CODE.MATH,    label: 'MATH' },
  { code: SUBJECT_CODE.SCIENCE, label: 'SCIENCE' },
  { code: SUBJECT_CODE.ARABIC,  label: 'ARABIC' },
  { code: SUBJECT_CODE.ENGLISH, label: 'ENGLISH' },
];

// ── Shimmer skeleton ──────────────────────────────────────────────────────────

function CoverageSkeleton() {
  return (
    <Stack flexDirection="column" gap="$3">
      {Array.from({ length: 4 }).map((_, i) => (
        <div
          key={i}
          style={{
            height: 48,
            borderRadius: 8,
            background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%',
            animation: 'lx-shimmer 1400ms linear infinite',
          }}
        />
      ))}
    </Stack>
  );
}

// ── Slot cell ─────────────────────────────────────────────────────────────────

function SlotCell({
  slot,
  strings,
}: {
  slot: PublicationCoverageSlotDto | undefined;
  strings: AdminStrings;
}) {
  if (!slot) {
    return (
      <span
        data-testid="coverage-slot-not-created"
        style={{
          display: 'inline-flex',
          alignItems: 'center',
          gap: 4,
          height: 22,
          paddingInline: 8,
          borderRadius: 9999,
          fontSize: 11,
          fontWeight: 500,
          color: '#64748B',
          backgroundColor: 'rgba(100,116,139,0.1)',
          border: '1px solid rgba(100,116,139,0.2)',
          letterSpacing: '0.02em',
        }}
      >
        {strings.clCoverageSlotNotCreated}
      </span>
    );
  }

  const isPublished = slot.lifecycleState === LIFECYCLE_STATE.Published;

  return (
    <Stack flexDirection="row" alignItems="center" gap="$2">
      <LifecycleBadge state={slot.lifecycleState as LifecycleStateValue} strings={strings} />
      {!isPublished && (
        <span
          aria-label={strings.clCoverageWarningMissingChip}
          title={strings.clCoverageWarningMissingChip}
          data-testid="coverage-warning-chip"
          style={{ color: '#F59E0B', fontSize: 13, lineHeight: 1 }}
        >
          ⚠
        </span>
      )}
    </Stack>
  );
}

// ── Props + component ─────────────────────────────────────────────────────────

export interface PublicationCoverageProps {
  strings: AdminStrings;
}

export function PublicationCoverage({ strings }: PublicationCoverageProps) {
  const [gradeId, setGradeId] = useState<number>(0);
  const { data, isLoading, isError, refetch } = usePublicationCoverage(gradeId);

  // Build a lookup: subjectCode → { ar: slot | undefined, en: slot | undefined }
  type SlotMap = Record<number, { ar?: PublicationCoverageSlotDto; en?: PublicationCoverageSlotDto }>;

  const slotMap: SlotMap = {};

  if (data?.coverage) {
    for (const slot of data.coverage) {
      if (!slotMap[slot.subjectCode]) {
        slotMap[slot.subjectCode] = {};
      }
      if (slot.language === CONTENT_LANGUAGE.Ar) {
        slotMap[slot.subjectCode]!.ar = slot;
      } else if (slot.language === CONTENT_LANGUAGE.En) {
        slotMap[slot.subjectCode]!.en = slot;
      }
    }
  }

  // Is the entire grade fully published?
  const allPublished = data?.coverage != null &&
    data.coverage.length >= SUBJECT_ROWS.length * 2 &&
    data.coverage.every((s) => s.lifecycleState === LIFECYCLE_STATE.Published);

  return (
    <Stack flexDirection="column" gap="$6" data-testid="publication-coverage">
      {/* Heading */}
      <Stack flexDirection="column" gap="$2">
        <Text fontFamily="$heading" fontSize={22} fontWeight="700" color="$fg1">
          {strings.clCoverageHeading}
        </Text>
        <Text fontFamily="$body" fontSize={14} color="$fg3">
          {strings.clCoverageSubheading}
        </Text>
      </Stack>

      {/* Grade selector */}
      <Stack flexDirection="column" gap="$2">
        <label
          htmlFor="coverage-grade-select"
          style={{ fontFamily: 'inherit', fontSize: 13, fontWeight: 600, color: '#94A3B8' }}
        >
          {strings.clCoverageGradeLabel}
        </label>
        <select
          id="coverage-grade-select"
          data-testid="coverage-grade-select"
          value={gradeId === 0 ? '' : gradeId}
          onChange={(e) => setGradeId(Number(e.target.value))}
          style={{
            height: 40,
            paddingInline: 12,
            borderRadius: 'var(--lx-radius-button)',
            border: '1px solid var(--lx-border)',
            backgroundColor: 'var(--lx-card)',
            color: '#F8FAFC',
            fontSize: 14,
            fontFamily: 'inherit',
            cursor: 'pointer',
            maxWidth: 200,
          }}
        >
          <option value="" disabled>
            {strings.clCoverageGradePlaceholder}
          </option>
          {GRADE_OPTIONS.map((g) => (
            <option key={g} value={g}>
              {strings.clCoverageGradeLabel} {g}
            </option>
          ))}
        </select>

        {gradeId === 0 && (
          <Text fontFamily="$body" fontSize={13} color="$fg3">
            {strings.clCoverageGradeHint}
          </Text>
        )}
      </Stack>

      {/* Coverage table — only shown when a grade is selected */}
      {gradeId > 0 && (
        <Stack flexDirection="column" gap="$4">
          {isLoading && <CoverageSkeleton />}

          {isError && !isLoading && (
            <Stack flexDirection="column" gap="$2">
              <AdminErrorBanner variant="error" message={strings.clCoverageError} />
              <button
                type="button"
                onClick={() => void refetch()}
                style={{
                  alignSelf: 'flex-start',
                  padding: '6px 12px',
                  borderRadius: 8,
                  border: '1px solid var(--lx-border)',
                  backgroundColor: 'transparent',
                  color: '#94A3B8',
                  fontSize: 12,
                  cursor: 'pointer',
                  fontFamily: 'inherit',
                }}
              >
                {strings.usersListRetry}
              </button>
            </Stack>
          )}

          {!isLoading && !isError && (
            <>
              {/* All-published success banner */}
              {allPublished && (
                <div
                  data-testid="coverage-all-published"
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                    padding: '10px 14px',
                    borderRadius: 8,
                    backgroundColor: 'rgba(34,197,94,0.08)',
                    border: '1px solid rgba(34,197,94,0.2)',
                    fontSize: 13,
                    color: '#4ADE80',
                    fontFamily: 'inherit',
                  }}
                >
                  <span aria-hidden="true">&#10003;</span>
                  {strings.clCoverageSuccess}
                </div>
              )}

              {/* Coverage matrix table */}
              <div style={{ overflowX: 'auto' }}>
                <table
                  aria-label={strings.clCoverageTableCaption}
                  data-testid="coverage-table"
                  style={{
                    width: '100%',
                    borderCollapse: 'collapse',
                    fontFamily: 'inherit',
                    fontSize: 13,
                  }}
                >
                  <caption
                    style={{
                      position: 'absolute',
                      width: 1,
                      height: 1,
                      padding: 0,
                      margin: -1,
                      overflow: 'hidden',
                      clip: 'rect(0,0,0,0)',
                      whiteSpace: 'nowrap',
                      border: 0,
                    }}
                  >
                    {strings.clCoverageTableCaption}
                  </caption>
                  <thead>
                    <tr>
                      <th
                        scope="col"
                        style={{
                          padding: '10px 12px',
                          textAlign: 'start',
                          fontWeight: 600,
                          color: '#94A3B8',
                          fontSize: 11,
                          textTransform: 'uppercase',
                          letterSpacing: '0.04em',
                          borderBottom: '1px solid var(--lx-border)',
                        }}
                      >
                        {strings.clCoverageColSubject}
                      </th>
                      <th
                        scope="col"
                        style={{
                          padding: '10px 12px',
                          textAlign: 'start',
                          fontWeight: 600,
                          color: '#94A3B8',
                          fontSize: 11,
                          textTransform: 'uppercase',
                          letterSpacing: '0.04em',
                          borderBottom: '1px solid var(--lx-border)',
                        }}
                      >
                        {strings.contentLangAr} — {strings.langArabic}
                      </th>
                      <th
                        scope="col"
                        style={{
                          padding: '10px 12px',
                          textAlign: 'start',
                          fontWeight: 600,
                          color: '#94A3B8',
                          fontSize: 11,
                          textTransform: 'uppercase',
                          letterSpacing: '0.04em',
                          borderBottom: '1px solid var(--lx-border)',
                        }}
                      >
                        {strings.contentLangEn} — {strings.langEnglish}
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {SUBJECT_ROWS.map((row, idx) => {
                      const isLast = idx === SUBJECT_ROWS.length - 1;
                      const slots = slotMap[row.code] ?? {};
                      return (
                        <tr
                          key={row.code}
                          data-testid={`coverage-row-${row.label.toLowerCase()}`}
                        >
                          <td
                            style={{
                              padding: '12px 12px',
                              fontWeight: 600,
                              color: '#F8FAFC',
                              fontSize: 13,
                              borderBottom: isLast ? 'none' : '1px solid var(--lx-border)',
                            }}
                          >
                            {row.label}
                          </td>
                          <td
                            style={{
                              padding: '12px 12px',
                              borderBottom: isLast ? 'none' : '1px solid var(--lx-border)',
                            }}
                          >
                            <SlotCell slot={slots.ar} strings={strings} />
                          </td>
                          <td
                            style={{
                              padding: '12px 12px',
                              borderBottom: isLast ? 'none' : '1px solid var(--lx-border)',
                            }}
                          >
                            <SlotCell slot={slots.en} strings={strings} />
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </Stack>
      )}
    </Stack>
  );
}
