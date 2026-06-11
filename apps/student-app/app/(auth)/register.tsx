/**
 * Parent Register screen (P1-11 capture) — the ONLY registration screen (no
 * student self-register). Web (≥768): split-panel — form column LEFT on `$bg`,
 * `$primary` feature panel RIGHT (the mirror of Login, whose brand panel is
 * left). Phone (≤768): the feature panel collapses to a single-column form.
 *
 * Left column: logo + wordmark · "STEP 1 OF 2" eyebrow + a ~50% progress bar ·
 * "Create your parent account" heading + subtitle · a "Parent / Guardian only"
 * info banner · the RegisterForm (full name, country, email, password, Terms,
 * Continue → Add Children). RTL-aware; all copy via i18n.
 */
import { gradientStops } from '@learnexia/design-system';
import { type Direction } from '@learnexia/shared';
import { GradientBox } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';

import { FormScaffold } from '../../src/components/FormScaffold';
import { ScreenHeader } from '../../src/components/ScreenHeader';
import { useLocale } from '../../src/hooks/useLocale';
import { RegisterFeaturePanel } from './_components/RegisterFeaturePanel';
import { RegisterForm } from './_components/RegisterForm';

export default function RegisterScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const align = direction === 'rtl' ? 'right' : 'left';
  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';

  return (
    <FormScaffold
      variant="split"
      brandSide="end"
      brandPanel={<RegisterFeaturePanel direction={direction} />}
      header={
        <ScreenHeader
          onBack={() => router.replace('/(auth)/login')}
          backLabel={t('auth.register.backToSignIn')}
        />
      }
    >
      <Stack gap="$6">
        {/* Logo + wordmark — always LTR mark→wordmark order (brand mark not mirrored). */}
        <Stack flexDirection="row" alignItems="center" gap="$3">
          <Stack
            width={40}
            height={40}
            borderRadius="$button"
            backgroundColor="$primary"
            alignItems="center"
            justifyContent="center"
          >
            <Text fontSize={20} accessibilityElementsHidden>
              ✦
            </Text>
          </Stack>
          <Text color="$fg1" fontSize={22} fontWeight="900" fontFamily="$heading" writingDirection="ltr">
            {t('common.appName')}
          </Text>
        </Stack>

        {/* Step eyebrow + progress bar on ONE row (eyebrow left, bar flex:1). */}
        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Text
            color="$primaryLight"
            fontSize={12}
            fontWeight="800"
            fontFamily="$heading"
            textTransform={direction === 'rtl' ? 'none' : 'uppercase'}
            letterSpacing={1.44}
            textAlign={align}
            writingDirection={direction}
          >
            {t('auth.register.step')}
          </Text>
          <Stack
            flex={1}
            height={4}
            borderRadius="$pill"
            backgroundColor="$card"
            overflow="hidden"
            // Progress fills L→R universally; keep the bar LTR even in RTL (SKILL.md).
            flexDirection="row"
            accessibilityRole="progressbar"
            accessible
            accessibilityValue={{ min: 1, max: 2, now: 1 }}
            accessibilityLabel={t('auth.register.progressA11y')}
            aria-label={t('auth.register.progressA11y')}
          >
            <GradientBox
              width="50%"
              height={4}
              borderRadius="$pill"
              stops={gradientStops.gradLevelup.colors}
              angle={90}
            />
          </Stack>
        </Stack>

        {/* Heading + subtitle */}
        <Stack gap="$2">
          <Text
            color="$fg1"
            fontSize={32}
            fontWeight="800"
            fontFamily="$heading"
            lineHeight={38}
            textAlign={align}
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('auth.register.title')}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" textAlign={align} writingDirection={direction}>
            {t('auth.register.subtitle')}
          </Text>
        </Stack>

        {/* Parent / Guardian only info banner */}
        <ParentOnlyBanner direction={direction} />

        <RegisterForm />
      </Stack>
    </FormScaffold>
  );
}

/** Purple-soft consent banner reinforcing the parent-driven onboarding rule. */
function ParentOnlyBanner({ direction }: { direction: Direction }) {
  const { t } = useTranslation();
  const align = direction === 'rtl' ? 'right' : 'left';
  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';

  return (
    <Stack
      flexDirection={rowDir}
      alignItems="flex-start"
      gap="$3"
      padding="$4"
      borderRadius="$card"
      borderWidth={1}
      borderColor="$purpleSoft"
      backgroundColor="$purpleSoft"
    >
      <Text fontSize={22} accessibilityElementsHidden>
        👨‍👩‍👧
      </Text>
      <Stack flex={1} gap="$1">
        <Text
          color="$purple"
          fontSize={13}
          fontWeight="700"
          fontFamily="$heading"
          textAlign={align}
          writingDirection={direction}
        >
          {t('auth.register.parentOnlyTitle')}
        </Text>
        <Text
          color="$fg2"
          fontSize={13}
          lineHeight={18}
          fontFamily="$body"
          textAlign={align}
          writingDirection={direction}
        >
          {t('auth.register.parentOnlyBody')}
        </Text>
      </Stack>
    </Stack>
  );
}
