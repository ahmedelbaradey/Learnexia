'use client';

/**
 * AdminTopBar — sticky top bar (Design Spec §4c).
 *
 * Built with Tamagui (`@tamagui/core` Stack/Text) + design-system tokens — no
 * CSS module. Shows the page title, the signed-in identity (avatar initials +
 * name from `authStore.user`), and a Sign Out button (ghost variant, from
 * `@learnexia/ui` — imported via its web-safe subpath export so no native peers
 * are pulled in). Below the laptop breakpoint it shows a hamburger toggle for
 * the side-nav drawer and hides the identity name.
 */

import { Stack, Text } from '@tamagui/core';
import { Button } from '@learnexia/ui/components/Button';
import { useAuthStore } from '@learnexia/shared/stores';

import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { useSignOut } from '../lib/hooks/useSignOut';

const strings = getStrings(ADMIN_LOCALE);

export interface AdminTopBarProps {
  title?: string;
  onToggleNav?: () => void;
}

function deriveInitial(name: string | null | undefined, fallback: string | null | undefined) {
  const source = name || fallback || '';
  const first = source.trim().charAt(0);
  return first ? first.toUpperCase() : '?';
}

export function AdminTopBar({ title = strings.pageTitleDashboard, onToggleNav }: AdminTopBarProps) {
  const user = useAuthStore((s) => s.user);
  const signOut = useSignOut();

  const displayName = user?.fullName || user?.userName || '';
  const initial = deriveInitial(user?.fullName, user?.userName);

  return (
    <Stack
      tag="header"
      top={0}
      zIndex={100}
      flexDirection="row"
      alignItems="center"
      gap="$4"
      height={64}
      backgroundColor="$bgElevated"
      borderBottomWidth={1}
      borderBottomColor="$border"
      paddingHorizontal="$6"
      style={{ position: 'sticky' }}
      $sm={{ height: 56, paddingHorizontal: '$4' }}
    >
      {onToggleNav ? (
        <Stack
          tag="button"
          aria-label={strings.openNav}
          alignItems="center"
          justifyContent="center"
          width={40}
          height={40}
          backgroundColor="transparent"
          borderWidth={0}
          borderRadius="$sm"
          cursor="pointer"
          display="flex"
          style={{ color: 'var(--lx-fg2)' }}
          $laptop={{ display: 'none' }}
          onPress={onToggleNav}
        >
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" aria-hidden="true">
            <path
              d="M3 6h18M3 12h18M3 18h18"
              stroke="currentColor"
              strokeWidth="1.8"
              strokeLinecap="round"
            />
          </svg>
        </Stack>
      ) : null}

      <Text fontFamily="$heading" fontSize={16} fontWeight="600" color="$fg1">
        {title}
      </Text>

      <Stack flex={1} />

      <Stack flexDirection="row" alignItems="center" gap="$3">
        <Stack
          aria-hidden
          alignItems="center"
          justifyContent="center"
          width={32}
          height={32}
          borderRadius="$pill"
          backgroundColor="$primarySoft"
          borderWidth={1}
          borderColor="rgba(79, 70, 229, 0.35)"
        >
          <Text fontFamily="$heading" fontSize={12} fontWeight="700" color="$primary">
            {initial}
          </Text>
        </Stack>
        {displayName ? (
          <Text
            fontFamily="$body"
            fontSize={14}
            fontWeight="500"
            color="$fg2"
            $sm={{ display: 'none' }}
          >
            {displayName}
          </Text>
        ) : null}
      </Stack>

      <Button
        variant="ghost"
        size="sm"
        accessibilityLabel={strings.signOutButton}
        onPress={() => {
          void signOut();
        }}
      >
        {strings.signOutButton}
      </Button>
    </Stack>
  );
}
