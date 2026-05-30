/**
 * (child) route group — authenticated student surfaces.
 *
 * Screens registered:
 *   index                          — Subjects list (replaces P1-09 placeholder)
 *   subjects/[subjectId]           — Subject detail (tab layout: Lessons | Tree)
 *   lessons/[lessonId]             — Lesson player stub (P2-05-FE owns the body)
 *
 * All screens headerShown: false — headers are custom per-screen.
 */
import { Stack } from 'expo-router';

export default function ChildLayout() {
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="subjects/[subjectId]" />
      <Stack.Screen name="lessons/[lessonId]" />
    </Stack>
  );
}
