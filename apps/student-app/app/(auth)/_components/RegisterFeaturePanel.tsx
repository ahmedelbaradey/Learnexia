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
      {/* Game-controller icon — bare emoji with a glow drop-shadow (no chip). */}
      <Stack
        alignSelf="flex-start"
        alignItems="center"
        justifyContent="center"
      >
        <Text
          fontSize={80}
          accessibilityElementsHidden
          style={{ filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.5))' } as object}
        >
          🎮
        </Text>
      </Stack>

      {/* Headline */}
      <Text
        color="$fg1"
        fontSize={40}
        fontWeight="900"
        fontFamily="$heading"
        lineHeight={46}
        letterSpacing={-0.8}
        maxWidth={460}
        textAlign={align}
        writingDirection={direction}
        accessibilityRole="header"
      >
        {t('auth.register.feature.title')}
      </Text>

      {/* Feature bullets */}
      <Stack gap="$4" maxWidth={460}>
        {BULLETS.map((bullet) => (
          <Stack key={bullet.key} flexDirection={rowDir} alignItems="center" gap="$3">
            <Stack
              width={40}
              height={40}
              borderRadius="$nav"
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
              color="$fg1"
              opacity={0.92}
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
