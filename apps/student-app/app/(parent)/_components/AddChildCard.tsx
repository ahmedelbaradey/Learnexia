/**
 * AddChildCard — the trailing dashed "+ Add a child" action card in the parent
 * dashboard grid (capture `web/04-my-children.png`). A circle "+" icon, a title,
 * and a subtitle; routes to the add-child flow.
 *
 * Token-only styling, RTL-aware, i18n labels. Built from a pressable Stack — no
 * new design pattern.
 */
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';

export interface AddChildCardProps {
  onPress: () => void;
}

export function AddChildCard({ onPress }: AddChildCardProps) {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const rowDir = isRtl ? 'row-reverse' : 'row';

  return (
    <Stack
      flexDirection={rowDir}
      alignItems="center"
      gap="$4"
      flex={1}
      minWidth={300}
      minHeight={120}
      borderRadius="$card"
      borderWidth={2}
      borderColor="$borderStrong"
      // dashed border (web). Native falls back to the solid strong border.
      style={{ borderStyle: 'dashed' }}
      backgroundColor="transparent"
      padding="$5"
      cursor="pointer"
      pressStyle={{ scale: 0.98 }}
      hoverStyle={{ borderColor: '$primary', backgroundColor: '$primarySoft' }}
      onPress={() => onPress()}
      accessibilityRole="button"
      accessible
      accessibilityLabel={t('parent.myChildren.addCardTitle')}
      aria-label={t('parent.myChildren.addCardTitle')}
    >
      <Stack
        width={48}
        height={48}
        borderRadius={9999}
        backgroundColor="$primarySoft"
        alignItems="center"
        justifyContent="center"
        accessibilityElementsHidden
      >
        <Text color="$primaryLight" fontSize={26} fontWeight="800" fontFamily="$heading">
          {'+'}
        </Text>
      </Stack>
      <Stack flexDirection="column" flex={1} gap="$1">
        <Text color="$fg1" fontSize={16} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
          {t('parent.myChildren.addCardTitle')}
        </Text>
        <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
          {t('parent.myChildren.addCardSubtitle')}
        </Text>
      </Stack>
    </Stack>
  );
}

AddChildCard.displayName = 'AddChildCard';
