'use client';

/**
 * CurriculumLifecycleControl — shared cross-cutting control for curriculum
 * lifecycle transitions (Publish / Unpublish / Archive / Restore).
 *
 * Given {entityType, entityId, currentState, language}, renders:
 *   - A state row: "Lifecycle status" label + LifecycleBadge + "View Preview" link.
 *   - An actions row: state-conditional buttons (max 2 at any state).
 *
 * Legal transition table (drives which buttons render):
 *   Draft (1)    → Publish only (no direct Archive)
 *   Published (2) → Unpublish + Archive
 *   Archived (3) → Restore only (no direct Publish)
 *
 * This is the control P7-01..04 compose in their entity detail surfaces.
 * It is standalone + composable — no provider, no compound-component pattern.
 *
 * The re-scoped FE-5 is implemented here: current LifecycleBadge + "View preview"
 * link. No dual live/draft toggle (backend has no pending-draft model — OQ1/D5).
 *
 * Design Spec P7-05 §Component 2.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import {
  LIFECYCLE_STATE,
  VERSIONED_ENTITY_TYPE,
  type LifecycleStateValue,
  type VersionedEntityTypeValue,
  type ContentLanguageValue,
} from '@learnexia/shared/constants';
import { LifecycleBadge } from './LifecycleBadge';
import { AdminErrorBanner } from './AdminErrorBanner';
import { PublishCurriculumDialog } from './PublishCurriculumDialog';
import { UnpublishCurriculumDialog } from './UnpublishCurriculumDialog';
import { ArchiveCurriculumDialog } from './ArchiveCurriculumDialog';
import { RestoreCurriculumDialog } from './RestoreCurriculumDialog';
import type { AdminStrings } from '../lib/strings';

// ── Inline icons ──────────────────────────────────────────────────────────────

function UploadCloudIcon({ size = 15 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="16 16 12 12 8 16" />
      <line x1="12" y1="12" x2="12" y2="21" />
      <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3" />
    </svg>
  );
}

function DownloadCloudIcon({ size = 15 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="8 17 12 21 16 17" />
      <line x1="12" y1="12" x2="12" y2="21" />
      <path d="M20.88 18.09A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.29" />
    </svg>
  );
}

function ArchiveIcon({ size = 15 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="21 8 21 21 3 21 3 8" />
      <rect x="1" y="3" width="22" height="5" />
      <line x1="10" y1="12" x2="14" y2="12" />
    </svg>
  );
}

function RotateCcwIcon({ size = 15 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="1 4 1 10 7 10" />
      <path d="M3.51 15a9 9 0 1 0 .49-4.5" />
    </svg>
  );
}

function EyeIcon({ size = 14 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}

// ── Entity-type slug map (for the preview link path) ─────────────────────────

const ENTITY_TYPE_SLUG: Record<VersionedEntityTypeValue, string> = {
  [VERSIONED_ENTITY_TYPE.Subject]:      'subject',
  [VERSIONED_ENTITY_TYPE.Unit]:         'unit',
  [VERSIONED_ENTITY_TYPE.Lesson]:       'lesson',
  [VERSIONED_ENTITY_TYPE.QuizQuestion]: 'quiz',
};

function getEntityLabel(entityType: VersionedEntityTypeValue, strings: AdminStrings): string {
  switch (entityType) {
    case VERSIONED_ENTITY_TYPE.Subject:      return strings.clEntityTypeSubject;
    case VERSIONED_ENTITY_TYPE.Unit:         return strings.clEntityTypeUnit;
    case VERSIONED_ENTITY_TYPE.Lesson:       return strings.clEntityTypeLesson;
    case VERSIONED_ENTITY_TYPE.QuizQuestion: return strings.clEntityTypeQuestion;
    default: return 'Entity';
  }
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface CurriculumLifecycleControlProps {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  currentState: LifecycleStateValue;
  language: ContentLanguageValue;
  strings: AdminStrings;
  gradeId?: number;
  /** Called after any successful transition so the parent can refresh its own data. */
  onSuccess?: () => void;
}

type ActiveDialog = 'publish' | 'unpublish' | 'archive' | 'restore' | null;

// ── Component ─────────────────────────────────────────────────────────────────

export function CurriculumLifecycleControl({
  entityType,
  entityId,
  currentState,
  language: _language,
  strings,
  gradeId,
  onSuccess,
}: CurriculumLifecycleControlProps) {
  const [activeDialog, setActiveDialog] = useState<ActiveDialog>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const entityLabel = getEntityLabel(entityType, strings);
  const typeSlug = ENTITY_TYPE_SLUG[entityType] ?? 'subject';
  const previewHref = `/curriculum/preview/${typeSlug}/${entityId}`;

  const handleSuccess = (message: string) => {
    setSuccessMessage(message);
    setActiveDialog(null);
    onSuccess?.();
    // Auto-dismiss after 5s
    setTimeout(() => setSuccessMessage(null), 5000);
  };

  // Dismiss success/error on new action
  const openDialog = (dialog: ActiveDialog) => {
    setSuccessMessage(null);
    setError(null);
    setActiveDialog(dialog);
  };

  return (
    <Stack flexDirection="column" gap="$4" data-testid="lifecycle-control">
      {/* State row */}
      <Stack
        flexDirection="row"
        alignItems="center"
        gap="$3"
        aria-live="polite"
      >
        <Text
          fontFamily="$body"
          fontSize={12}
          fontWeight="600"
          style={{
            color: '#94A3B8',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
          }}
        >
          {strings.clLifecycleStatusLabel}
        </Text>

        <LifecycleBadge state={currentState} strings={strings} testId="lifecycle-badge" />

        {/* View Preview link — inline-end push */}
        <a
          href={previewHref}
          data-testid="lifecycle-preview-link"
          style={{
            marginInlineStart: 'auto',
            fontSize: 13,
            color: '#4F46E5',
            textDecoration: 'underline',
            display: 'inline-flex',
            alignItems: 'center',
            gap: 4,
          }}
        >
          <EyeIcon size={14} />
          {strings.clLifecycleViewPreview}
        </a>
      </Stack>

      {/* Actions row */}
      <Stack
        flexDirection="row"
        alignItems="center"
        gap="$3"
        style={{ flexWrap: 'wrap' }}
      >
        {/* Draft state: one button (Publish) */}
        {currentState === LIFECYCLE_STATE.Draft && (
          <button
            type="button"
            data-testid="lifecycle-publish-btn"
            aria-label={`${strings.clPublishTitle} ${entityLabel}`}
            onClick={() => openDialog('publish')}
            style={{
              height: 36,
              paddingInline: 16,
              borderRadius: 'var(--lx-radius-button)',
              border: 'none',
              backgroundColor: '#4F46E5',
              color: '#F8FAFC',
              fontSize: 13,
              fontWeight: 600,
              cursor: 'pointer',
              fontFamily: 'inherit',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              transition: 'filter 120ms var(--lx-ease-out)',
            }}
            onMouseEnter={(e) => { (e.currentTarget as HTMLButtonElement).style.filter = 'brightness(1.08)'; }}
            onMouseLeave={(e) => { (e.currentTarget as HTMLButtonElement).style.filter = 'none'; }}
          >
            <UploadCloudIcon size={15} />
            {strings.clPublishTitle}
          </button>
        )}

        {/* Published state: two buttons (Unpublish + Archive) */}
        {currentState === LIFECYCLE_STATE.Published && (
          <>
            <button
              type="button"
              data-testid="lifecycle-unpublish-btn"
              aria-label={`${strings.clUnpublishTitle} ${entityLabel}`}
              onClick={() => openDialog('unpublish')}
              style={{
                height: 36,
                paddingInline: 16,
                borderRadius: 'var(--lx-radius-button)',
                border: '1px solid rgba(255,255,255,0.16)',
                backgroundColor: 'transparent',
                color: '#CBD5E1',
                fontSize: 13,
                fontWeight: 500,
                cursor: 'pointer',
                fontFamily: 'inherit',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                transition: 'background-color 120ms var(--lx-ease-out), border-color 120ms',
              }}
              onMouseEnter={(e) => {
                const btn = e.currentTarget as HTMLButtonElement;
                btn.style.backgroundColor = 'var(--lx-card)';
                btn.style.borderColor = 'rgba(255,255,255,0.25)';
              }}
              onMouseLeave={(e) => {
                const btn = e.currentTarget as HTMLButtonElement;
                btn.style.backgroundColor = 'transparent';
                btn.style.borderColor = 'rgba(255,255,255,0.16)';
              }}
            >
              <DownloadCloudIcon size={15} />
              {strings.clUnpublishTitle}
            </button>

            <button
              type="button"
              data-testid="lifecycle-archive-btn"
              aria-label={`${strings.clArchiveTitle} ${entityLabel}`}
              onClick={() => openDialog('archive')}
              style={{
                height: 36,
                paddingInline: 16,
                borderRadius: 'var(--lx-radius-button)',
                border: '1px solid rgba(245,158,11,0.3)',
                backgroundColor: 'transparent',
                color: '#F59E0B',
                fontSize: 13,
                fontWeight: 500,
                cursor: 'pointer',
                fontFamily: 'inherit',
                display: 'flex',
                alignItems: 'center',
                gap: 6,
                transition: 'background-color 120ms var(--lx-ease-out), border-color 120ms',
              }}
              onMouseEnter={(e) => {
                const btn = e.currentTarget as HTMLButtonElement;
                btn.style.backgroundColor = 'rgba(245,158,11,0.08)';
                btn.style.borderColor = '#F59E0B';
              }}
              onMouseLeave={(e) => {
                const btn = e.currentTarget as HTMLButtonElement;
                btn.style.backgroundColor = 'transparent';
                btn.style.borderColor = 'rgba(245,158,11,0.3)';
              }}
            >
              <ArchiveIcon size={15} />
              {strings.clArchiveTitle}
            </button>
          </>
        )}

        {/* Archived state: one button (Restore) */}
        {currentState === LIFECYCLE_STATE.Archived && (
          <button
            type="button"
            data-testid="lifecycle-restore-btn"
            aria-label={`${strings.clRestoreTitle} ${entityLabel}`}
            onClick={() => openDialog('restore')}
            style={{
              height: 36,
              paddingInline: 16,
              borderRadius: 'var(--lx-radius-button)',
              border: '1px solid rgba(34,197,94,0.3)',
              backgroundColor: 'transparent',
              color: '#22C55E',
              fontSize: 13,
              fontWeight: 500,
              cursor: 'pointer',
              fontFamily: 'inherit',
              display: 'flex',
              alignItems: 'center',
              gap: 6,
              transition: 'background-color 120ms var(--lx-ease-out), border-color 120ms',
            }}
            onMouseEnter={(e) => {
              const btn = e.currentTarget as HTMLButtonElement;
              btn.style.backgroundColor = 'rgba(34,197,94,0.08)';
              btn.style.borderColor = '#22C55E';
            }}
            onMouseLeave={(e) => {
              const btn = e.currentTarget as HTMLButtonElement;
              btn.style.backgroundColor = 'transparent';
              btn.style.borderColor = 'rgba(34,197,94,0.3)';
            }}
          >
            <RotateCcwIcon size={15} />
            {strings.clRestoreTitle}
          </button>
        )}
      </Stack>

      {/* Success/error banners */}
      {successMessage && (
        <AdminErrorBanner variant="success" message={successMessage} />
      )}
      {error && (
        <AdminErrorBanner variant="error" message={error} />
      )}

      {/* Dialogs */}
      <PublishCurriculumDialog
        open={activeDialog === 'publish'}
        entityType={entityType}
        entityId={entityId}
        entityLabel={entityLabel}
        gradeId={gradeId}
        strings={strings}
        onClose={() => setActiveDialog(null)}
        onSuccess={() => handleSuccess(strings.clLifecycleSuccessBannerTransitioned)}
      />
      <UnpublishCurriculumDialog
        open={activeDialog === 'unpublish'}
        entityType={entityType}
        entityId={entityId}
        entityLabel={entityLabel}
        gradeId={gradeId}
        strings={strings}
        onClose={() => setActiveDialog(null)}
        onSuccess={() => handleSuccess(strings.clLifecycleSuccessBannerTransitioned)}
      />
      <ArchiveCurriculumDialog
        open={activeDialog === 'archive'}
        entityType={entityType}
        entityId={entityId}
        entityLabel={entityLabel}
        gradeId={gradeId}
        strings={strings}
        onClose={() => setActiveDialog(null)}
        onSuccess={() => handleSuccess(strings.clLifecycleSuccessBannerTransitioned)}
      />
      <RestoreCurriculumDialog
        open={activeDialog === 'restore'}
        entityType={entityType}
        entityId={entityId}
        entityLabel={entityLabel}
        gradeId={gradeId}
        strings={strings}
        onClose={() => setActiveDialog(null)}
        onSuccess={() => handleSuccess(strings.clLifecycleSuccessBannerTransitioned)}
      />
    </Stack>
  );
}
