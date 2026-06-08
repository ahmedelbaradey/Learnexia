/**
 * Splash / boot screen (Design Spec Screen 1) + the routing guard mount point.
 *
 * Always-visible brand splash while `authStore.status === 'unknown'` (hydrating)
 * and while a signed-in user's `Me` is still loading — no content flash. Once
 * the guard (`useAuthRoute`) resolves the target, it `router.replace`s away.
 *
 * Presentation matches `design-system/screenshots/mobile/01-splash.png` (EN) and
 * `design-system/screenshots/mobile-ar/01-splash.png` (AR), per the
 * `align-splash.md` pixel-alignment pass: warm-purple radial-gradient background
 * with faint scattered "star" dots, the "Learnexia" wordmark (always Latin) +
 * subtitle, the `DotPulse` loader, a violet gradient progress bar, a "Loading… ⚡"
 * label, and a "POWERED BY AI" / tagline footer. AR adds a glowing star mascot
 * hero above the wordmark and renders RTL with Cairo/Tajawal fonts.
 *
 * If a session-expired flash message is pending, it is shown at the bottom.
 * Brand name "Learnexia" is forced LTR in all locales; the progress bar track is
 * forced LTR; everything else flips to RTL in Arabic.
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
 * Trimmed to 9 stars, max size 3 (align-splash m1, m2).
 */
const STARS = [
  { top: '8%', left: '22%', size: 3, opacity: 0.45 },
  { top: '12%', left: '64%', size: 2, opacity: 0.35 },
  { top: '17%', left: '11%', size: 3, opacity: 0.5 },
  { top: '23%', left: '42%', size: 2, opacity: 0.3 },
  { top: '44%', left: '92%', size: 3, opacity: 0.4 },
  { top: '54%', left: '83%', size: 2, opacity: 0.3 },
  { top: '58%', left: '26%', size: 3, opacity: 0.45 },
  { top: '67%', left: '9%', size: 3, opacity: 0.5 },
  { top: '82%', left: '78%', size: 3, opacity: 0.4 },
] as const;

/** Semi-transparent black progress track (align-splash B4). */
const PROGRESS_TRACK = 'rgba(0, 0, 0, 0.35)';
/** 70%-alpha white for subtitle / loading label / tagline (align-splash M5/M6/M9). */
const FG2_ALPHA = 'rgba(255, 255, 255, 0.70)';
/** 45%-alpha white for the footer eyebrow (align-splash M7). */
const FG_FOOTER = 'rgba(255, 255, 255, 0.45)';

export default function SplashScreen() {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  useAuthRoute();
  const flashKey = useFlashMessageStore((s) => s.messageKey);
  const hasFlash = Boolean(flashKey);

  // Locale-conditional Arabic fonts (Cairo headings / Tajawal body) — align-splash M15–M18.
  const headingFont = isRtl ? '$cairo' : '$heading';
  const bodyFont = isRtl ? '$tajawal' : '$body';

  const tagline = t('common.splash.tagline');

  return (
    <GradientBox
      testID="splash-screen"
      flex={1}
      stops={gradientStops.splashBg.colors}
      angle={gradientStops.splashBg.angle}
      // Web reads the true centered radial glow; native uses the linear stops.
      // Root flips to RTL in Arabic so flex start/end invert (align-splash M19).
      style={{ backgroundImage: radialGradients.splashBg, direction } as object}
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

      <Stack flex={1} alignItems="center" justifyContent="center" gap="$8" paddingHorizontal="$6">
        <Stack alignItems="center" gap="$2">
          {/* AR mascot hero — glowing star disc above the wordmark (align-splash B1).
              🌟 is a PLACEHOLDER until real mascot art ships. AR only; EN has none. */}
          {isRtl ? (
            <Stack
              width={132}
              height={132}
              borderRadius={9999}
              alignItems="center"
              justifyContent="center"
              pointerEvents="none"
              accessibilityElementsHidden
              aria-hidden
              style={
                {
                  backgroundImage:
                    'radial-gradient(circle, rgba(250,204,21,0.35), rgba(168,85,247,0) 65%)',
                } as object
              }
            >
              <Text
                fontSize={88}
                lineHeight={132}
                textAlign="center"
                style={{ filter: 'drop-shadow(0 0 20px rgba(250,204,21,0.6))' } as object}
              >
                {'🌟'}
              </Text>
            </Stack>
          ) : null}

          <Text
            fontFamily={headingFont}
            fontSize={36}
            lineHeight={41}
            fontWeight="900"
            letterSpacing={-0.72}
            color="$fg1"
            textAlign="center"
            // Brand name is always Latin + LTR, even in Arabic (align-splash M14/M19).
            writingDirection="ltr"
          >
            {t('common.appName')}
          </Text>
          <Text
            fontFamily={bodyFont}
            fontSize={14}
            lineHeight={22}
            fontWeight="500"
            color={FG2_ALPHA}
            textAlign="center"
            maxWidth={240}
          >
            {t('common.splash.subtitle')}
          </Text>
        </Stack>

        {/* Loader group — DotPulse + progress bar + loading label (align-splash m3/m4). */}
        <Stack alignItems="center" gap="$3">
          <DotPulse />

          {/* Decorative indeterminate progress bar (track + partial gradient fill).
              Track stays LTR so the fill always grows left→right (align-splash B4 + RTL rule). */}
          <Stack
            width={220}
            height={6}
            borderRadius={9999}
            overflow="hidden"
            accessibilityElementsHidden
            aria-hidden
            style={{ backgroundColor: PROGRESS_TRACK, direction: 'ltr' } as object}
          >
            <GradientBox
              width="55%"
              height="100%"
              borderRadius={9999}
              stops={gradientStops.splashProgress.colors}
              angle={gradientStops.splashProgress.angle}
            />
          </Stack>

          <Text testID="splash-loading" fontFamily={bodyFont} fontSize={14} color={FG2_ALPHA} textAlign="center">
            {t('common.splash.loading')}
          </Text>
        </Stack>
      </Stack>

      {/* Footer eyebrow + tagline (tagline hidden in AR — empty key, align-splash M10) */}
      <Stack position="absolute" bottom={88} left={0} right={0} alignItems="center" gap="$2">
        <Text
          fontFamily={headingFont}
          fontSize={12}
          fontWeight="700"
          color={FG_FOOTER}
          letterSpacing={2.2}
          textTransform="uppercase"
          textAlign="center"
        >
          {t('common.splash.poweredBy')}
        </Text>
        {tagline ? (
          <Text fontFamily={bodyFont} fontSize={13} color={FG2_ALPHA} textAlign="center">
            {tagline}
          </Text>
        ) : null}
      </Stack>

      {hasFlash && flashKey ? (
        <Stack position="absolute" bottom={24} left={0} right={0} alignItems="center">
          <Stack width="80%" alignItems="center">
            <Card testID="session-expired-flash" variant="soft">
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
