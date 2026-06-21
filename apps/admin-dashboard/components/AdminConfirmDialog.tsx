'use client';

/**
 * AdminConfirmDialog — reusable dialog shell for all lifecycle and child-edit
 * confirmation dialogs (Suspend, Reactivate, Delete, Grade Override, Change
 * Learning Language).
 *
 * NOT a compound-component or new design pattern — a plain wrapper around a
 * native HTML `<dialog>` element with the established token treatment. Callers
 * supply the interior content as `children`.
 *
 * Design Spec Part A §A.2.
 *
 * Behaviour:
 *   - Focus trapped inside on open (focus moves to first interactive element).
 *   - ESC key cancels (never confirms).
 *   - Backdrop click does NOT dismiss (prevents accidental destructive actions).
 *   - On close, focus returns to the trigger element.
 *   - Entrance motion: overlay fade 200ms + card translateY(8px)→0 200ms.
 *   - Exit: reverse 160ms (handled via CSS class toggle).
 *
 * Icon variants:
 *   suspend     → pause-circle amber
 *   reactivate  → play-circle  green
 *   delete      → trash-2      danger-red
 *   grade       → graduation-cap indigo
 *   destructive → alert-triangle danger-red  (used by change-learning-language)
 */

import {
  type ReactNode,
  useEffect,
  useRef,
  useCallback,
} from 'react';
import { Stack, Text } from '@tamagui/core';
import type { AdminStrings } from '../lib/strings';

export type DialogVariant =
  | 'suspend'
  | 'reactivate'
  | 'delete'
  | 'grade'
  | 'destructive'
  // P7-05 curriculum lifecycle variants (additive — no structural change)
  | 'publish'
  | 'unpublish'
  | 'archive'
  | 'restore'
  // P7-13 gamification variants (additive — no structural change)
  | 'retire'               // deactivate catalog items — amber archive icon
  | 'expire'               // expire timed events — amber clock icon
  | 'gamification-override' // league-tier / freeze grant — indigo shield icon
  // P7-09 moderation review variants (additive — no structural change)
  | 'mod-approve'          // approve content item — green check-circle
  | 'mod-reject'           // reject content item — red x-circle
  | 'mod-flag';            // flag for escalation — purple flag

export interface AdminConfirmDialogProps {
  /** Whether the dialog is visible. */
  open: boolean;
  /** Triggered when the user cancels (ESC or Cancel button). */
  onClose: () => void;
  /** Dialog variant controls the icon and its colour. */
  variant: DialogVariant;
  /** Title text (rendered as the accessible dialog label). */
  title: string;
  /** Optional subtitle beneath the title. */
  subtitle?: string;
  /** Dialog body — reason fields, notices, confirmation inputs. */
  children: ReactNode;
  /**
   * Cancel button label (pass strings.cancel etc. from the caller).
   * The Cancel button is always enabled.
   */
  cancelLabel: AdminStrings['signOutButton'] | string;
  /**
   * Confirm button content node. The caller owns the styling and disabled state
   * of the Confirm button (since button colour differs by variant). Pass a
   * fully-styled button element here.
   */
  confirmButton: ReactNode;
  /**
   * Optional per-dialog `data-testid` override for the dialog card element.
   * When provided it is used instead of the default "admin-dialog".
   * E.g. "suspend-dialog", "delete-dialog", etc.
   */
  dialogTestId?: string;
}

// ── Icon SVGs (Lucide-compatible, 20px, 2px stroke, rounded caps) ─────────────

function PauseCircleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <line x1="10" y1="15" x2="10" y2="9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <line x1="14" y1="15" x2="14" y2="9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function PlayCircleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <polygon points="10,8 16,12 10,16" fill="currentColor" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M3 6h18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M19 6l-1 14H6L5 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M10 11v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M14 11v6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M9 6V4h6v2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function GraduationCapIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M22 10L12 4 2 10l10 6 10-6z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M6 12v5c0 1.657 2.686 3 6 3s6-1.343 6-3v-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function AlertTriangleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="12" y1="9" x2="12" y2="13" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <circle cx="12" cy="17" r="1" fill="currentColor" />
    </svg>
  );
}

// ── P7-05 lifecycle icon SVGs (Lucide-compatible, 20px, 2px stroke) ───────────

function UploadCloudIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <polyline points="16 16 12 12 8 16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="12" y1="12" x2="12" y2="21" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M20.39 18.39A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.3" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function DownloadCloudIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <polyline points="8 17 12 21 16 17" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="12" y1="12" x2="12" y2="21" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <path d="M20.88 18.09A5 5 0 0 0 18 9h-1.26A8 8 0 1 0 3 16.29" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function ArchiveIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <polyline points="21 8 21 21 3 21 3 8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <rect x="1" y="3" width="22" height="5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="10" y1="12" x2="14" y2="12" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function RotateCcwIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <polyline points="1 4 1 10 7 10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M3.51 15a9 9 0 1 0 .49-4.5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

// ── P7-13 gamification icon SVGs (Lucide-compatible, 20px, 2px stroke) ─────────

/** Clock icon — used for 'expire' variant */
function ClockIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <polyline points="12 6 12 12 16 14" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

/** Shield icon (plain outline, no check) — used for 'gamification-override' variant */
function ShieldIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

// ── P7-09 moderation icon SVGs (Lucide-compatible, 20px, 2px stroke) ───────────

function CheckCircleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <polyline points="9,12 11,14 15,10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function XCircleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <line x1="15" y1="9" x2="9" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <line x1="9" y1="9" x2="15" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

function FlagIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
      <line x1="4" y1="22" x2="4" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

interface VariantStyle {
  iconBg: string;
  iconColor: string;
  icon: ReactNode;
}

const VARIANT_STYLES: Record<DialogVariant, VariantStyle> = {
  suspend: {
    iconBg: 'rgba(245,158,11,0.15)',
    iconColor: '#F59E0B',
    icon: <PauseCircleIcon />,
  },
  reactivate: {
    iconBg: 'rgba(34,197,94,0.15)',
    iconColor: '#22C55E',
    icon: <PlayCircleIcon />,
  },
  delete: {
    iconBg: 'rgba(239,68,68,0.15)',
    iconColor: '#EF4444',
    icon: <TrashIcon />,
  },
  grade: {
    iconBg: 'rgba(79,70,229,0.15)',
    iconColor: '#4F46E5',
    icon: <GraduationCapIcon />,
  },
  destructive: {
    iconBg: 'rgba(239,68,68,0.15)',
    iconColor: '#EF4444',
    icon: <AlertTriangleIcon />,
  },
  // P7-05 curriculum lifecycle variants
  publish: {
    iconBg: 'rgba(79,70,229,0.15)',
    iconColor: '#4F46E5',
    icon: <UploadCloudIcon />,
  },
  unpublish: {
    iconBg: 'rgba(148,163,184,0.12)',
    iconColor: '#94A3B8',
    icon: <DownloadCloudIcon />,
  },
  archive: {
    iconBg: 'rgba(245,158,11,0.15)',
    iconColor: '#F59E0B',
    icon: <ArchiveIcon />,
  },
  restore: {
    iconBg: 'rgba(34,197,94,0.15)',
    iconColor: '#22C55E',
    icon: <RotateCcwIcon />,
  },
  // P7-13 gamification variants
  retire: {
    iconBg: 'rgba(245,158,11,0.15)',
    iconColor: '#F59E0B',
    icon: <ArchiveIcon />,
  },
  expire: {
    iconBg: 'rgba(245,158,11,0.15)',
    iconColor: '#F59E0B',
    icon: <ClockIcon />,
  },
  'gamification-override': {
    iconBg: 'rgba(79,70,229,0.15)',
    iconColor: '#4F46E5',
    icon: <ShieldIcon />,
  },
  // P7-09 moderation review variants (additive)
  'mod-approve': {
    iconBg: 'rgba(34,197,94,0.15)',
    iconColor: '#22C55E',
    icon: <CheckCircleIcon />,
  },
  'mod-reject': {
    iconBg: 'rgba(239,68,68,0.15)',
    iconColor: '#EF4444',
    icon: <XCircleIcon />,
  },
  'mod-flag': {
    iconBg: 'rgba(168,85,247,0.15)',
    iconColor: '#A855F7',
    icon: <FlagIcon />,
  },
};

// ── Focus trap helpers ─────────────────────────────────────────────────────────

const FOCUSABLE_SELECTORS =
  'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

function getFocusableElements(el: HTMLElement): HTMLElement[] {
  return Array.from(el.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTORS)).filter(
    (e) => !e.closest('[aria-hidden="true"]'),
  );
}

// ── Component ──────────────────────────────────────────────────────────────────

export function AdminConfirmDialog({
  open,
  onClose,
  variant,
  title,
  subtitle,
  children,
  cancelLabel,
  confirmButton,
  dialogTestId = 'admin-dialog',
}: AdminConfirmDialogProps) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const titleId = useRef(`dialog-title-${Math.random().toString(36).slice(2)}`).current;
  // Keep track of the element that triggered the dialog so we can restore focus.
  const triggerRef = useRef<Element | null>(null);

  // Capture the active element when the dialog opens.
  useEffect(() => {
    if (open) {
      triggerRef.current = document.activeElement;
      // Defer to let the DOM settle, then focus first interactive element.
      const id = setTimeout(() => {
        if (dialogRef.current) {
          const focusable = getFocusableElements(dialogRef.current);
          const first = focusable.at(0);
          if (first) {
            first.focus();
          }
        }
      }, 50);
      return () => clearTimeout(id);
    } else {
      // Return focus to the trigger.
      if (triggerRef.current && 'focus' in triggerRef.current) {
        (triggerRef.current as HTMLElement).focus();
      }
    }
  }, [open]);

  // ESC closes from anywhere while open — the dialog's onKeyDown only fires when
  // focus is inside the dialog, which misses the case where focus sits on the backdrop.
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [open, onClose]);

  // Focus trap — keep Tab inside the dialog. (ESC is handled globally above so it
  // works regardless of where focus currently sits.)
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent<HTMLDivElement>) => {
      if (e.key !== 'Tab') return;
      if (!dialogRef.current) return;

      const focusable = getFocusableElements(dialogRef.current);
      if (focusable.length === 0) return;

      const first = focusable.at(0);
      const last = focusable.at(-1);
      if (!first || !last) return;

      if (e.shiftKey) {
        if (document.activeElement === first) {
          e.preventDefault();
          last.focus();
        }
      } else {
        if (document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    },
    [],
  );

  if (!open) return null;

  const vs = VARIANT_STYLES[variant];

  return (
    // Overlay — does NOT close on click (prevents accidental destructive dismiss).
    <div
      aria-hidden={!open}
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'var(--lx-overlay)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 500,
        animation: 'lx-dialog-overlay-in 200ms var(--lx-ease-out)',
      }}
    >
      {/* Dialog card */}
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-testid={dialogTestId}
        onKeyDown={handleKeyDown}
        style={{
          backgroundColor: 'var(--lx-card)',
          borderRadius: 'var(--lx-radius-modal)',
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)',
          padding: 32,
          minWidth: 360,
          maxWidth: 480,
          width: 'calc(100vw - 64px)',
          display: 'flex',
          flexDirection: 'column',
          gap: 24,
          animation: 'lx-dialog-card-in 200ms var(--lx-ease-out)',
          outline: 'none',
        }}
      >
        {/* Header */}
        <Stack flexDirection="row" alignItems="flex-start" gap="$4">
          {/* Icon circle */}
          <Stack
            aria-hidden
            width={40}
            height={40}
            borderRadius="$pill"
            alignItems="center"
            justifyContent="center"
            flexShrink={0}
            style={{ backgroundColor: vs.iconBg, color: vs.iconColor }}
          >
            {vs.icon}
          </Stack>

          {/* Title block */}
          <Stack flex={1} flexDirection="column" gap="$1">
            <Text
              id={titleId}
              fontFamily="$heading"
              fontSize={18}
              fontWeight="700"
              color="$fg1"
              style={{ lineHeight: 1.3 }}
            >
              {title}
            </Text>
            {subtitle ? (
              <Text
                fontFamily="$body"
                fontSize={14}
                color="$fg3"
                style={{ lineHeight: 1.5, marginTop: 4 }}
              >
                {subtitle}
              </Text>
            ) : null}
          </Stack>
        </Stack>

        {/* Body — caller-supplied content */}
        <Stack flexDirection="column" gap="$4">
          {children}
        </Stack>

        {/* Actions row */}
        <Stack
          flexDirection="row"
          justifyContent="flex-end"
          alignItems="center"
          gap="$3"
          style={{ marginTop: 8 }}
          // RTL: row-reverse so Cancel is on right and Confirm on left in RTL
          className="lx-dialog-actions"
        >
          <button
            type="button"
            data-testid="dialog-cancel-btn"
            onClick={onClose}
            style={{
              height: 36,
              paddingInline: 16,
              borderRadius: 'var(--lx-radius-button)',
              border: '1px solid rgba(255,255,255,0.16)',
              backgroundColor: 'transparent',
              color: 'var(--lx-fg2)',
              fontSize: 14,
              fontWeight: 500,
              cursor: 'pointer',
              fontFamily: 'inherit',
              transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)',
            }}
            onMouseEnter={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'var(--lx-card-soft)';
            }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
            }}
          >
            {cancelLabel}
          </button>
          {confirmButton}
        </Stack>
      </div>
    </div>
  );
}
