'use client';

/**
 * AdminShell (FE-3) — the authenticated chrome that wraps all admin routes.
 *
 * Composes `AdminSideNav` + `AdminTopBar` + `{children}` and applies the
 * client-side admin guard (FE-4):
 *   - guard 'loading'     → full-page skeleton (no flash of protected content)
 *   - guard 'redirecting' → render nothing (the hook redirects to /login)
 *   - guard 'authorized'  → render the shell
 *
 * Layout is built with Tamagui (`@tamagui/core` Stack) + design-system tokens —
 * no CSS module. Holds the nav drawer open/close state for the tablet/phone
 * breakpoints (Design Spec §4a, Gap 2).
 */

import { Stack } from '@tamagui/core';
import { useState, type ReactNode } from 'react';

import { ADMIN_LOCALE, getStrings } from '../lib/strings';
import { useAdminGuard } from '../lib/hooks/useAdminGuard';
import { AdminLoadingSkeleton } from './AdminLoadingSkeleton';
import { AdminSideNav } from './AdminSideNav';
import { AdminTopBar } from './AdminTopBar';

const strings = getStrings(ADMIN_LOCALE);

export function AdminShell({ children }: { children: ReactNode }) {
  const guard = useAdminGuard();
  const [navOpen, setNavOpen] = useState(false);

  if (guard === 'loading') {
    return <AdminLoadingSkeleton label={strings.loadingLabel} />;
  }
  if (guard === 'redirecting') {
    // The guard hook is issuing router.replace('/login'); render nothing so no
    // protected content appears during the transition.
    return null;
  }

  return (
    <Stack flexDirection="row" backgroundColor="$bg" style={{ minHeight: '100vh' }}>
      <AdminSideNav isOpen={navOpen} onClose={() => setNavOpen(false)} />
      <Stack flex={1} minWidth={0} flexDirection="column">
        <AdminTopBar onToggleNav={() => setNavOpen((v) => !v)} />
        <Stack
          tag="main"
          flex={1}
          overflow="scroll"
          padding="$8"
          $sm={{ padding: '$4' }}
        >
          {children}
        </Stack>
      </Stack>
    </Stack>
  );
}
