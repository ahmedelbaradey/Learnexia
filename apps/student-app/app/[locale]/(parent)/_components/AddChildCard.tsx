/**
 * AddChildCard — the trailing dashed "+ Add a child" action card in the parent
 * dashboard grid (captures `web/04-my-children.png` + `web-ar/04`). A rounded-
 * square "+" icon centered above a title and subtitle (vertical column layout);
 * routes to the add-child flow.
 *
 * Token-only styling, RTL-aware, i18n labels. Built from a pressable Stack — no
 * new design pattern.
 */
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '@/hooks/useLocale';

export interface AddChildCardProps {
  onPress: () => void;
  testID?: string;
}

export function AddChildCard({ onPress, testID }: AddChildCardProps) {
  const { t } = useTranslation();
  const { direction } = useLocale();

  return (
    <Stack
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      gap="$3"
      flex={1}
      minWidth={300}
      minHeight={260}
      borderRadius="$modal"
      borderWidth={2}
      borderColor="$primarySoft"
      // dashed border (web). Native falls back to the solid indigo-tinted border.
      style={{ borderStyle: 'dashed' }}
      backgroundColor="transparent"
      padding={22}
      cursor="pointer"
      pressStyle={{ scale: 0.95 }}
      hoverStyle={{ borderColor: '$primary', backgroundColor: '$primarySoft' }}
      onPress={() => onPress()}
      accessibilityRole="button"
      accessible
      accessibilityLabel={t('parent.myChildren.addCardTitle')}
      aria-label={t('parent.myChildren.addCardTitle')}
      testID={testID}
    >
      <Stack
        width={64}
        height={64}
        borderRadius={20}
        backgroundColor="$primarySoft"
        alignItems="center"
        justifyContent="center"
        accessibilityElementsHidden
      >
        <Text color="$primaryLight" fontSize={32} fontWeight="800" fontFamily="$heading">
          {'+'}
        </Text>
      </Stack>
      <Stack flexDirection="column" alignItems="center" gap="$1">
        <Text color="$fg1" fontSize={16} fontWeight="800" fontFamily="$heading" textAlign="center" writingDirection={direction}>
          {t('parent.myChildren.addCardTitle')}
        </Text>
        <Text
          color="$fg3"
          fontSize={12}
          fontFamily="$body"
          textAlign="center"
          maxWidth={200}
          writingDirection={direction}
        >
          {t('parent.myChildren.addCardSubtitle')}
        </Text>
      </Stack>
    </Stack>
  );
}

AddChildCard.displayName = 'AddChildCard';
