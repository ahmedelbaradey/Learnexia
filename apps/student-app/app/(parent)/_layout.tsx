/**
 * (parent) route group — authenticated parent surfaces: dashboard placeholder,
 * My-Children, Overview, Link-Child.
 *
 * Auth/role guard: signed-out users and students (ROLES.Student) are redirected
 * away before any parent content is rendered (useGroupGuard).
 */
import { Stack } from 'expo-router';

import { useGroupGuard } from '../../src/hooks/useGroupGuard';

export default function ParentLayout() {
  const { isResolving } = useGroupGuard('(parent)');

  // Render nothing while the guard is resolving auth/role (prevents content flash).
  if (isResolving) return null;

  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="children" />
      <Stack.Screen name="overview" />
      <Stack.Screen name="link-child" />
    </Stack>
  );
}
