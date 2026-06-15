/**
 * (auth) route group — guest-facing screens (role-select, login, register).
 *
 * Three routes exist here: `role-select`, `login`, and `register`. There is
 * intentionally NO student self-register route (parent-driven onboarding) —
 * children are added by a parent via the onboarding flow, never self-registered
 * (AC: no anonymous student self-registration path).
 *
 * Boot sequence (Batch A): signed-out guard → role-select → login (with ?role param).
 */
import { Stack } from 'expo-router';

export default function AuthLayout() {
  return (
    <Stack screenOptions={{ headerShown: false, animation: 'fade' }}>
      <Stack.Screen name="role-select" />
      <Stack.Screen name="login" />
      <Stack.Screen name="register" />
    </Stack>
  );
}
