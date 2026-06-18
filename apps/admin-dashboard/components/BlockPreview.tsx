'use client';

/**
 * BlockPreview — inline sanitized renderer for content blocks.
 * Design Spec §S9 — P7-02.
 *
 * SECURITY — This is the primary XSS risk surface.
 * All Markdown is run through renderMarkdown() (marked + DOMPurify) before
 * injection via dangerouslySetInnerHTML. No raw Markdown is ever injected.
 * Image/Video URLs are validated through isSecureImageUrl() before rendering.
 * This component is the ONLY place dangerouslySetInnerHTML is used for
 * Markdown content in the admin dashboard.
 */

import type { AdminContentBlockDto, TextPayload, ImagePayload, VideoPayload, CalloutPayload } from '@learnexia/api-client';
import { CONTENT_BLOCK_TYPE, CONTENT_LANGUAGE, type ContentLanguageValue } from '@learnexia/shared/constants';
import { renderMarkdown } from '../lib/renderMarkdown';
import type { AdminStrings } from '../lib/strings';

// ── Secure URL guard (§9.3 Image + Video) ─────────────────────────────────────

/**
 * Validates that a URL is safe to use as an image src or as a link href.
 *
 * Rules (mirrors backend P7-SEC-3):
 * - Protocol must be https:
 * - Host must not be localhost / loopback / private / link-local / bare-IP
 * - For image-only calls, the pathname extension must be in the ALLOWED list
 *
 * @param url - URL string to validate.
 * @param requireExtension - When true, require an allowed image extension.
 */
export function isSecureImageUrl(url: string, requireExtension = true): boolean {
  try {
    const u = new URL(url);
    if (u.protocol !== 'https:') return false;
    const host = u.hostname;
    if (/^(localhost|127\.|10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|169\.254\.|0\.0\.0\.0)/.test(host)) return false;
    // IPv6 loopback [::1], link-local [fe80::…], and ULA [fc00:: / fd00::]
    if (/^\[?(::1|fe[89ab][0-9a-f]:|fc[0-9a-f]{2}:|fd[0-9a-f]{2}:)/i.test(host)) return false;
    if (requireExtension) {
      const ext = u.pathname.split('.').pop()?.toLowerCase();
      const ALLOWED = ['jpg', 'jpeg', 'png', 'webp', 'gif', 'svg', 'avif'];
      return !!ext && ALLOWED.includes(ext);
    }
    return true;
  } catch {
    return false;
  }
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────
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
    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="#A855F7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><polygon points="10 8 16 12 10 16 10 8" />
    </svg>
  );
}
function InfoIcon({ color }: { color: string }) {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" /><line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  );
}
function AlertTriangleIcon({ color }: { color: string }) {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  );
}
function LightbulbIcon({ color }: { color: string }) {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke={color} strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="9" y1="18" x2="15" y2="18" /><line x1="10" y1="22" x2="14" y2="22" />
      <path d="M15.09 14c.18-.98.65-1.74 1.41-2.5A4.65 4.65 0 0 0 18 8 6 6 0 0 0 6 8c0 1 .23 2.23 1.5 3.5A4.61 4.61 0 0 1 8.91 14" />
    </svg>
  );
}
function AlertCircleIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="var(--lx-danger)" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="12" /><line x1="12" y1="16" x2="12.01" y2="16" />
    </svg>
  );
}

// ── Callout config ─────────────────────────────────────────────────────────────

const CALLOUT_CONFIG = {
  info: {
    border: '#4F46E5',
    bg: 'rgba(79,70,229,0.08)',
    icon: (_s: AdminStrings) => <InfoIcon color="#6366F1" />,
    label: (s: AdminStrings) => s.blockPreviewCalloutInfo,
  },
  warning: {
    border: '#F59E0B',
    bg: 'rgba(245,158,11,0.08)',
    icon: (_s: AdminStrings) => <AlertTriangleIcon color="#F59E0B" />,
    label: (s: AdminStrings) => s.blockPreviewCalloutWarning,
  },
  tip: {
    border: '#22C55E',
    bg: 'rgba(34,197,94,0.08)',
    icon: (_s: AdminStrings) => <LightbulbIcon color="#22C55E" />,
    label: (s: AdminStrings) => s.blockPreviewCalloutTip,
  },
} as const;

// ── Props ──────────────────────────────────────────────────────────────────────

export interface BlockPreviewProps {
  block: AdminContentBlockDto;
  language: ContentLanguageValue;
  strings: AdminStrings;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function BlockPreview({ block, language, strings }: BlockPreviewProps) {
  const isRtl = language === CONTENT_LANGUAGE.Ar;
  const dir: 'rtl' | 'ltr' = isRtl ? 'rtl' : 'ltr';
  const fontFamily = isRtl ? 'var(--lx-font-arabic-body, inherit)' : 'var(--lx-font-body, inherit)';

  const containerStyle: React.CSSProperties = {
    fontFamily,
    borderTop: '1px solid var(--lx-border)',
    paddingTop: 12,
    marginTop: 4,
  };

  // Parse payload (JSON.parse is guarded by try/catch)
  let parsed: unknown;
  try {
    parsed = JSON.parse(block.payload);
  } catch {
    // Parse failure fallback
    return (
      <div dir={dir} style={{ ...containerStyle, padding: 12, borderRadius: 8, backgroundColor: 'rgba(239,68,68,0.08)', border: '1px solid rgba(239,68,68,0.15)', display: 'flex', flexDirection: 'column', gap: 8 }}>
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          <AlertCircleIcon />
          <span style={{ fontSize: 12, color: 'var(--lx-danger)' }}>{strings.blockPreviewParseError}</span>
        </div>
        <pre style={{ fontFamily: 'var(--lx-font-mono, monospace)', fontSize: 11, color: 'var(--lx-fg3)', whiteSpace: 'pre-wrap', wordBreak: 'break-all', margin: 0 }}>
          {block.payload.slice(0, 200)}
        </pre>
      </div>
    );
  }

  // ── Text block (blockType = 1) ─────────────────────────────────────────────
  if (block.blockType === CONTENT_BLOCK_TYPE.Text) {
    const p = parsed as TextPayload;
    const safeHtml = renderMarkdown(p.markdown ?? '');
    return (
      <div
        dir={dir}
        style={containerStyle}
        dangerouslySetInnerHTML={{ __html: safeHtml }}
        className="lx-block-preview-text"
      />
    );
  }

  // ── Image block (blockType = 2) ───────────────────────────────────────────
  if (block.blockType === CONTENT_BLOCK_TYPE.Image) {
    const p = parsed as ImagePayload;
    const isSecure = isSecureImageUrl(p.url ?? '', true);
    if (isSecure) {
      return (
        <div dir={dir} style={containerStyle}>
          {/* eslint-disable-next-line @next/next/no-img-element */}
          <img
            src={p.url}
            alt={p.altText ?? strings.blockPreviewImageAlt}
            style={{ maxWidth: '100%', borderRadius: 8, display: 'block' }}
            loading="lazy"
          />
          {p.altText && (
            <p style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 4, margin: '4px 0 0 0' }}>{p.altText}</p>
          )}
        </div>
      );
    }
    // URL fallback
    const truncatedUrl = (p.url ?? '').length > 60 ? (p.url ?? '').slice(0, 60) + '…' : (p.url ?? '');
    return (
      <div dir={dir} style={{ ...containerStyle, display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
        <ImageIcon />
        <span style={{ fontSize: 13, color: 'var(--lx-fg3)', fontFamily: 'var(--lx-font-mono, monospace)', wordBreak: 'break-all' }}>
          {truncatedUrl}
        </span>
        <span style={{
          fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em',
          color: 'var(--lx-danger)', backgroundColor: 'rgba(239,68,68,0.1)',
          borderRadius: 9999, paddingLeft: 8, paddingRight: 8, height: 18, display: 'inline-flex', alignItems: 'center',
        }}>
          {strings.blockPreviewCannotPreview}
        </span>
      </div>
    );
  }

  // ── Video block (blockType = 3) ───────────────────────────────────────────
  if (block.blockType === CONTENT_BLOCK_TYPE.Video) {
    const p = parsed as VideoPayload;
    const url = p.url ?? '';
    const isSecure = isSecureImageUrl(url, false); // no extension check for video
    const truncatedUrl = url.length > 80 ? url.slice(0, 80) + '…' : url;
    return (
      <div dir={dir} style={{ ...containerStyle, backgroundColor: 'var(--lx-bg)', borderRadius: 8, padding: 12, display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 12, border: '1px solid var(--lx-border)' }}>
        <PlayCircleIcon />
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4, flex: 1, minWidth: 0 }}>
          <span style={{ fontSize: 13, color: 'var(--lx-fg3)', fontFamily: 'var(--lx-font-mono, monospace)', wordBreak: 'break-all', maxWidth: 360 }} title={url}>
            {truncatedUrl}
          </span>
          {p.caption && (
            <p style={{ margin: 0, fontSize: 12, color: 'var(--lx-fg3)', fontStyle: 'italic' }}>{p.caption}</p>
          )}
        </div>
        {isSecure && (
          <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            style={{ fontSize: 12, color: 'var(--lx-primary)', textDecoration: 'underline', flexShrink: 0, marginInlineStart: 'auto' }}
            aria-label="Open video in new tab"
          >
            {strings.blockPreviewOpenLink}
          </a>
        )}
      </div>
    );
  }

  // ── Callout block (blockType = 4) ─────────────────────────────────────────
  if (block.blockType === CONTENT_BLOCK_TYPE.Callout) {
    const p = parsed as CalloutPayload;
    const variant = p.variant ?? 'info';
    const cfg = CALLOUT_CONFIG[variant as keyof typeof CALLOUT_CONFIG] ?? CALLOUT_CONFIG.info;
    const safeHtml = renderMarkdown(p.markdown ?? '');
    return (
      <div dir={dir} style={{
        ...containerStyle,
        borderRadius: 8,
        borderInlineStart: `3px solid ${cfg.border}`,
        backgroundColor: cfg.bg,
        padding: '12px 16px',
        display: 'flex',
        flexDirection: 'column',
        gap: 8,
      }}>
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 8 }}>
          {cfg.icon(strings)}
          <span style={{ fontSize: 11, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.04em', color: cfg.border }}>
            {cfg.label(strings)}
          </span>
        </div>
        <div
          style={{ fontSize: 14, color: 'var(--lx-fg2)', lineHeight: 1.6 }}
          dangerouslySetInnerHTML={{ __html: safeHtml }}
          className="lx-block-preview-text"
        />
      </div>
    );
  }

  return null;
}
