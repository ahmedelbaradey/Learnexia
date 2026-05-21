/**
 * RN-Web smoke subset — Button, Card, XPBar.
 *
 * These three have no Skia in their core render path, so this tree is safe to
 * import in a Node/DOM environment for a lightweight render check. If a host's
 * bundler still resolves `@shopify/react-native-skia`, map it to the mock at
 * `__mocks__/@shopify/react-native-skia.ts` (see README.md).
 */
import { LearnexiaProvider } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React from 'react';

import { Button, Card, XPBar } from '../src';
import { Text } from '../src/internal/primitives';

export function Smoke() {
  return (
    <LearnexiaProvider locale="en" theme="dark">
      <Stack backgroundColor="$background" padding="$4" gap="$4">
        <Button accessibilityLabel="primary smoke">Primary</Button>
        <Card>
          <Text color="$fg1">Smoke card</Text>
        </Card>
        <XPBar currentXp={820} totalXp={1000} level={7} accessibilityLabel="820 of 1000 XP" />
      </Stack>
    </LearnexiaProvider>
  );
}

Smoke.displayName = 'Smoke';

export default Smoke;
