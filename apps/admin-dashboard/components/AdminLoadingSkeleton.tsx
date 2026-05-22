'use client';

/**
 * AdminLoadingSkeleton — full-viewport shimmer skeleton (Design Spec §5b).
 *
 * Built with Tamagui (`@tamagui/core` Stack) + design-system tokens — no CSS
 * module. Shown while `authStore.status === 'unknown'` (hydration) to prevent
 * any flash of protected content or the login redirect. The shimmer is a CSS
 * gradient + `@keyframes lx-shimmer` (defined in globals.css) applied via an
 * inline `style` — appropriate for the web target, no Moti/Reanimated.
 * `role="status"` + label for a11y.
 */

import { Stack, type GetProps } from '@tamagui/core';

export interface AdminLoadingSkeletonProps {
  label: string;
}

// CSS shimmer sweep: a token-driven gradient animated by the `lx-shimmer`
// keyframe in globals.css. Inline `style` because the moving background-position
// is a pure web CSS animation (not expressible as a Tamagui token).
const SHIMMER_STYLE: React.CSSProperties = {
  background:
    'linear-gradient(90deg, var(--lx-card) 0%, var(--lx-card-soft) 50%, var(--lx-card) 100%)',
  backgroundSize: '400px 100%',
  animation: 'lx-shimmer 1.4s ease infinite',
};

function SkeletonBlock(props: GetProps<typeof Stack>) {
  return <Stack borderRadius="$sm" style={SHIMMER_STYLE} {...props} />;
}

export function AdminLoadingSkeleton({ label }: AdminLoadingSkeletonProps) {
  return (
    <Stack
      role="status"
      aria-live="polite"
      aria-label={label}
      flexDirection="row"
      backgroundColor="$bg"
      style={{ minHeight: '100vh' }}
    >
      <Stack
        tag="aside"
        flexDirection="column"
        width={240}
        backgroundColor="$bgElevated"
        borderInlineEndWidth={1}
        borderInlineEndColor="$border"
        $sm={{ display: 'none' }}
      >
        <SkeletonBlock height={28} margin="$6" />
        <Stack flexDirection="column" gap="$3" paddingHorizontal="$4">
          <SkeletonBlock height={44} borderRadius="$button" />
          <SkeletonBlock height={44} borderRadius="$button" />
        </Stack>
      </Stack>

      <Stack flex={1} flexDirection="column">
        <Stack
          flexDirection="row"
          alignItems="center"
          justifyContent="flex-end"
          gap="$4"
          height={64}
          backgroundColor="$bgElevated"
          borderBottomWidth={1}
          borderBottomColor="$border"
          paddingHorizontal="$6"
        >
          <SkeletonBlock width={100} height={16} />
          <SkeletonBlock width={80} height={32} />
        </Stack>
        <Stack flex={1} flexDirection="column" gap="$4" padding="$8">
          <SkeletonBlock width="40%" height={24} />
          <SkeletonBlock width="60%" height={16} />
        </Stack>
      </Stack>
    </Stack>
  );
}
