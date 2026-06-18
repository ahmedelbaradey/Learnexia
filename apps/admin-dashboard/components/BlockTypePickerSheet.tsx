'use client';

/**
 * BlockTypePickerSheet — inline dropdown type picker for adding content blocks.
 * Design Spec §8.4 — P7-02.
 *
 * Renders as an absolutely-positioned menu below the trigger button.
 * Uses role="menu" / role="menuitem" pattern.
 * ESC closes; click-outside closes.
 */

import { useEffect, useRef } from 'react';
import { CONTENT_BLOCK_TYPE, type ContentBlockTypeValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// Type-specific icons (12px, same as BlockTypeBadge)
function AlignLeftIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="17" y1="10" x2="3" y2="10" /><line x1="21" y1="6" x2="3" y2="6" />
      <line x1="21" y1="14" x2="3" y2="14" /><line x1="13" y1="18" x2="3" y2="18" />
    </svg>
  );
}
function ImageIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
      <circle cx="8.5" cy="8.5" r="1.5" /><polyline points="21 15 16 10 5 21" />
    </svg>
  );
}
function PlayCircleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><polygon points="10 8 16 12 10 16 10 8" />
    </svg>
  );
}
function AlertCircleIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}

const TYPE_OPTIONS: Array<{ type: ContentBlockTypeValue; icon: React.ReactNode; label: string; testId: string }> = [
  { type: CONTENT_BLOCK_TYPE.Text,    icon: <AlignLeftIcon />,   label: strings.blockPickerText,    testId: 'pick-type-1' },
  { type: CONTENT_BLOCK_TYPE.Image,   icon: <ImageIcon />,       label: strings.blockPickerImage,   testId: 'pick-type-2' },
  { type: CONTENT_BLOCK_TYPE.Video,   icon: <PlayCircleIcon />,  label: strings.blockPickerVideo,   testId: 'pick-type-3' },
  { type: CONTENT_BLOCK_TYPE.Callout, icon: <AlertCircleIcon />, label: strings.blockPickerCallout, testId: 'pick-type-4' },
];

export interface BlockTypePickerSheetProps {
  open: boolean;
  onClose: () => void;
  onPick: (type: ContentBlockTypeValue) => void;
}

export function BlockTypePickerSheet({ open, onClose, onPick }: BlockTypePickerSheetProps) {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); onClose(); }
    };
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) onClose();
    };
    document.addEventListener('keydown', handleKey);
    document.addEventListener('mousedown', handleClickOutside);
    return () => {
      document.removeEventListener('keydown', handleKey);
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [open, onClose]);

  useEffect(() => {
    if (open && menuRef.current) {
      const firstItem = menuRef.current.querySelector<HTMLButtonElement>('[role="menuitem"]');
      firstItem?.focus();
    }
  }, [open]);

  if (!open) return null;

  return (
    <div
      ref={menuRef}
      role="menu"
      aria-label={strings.blockPickerHeading}
      style={{
        position: 'absolute',
        zIndex: 200,
        top: '100%',
        insetInlineEnd: 0,
        marginTop: 8,
        backgroundColor: 'var(--lx-card-soft)',
        borderRadius: 20,
        border: '1px solid var(--lx-border)',
        boxShadow: 'var(--lx-shadow-float, 0 8px 24px rgba(0,0,0,0.3))',
        padding: 8,
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
        minWidth: 180,
      }}
    >
      <p style={{
        margin: 0,
        fontSize: 11,
        fontWeight: 600,
        color: 'var(--lx-fg3)',
        textTransform: 'uppercase',
        letterSpacing: '0.06em',
        paddingLeft: 12,
        paddingRight: 12,
        paddingBottom: 6,
      }}>
        {strings.blockPickerHeading}
      </p>
      {TYPE_OPTIONS.map(({ type, icon, label, testId }) => (
        <button
          key={type}
          role="menuitem"
          type="button"
          data-testid={testId}
          aria-label={`${strings.blockEditorAddBtn} ${label}`}
          onClick={() => { onPick(type); onClose(); }}
          style={{
            height: 40,
            borderRadius: 8,
            paddingLeft: 12,
            paddingRight: 12,
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            cursor: 'pointer',
            border: 'none',
            backgroundColor: 'transparent',
            color: 'var(--lx-fg3)',
            fontSize: 14,
            fontFamily: 'inherit',
            textAlign: 'start',
            transition: 'background-color 120ms var(--lx-ease-out, ease-out)',
          }}
          onMouseEnter={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(255,255,255,0.06)'; }}
          onMouseLeave={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent'; }}
          onFocus={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(255,255,255,0.06)'; }}
          onBlur={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent'; }}
        >
          {icon}
          <span style={{ fontSize: 14, color: 'var(--lx-fg2)', fontWeight: 500 }}>{label}</span>
        </button>
      ))}
    </div>
  );
}
