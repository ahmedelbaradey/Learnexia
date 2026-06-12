/**
 * (child) route group — authenticated student surfaces.
 *
 * B0-nav (carryover batch 2b): converted from `<Stack>` to expo-router `<Tabs>`
 * with the custom floating glass `ChildTabBar` (Design Spec
 * `design-system/ui_kits/student-app/B0-nav-tabbar.md`, the ONE lead-approved
 * new pattern — plan decision L3). Batch 2b is the SOLE owner of this file in
 * Batch 2; batch 3c (B-int) finalizes tab wiring + removes the stub bodies.
 *
 * Tab roots (spec §1 — Home / Missions / League / Badges):
 *   index      — W13 home dashboard (content untouched; now the Home tab)
 *   missions   — stub (B5 / P4-06-FE fills the body in batch 3b)
 *   league     — stub (B6 / P4-07-FE)
 *   badges     — stub (B4 / P4-05-FE)
 *
 * Non-tab push screens — registered with `href: null` so they never appear on
 * the bar; they render inside the same navigator and push on top of the active
 * tab (spec §3):
 *   subjects/[subjectId] — subject detail (bar stays VISIBLE here per spec §3)
 *   lessons/[lessonId]   — lesson player (bar HIDDEN — focus mode)
 *
 * Bar visibility is the allowlist in `ChildTabBar` (`TAB_BAR_VISIBLE_ROUTES`):
 * Wave-B secondary screens (streak / hearts / xp / attempts) just add an
 * `href: null` <Tabs.Screen> here — the bar hides on them automatically.
 *
 * Android hardware back on a non-home tab returns to the Home tab first —
 * the Tabs default backBehavior, kept deliberately (spec §3).
 *
 * All screens headerShown: false — headers are custom per-screen.
 *
 * Auth/role guard: signed-out users and parents are redirected away before any
 * child content is rendered (useGroupGuard).
 */
import { Tabs } from 'expo-router';
import React from 'react';

import { useGroupGuard } from '../../src/hooks/useGroupGuard';
import {
  ChildPushRoute,
  ChildTabBar,
  ChildTabRoute,
} from './_components/ChildTabBar';

export default function ChildLayout() {
  const { isResolving } = useGroupGuard('(child)');

  // Render nothing while the guard is resolving auth/role (prevents content flash).
  if (isResolving) return null;

  return (
    <Tabs
      tabBar={(props) => <ChildTabBar {...props} />}
      screenOptions={{ headerShown: false }}
    >
      {/* Tab roots — logical order per spec §1 (RTL flips the bar visually). */}
      <Tabs.Screen name={ChildTabRoute.Home} />
      <Tabs.Screen name={ChildTabRoute.Missions} />
      <Tabs.Screen name={ChildTabRoute.League} />
      <Tabs.Screen name={ChildTabRoute.Badges} />

      {/* Push screens — hidden from the bar (spec §3). */}
      <Tabs.Screen name={ChildPushRoute.SubjectDetail} options={{ href: null }} />
      <Tabs.Screen name={ChildPushRoute.LessonPlayer} options={{ href: null }} />
    </Tabs>
  );
}
