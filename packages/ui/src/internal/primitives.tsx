/**
 * Local Tamagui primitives built on `@tamagui/core`.
 *
 * We deliberately depend only on `@tamagui/core` (not the full `tamagui`
 * package) to keep the package light and importable in a Node/SSR smoke env.
 * `XStack` / `YStack` are the standard row/column flex stacks; `Text` is the
 * core text primitive. All consume the `@learnexia/design-system` token config
 * (registered via that package's module augmentation).
 */
import { Stack, Text as CoreText, styled } from '@tamagui/core';

export const YStack = styled(Stack, {
  flexDirection: 'column',
});

export const XStack = styled(Stack, {
  flexDirection: 'row',
});

export const Text = styled(CoreText, {
  fontFamily: '$body',
  color: '$color',
});

export { Stack, styled };
export type { GetProps } from '@tamagui/core';
