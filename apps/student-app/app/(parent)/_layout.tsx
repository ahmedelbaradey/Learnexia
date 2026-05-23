/**
 * (parent) route group — authenticated parent surfaces: dashboard placeholder,
 * My-Children, Overview, Link-Child.
 */
import { Stack } from 'expo-router';

export default function ParentLayout() {
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Screen name="index" />
      <Stack.Screen name="children" />
      <Stack.Screen name="overview" />
      <Stack.Screen name="link-child" />
    </Stack>
  );
}
