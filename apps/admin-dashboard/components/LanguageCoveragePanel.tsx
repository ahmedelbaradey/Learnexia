'use client';

/**
 * LanguageCoveragePanel — 6-slot coverage grid for a grade (Design Spec §2.2.4).
 *
 * Fetched via useSubjectCoverage(gradeId). Shows which (SubjectCode, Language)
 * pairs exist and flags missing slots with a dashed danger border.
 * Collapsed by default; expands on click.
 * Each gap slot has a "Create" shortcut that pre-fills SubjectForm.
 *
 * P7-05 lifecycle slot reserved in each coverage slot (§S5).
 */

import { useState } from 'react';
import { useSubjectCoverage } from '@learnexia/api-client';
import type { SubjectLanguageCoverageDto, AddSubjectDto } from '@learnexia/api-client';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';
import { SubjectCodeBadge } from './SubjectCodeBadge';
import { LanguageBadge } from './LanguageBadge';
import { ActiveBadge } from './ActiveBadge';

const strings = getStrings(ADMIN_LOCALE);

// Chevron icons
function ChevronDown() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="6 9 12 15 18 9" />
    </svg>
  );
}
function ChevronUp() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="18 15 12 9 6 15" />
    </svg>
  );
}
function GridIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="7" height="7" /><rect x="14" y="3" width="7" height="7" />
      <rect x="14" y="14" width="7" height="7" /><rect x="3" y="14" width="7" height="7" />
    </svg>
  );
}
function PlusIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}


function CoverageSlot({
  slot,
  onCreateClick,
}: {
  slot: SubjectLanguageCoverageDto;
  onCreateClick: (prefill: Partial<AddSubjectDto>) => void;
}) {
  const isGap = !slot.exists;
  const existsActive = slot.exists && slot.isActive === true;

  let borderStyle: string;
  let bgStyle: string;
  if (isGap) {
    borderStyle = '1px dashed rgba(239,68,68,0.4)';
    bgStyle = 'rgba(239,68,68,0.06)';
  } else if (existsActive) {
    borderStyle = '1px solid rgba(34,197,94,0.3)';
    bgStyle = 'rgba(34,197,94,0.06)';
  } else {
    borderStyle = '1px solid var(--lx-border)';
    bgStyle = 'rgba(255,255,255,0.02)';
  }

  return (
    <div
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
        padding: 12,
        borderRadius: 8,
        border: borderStyle,
        backgroundColor: bgStyle,
      }}
    >
      <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 4 }}>
          <SubjectCodeBadge code={slot.subjectCode} />
          <LanguageBadge language={slot.language} />
        </div>
        {slot.exists && slot.isActive !== null && (
          <ActiveBadge isActive={slot.isActive ?? false} />
        )}
      </div>

      {slot.exists ? (
        <>
          <span style={{ fontSize: 13, fontWeight: 500, color: 'var(--lx-fg2)', lineHeight: 1.4 }}>
            {slot.name}
          </span>
          {/* P7-05 Lifecycle Slot — reserved placeholder */}
          <div
            data-slot="lifecycle"
            data-testid={`coverage-slot-${slot.subjectCode}-${slot.language}-lifecycle-slot`}
            style={{ marginTop: 4, minHeight: 22 }}
          >
            {/* P7-05 drops <LifecycleBadge entityType={1} entityId={slot.subjectId} /> here */}
          </div>
        </>
      ) : (
        <>
          <span style={{ fontSize: 12, fontWeight: 500, color: '#EF4444' }}>
            {strings.coverageMissingSlot}
          </span>
          <button
            data-testid={`coverage-create-${slot.subjectCode}-${slot.language}`}
            onClick={() => onCreateClick({
              subjectCode: slot.subjectCode,
              language: slot.language,
            })}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
              height: 30,
              paddingLeft: 12,
              paddingRight: 12,
              borderRadius: 16,
              backgroundColor: 'transparent',
              border: '1px solid rgba(79,70,229,0.3)',
              color: '#4F46E5',
              fontSize: 12,
              fontWeight: 500,
              cursor: 'pointer',
              fontFamily: 'inherit',
              transition: 'background-color 80ms ease-out',
            }}
          >
            <PlusIcon />
            {strings.coverageCreateShortcut}
          </button>
        </>
      )}
    </div>
  );
}

export interface LanguageCoveragePanelProps {
  gradeId: number;
  gradeNumber: number;
  onCreateClick: (prefill: Partial<AddSubjectDto>) => void;
}

export function LanguageCoveragePanel({ gradeId, gradeNumber, onCreateClick }: LanguageCoveragePanelProps) {
  const [expanded, setExpanded] = useState(false);
  const { data, isLoading, isError, refetch } = useSubjectCoverage(gradeId);

  const panelBodyId = `coverage-panel-body-${gradeId}`;

  return (
    <div
      style={{
        backgroundColor: 'var(--lx-card)',
        borderRadius: 20,
        border: '1px solid var(--lx-border)',
        padding: 20,
        marginBottom: 8,
      }}
    >
      {/* Panel header — clickable, toggles expansion */}
      <button
        aria-expanded={expanded}
        aria-controls={panelBodyId}
        onClick={() => setExpanded(!expanded)}
        style={{
          width: '100%',
          background: 'none',
          border: 'none',
          cursor: 'pointer',
          padding: 0,
          display: 'flex',
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'space-between',
          fontFamily: 'inherit',
        }}
      >
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <span style={{ color: 'var(--lx-fg3)' }}>
            <GridIcon />
          </span>
          <span style={{
            fontSize: 13,
            fontWeight: 600,
            color: 'var(--lx-fg2)',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
          }}>
            {strings.coveragePanelHeading} {gradeNumber}
          </span>
          {data && data.gapCount > 0 && (
            <span style={{
              display: 'inline-flex',
              alignItems: 'center',
              backgroundColor: 'rgba(239,68,68,0.15)',
              borderRadius: 9999,
              paddingLeft: 8,
              paddingRight: 8,
              height: 20,
              fontSize: 11,
              fontWeight: 600,
              color: '#EF4444',
            }}>
              {data.gapCount} {strings.coverageGapBadge}
            </span>
          )}
        </div>
        <span style={{ color: 'var(--lx-fg3)', transition: 'transform 200ms var(--lx-ease-out)' }}>
          {expanded ? <ChevronUp /> : <ChevronDown />}
        </span>
      </button>

      {/* Expanded body */}
      {expanded && (
        <div id={panelBodyId} style={{ flexDirection: 'column', gap: 12, marginTop: 16 }}>
          {isLoading && (
            <div
              role="status"
              aria-label={strings.subjectsListLoadingLabel}
              style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}
            >
              {Array.from({ length: 6 }).map((_, i) => (
                <div
                  key={i}
                  style={{
                    height: 80,
                    borderRadius: 8,
                    background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
                    backgroundSize: '400px 100%',
                    animation: 'lx-shimmer 1400ms linear infinite',
                  }}
                />
              ))}
            </div>
          )}
          {isError && (
            <div>
              <AdminErrorBanner variant="error" message={strings.subjectsListError} />
              <button
                onClick={() => void refetch()}
                style={{
                  marginTop: 8,
                  padding: '8px 16px',
                  borderRadius: 16,
                  border: '1px solid var(--lx-border)',
                  background: 'transparent',
                  color: 'var(--lx-fg2)',
                  cursor: 'pointer',
                  fontFamily: 'inherit',
                  fontSize: 13,
                }}
              >
                {strings.usersListRetry}
              </button>
            </div>
          )}
          {data && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 12 }}>
              {data.coverage.map((slot) => (
                <CoverageSlot
                  key={`${slot.subjectCode}-${slot.language}`}
                  slot={slot}
                  onCreateClick={onCreateClick}
                />
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
