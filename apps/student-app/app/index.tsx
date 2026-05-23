/**
 * Splash / boot screen (Design Spec Screen 1) + the routing guard mount point.
 *
 * Always-visible brand splash while `authStore.status === 'unknown'` (hydrating)
 * and while a signed-in user's `Me` is still loading — no content flash. Once
 * the guard (`useAuthRoute`) resolves the target, it `router.replace`s away.
 *
 * Presentation matches `design-system/screenshots/mobile/01-splash.png`:
 * purple radial-gradient background with faint scattered "star" dots, the
 * "Learnexia" wordmark + subtitle, the `DotPulse` loader, a decorative gradient
 * progress bar, a "Loading… ⚡" label, and a "POWERED BY AI" / tagline footer.
 *
 * If a session-expired flash message is pending, it is shown at the bottom.
 * Splash is brand chrome → LTR-always layout (RTL not required), but all copy is
 * i18n and the flash card respects `direction`.
 */
import { gradientStops, radialGradients } from '@learnexia/design-system';
import { useFlashMessageStore } from '@learnexia/shared';
import { Card, GradientBox } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useTranslation } from 'react-i18next';

import { DotPulse } from '../src/components/DotPulse';
import { RestartPrompt } from '../src/components/RestartPrompt';
import { useAuthRoute } from '../src/hooks/useAuthRoute';
import { useLocale } from '../src/hooks/useLocale';

/**
 * Faint decorative "stars" scattered across the background (percent-based so
 * they scale with any viewport). Purely visual — hidden from screen readers.
 */
const STARS = [
  { top: '8%', left: '22%', size: 3, opacity: 0.45 },
  { top: '12%', left: '64%', size: 2, opacity: 0.35 },
  { top: '17%', left: '11%', size: 4, opacity: 0.5 },
  { top: '23%', left: '42%', size: 2, opacity: 0.3 },
  { top: '31%', left: '30%', size: 3, opacity: 0.4 },
  { top: '44%', left: '92%', size: 3, opacity: 0.4 },
  { top: '54%', left: '83%', size: 2, opacity: 0.3 },
  { top: '58%', left: '26%', size: 3, opacity: 0.45 },
  { top: '67%', left: '9%', size: 4, opacity: 0.5 },
  { top: '74%', left: '46%', size: 2, opacity: 0.3 },
  { top: '82%', left: '78%', size: 3, opacity: 0.4 },
] as const;

export default function SplashScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  useAuthRoute();
  const flashKey = useFlashMessageStore((s) => s.messageKey);
  const hasFlash = Boolean(flashKey);

  return (
    <GradientBox
      flex={1}
      stops={gradientStops.splashBg.colors}
      angle={gradientStops.splashBg.angle}
      // Web reads the true centered radial glow; native uses the linear stops.
      style={{ backgroundImage: radialGradients.splashBg } as object}
    >
      {/* Decorative star field */}
      {STARS.map((star) => (
        <Stack
          key={`${star.top}-${star.left}`}
          position="absolute"
          top={star.top}
          left={star.left}
          width={star.size}
          height={star.size}
          borderRadius={9999}
          backgroundColor="$fg1"
          opacity={star.opacity}
          pointerEvents="none"
          accessibilityElementsHidden
          aria-hidden
        />
      ))}

      <Stack flex={1} alignItems="center" justifyContent="center" gap="$6" paddingHorizontal="$6">
        <Stack alignItems="center" gap="$2">
          <Text
            fontFamily="$heading"
            fontSize={40}
            lineHeight={44}
            fontWeight="800"
            color="$fg1"
            textAlign="center"
          >
            {t('common.appName')}
          </Text>
          <Text
            fontFamily="$body"
            fontSize={15}
            lineHeight={22}
            color="$fg2"
            textAlign="center"
            maxWidth={240}
          >
            {t('common.splash.subtitle')}
          </Text>
        </Stack>

        <DotPulse />

        {/* Decorative indeterminate progress bar (track + partial gradient fill) */}
        <Stack
          width={220}
          height={6}
          borderRadius={9999}
          backgroundColor="$bg"
          overflow="hidden"
          accessibilityElementsHidden
          aria-hidden
        >
          <GradientBox
            width="70%"
            height="100%"
            borderRadius={9999}
            stops={gradientStops.gradXp.colors}
            angle={gradientStops.gradXp.angle}
          />
        </Stack>

        <Text fontFamily="$body" fontSize={14} color="$fg3" textAlign="center">
          {t('common.splash.loading')}
        </Text>
      </Stack>

      {/* Footer eyebrow + tagline */}
      <Stack position="absolute" bottom={72} left={0} right={0} alignItems="center" gap="$2">
        <Text
          fontFamily="$heading"
          fontSize={12}
          fontWeight="700"
          color="$fg3"
          letterSpacing={2}
          textTransform="uppercase"
          textAlign="center"
        >
          {t('common.splash.poweredBy')}
        </Text>
        <Text fontFamily="$body" fontSize={13} color="$fg2" textAlign="center">
          {t('common.splash.tagline')}
        </Text>
      </Stack>

      {hasFlash && flashKey ? (
        <Stack position="absolute" bottom={24} left={0} right={0} alignItems="center">
          <Stack width="80%" alignItems="center">
            <Card variant="soft">
              <Text color="$fg2" fontSize={14} textAlign="center" writingDirection={direction}>
                {t(flashKey)}
              </Text>
            </Card>
          </Stack>
        </Stack>
      ) : null}

      <RestartPrompt />
    </GradientBox>
  );
}
