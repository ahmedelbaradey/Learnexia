/**
 * ChangeLearningLanguageModal — fresh-start confirmation overlay (P8-04-FE-3).
 *
 * Shape: mirrors `EditChildSheet` — RN `Modal transparent animationType="slide"`
 * with `$overlay` scrim, bottom sheet card. Opened by the per-child "Learning
 * language" row in `LinkedChildrenPanel`.
 *
 * Anatomy (top → bottom):
 *   1. Danger marker chip + title ("Reset Math & Science Progress?")
 *   2. From → To restatement (RTL-mirrored via i18n key)
 *   3. Consequence lines: loss ($danger) + kept ($fg2) + rare-note ($fg3)
 *   4. Required ack CheckboxField (gates Confirm; deliberate tick maps to
 *      `confirmFreshStart: true`)
 *   5. Stacked actions: Confirm (ad-hoc $danger Stack) + Cancel (ghost text)
 *
 * Guards:
 * - Confirm disabled until ack checked AND not submitting.
 * - No backdrop-dismiss — only explicit Cancel / ✕ / hardware-back cancels.
 * - `accessibilityViewIsModal` traps focus (mirrors RewardPopup).
 *
 * Error handling:
 * - 403 → `parent.settings.linkedChildren.learningLanguage.error.forbidden`
 * - 424 → `parent.settings.linkedChildren.learningLanguage.error.confirmMissing`
 * - generic → `common.error.serverError`
 *
 * RTL: rowDir on all rows; textAlign + writingDirection per direction prop.
 * Motion: animationType="slide" (native); Confirm pressStyle scale 0.95.
 * No API calls in this component — onConfirm fires the mutation from LinkedChildrenPanel.
 */
import { CheckboxField } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useCallback } from 'react';
import { Modal } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../../src/components/ServerErrorBanner';
import { useServerError } from '../../../../src/hooks/useServerError';

export interface ChangeLearningLanguageModalProps {
  visible: boolean;
  childId: number;
  childName: string;
  currentLanguage: string | null;
  selectedLanguage: string;
  ackChecked: boolean;
  onAckChange: (checked: boolean) => void;
  isPending: boolean;
  isError: boolean;
  error: Error | null;
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
  onCancel: () => void;
  onConfirm: () => void;
}

export function ChangeLearningLanguageModal({
  visible,
  childName,
  currentLanguage,
  selectedLanguage,
  ackChecked,
  onAckChange,
  isPending,
  isError,
  error,
  direction,
  rowDir,
  onCancel,
  onConfirm,
}: ChangeLearningLanguageModalProps) {
  const { t } = useTranslation();
  const insets = useSafeAreaInsets();
  const resolveError = useServerError();

  const serverMessage = isError
    ? resolveError(error, {
        byStatus: {
          403: 'parent.settings.linkedChildren.learningLanguage.error.forbidden',
          424: 'parent.settings.linkedChildren.learningLanguage.error.confirmMissing',
        },
        hints: [
          {
            contains: ['forbidden', 'not your child', 'family'],
            key: 'parent.settings.linkedChildren.learningLanguage.error.forbidden',
          },
          {
            contains: ['confirmfreshstart', 'confirm', 'fresh'],
            key: 'parent.settings.linkedChildren.learningLanguage.error.confirmMissing',
          },
        ],
      })
    : null;

  const confirmDisabled = !ackChecked || isPending;

  // Localized language names for from→to restatement.
  const fromLabel = currentLanguage
    ? t(`onboarding.language.${currentLanguage}` as const)
    : null;
  const toLabel = t(`onboarding.language.${selectedLanguage}` as const);

  // The i18n key carries the correct directional arrow per locale (→ in en, ← in ar).
  const fromToText = fromLabel
    ? t('parent.settings.linkedChildren.learningLanguage.confirm.fromTo', {
        from: fromLabel,
        to: toLabel,
      })
    : toLabel;

  const handleClose = useCallback(() => {
    if (!isPending) onCancel();
  }, [isPending, onCancel]);

  // Silence unused warning — childName is used in parent success message
  void childName;

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={handleClose}
      accessibilityViewIsModal
    >
      {/* Scrim — bottom sheet layout (mirrors EditChildSheet) */}
      <Stack flex={1} backgroundColor="$overlay" justifyContent="flex-end">
        {/* Card */}
        <Stack
          backgroundColor="$card"
          borderTopStartRadius="$modal"
          borderTopEndRadius="$modal"
          borderWidth={1}
          borderColor="rgba(255,255,255,0.10)"
          paddingBottom={insets.bottom + 20}
          paddingHorizontal={24}
          paddingTop={20}
          shadowColor="#000"
          shadowOpacity={0.5}
          shadowRadius={48}
          shadowOffset={{ width: 0, height: 16 }}
          gap={18}
          role="dialog"
        >
          {/* Drag handle */}
          <Stack alignItems="center" marginTop={-8} marginBottom={-8}>
            <Stack width={40} height={4} borderRadius="$pill" backgroundColor="rgba(255,255,255,0.15)" />
          </Stack>

          {/* 1. Danger marker + title row + close button */}
          <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap={12}>
            <Stack flexDirection={rowDir} alignItems="center" gap={12} flex={1}>
              {/* Danger chip — $dangerSoft bg, warning indicator */}
              <Stack
                width={44}
                height={44}
                borderRadius={9999}
                backgroundColor="$dangerSoft"
                alignItems="center"
                justifyContent="center"
                flexShrink={0}
                accessibilityElementsHidden
              >
                <Text fontSize={20} color="$danger" accessibilityElementsHidden>
                  {'⚠'}
                </Text>
              </Stack>
              <Text
                flex={1}
                color="$fg1"
                fontSize={18}
                fontWeight="800"
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={direction === 'rtl' ? 'right' : 'left'}
                accessibilityRole="header"
              >
                {t('parent.settings.linkedChildren.learningLanguage.confirm.title')}
              </Text>
            </Stack>

            {/* Close ✕ */}
            <Stack
              minWidth={44}
              minHeight={44}
              alignItems="center"
              justifyContent="center"
              cursor={isPending ? 'not-allowed' : 'pointer'}
              opacity={isPending ? 0.4 : 1}
              onPress={isPending ? undefined : handleClose}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('onboarding.close')}
              aria-label={t('onboarding.close')}
            >
              <Text fontSize={18} color="$fg3" accessibilityElementsHidden>
                {'✕'}
              </Text>
            </Stack>
          </Stack>

          {/* 2. From → To restatement */}
          <Stack
            backgroundColor="$bgElevated"
            borderRadius={14}
            padding={14}
            borderWidth={1}
            borderColor="rgba(255,255,255,0.06)"
          >
            <Text
              color="$fg2"
              fontSize={14}
              fontWeight="700"
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {fromToText}
            </Text>
          </Stack>

          {/* 3. Consequence lines */}
          <Stack gap={10}>
            {/* Loss line — $danger, bold subjects (copy in i18n) */}
            <Stack flexDirection={rowDir} alignItems="flex-start" gap={8}>
              <Text
                color="$danger"
                fontSize={16}
                flexShrink={0}
                accessibilityElementsHidden
              >
                {'✕'}
              </Text>
              <Text
                flex={1}
                color="$danger"
                fontSize={14}
                fontWeight="600"
                fontFamily="$body"
                writingDirection={direction}
                textAlign={direction === 'rtl' ? 'right' : 'left'}
              >
                {t('parent.settings.linkedChildren.learningLanguage.confirm.willReset')}
              </Text>
            </Stack>

            {/* Kept line — $fg2 */}
            <Stack flexDirection={rowDir} alignItems="flex-start" gap={8}>
              <Text
                color="$success"
                fontSize={16}
                flexShrink={0}
                accessibilityElementsHidden
              >
                {'✓'}
              </Text>
              <Text
                flex={1}
                color="$fg2"
                fontSize={13}
                fontFamily="$body"
                writingDirection={direction}
                textAlign={direction === 'rtl' ? 'right' : 'left'}
              >
                {t('parent.settings.linkedChildren.learningLanguage.confirm.willKeep')}
              </Text>
            </Stack>

            {/* Rare note — $fg3 */}
            <Text
              color="$fg3"
              fontSize={12}
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {t('parent.settings.linkedChildren.learningLanguage.confirm.rareNote')}
            </Text>
          </Stack>

          {/* 4. Required ack CheckboxField — gates Confirm button */}
          <CheckboxField
            checked={ackChecked}
            onChange={onAckChange}
            label={t('parent.settings.linkedChildren.learningLanguage.confirm.ack')}
            accessibilityLabel={t('parent.settings.linkedChildren.learningLanguage.confirm.ack')}
            direction={direction}
            disabled={isPending}
          />

          {/* Error banner — inside overlay (overlay stays open on error) */}
          <ServerErrorBanner message={serverMessage} direction={direction} />

          {/* 5. Actions — stacked (phone) per design spec §4.1 for ≥48px targets */}
          <Stack flexDirection="column" gap={10}>
            {/* Destructive Confirm — ad-hoc $danger bg Stack.
                No new Button variant — per design spec §4.2 / CLAUDE.md rule 8.
                Mirrors InlineUnlinkStrip's approach. */}
            <Stack
              backgroundColor={confirmDisabled ? '$dangerSoft' : '$danger'}
              borderRadius="$button"
              minHeight={48}
              justifyContent="center"
              alignItems="center"
              paddingHorizontal={20}
              opacity={confirmDisabled ? 0.4 : 1}
              onPress={confirmDisabled ? undefined : onConfirm}
              cursor={confirmDisabled ? 'not-allowed' : 'pointer'}
              pressStyle={confirmDisabled ? undefined : { scale: 0.95 }}
              accessibilityRole="button"
              accessible
              accessibilityState={{ disabled: confirmDisabled }}
              accessibilityLabel={t('parent.settings.linkedChildren.learningLanguage.confirm.cta')}
              aria-label={t('parent.settings.linkedChildren.learningLanguage.confirm.cta')}
            >
              <Text color="$fg1" fontSize={15} fontWeight="700" fontFamily="$body">
                {isPending
                  ? t('common.loading')
                  : t('parent.settings.linkedChildren.learningLanguage.confirm.cta')}
              </Text>
            </Stack>

            {/* Cancel — ghost */}
            <Stack
              borderRadius="$button"
              minHeight={48}
              justifyContent="center"
              alignItems="center"
              paddingHorizontal={20}
              opacity={isPending ? 0.4 : 1}
              onPress={isPending ? undefined : handleClose}
              cursor={isPending ? 'not-allowed' : 'pointer'}
              pressStyle={isPending ? undefined : { opacity: 0.7 }}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('common.cancel')}
              aria-label={t('common.cancel')}
            >
              <Text color="$fg3" fontSize={15} fontWeight="600" fontFamily="$body">
                {t('common.cancel')}
              </Text>
            </Stack>
          </Stack>
        </Stack>
      </Stack>
    </Modal>
  );
}

ChangeLearningLanguageModal.displayName = 'ChangeLearningLanguageModal';
