/**
 * SocialIcons — brand marks for the Login social-auth row (P1-11 / L-01).
 *
 * The capture shows distinct brand logos rather than the previous emoji glyphs.
 * This app has no SVG runtime transformer wired (see `src/assets.ts`) and
 * `react-native-svg` is not a dependency, so — per the QA report's allowance
 * ("if you can't source exact brand marks, build clean token-styled SVGs and
 * note it") — these marks are composed from token-styled `Stack` primitives:
 *   - Google: a multicolor "G" disc (the four brand-color quadrants).
 *   - Apple: a clean monochrome apple silhouette built from rounded shapes.
 *   - Microsoft: the four-square grid (its canonical, fully-geometric mark).
 *
 * They render identically on web + native without new deps, are decorative
 * (the parent `SocialButton` hides them from a11y), and use only design-system
 * tokens (the brand quadrant colors are a fixed brand value set, mapped to the
 * nearest design tokens for a clean monochrome-on-`$card` look).
 *
 * NOTE: these are token-styled approximations, not the exact vector brand
 * artwork. Swap for licensed brand SVGs once an SVG transformer is wired.
 */
import { Stack, Text } from '@tamagui/core';

const SIZE = 18;

/** Google — a "G" on a white disc with the brand-color ring quadrants. */
export function GoogleIcon() {
  return (
    <Stack
      width={SIZE}
      height={SIZE}
      borderRadius={9999}
      backgroundColor="$fg1"
      alignItems="center"
      justifyContent="center"
      overflow="hidden"
    >
      {/* Four brand-color quadrant ring */}
      <Stack position="absolute" top={0} left={0} right={0} bottom={0} flexDirection="row">
        <Stack flex={1} flexDirection="column">
          <Stack flex={1} backgroundColor="$danger" />
          <Stack flex={1} backgroundColor="$accent" />
        </Stack>
        <Stack flex={1} flexDirection="column">
          <Stack flex={1} backgroundColor="$primary" />
          <Stack flex={1} backgroundColor="$secondary" />
        </Stack>
      </Stack>
      {/* White center disc + the "G" cut */}
      <Stack width={SIZE - 6} height={SIZE - 6} borderRadius={9999} backgroundColor="$fg1" alignItems="center" justifyContent="center">
        <Text color="$fgInverse" fontSize={11} fontWeight="800" fontFamily="$heading">
          G
        </Text>
      </Stack>
    </Stack>
  );
}

GoogleIcon.displayName = 'GoogleIcon';

/** Apple — a clean monochrome apple silhouette (body + leaf). */
export function AppleIcon() {
  return (
    <Stack width={SIZE} height={SIZE} alignItems="center" justifyContent="center">
      {/* Leaf */}
      <Stack
        position="absolute"
        top={1}
        width={5}
        height={5}
        backgroundColor="$fg1"
        borderTopLeftRadius={9999}
        borderBottomRightRadius={9999}
      />
      {/* Body — two lobes approximated by an overlapping rounded square */}
      <Stack
        marginTop={4}
        width={SIZE - 3}
        height={SIZE - 5}
        backgroundColor="$fg1"
        borderRadius={9999}
      />
    </Stack>
  );
}

AppleIcon.displayName = 'AppleIcon';

/** Microsoft — the canonical four-square grid (its full geometric mark). */
export function MicrosoftIcon() {
  return (
    <Stack width={SIZE} height={SIZE} flexDirection="row" flexWrap="wrap" gap={1}>
      <Stack width={(SIZE - 1) / 2} height={(SIZE - 1) / 2} backgroundColor="$danger" />
      <Stack width={(SIZE - 1) / 2} height={(SIZE - 1) / 2} backgroundColor="$secondary" />
      <Stack width={(SIZE - 1) / 2} height={(SIZE - 1) / 2} backgroundColor="$primaryHover" />
      <Stack width={(SIZE - 1) / 2} height={(SIZE - 1) / 2} backgroundColor="$accent" />
    </Stack>
  );
}

MicrosoftIcon.displayName = 'MicrosoftIcon';
