/**
 * Parent Register screen (P1-11 capture) — the ONLY registration screen (no
 * student self-register). Web (≥768): split-panel — form column LEFT on `$bg`,
 * `$primary` feature panel RIGHT (the mirror of Login, whose brand panel is
 * left). Phone (≤768): the feature panel collapses to a single-column form.
 *
 * TWO-STEP WIZARD (inline, no route jump):
 *   Step 1: parent account form (full name, country, email, password, Terms,
 *            Continue → Add Children). On success sets local `step = 2`.
 *   Step 2: add-child step, rendered inline within the SAME scaffold/frame.
 *           Shows the My-Children style list + AddChildModal + Continue button
 *           (routes to /(onboarding)/complete once ≥1 child is added).
 *
 * The /(onboarding)/add-child route is kept intact for backward compatibility
 * (LinkedChildrenPanel fallback uses it). This screen no longer navigates there.
 *
 * RTL-aware; all copy via i18n.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import { useMyChildren } from '@learnexia/api-client';
import { gradientStops } from '@learnexia/design-system';
import { type Direction } from '@learnexia/shared';
import { Button, ChildCard, GradientBox } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { AddChildModal } from '@/components/AddChildModal';
import { AddChildCard } from '../(parent)/_components/AddChildCard';
import { FormScaffold } from '@/components/FormScaffold';
import { ScreenHeader } from '@/components/ScreenHeader';
import { useLocale } from '@/hooks/useLocale';
import { RegisterFeaturePanel } from './_components/RegisterFeaturePanel';
import { RegisterForm } from './_components/RegisterForm';

export default function RegisterScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useLocalizedRouter();
  const align = direction === 'rtl' ? 'right' : 'left';
  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';

  /**
   * Wizard step state (UI-only — NOT server data, so local state is correct).
   * 1 = parent account form, 2 = add-child step.
   */
  const [step, setStep] = useState<1 | 2>(1);

  return (
    <FormScaffold
      variant="split"
      brandSide="end"
      brandPanel={<RegisterFeaturePanel direction={direction} />}
      header={
        <ScreenHeader
          onBack={step === 2
            ? () => setStep(1)
            : () => router.replace('/(auth)/login')}
          backLabel={step === 2
            ? t('onboarding.back')
            : t('auth.register.backToSignIn')}
        />
      }
    >
      <Stack gap="$6">
        {/* Logo + wordmark — always LTR mark→wordmark order (brand mark not mirrored).
            Brand name stays Latin in all locales (SKILL.md rule — no transliteration). */}
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
          {/* Latin brand name (common.appName = 'Learnexia' in both locales) + forced LTR (align-register m-16). */}
          <Text color="$fg1" fontSize={22} fontWeight="900" fontFamily="$heading" writingDirection="ltr" direction="ltr">
            {t('common.appName')}
          </Text>
        </Stack>

        {/* Step eyebrow + progress bar on ONE row (eyebrow leading side, bar flex:1).
            Bar remains direction:ltr universally (SKILL.md §4 rule 6 — progress L→R). */}
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
            {step === 1
            ? t('auth.register.step')
            : t('onboarding.stepLabel', { current: 2, total: 2 })}
          </Text>
          <Stack
            flex={1}
            height={4}
            borderRadius="$pill"
            backgroundColor="$card"
            overflow="hidden"
            flexDirection="row"
            accessibilityRole="progressbar"
            accessible
            accessibilityValue={{ min: 1, max: 2, now: step }}
            accessibilityLabel={
              step === 1
                ? t('auth.register.progressA11y')
                : t('onboarding.stepLabel', { current: 2, total: 2 })
            }
            aria-label={
              step === 1
                ? t('auth.register.progressA11y')
                : t('onboarding.stepLabel', { current: 2, total: 2 })
            }
          >
            {/* Fill width: 50% step 1, 100% step 2. */}
            <GradientBox
              width={step === 1 ? '50%' : '100%'}
              height={4}
              borderRadius="$pill"
              stops={gradientStops.gradLevelup.colors}
              angle={90}
            />
          </Stack>
        </Stack>

        {/* Heading + subtitle — change copy for step 2 */}
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
            {step === 1 ? t('auth.register.title') : t('onboarding.addChild.title')}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" textAlign={align} writingDirection={direction}>
            {step === 1 ? t('auth.register.subtitle') : t('onboarding.addChild.subtitle')}
          </Text>
        </Stack>

        {step === 1 ? (
          <>
            {/* Parent / Guardian only info banner (step 1 only). */}
            <ParentOnlyBanner direction={direction} />
            <RegisterForm onSuccess={() => setStep(2)} />
          </>
        ) : (
          <AddChildStep direction={direction} onContinue={() => router.replace('/(onboarding)/complete')} />
        )}
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
      // M-13: purple 30% border ($purpleBorder; not $purpleSoft which is the bg token).
      borderColor="$purpleBorder"
      // M-14: $purpleSoft background (rgba(168,85,247,0.18)).
      backgroundColor="$purpleSoft"
    >
      <Text fontSize={22} accessibilityElementsHidden>
        👨‍👩‍👧
      </Text>
      <Stack flex={1} gap="$1">
        {/* M-15: $purple title color. */}
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

/**
 * AddChildStep — step 2 of the register wizard, rendered inline within the
 * same FormScaffold frame (same width, same visual chrome). Mirrors the
 * /(onboarding)/add-child screen content: My-Children list driven by
 * useMyChildren + dashed AddChildCard tile that opens AddChildModal + Continue
 * button gated on child count ≥ 1. Reuses existing components — no new pattern.
 */
function AddChildStep({
  direction,
  onContinue,
}: {
  direction: Direction;
  onContinue: () => void;
}) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const query = useMyChildren();
  const [addOpen, setAddOpen] = useState(false);

  const children = query.data ?? [];
  const childCount = children.length;

  function formatNumber(n: number): string {
    return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(n);
  }

  function childMeta(grade: number | undefined, language: string | undefined): string {
    const gradePart =
      grade != null
        ? t(`onboarding.grade.${grade}`, { defaultValue: `${formatNumber(grade)}` })
        : '';
    const langPart = language
      ? t(`onboarding.language.${language}`, { defaultValue: language })
      : '';
    if (gradePart && langPart) return `${gradePart} · ${langPart}`;
    return gradePart || langPart;
  }

  return (
    <>
      {/* Added-children list */}
      {childCount > 0 ? (
        <Stack gap="$3" testID="onboarding-children-list">
          <Text
            color="$fg3"
            fontSize={12}
            fontWeight="600"
            fontFamily="$heading"
            textTransform="uppercase"
            letterSpacing={0.6}
            writingDirection={direction}
          >
            {t('onboarding.addChild.listLabel', { count: childCount })}
          </Text>
          {children.map((child) => (
            <ChildCard
              key={child.id}
              testID={`onboarding-child-card-${child.id}`}
              variant="status"
              child={{
                fullName: child.fullName ?? '',
                meta: childMeta(child.grade ?? undefined, child.language ?? undefined),
              }}
              status="success"
              direction={direction}
              accessibilityLabel={child.fullName ?? ''}
            />
          ))}
        </Stack>
      ) : null}

      {/* Empty-state hint */}
      {childCount === 0 && !query.isLoading ? (
        <Text
          color="$fg3"
          fontSize={13}
          fontFamily="$body"
          textAlign="center"
          writingDirection={direction}
        >
          {t('onboarding.addChild.emptyHint')}
        </Text>
      ) : null}

      {/* Dashed "Add a child" tile */}
      <AddChildCard
        onPress={() => setAddOpen(true)}
        testID="onboarding-add-child-tile"
      />

      {/* Continue — enabled once ≥1 child added */}
      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('onboarding.addChild.continue')}
        disabled={childCount === 0}
        onPress={onContinue}
        testID="onboarding-continue"
      >
        {t('onboarding.addChild.continue')}
      </Button>

      {/* AddChildModal — local state; creates the child immediately on confirm */}
      <AddChildModal visible={addOpen} onClose={() => setAddOpen(false)} />
    </>
  );
}
