'use client';

/**
 * VersionHistory — collapsible panel showing the published version history
 * for a curriculum entity, with rollback affordance per row.
 *
 * States: loading (shimmer) / empty (never published) / error / results.
 * "Latest" indicator on the first row (index 0) — a LifecycleBadge Published.
 * Each non-latest row has a "Roll back" button that opens RollbackVersionDialog.
 *
 * Publisher is shown as "Published by ID {N}" (raw int, no name lookup — DG-3).
 * Date is always dir="ltr" (technical string) for EN runtime.
 *
 * Design Spec P7-05 §Component 4 — VersionHistory panel.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import { useContentVersionHistory, type ContentVersionDto } from '@learnexia/api-client';
import { CONTENT_LANGUAGE, LIFECYCLE_STATE, type VersionedEntityTypeValue } from '@learnexia/shared/constants';
import { LifecycleBadge } from './LifecycleBadge';
import { AdminErrorBanner } from './AdminErrorBanner';
import { RollbackVersionDialog } from './RollbackVersionDialog';
import type { AdminStrings } from '../lib/strings';

// ── Shimmer skeleton (3 rows) ─────────────────────────────────────────────────

function VersionHistorySkeleton() {
  return (
    <Stack
      flexDirection="column"
      role="status"
      aria-label="Loading version history"
    >
      {Array.from({ length: 3 }).map((_, i) => (
        <Stack
          key={i}
          flexDirection="row"
          alignItems="flex-start"
          gap="$3"
          padding="$4"
          paddingHorizontal="$5"
          style={{ borderBottom: '1px solid var(--lx-border)' }}
        >
          <div style={{
            width: 36, height: 36, borderRadius: '50%', flexShrink: 0,
            background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
          }} />
          <Stack flex={1} flexDirection="column" gap="$1">
            <div style={{
              height: 14, width: 120, borderRadius: 4,
              background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
              backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
            }} />
            <div style={{
              height: 12, width: 80, borderRadius: 4, marginTop: 4,
              background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
              backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
            }} />
          </Stack>
          <div style={{
            height: 32, width: 80, borderRadius: 16, flexShrink: 0,
            background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
          }} />
        </Stack>
      ))}
    </Stack>
  );
}

// ── Inline icons ──────────────────────────────────────────────────────────────

function HistoryIcon({ size = 28 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="1 4 1 10 7 10" />
      <path d="M3.51 15a9 9 0 1 0 .49-4.5" />
    </svg>
  );
}

function HistorySmIcon({ size = 13 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="1 4 1 10 7 10" />
      <path d="M3.51 15a9 9 0 1 0 .49-4.5" />
    </svg>
  );
}

// ── Version row ───────────────────────────────────────────────────────────────

function VersionRow({
  version,
  isLatest,
  onRollback,
  strings,
}: {
  version: ContentVersionDto;
  isLatest: boolean;
  onRollback: (version: ContentVersionDto) => void;
  strings: AdminStrings;
}) {
  const formattedDate = (() => {
    try {
      return new Intl.DateTimeFormat('en-GB', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      }).format(new Date(version.publishedAtUtc));
    } catch {
      return version.publishedAtUtc;
    }
  })();

  const langLabel = version.language === CONTENT_LANGUAGE.Ar ? 'AR' : 'EN';

  return (
    <Stack
      flexDirection="row"
      alignItems="flex-start"
      gap="$3"
      padding="$4"
      paddingHorizontal="$5"
      style={{ borderBottom: '1px solid var(--lx-border)' }}
    >
      {/* Version number badge */}
      <Stack
        width={36}
        height={36}
        borderRadius="$pill"
        alignItems="center"
        justifyContent="center"
        flexShrink={0}
        style={{ backgroundColor: 'var(--lx-card-soft)' }}
      >
        <Text
          fontFamily="$body"
          fontSize={13}
          fontWeight="700"
          color="$fg1"
          style={{ fontVariantNumeric: 'tabular-nums' }}
          dir="ltr"
        >
          v{version.versionNumber}
        </Text>
      </Stack>

      {/* Info block */}
      <Stack flex={1} flexDirection="column" gap="$1">
        <Stack flexDirection="row" alignItems="center" gap="$2" flexWrap="wrap">
          <Text
            fontFamily="$body"
            fontSize={13}
            fontWeight="500"
            color="$fg1"
            dir="ltr"
          >
            {formattedDate}
          </Text>
          {isLatest && (
            <LifecycleBadge state={LIFECYCLE_STATE.Published} strings={strings} />
          )}
        </Stack>

        <Stack flexDirection="row" alignItems="center" gap="$2" flexWrap="wrap">
          <Text fontFamily="$body" fontSize={12} color="$fg3">
            {strings.clVersionPublishedBy.replace('{N}', String(version.publishedByUserId))}
          </Text>
          {/* Language chip */}
          <span
            style={{
              fontSize: 11,
              color: '#94A3B8',
              backgroundColor: 'rgba(255,255,255,0.05)',
              padding: '2px 6px',
              borderRadius: 9999,
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
            }}
          >
            {langLabel}
          </span>
        </Stack>
      </Stack>

      {/* Rollback button — not shown on the latest version (it IS the current state) */}
      {!isLatest && (
        <button
          type="button"
          data-testid={`rollback-btn-v${version.versionNumber}`}
          aria-label={strings.clVersionRollbackAriaLabel.replace('{N}', String(version.versionNumber))}
          onClick={() => onRollback(version)}
          style={{
            flexShrink: 0,
            height: 32,
            paddingInline: 12,
            borderRadius: 'var(--lx-radius-button)',
            border: '1px solid rgba(255,255,255,0.12)',
            backgroundColor: 'transparent',
            color: '#94A3B8',
            fontSize: 12,
            fontWeight: 500,
            cursor: 'pointer',
            fontFamily: 'inherit',
            display: 'flex',
            alignItems: 'center',
            gap: 4,
            transition: 'background-color 120ms var(--lx-ease-out), color 120ms, border-color 120ms',
          }}
          onMouseEnter={(e) => {
            const btn = e.currentTarget as HTMLButtonElement;
            btn.style.backgroundColor = 'var(--lx-card)';
            btn.style.color = '#F8FAFC';
            btn.style.borderColor = 'rgba(255,255,255,0.25)';
          }}
          onMouseLeave={(e) => {
            const btn = e.currentTarget as HTMLButtonElement;
            btn.style.backgroundColor = 'transparent';
            btn.style.color = '#94A3B8';
            btn.style.borderColor = 'rgba(255,255,255,0.12)';
          }}
        >
          <HistorySmIcon size={13} />
          {strings.clVersionRollbackBtn}
        </button>
      )}
    </Stack>
  );
}

// ── Props + component ─────────────────────────────────────────────────────────

export interface VersionHistoryProps {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  gradeId?: number;
  strings: AdminStrings;
  onRollbackSuccess?: () => void;
}

export function VersionHistory({
  entityType,
  entityId,
  gradeId,
  strings,
  onRollbackSuccess,
}: VersionHistoryProps) {
  const { data, isLoading, isError, refetch } = useContentVersionHistory(entityType, entityId);

  const [rollbackTarget, setRollbackTarget] = useState<ContentVersionDto | null>(null);

  const versions = data ?? [];

  return (
    <Stack flexDirection="column" gap={0} data-testid="version-history-panel">
      {/* Panel header */}
      <Stack
        flexDirection="row"
        alignItems="center"
        justifyContent="space-between"
        padding="$4"
        paddingHorizontal="$5"
        style={{ borderBottom: '1px solid var(--lx-border)' }}
      >
        <Text fontFamily="$heading" fontSize={14} fontWeight="600" color="$fg1">
          {strings.clVersionHistoryHeading}
        </Text>
        {!isLoading && !isError && versions.length > 0 && (
          <span
            style={{
              fontSize: 12,
              color: '#94A3B8',
              backgroundColor: 'var(--lx-card-soft)',
              padding: '3px 8px',
              borderRadius: 9999,
            }}
          >
            {versions.length} {strings.clVersionHistoryCount}
          </span>
        )}
      </Stack>

      {/* Body */}
      {isLoading && <VersionHistorySkeleton />}

      {isError && !isLoading && (
        <Stack padding="$4" paddingHorizontal="$5" flexDirection="column" gap="$2">
          <AdminErrorBanner variant="error" message={strings.clVersionHistoryError} />
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

      {!isLoading && !isError && versions.length === 0 && (
        <Stack
          padding="$6"
          alignItems="center"
          gap="$3"
          data-testid="version-history-empty"
        >
          <span style={{ color: '#94A3B8' }}><HistoryIcon size={28} /></span>
          <Text fontFamily="$body" fontSize={13} color="$fg3" style={{ textAlign: 'center' }}>
            {strings.clVersionHistoryEmpty}
          </Text>
        </Stack>
      )}

      {!isLoading && !isError && versions.length > 0 && (
        <>
          {versions.map((version, index) => (
            <VersionRow
              key={version.id}
              version={version}
              isLatest={index === 0}
              onRollback={setRollbackTarget}
              strings={strings}
            />
          ))}
        </>
      )}

      {/* Rollback dialog */}
      {rollbackTarget && (
        <RollbackVersionDialog
          open={rollbackTarget !== null}
          entityType={entityType}
          entityId={entityId}
          versionNumber={rollbackTarget.versionNumber}
          publishedAtUtc={rollbackTarget.publishedAtUtc}
          publishedByUserId={rollbackTarget.publishedByUserId}
          gradeId={gradeId}
          strings={strings}
          onClose={() => setRollbackTarget(null)}
          onSuccess={() => {
            setRollbackTarget(null);
            onRollbackSuccess?.();
          }}
        />
      )}
    </Stack>
  );
}
