'use client';

/**
 * AdminErrorBanner — inline error/forbidden/warning banner (Design Spec §3e).
 *
 * Built with Tamagui (`@tamagui/core` Stack/Text) + design-system tokens — no
 * CSS module. `role="alert"` so screen readers announce immediately. The
 * entrance animation is a CSS `@keyframes lx-banner-in` (defined in globals.css)
 * applied via an inline `animation` style — appropriate for the web target and
 * avoids native-only Moti/Reanimated.
 */

import { Stack, Text } from '@tamagui/core';

/**
 * 'success' variant added for P7-07/P7-08 positive-outcome banners
 * (Reactivate, Grade Override, Learning Language change).
 * Design Spec Part A §A — Gap 2.
 */
export type AdminBannerVariant = 'error' | 'forbidden' | 'warning' | 'success';

export interface AdminErrorBannerProps {
  variant?: AdminBannerVariant;
  message: string;
}

interface VariantTokens {
  /** Token-driven CSS-variable references (resolved from globals.css). */
  background: string;
  borderColor: string;
  iconColor: string;
}

const VARIANTS: Record<AdminBannerVariant, VariantTokens> = {
  error: {
    background: 'var(--lx-danger-soft)',
    borderColor: 'rgba(239, 68, 68, 0.3)',
    iconColor: 'var(--lx-danger)',
  },
  forbidden: {
    background: 'rgba(168, 85, 247, 0.15)',
    borderColor: 'rgba(168, 85, 247, 0.3)',
    iconColor: 'var(--lx-purple)',
  },
  warning: {
    background: 'var(--lx-warning-soft)',
    borderColor: 'rgba(245, 158, 11, 0.3)',
    iconColor: 'var(--lx-accent)',
  },
  /** P7-07 Gap 2 — success variant for positive-outcome banners. */
  success: {
    background: 'rgba(34, 197, 94, 0.15)',
    borderColor: 'rgba(34, 197, 94, 0.3)',
    iconColor: '#22C55E',
  },
};

export function AdminErrorBanner({ variant = 'error', message }: AdminErrorBannerProps) {
  const tokens = VARIANTS[variant];

  return (
    <Stack
      role="alert"
      flexDirection="row"
      alignItems="flex-start"
      gap="$3"
      padding="$4"
      borderRadius="$sm"
      borderWidth={1}
      style={{
        // Variant colors + CSS keyframe entrance (globals.css). The variant
        // soft-fills are non-token rgba values, so they go through `style`.
        background: tokens.background,
        borderColor: tokens.borderColor,
        animation: 'lx-banner-in var(--lx-dur-base) var(--lx-ease-out)',
      }}
    >
      <span
        aria-hidden="true"
        style={{ flexShrink: 0, marginTop: 1, display: 'inline-flex', color: tokens.iconColor }}
      >
        {variant === 'forbidden' ? (
        <LockIcon />
      ) : variant === 'success' ? (
        <CheckCircleIcon />
      ) : (
        <WarningIcon />
      )}
      </span>
      <Text fontFamily="$body" fontSize={14} lineHeight={21} color="$fg1">
        {message}
      </Text>
    </Stack>
  );
}

function WarningIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M12 3 1.8 21h20.4L12 3Z"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinejoin="round"
      />
      <path d="M12 9v5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
      <circle cx="12" cy="17" r="1" fill="currentColor" />
    </svg>
  );
}

function CheckCircleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="1.6" />
      <polyline points="9,12 11,14 15,10" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

function LockIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <rect x="4" y="10" width="16" height="11" rx="2" stroke="currentColor" strokeWidth="1.6" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" stroke="currentColor" strokeWidth="1.6" />
      <circle cx="12" cy="15.5" r="1.4" fill="currentColor" />
    </svg>
  );
}
