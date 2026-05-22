/**
 * (child) route group — authenticated student surfaces. P1-09 ships only the
 * home placeholder; no child self-onboarding route exists.
 */
import { Stack } from 'expo-router';

export default function ChildLayout() {
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
    </Stack>
  );
}
