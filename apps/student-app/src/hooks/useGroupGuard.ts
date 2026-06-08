/**
 * useGroupGuard — route-group auth/role guard (reused by (onboarding), (parent),
 * and (child) layout files).
 *
 * `useAuthRoute` drives redirects FROM the splash screen. This hook guards the
 * same routes for direct navigation (e.g. a signed-out user typing "/add-child"
 * into the address bar, or a child navigating directly to "/children").
 *
 * Rules (mirror useAuthRoute exactly — do NOT diverge):
 *   - status unknown (hydrating)  → wait (render nothing until resolved)
 *   - signed-out                  → redirect /(auth)/login
 *   - student (ROLES.Student) in (onboarding) or (parent) → redirect /(child)
 *   - parent in (child)           → redirect /(parent), or /(onboarding)/add-child
 *                                   if the parent has no children yet
 *   - correct group               → no redirect (let the Slot render)
 *
 * Returns `{ isResolving }`. Callers render their Slot only when
 * `!isResolving`.
 */
import { useMe } from '@learnexia/api-client';
import { ROLES, useAuthStore, type Locale, LOCALES } from '@learnexia/shared';
import { applyWebDirection } from '@learnexia/shared/i18n';
import { useRouter } from 'expo-router';
import { useEffect } from 'react';

import { useLocaleStore } from '../providers/localeStore';

type GroupName = '(onboarding)' | '(parent)' | '(child)';

function rolesInclude(roles: string[], role: string): boolean {
  return roles.some((r) => r.toLowerCase() === role.toLowerCase());
}

function isLocale(value: string | null | undefined): value is Locale {
  return Boolean(value) && (LOCALES as readonly string[]).includes(value as string);
}

export interface GroupGuardState {
  /** True while the guard cannot yet decide (render nothing — no content flash). */
  isResolving: boolean;
}

export function useGroupGuard(group: GroupName): GroupGuardState {
  const router = useRouter();
  const status = useAuthStore((s) => s.status);
  const setUser = useAuthStore((s) => s.setUser);
  const setLocale = useLocaleStore((s) => s.setLocale);

  const signedIn = status === 'signed-in';
  const me = useMe({ enabled: signedIn });

  // Mirror useAuthRoute: sync the resolved identity into authStore so screens
  // can read it without re-fetching. Also apply the user's preferred locale
  // eagerly (both Zustand store update AND direct DOM update) before navigation
  // fires, so html[dir]/lang are correct at the target route.
  useEffect(() => {
    if (!signedIn || !me.data) return;
    setUser({
      id: me.data.id ?? 0,
      fullName: me.data.fullName ?? null,
      roles: (me.data.roles ?? []) as never,
      preferredLocale: me.data.preferredLanguage ?? null,
    });
    if (isLocale(me.data.preferredLanguage)) {
      // Eagerly apply to the DOM before the React render chain propagates the
      // new locale through LearnexiaProvider (prevents the timing race where
      // navigation completes before applyWebDirection fires via useEffect).
      applyWebDirection(me.data.preferredLanguage);
      setLocale(me.data.preferredLanguage);
    }
  }, [signedIn, me.data, setUser, setLocale]);

  // Can we decide yet?
  const meReady = signedIn ? me.isSuccess && Boolean(me.data) : true;
  const isResolving = status === 'unknown' || (signedIn && !meReady);

  useEffect(() => {
    if (isResolving) return;

    if (status === 'signed-out') {
      router.replace('/(auth)/login');
      return;
    }

    const data = me.data;
    if (!data) return;

    const isStudent = rolesInclude(data.roles ?? [], ROLES.Student);

    if (group === '(onboarding)' || group === '(parent)') {
      // Students must not access parent/onboarding surfaces.
      if (isStudent) {
        router.replace('/(child)');
        return;
      }
      // Correct group: parent in (parent) or (onboarding) — no redirect.
    }

    if (group === '(child)') {
      // Parents must not sit on child routes.
      if (!isStudent) {
        if (!data.hasChildren) {
          router.replace('/(onboarding)/add-child');
        } else {
          router.replace('/(parent)');
        }
      }
      // Correct group: student in (child) — no redirect.
    }
  }, [isResolving, status, me.data, group, router]);

  return { isResolving };
}
