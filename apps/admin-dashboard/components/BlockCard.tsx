'use client';

/**
 * BlockCard — single content-block card in the ContentBlockEditor list.
 * Design Spec §8.3 — P7-02.
 *
 * Shows: block number, BlockTypeBadge, IsActive (read-only ActiveBadge),
 * BlockPreview, move-up/move-down, edit/delete action buttons.
 * "Saving" overlay while reorder mutation is in-flight.
 */

import { useState } from 'react';
import type { AdminContentBlockDto } from '@learnexia/api-client';
import type { ContentLanguageValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { BlockTypeBadge } from './BlockTypeBadge';
import { BlockPreview } from './BlockPreview';
import { ActiveBadge } from './ActiveBadge';

const strings = getStrings(ADMIN_LOCALE);

// ── Icons ─────────────────────────────────────────────────────────────────────
function ChevronUpIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="18 15 12 9 6 15" />
    </svg>
  );
}
function ChevronDownIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="6 9 12 15 18 9" />
    </svg>
  );
}
function PencilIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  );
}
function TrashIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14H6L5 6" />
      <path d="M10 11v6M14 11v6" />
      <path d="M9 6V4h6v2" />
    </svg>
  );
}

// ── Props ─────────────────────────────────────────────────────────────────────
export interface BlockCardProps {
  block: AdminContentBlockDto;
  language: ContentLanguageValue;
  index: number;           // 0-based position in the local list
  total: number;           // total blocks
  isSavingOrder: boolean;  // while reorder mutation is in-flight
  onMoveUp: () => void;
  onMoveDown: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

const iconBtnStyle: React.CSSProperties = {
  height: 32, width: 32, borderRadius: 8, border: 'none',
  display: 'flex', alignItems: 'center', justifyContent: 'center',
  cursor: 'pointer', backgroundColor: 'rgba(255,255,255,0.06)',
  color: 'var(--lx-fg3)', transition: 'background-color 100ms ease',
};

export function BlockCard({
  block, language, index, total, isSavingOrder,
  onMoveUp, onMoveDown, onEdit, onDelete,
}: BlockCardProps) {
  const [previewExpanded, setPreviewExpanded] = useState(false);
  const blockNumber = index + 1;
  const isFirst = index === 0;
  const isLast = index === total - 1;

  return (
    <li
      data-testid={`block-card-${block.id}`}
      style={{
        position: 'relative',
        backgroundColor: 'var(--lx-card)',
        borderRadius: 16,
        border: '1px solid var(--lx-border)',
        padding: 16,
        display: 'flex',
        flexDirection: 'column',
        gap: 12,
      }}
    >
      {/* Saving overlay — shown while reorder in-flight */}
      {isSavingOrder && (
        <div
          aria-label={strings.loadingLabel}
          style={{
            position: 'absolute', inset: 0, borderRadius: 16,
            backgroundColor: 'rgba(15,23,42,0.55)',
            display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 10,
          }}
        >
          <span style={{ fontSize: 12, color: 'var(--lx-fg2)' }}>{strings.loadingLabel}</span>
        </div>
      )}

      {/* Header row: number + type + active badge + actions */}
      <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 10 }}>
        {/* Block number */}
        <span
          data-testid={`block-card-${block.id}-number`}
          style={{
            fontSize: 11, fontWeight: 700, color: 'var(--lx-fg3)',
            textTransform: 'uppercase', letterSpacing: '0.06em',
            flexShrink: 0,
          }}
        >
          {strings.blockCardNumberPrefix}{blockNumber}
        </span>

        {/* Type badge */}
        <BlockTypeBadge blockType={block.blockType} />

        {/* Active state (read-only per brief Q4) */}
        <ActiveBadge isActive={block.isActive} />

        {/* Inactive notice */}
        {!block.isActive && (
          <span style={{ fontSize: 12, color: '#94A3B8', fontStyle: 'italic' }}>
            {strings.blockCardInactiveNotice}
          </span>
        )}

        {/* Spacer */}
        <div style={{ flex: 1 }} />

        {/* Reorder buttons */}
        <button
          type="button"
          aria-label={strings.blockCardMoveUpBtn}
          data-testid={`block-card-${block.id}-move-up`}
          onClick={onMoveUp}
          disabled={isFirst || isSavingOrder}
          style={{
            ...iconBtnStyle,
            opacity: isFirst || isSavingOrder ? 0.35 : 1,
            cursor: isFirst || isSavingOrder ? 'not-allowed' : 'pointer',
          }}
        >
          <ChevronUpIcon />
        </button>
        <button
          type="button"
          aria-label={strings.blockCardMoveDownBtn}
          data-testid={`block-card-${block.id}-move-down`}
          onClick={onMoveDown}
          disabled={isLast || isSavingOrder}
          style={{
            ...iconBtnStyle,
            opacity: isLast || isSavingOrder ? 0.35 : 1,
            cursor: isLast || isSavingOrder ? 'not-allowed' : 'pointer',
          }}
        >
          <ChevronDownIcon />
        </button>

        {/* Edit button */}
        <button
          type="button"
          aria-label={strings.blockCardEditBtn}
          data-testid={`block-card-${block.id}-edit`}
          onClick={onEdit}
          disabled={isSavingOrder}
          style={{
            ...iconBtnStyle,
            color: '#F59E0B',
            opacity: isSavingOrder ? 0.5 : 1,
            cursor: isSavingOrder ? 'not-allowed' : 'pointer',
          }}
        >
          <PencilIcon />
        </button>

        {/* Delete button */}
        <button
          type="button"
          aria-label={strings.blockCardDeleteBtn}
          data-testid={`block-card-${block.id}-delete`}
          onClick={onDelete}
          disabled={isSavingOrder}
          style={{
            ...iconBtnStyle,
            color: 'var(--lx-danger)',
            opacity: isSavingOrder ? 0.5 : 1,
            cursor: isSavingOrder ? 'not-allowed' : 'pointer',
          }}
        >
          <TrashIcon />
        </button>
      </div>

      {/* Preview */}
      <div
        data-testid={`block-card-${block.id}-preview`}
        style={{
          backgroundColor: 'var(--lx-bg)',
          borderRadius: 10,
          border: '1px solid rgba(255,255,255,0.06)',
          padding: previewExpanded ? 0 : undefined,
          overflow: 'hidden',
          maxHeight: previewExpanded ? 'none' : 200,
          transition: 'max-height 200ms ease',
        }}
      >
        <BlockPreview block={block} language={language} strings={strings} />
      </div>

      {/* Expand/collapse toggle */}
      <button
        type="button"
        aria-expanded={previewExpanded}
        data-testid={`block-card-${block.id}-expand`}
        onClick={() => setPreviewExpanded((prev) => !prev)}
        style={{
          border: 'none', background: 'none', cursor: 'pointer',
          fontSize: 12, color: 'var(--lx-fg3)', fontFamily: 'inherit',
          textAlign: 'start', padding: 0,
        }}
      >
        {previewExpanded ? strings.blockCardCollapse : strings.blockCardExpand}
      </button>
    </li>
  );
}
