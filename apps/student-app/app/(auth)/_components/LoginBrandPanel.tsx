/**
 * LoginBrandPanel — the left brand panel of the web split login (P1-11 / B-01).
 * Logo + wordmark top, glowing star center, "Welcome back to the adventure."
 * tagline + body, "240,000+ kids learning today" social proof at the bottom.
 * Rendered on `$primary`; only shown at `$tablet`+ (the scaffold hides it ≤768).
 * RTL-aware; all copy via i18n.
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { useTranslation } from 'react-i18next';

export interface LoginBrandPanelProps {
  direction?: Direction;
  appName: string;
}

export function LoginBrandPanel({ direction = 'ltr', appName }: LoginBrandPanelProps) {
  const { t } = useTranslation();
  const align = direction === 'rtl' ? 'right' : 'left';
  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';

  return (
    <>
      {/* Logo + wordmark — always LTR mark→wordmark order (brand mark not mirrored),
          but the cluster sits at the reading start (right in RTL) like the rest of the panel. */}
      <Stack
        flexDirection="row"
        alignItems="center"
        justifyContent={direction === 'rtl' ? 'flex-end' : 'flex-start'}
        gap="$3"
      >
        <Stack
          width={44}
          height={44}
          borderRadius={20}
          backgroundColor="$primaryHover"
          alignItems="center"
          justifyContent="center"
        >
          <Text fontSize={22} accessibilityElementsHidden>
            ✦
          </Text>
        </Stack>
        <Text color="$fg1" fontSize={22} fontWeight="800" fontFamily="$heading" writingDirection="ltr">
          {appName}
        </Text>
      </Stack>

      {/* Glowing star + tagline */}
      <Stack gap="$5" alignItems={direction === 'rtl' ? 'flex-end' : 'flex-start'}>
        <Text
          fontSize={80}
          accessibilityElementsHidden
          // Star glow — drop-shadow per web-auth-split.html spec.
          style={{
            filter: 'drop-shadow(0 0 16px rgba(250,204,21,0.5))',
          } as object}
        >
          {'🌟'}
        </Text>
        <Text
          color="$fg1"
          fontSize={28}
          fontWeight="900"
          fontFamily="$heading"
          lineHeight={32}
          textAlign={align}
          writingDirection={direction}
          accessibilityRole="header"
        >
          {t('auth.login.brand.title')}
        </Text>
        <Text
          color="$fg2Alpha"
          fontSize={13}
          fontFamily="$body"
          lineHeight={20}
          maxWidth={320}
          textAlign={align}
          writingDirection={direction}
        >
          {t('auth.login.brand.body')}
        </Text>
      </Stack>

      {/* Social proof — DOM order [text, 🔥] so that row-reverse in RTL puts text at right (reading start) and fire at left (visual accent end). */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$2">
        <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
          {t('auth.login.brand.socialProof')}
        </Text>
        <Text fontSize={18} accessibilityElementsHidden>
          🔥
        </Text>
      </Stack>
    </>
  );
}
