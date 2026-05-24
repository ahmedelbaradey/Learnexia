/**
 * SocialIcons — brand marks for the Login social-auth row (P1-11 / L-01, align-login
 * m-26..m-29). The dark-theme social buttons use a monochrome-on-dark icon style
 * (matching the capture + `PagesPublic.jsx`): Google is a white disc with a dark
 * "G"; Apple is the apple emoji; Microsoft is the canonical grid symbol "⊞".
 *
 * These render identically on web + native without new SVG deps, are decorative
 * (the parent `SocialButton` hides them from a11y), and use only design tokens.
 */
import { Stack, Text } from '@tamagui/core';

const SIZE = 20;

/** Google — a white disc with a dark "G" (monochrome-on-dark social style). */
export function GoogleIcon() {
  return (
    <Stack
      width={SIZE}
      height={SIZE}
      borderRadius={9999}
      backgroundColor="$fg1"
      alignItems="center"
      justifyContent="center"
    >
      <Text color="$fgInverse" fontSize={13} fontWeight="900" fontFamily="$heading" accessibilityElementsHidden>
        G
      </Text>
    </Stack>
  );
}

GoogleIcon.displayName = 'GoogleIcon';

/** Apple — the apple emoji as a semantic icon. */
export function AppleIcon() {
  return (
    <Stack width={SIZE} height={SIZE} alignItems="center" justifyContent="center">
      <Text fontSize={SIZE} accessibilityElementsHidden>
        🍎
      </Text>
    </Stack>
  );
}

AppleIcon.displayName = 'AppleIcon';

/** Microsoft — the canonical four-square grid symbol, monochrome on dark. */
export function MicrosoftIcon() {
  return (
    <Stack width={SIZE} height={SIZE} alignItems="center" justifyContent="center">
      <Text color="$fg1" fontSize={SIZE} accessibilityElementsHidden>
        ⊞
      </Text>
    </Stack>
  );
}

MicrosoftIcon.displayName = 'MicrosoftIcon';
