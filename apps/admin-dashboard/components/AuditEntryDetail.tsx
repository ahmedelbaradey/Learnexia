'use client';

/**
 * AuditEntryDetail — read-only expanded detail panel for an audit log entry.
 *
 * Design Spec §E. Renders inside the colSpan=5 table cell of an expanded row.
 * Shows all 8 `AuditLogDto` fields + the `details` block.
 *
 * CRITICAL SECURITY INVARIANTS (enforced in this component):
 * 1. ZERO mutation affordances — no form elements, no mutating hooks.
 * 2. `details` is rendered as TEXT CONTENT inside a `<pre>`, NEVER as HTML.
 *    NEVER `dangerouslySetInnerHTML`, NEVER `marked`/any HTML renderer.
 *    If `details` parses as JSON → pretty-print with `JSON.stringify(parsed, null, 2)`.
 *    If `details` is not valid JSON → render the raw string as plain text.
 *    If `details` is null → render "—" and omit the `<pre>` block.
 * 3. Technical identifiers (EventId, adminUserId, targetEntityId, ISO dates,
 *    raw action strings, `<pre>` block) are always wrapped in `dir="ltr"`.
 *
 * RTL spec (Design Spec §E.3 + §I):
 * - Panel inline-start border renders on the correct edge under `dir="rtl"` (logical).
 * - Field labels are `textAlign: start` (flips under RTL).
 * - Technical strings stay `dir="ltr"`.
 */

import { useState, useCallback } from 'react';
import type { AuditLogDto } from '@learnexia/api-client';
import { ActionTypeBadge } from './ActionTypeBadge';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { getTargetTypeLabel } from '../lib/auditActionLabels';

const strings = getStrings(ADMIN_LOCALE);

// ── Detail panel ──────────────────────────────────────────────────────────────

interface AuditEntryDetailProps {
  entry: AuditLogDto;
}

/**
 * Pretty-format `details` safely as escaped text:
 * - null → undefined (caller omits the <pre>)
 * - valid JSON → JSON.stringify(parsed, null, 2) (indented, but still text)
 * - non-JSON string → raw string as-is (plain text)
 *
 * NEVER produces HTML. Text is placed as children of <pre>, not innerHTML.
 */
function formatDetails(details: string | null): string | null {
  if (details === null) return null;
  try {
    const parsed: unknown = JSON.parse(details);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return details;
  }
}

/** Format a timestamp for display. */
function formatTimestamp(iso: string): string {
  try {
    return new Intl.DateTimeFormat(ADMIN_LOCALE === 'ar' ? 'ar-EG' : 'en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

const fieldLabelStyle: React.CSSProperties = {
  fontSize: 11,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.06em',
  textAlign: 'start',
  marginBottom: 4,
  fontFamily: 'inherit',
};

const fieldValueStyle: React.CSSProperties = {
  fontSize: 14,
  fontWeight: 500,
  color: 'var(--lx-fg1)',
  lineHeight: 1.5,
  fontFamily: 'inherit',
};

export function AuditEntryDetail({ entry }: AuditEntryDetailProps) {
  const formattedDetails = formatDetails(entry.details);
  const localizedTargetType = getTargetTypeLabel(entry.targetEntityType, ADMIN_LOCALE);

  // Copy-to-clipboard state (label-swap only — no API call, no mutation)
  const [copyLabel, setCopyLabel] = useState<string>(strings.auditDetailCopy);
  const handleCopy = useCallback(() => {
    if (!entry.details) return;
    // navigator.clipboard requires secure context (HTTPS / localhost)
    navigator.clipboard.writeText(entry.details).then(() => {
      setCopyLabel(strings.auditDetailCopied);
      setTimeout(() => setCopyLabel(strings.auditDetailCopy), 1500);
    }).catch(() => {
      // Clipboard unavailable (non-secure context) — silently ignore
    });
  }, [entry.details]);

  return (
    <div
      style={{
        padding: 24,
        backgroundColor: 'rgba(79,70,229,0.04)',
        borderInlineStart: '3px solid rgba(79,70,229,0.3)',
      }}
    >
      {/* Fields grid — auto-fit collapses at tablet/mobile widths */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
          gap: 16,
          marginBottom: 16,
        }}
      >
        {/* 1. Event ID */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailEventId}</div>
          <span
            dir="ltr"
            style={{
              ...fieldValueStyle,
              fontFamily: 'var(--lx-font-mono, monospace)',
              fontSize: 13,
              color: 'var(--lx-fg2)',
              wordBreak: 'break-all',
            }}
          >
            {entry.eventId}
          </span>
        </div>

        {/* 2. Admin (actor) */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailAdmin}</div>
          <span
            dir="ltr"
            style={{
              ...fieldValueStyle,
              fontFamily: 'var(--lx-font-mono, monospace)',
            }}
          >
            #{entry.adminUserId}
          </span>
        </div>

        {/* 3. Action */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailAction}</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <ActionTypeBadge action={entry.action} />
            <span
              dir="ltr"
              style={{
                fontSize: 12,
                color: 'var(--lx-fg3)',
                fontFamily: 'var(--lx-font-mono, monospace)',
              }}
            >
              {entry.action}
            </span>
          </div>
        </div>

        {/* 4. Target type */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailTargetType}</div>
          <span style={fieldValueStyle}>{localizedTargetType}</span>
        </div>

        {/* 5. Target ID */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailTargetId}</div>
          <span
            dir="ltr"
            style={{
              ...fieldValueStyle,
              fontFamily: 'var(--lx-font-mono, monospace)',
            }}
          >
            {entry.targetEntityId > 0 ? `#${entry.targetEntityId}` : strings.auditDetailNoDetails}
          </span>
        </div>

        {/* 6. Occurred at */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailOccurredAt}</div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
            <span
              dir="ltr"
              style={{
                ...fieldValueStyle,
                fontFamily: 'var(--lx-font-mono, monospace)',
                fontSize: 13,
                wordBreak: 'break-all',
              }}
            >
              {entry.occurredAtUtc}
            </span>
            <span
              dir="ltr"
              style={{
                fontSize: 12,
                color: 'var(--lx-fg3)',
                fontFamily: 'inherit',
              }}
            >
              {formatTimestamp(entry.occurredAtUtc)}
            </span>
          </div>
        </div>

        {/* 7. Created at (row-insert time) */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <div style={fieldLabelStyle}>{strings.auditDetailCreatedAt}</div>
          <span
            dir="ltr"
            style={{
              ...fieldValueStyle,
              fontFamily: 'var(--lx-font-mono, monospace)',
              fontSize: 13,
              color: 'var(--lx-fg3)',
              wordBreak: 'break-all',
            }}
          >
            {entry.createdAt}
          </span>
        </div>
      </div>

      {/* Details panel */}
      <div style={{ marginTop: 8 }}>
        {/* Details label row */}
        <div
          style={{
            display: 'flex',
            flexDirection: 'row',
            alignItems: 'center',
            gap: 12,
            marginBottom: 8,
          }}
        >
          <span style={fieldLabelStyle}>{strings.auditDetailDetailsLabel}</span>

          {/* Optional copy button — only when details is non-null.
              Pure client-side: navigator.clipboard.writeText — no API call. */}
          {entry.details !== null && (
            <button
              type="button"
              data-testid={`audit-detail-copy-${entry.id}`}
              onClick={handleCopy}
              style={{
                height: 24,
                paddingLeft: 8,
                paddingRight: 8,
                borderRadius: 8,
                border: '1px solid var(--lx-border)',
                backgroundColor: 'transparent',
                color: 'var(--lx-fg3)',
                fontSize: 11,
                cursor: 'pointer',
                fontFamily: 'inherit',
              }}
            >
              {copyLabel}
            </button>
          )}
        </div>

        {/* Details content — ALWAYS text, NEVER HTML */}
        {formattedDetails !== null ? (
          <pre
            dir="ltr"
            style={{
              backgroundColor: 'var(--lx-bg)',
              borderRadius: 'var(--lx-radius-sm)',
              border: '1px solid var(--lx-border)',
              padding: 12,
              margin: 0,
              fontFamily: 'var(--lx-font-mono, monospace)',
              fontSize: 13,
              lineHeight: 1.6,
              color: 'var(--lx-fg2)',
              overflowX: 'auto',
              maxHeight: 320,
              overflowY: 'auto',
              whiteSpace: 'pre-wrap',
              wordBreak: 'break-word',
              userSelect: 'text',
            }}
          >
            {/* Text content only — never dangerouslySetInnerHTML */}
            {formattedDetails}
          </pre>
        ) : (
          <span
            style={{
              color: 'var(--lx-fg3)',
              fontStyle: 'italic',
              fontSize: 14,
              fontFamily: 'inherit',
            }}
          >
            {strings.auditDetailNoDetails}
          </span>
        )}
      </div>
    </div>
  );
}
