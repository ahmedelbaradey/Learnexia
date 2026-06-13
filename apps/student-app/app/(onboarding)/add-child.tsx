/**
 * Add-Child onboarding screen (My-Children style).
 *
 * Shows:
 *   1. Heading + subtitle (onboarding copy).
 *   2. List of children ALREADY ADDED, driven by useMyChildren (TanStack Query).
 *      Each child is rendered as a ChildCard (status/display variant).
 *   3. A dashed "Add a child" tile (AddChildCard) that opens AddChildModal.
 *   4. A primary "Continue" button, enabled once ≥1 child exists, that routes
 *      to /(onboarding)/complete.
 *
 * The modal creates each child IMMEDIATELY via its internal useAddChild.
 * On close, the list refreshes from useMyChildren (already invalidated by
 * useAddChild.onSuccess). Parents can add several children before continuing.
 *
 * Modal open/close is local useState — NOT activeChildStore (which is
 * dashboard-scoped). See brief §2 "Modal-open state in onboarding".
 *
 * testIDs:
 *   onboarding-add-child-tile      — dashed add tile / open-modal CTA
 *   onboarding-children-list       — container for added children
 *   onboarding-child-card-{id}     — each added child card (id = LinkedChildResponse.id)
 *   onboarding-continue            — Continue / Finish button
 *   (modal exposes its own set: add-child-modal, add-child-submit, grade-tile-{1..6}, etc.)
 */
import { useMyChildren } from '@learnexia/api-client';
import { Button, ChildCard } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';

import { AddChildModal } from '../../src/components/AddChildModal';
import { useLocale } from '../../src/hooks/useLocale';
import { AddChildCard } from '../(parent)/_components/AddChildCard';

export default function AddChildScreen() {
  const { t } = useTranslation();
  const { direction, locale } = useLocale();
  const router = useRouter();
  const query = useMyChildren();

  // Local modal visibility — do NOT use activeChildStore (dashboard-scoped).
  const [addOpen, setAddOpen] = useState(false);

  const children = query.data ?? [];
  const childCount = children.length;

  /** Format a number using the locale's numeral system (e.g. Eastern-Arabic). */
  function formatNumber(n: number): string {
    return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(n);
  }

  /** Build a display meta line for a child: "Grade X · Language". */
  function childMeta(grade: number | undefined, language: string | undefined): string {
    const gradePart = grade != null
      ? t(`onboarding.grade.${grade}`, { defaultValue: `${formatNumber(grade)}` })
      : '';
    const langPart = language ? t(`onboarding.language.${language}`, { defaultValue: language }) : '';
    if (gradePart && langPart) return `${gradePart} · ${langPart}`;
    return gradePart || langPart;
  }

  return (
    <Stack flex={1} backgroundColor="$bg">
      <ScrollView contentContainerStyle={{ flexGrow: 1 }} keyboardShouldPersistTaps="handled">
        <Stack paddingHorizontal="$6" paddingTop="$6" paddingBottom="$16" gap="$6">

          {/* Screen heading + subtitle */}
          <Stack gap="$2">
            <Text
              color="$fg1"
              fontSize={22}
              fontWeight="700"
              fontFamily="$heading"
              accessibilityRole="header"
              writingDirection={direction}
            >
              {t('onboarding.addChild.title')}
            </Text>
            <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
              {t('onboarding.addChild.subtitle')}
            </Text>
          </Stack>

          {/* Added-children list — driven by useMyChildren, NOT local drafts */}
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

          {/* Empty-state hint — shown while no children added yet */}
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

          {/* Dashed "Add a child" tile — opens AddChildModal */}
          <AddChildCard
            onPress={() => setAddOpen(true)}
            testID="onboarding-add-child-tile"
          />

          {/* Continue button — enabled once ≥1 child is added */}
          <Button
            variant="primary"
            size="full"
            accessibilityLabel={t('onboarding.addChild.continue')}
            disabled={childCount === 0}
            onPress={() => router.replace('/(onboarding)/complete')}
            testID="onboarding-continue"
          >
            {t('onboarding.addChild.continue')}
          </Button>

        </Stack>
      </ScrollView>

      {/* AddChildModal — local state, creates child immediately on confirm */}
      <AddChildModal
        visible={addOpen}
        onClose={() => setAddOpen(false)}
      />
    </Stack>
  );
}
