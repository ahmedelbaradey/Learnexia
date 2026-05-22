/**
 * (auth) route group — guest-facing screens (login, register).
 *
 * Only two routes exist here: `login` and `register`. There is intentionally NO
 * student self-register route (parent-driven onboarding) — children are added
 * by a parent via the onboarding flow, never self-registered (AC: no anonymous
 * student self-registration path).
 */
import { Stack } from 'expo-router';

export default function AuthLayout() {
  return (
    <Stack screenOptions={{ headerShown: false, animation: 'fade' }}>
      <Stack.Screen name="login" />
      <Stack.Screen name="register" />
    </Stack>
  );
}
