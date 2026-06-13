/**
 * (child) route group — authenticated student surfaces.
 *
 * B0-nav (carryover batch 2b): converted from `<Stack>` to expo-router `<Tabs>`
 * with the custom floating glass `ChildTabBar` (Design Spec
 * `design-system/ui_kits/student-app/B0-nav-tabbar.md`, the ONE lead-approved
 * new pattern — plan decision L3). Batch 2b owned this file in Batch 2;
 * batch 3c (B-int) finalized the route wiring (this version).
 *
 * Tab roots (spec §1 — Home / Missions / League / Badges):
 *   index      — W13 home dashboard + 3c gamification entry points (Home tab)
 *   missions   — B5 / P4-06-FE missions screen (batch 3b)
 *   league     — B6 / P4-07-FE league standings (batch 3b)
 *   badges     — B4 / P4-05-FE badge collection (batch 3b)
 *
 * Non-tab push screens — registered with `href: null` so they never appear on
 * the bar; they render inside the same navigator and push on top of the active
 * tab (spec §3):
 *   subjects/[subjectId] — subject detail (bar stays VISIBLE here per spec §3)
 *   lessons/[lessonId]   — lesson player (bar HIDDEN — focus mode)
 *   attempts             — A5 "My activity" history (bar HIDDEN, batch 2d)
 *   xp / streak / hearts / events — Wave-B gamification secondary screens
 *                          (bar HIDDEN, batches 3a/3b; registered by 3c)
 *
 * Bar visibility is the allowlist in `ChildTabBar` (`TAB_BAR_VISIBLE_ROUTES`):
 * any `href: null` <Tabs.Screen> not in the allowlist hides the bar
 * automatically — no per-screen wiring.
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

import { useGroupGuard } from '@/hooks/useGroupGuard';
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
      {/* A5 — "My activity" attempt history (carryover 2d). Route name is a
          technical identifier; not in the ChildTabBar allowlist, so the bar
          auto-hides here. Home-dashboard entry link is wired by 3c (B-int). */}
      <Tabs.Screen name="attempts" options={{ href: null }} />
      {/* Wave-B gamification secondary screens (carryover 3c / B-int).
          Route names are technical identifiers; none are in the ChildTabBar
          allowlist, so the bar auto-hides on all of them (spec §3). Entry
          points live on the Home dashboard (index.tsx). */}
      <Tabs.Screen name="xp" options={{ href: null }} />
      <Tabs.Screen name="streak" options={{ href: null }} />
      <Tabs.Screen name="hearts" options={{ href: null }} />
      <Tabs.Screen name="events" options={{ href: null }} />
    </Tabs>
  );
}
