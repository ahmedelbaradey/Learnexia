/**
 * RegisterFeaturePanel — the right `$primary` feature panel of the web split
 * register (P1-11 capture, the mirror of `LoginBrandPanel` which sits left).
 * A game-controller icon chip, the "Set up once. Watch them learn forever."
 * headline, and four value bullets each with an icon chip. Presentational only.
 * Rendered on `$primary`; only shown at `$tablet`+ (the scaffold hides it ≤768).
 * RTL-aware; all copy via i18n.
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { useTranslation } from 'react-i18next';

export interface RegisterFeaturePanelProps {
  direction?: Direction;
}

const BULLETS = [
  { key: 'auth.register.feature.bullet1', icon: '✨' },
  { key: 'auth.register.feature.bullet2', icon: '📊' },
  { key: 'auth.register.feature.bullet3', icon: '🎯' },
  { key: 'auth.register.feature.bullet4', icon: '🛡️' },
] as const;

export function RegisterFeaturePanel({ direction = 'ltr' }: RegisterFeaturePanelProps) {
  const { t } = useTranslation();
  const align = direction === 'rtl' ? 'right' : 'left';
  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';

  return (
    <Stack flex={1} justifyContent="center" gap="$8">
      {/* Game-controller icon chip */}
      <Stack
        width={120}
        height={120}
        borderRadius="$pill"
        backgroundColor="$primaryHover"
        alignItems="center"
        justifyContent="center"
        alignSelf={direction === 'rtl' ? 'flex-end' : 'flex-start'}
      >
        <Text fontSize={56} accessibilityElementsHidden>
          🎮
        </Text>
      </Stack>

      {/* Headline */}
      <Text
        color="$fg1"
        fontSize={40}
        fontWeight="800"
        fontFamily="$heading"
        lineHeight={46}
        maxWidth={460}
        textAlign={align}
        writingDirection={direction}
        accessibilityRole="header"
      >
        {t('auth.register.feature.title')}
      </Text>

      {/* Feature bullets */}
      <Stack gap="$5" maxWidth={520}>
        {BULLETS.map((bullet) => (
          <Stack key={bullet.key} flexDirection={rowDir} alignItems="center" gap="$3">
            <Stack
              width={40}
              height={40}
              borderRadius="$button"
              backgroundColor="$primaryHover"
              alignItems="center"
              justifyContent="center"
            >
              <Text fontSize={20} accessibilityElementsHidden>
                {bullet.icon}
              </Text>
            </Stack>
            <Text
              flex={1}
              color="$fg2"
              fontSize={16}
              fontFamily="$body"
              lineHeight={22}
              textAlign={align}
              writingDirection={direction}
            >
              {t(bullet.key)}
            </Text>
          </Stack>
        ))}
      </Stack>
    </Stack>
  );
}

RegisterFeaturePanel.displayName = 'RegisterFeaturePanel';
