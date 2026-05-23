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
      {/* Logo + wordmark */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$3">
        <Stack
          width={44}
          height={44}
          borderRadius="$card"
          backgroundColor="$primaryHover"
          alignItems="center"
          justifyContent="center"
        >
          <Text fontSize={22} accessibilityElementsHidden>
            ✦
          </Text>
        </Stack>
        <Text color="$fg1" fontSize={22} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
          {appName}
        </Text>
      </Stack>

      {/* Glowing star + tagline */}
      <Stack gap="$5" alignItems={direction === 'rtl' ? 'flex-end' : 'flex-start'}>
        <Stack
          width={140}
          height={140}
          borderRadius="$pill"
          backgroundColor="$primaryHover"
          alignItems="center"
          justifyContent="center"
        >
          <Text fontSize={72} accessibilityElementsHidden>
            ⭐
          </Text>
        </Stack>
        <Text
          color="$fg1"
          fontSize={44}
          fontWeight="800"
          fontFamily="$heading"
          lineHeight={48}
          textAlign={align}
          writingDirection={direction}
          accessibilityRole="header"
        >
          {t('auth.login.brand.title')}
        </Text>
        <Text
          color="$fg2"
          fontSize={16}
          fontFamily="$body"
          lineHeight={24}
          maxWidth={400}
          textAlign={align}
          writingDirection={direction}
        >
          {t('auth.login.brand.body')}
        </Text>
      </Stack>

      {/* Social proof */}
      <Stack flexDirection={rowDir} alignItems="center" gap="$2">
        <Text fontSize={18} accessibilityElementsHidden>
          🔥
        </Text>
        <Text color="$fg2" fontSize={14} fontFamily="$body" writingDirection={direction}>
          {t('auth.login.brand.socialProof')}
        </Text>
      </Stack>
    </>
  );
}
