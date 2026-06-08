/**
 * (child) route group — authenticated student surfaces.
 *
 * Screens registered:
 *   index                          — Subjects list (replaces P1-09 placeholder)
 *   subjects/[subjectId]           — Subject detail (tab layout: Lessons | Tree)
 *   lessons/[lessonId]             — Lesson player stub (P2-05-FE owns the body)
 *
 * All screens headerShown: false — headers are custom per-screen.
 *
 * Auth/role guard: signed-out users and parents are redirected away before any
 * child content is rendered (useGroupGuard).
 */
import { Stack } from 'expo-router';

import { useGroupGuard } from '../../src/hooks/useGroupGuard';

export default function ChildLayout() {
  const { isResolving } = useGroupGuard('(child)');

  // Render nothing while the guard is resolving auth/role (prevents content flash).
  if (isResolving) return null;

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="subjects/[subjectId]" />
      <Stack.Screen name="lessons/[lessonId]" />
    </Stack>
  );
}
