'use client';

/**
 * BlockTypeBadge — maps ContentBlockTypeValue (1/2/3/4) to a colored chip.
 * Design Spec §S3 — P7-02.
 */

import { CONTENT_BLOCK_TYPE, type ContentBlockTypeValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// Lucide-style inline SVGs for each block type.
function AlignLeftIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="17" y1="10" x2="3" y2="10" /><line x1="21" y1="6" x2="3" y2="6" />
      <line x1="21" y1="14" x2="3" y2="14" /><line x1="13" y1="18" x2="3" y2="18" />
    </svg>
  );
}
function ImageIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
      <circle cx="8.5" cy="8.5" r="1.5" />
      <polyline points="21 15 16 10 5 21" />
    </svg>
  );
}
function PlayCircleIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><polygon points="10 8 16 12 10 16 10 8" />
    </svg>
  );
}
function AlertCircleIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}

interface BlockTypeBadgeConfig {
  icon: React.ReactNode;
  label: string;
  bg: string;
  color: string;
}

const CONFIG: Record<ContentBlockTypeValue, BlockTypeBadgeConfig> = {
  [CONTENT_BLOCK_TYPE.Text]:    { icon: <AlignLeftIcon />, label: strings.blockTypeText,    bg: 'rgba(79,70,229,0.12)',   color: '#6366F1' },
  [CONTENT_BLOCK_TYPE.Image]:   { icon: <ImageIcon />,     label: strings.blockTypeImage,   bg: 'rgba(34,197,94,0.12)',   color: '#22C55E' },
  [CONTENT_BLOCK_TYPE.Video]:   { icon: <PlayCircleIcon />, label: strings.blockTypeVideo,  bg: 'rgba(168,85,247,0.12)', color: '#A855F7' },
  [CONTENT_BLOCK_TYPE.Callout]: { icon: <AlertCircleIcon />, label: strings.blockTypeCallout, bg: 'rgba(245,158,11,0.12)', color: '#F59E0B' },
};

interface BlockTypeBadgeProps {
  blockType: ContentBlockTypeValue;
}

export function BlockTypeBadge({ blockType }: BlockTypeBadgeProps) {
  const cfg = CONFIG[blockType];
  if (!cfg) return null;

  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 4,
        height: 20,
        borderRadius: 9999,
        paddingLeft: 8,
        paddingRight: 8,
        backgroundColor: cfg.bg,
        color: cfg.color,
        flexShrink: 0,
      }}
    >
      {cfg.icon}
      <span
        style={{
          fontSize: 11,
          fontWeight: 600,
          textTransform: 'uppercase',
          letterSpacing: '0.04em',
        }}
      >
        {cfg.label}
      </span>
    </span>
  );
}
