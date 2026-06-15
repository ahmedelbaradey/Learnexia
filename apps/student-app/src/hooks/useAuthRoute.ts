/**
 * useAuthRoute — the routing guard (Design Spec §3).
 *
 * Drives redirects from `authStore.status` + the `useMe` projection. No content
 * flash: while `status === 'unknown'` (hydrating) or while signed-in but `Me`
 * is still loading, we STAY on the splash (`app/index.tsx`) — the caller renders
 * the splash, this hook just doesn't navigate yet.
 *
 * Targets:
 *   signed-out                        → /(auth)/role-select  (Batch A: was /(auth)/login)
 *   parent, hasChildren = false       → /(onboarding)/add-child
 *   parent, hasChildren = true        → /(parent)
 *   student                           → /(child)
 *
 * Also: when a signed-in user is resolved, the active locale is set from the
 * child's `preferredLanguage` (so the child sees their language). No anonymous
 * student self-registration path exists — registration is parent-only.
 */
import { useMe } from '@learnexia/api-client';
import {
  ROLES,
  useAuthStore,
  type Locale,
  LOCALES,
} from '@learnexia/shared';
import { applyWebDirection } from '@learnexia/shared/i18n';
import { useRouter, useSegments, useRootNavigationState } from 'expo-router';
import { useEffect } from 'react';

import { useLocaleStore } from '../providers/localeStore';

type TargetGroup = '(auth)' | '(onboarding)' | '(parent)' | '(child)' | null;

function rolesInclude(roles: string[], role: string): boolean {
  return roles.some((r) => r.toLowerCase() === role.toLowerCase());
}

function isLocale(value: string | null | undefined): value is Locale {
  return Boolean(value) && (LOCALES as readonly string[]).includes(value as string);
}

export interface AuthRouteState {
  /** True while the guard cannot yet decide (keep showing the splash). */
  isResolving: boolean;
}

export function useAuthRoute(): AuthRouteState {
  const router = useRouter();
  const segments = useSegments();
  // Root navigator readiness: on native (bridgeless/New Arch) the first effect
  // can run before the root `<Slot>` has mounted its navigation state, which
  // throws "Attempted to navigate before mounting the Root Layout". Gate every
  // redirect on `rootNavState?.key` so we only navigate once it's mounted.
  const rootNavState = useRootNavigationState();
  const navReady = Boolean(rootNavState?.key);
  const status = useAuthStore((s) => s.status);
  const setUser = useAuthStore((s) => s.setUser);
  const setLocale = useLocaleStore((s) => s.setLocale);

  const signedIn = status === 'signed-in';
  const me = useMe({ enabled: signedIn });

  // Sync the resolved identity into the auth store (id/roles/name/locale) so
  // screens (e.g. child home greeting, sign-out) can read it without re-fetching.
  useEffect(() => {
    if (!signedIn || !me.data) return;
    setUser({
      id: me.data.id ?? 0,
      fullName: me.data.fullName ?? null,
      roles: (me.data.roles ?? []) as never,
      preferredLocale: me.data.preferredLanguage ?? null,
    });
    // Backend may return a BCP-47 region tag (e.g. 'ar-EG'/'en-US'); normalize to
    // the base subtag so it matches our 2-letter locales before applying.
    const preferredLocale = (me.data.preferredLanguage ?? '').split('-')[0];
    if (isLocale(preferredLocale)) {
      // Eagerly apply to the DOM before the React render chain propagates the
      // new locale through LearnexiaProvider (prevents the timing race where
      // router.replace fires before applyWebDirection runs via useEffect in
      // LearnexiaProvider).
      applyWebDirection(preferredLocale);
      setLocale(preferredLocale);
    }
  }, [signedIn, me.data, setUser, setLocale]);

  // Decide whether we can navigate yet.
  const meReady = signedIn ? me.isSuccess && Boolean(me.data) : true;
  const isResolving = status === 'unknown' || (signedIn && !meReady);

  useEffect(() => {
    if (isResolving) return;
    // Don't navigate until the root navigator is mounted (native timing guard).
    if (!navReady) return;

    const current = (segments[0] ?? null) as TargetGroup;

    if (status === 'signed-out') {
      // Batch A: signed-out users go to Role Select (the new pre-login default).
      // The `(auth)` group guard remains: if the user is already anywhere in the
      // (auth) group (role-select, login, register, forgot-password, reset-password)
      // we don't navigate again — idempotency preserved.
      if (current !== '(auth)') router.replace('/(auth)/role-select');
      return;
    }

    // signed-in with Me resolved
    const data = me.data;
    if (!data) return;

    if (rolesInclude(data.roles ?? [], ROLES.Student)) {
      if (current !== '(child)') router.replace('/(child)');
      return;
    }

    // parent (and admin/superadmin treated as parent for this app)
    if (!data.hasChildren) {
      if (current !== '(onboarding)') router.replace('/(onboarding)/add-child');
      return;
    }
    if (current !== '(parent)') router.replace('/(parent)/overview');
  }, [isResolving, navReady, status, me.data, segments, router]);

  return { isResolving };
}
