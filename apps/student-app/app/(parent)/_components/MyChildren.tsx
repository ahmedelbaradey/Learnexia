/**
 * MyChildren (Design Spec Screen 8) — list of linked children with the active-
 * child switcher. `useMyChildren` query → loading skeletons / empty / error /
 * loaded states. Tapping a child writes `childContextStore.activeChildId`.
 */
import { useMyChildren } from '@learnexia/api-client';
import { useChildContextStore } from '@learnexia/shared';
import { Button, ChildCard } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { MotiView } from 'moti';
import { Image } from 'react-native';
import { useTranslation } from 'react-i18next';

import { assets } from '../../../src/assets';
import { useLocale } from '../../../src/hooks/useLocale';

function SkeletonRow() {
  return (
    <MotiView
      from={{ opacity: 0.4 }}
      animate={{ opacity: 0.7 }}
      transition={{ type: 'timing', duration: 400, loop: true, repeatReverse: true }}
    >
      <Stack height={72} backgroundColor="$card" borderRadius="$card" flexDirection="row" alignItems="center" gap="$3" padding="$4">
        <Stack width={44} height={44} borderRadius="$pill" backgroundColor="$cardSoft" />
        <Stack flex={1} gap="$2">
          <Stack height={12} width="60%" borderRadius="$sm" backgroundColor="$cardSoft" />
          <Stack height={10} width="40%" borderRadius="$sm" backgroundColor="$cardSoft" />
        </Stack>
      </Stack>
    </MotiView>
  );
}

export function MyChildren() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const query = useMyChildren();
  const activeChildId = useChildContextStore((s) => s.activeChildId);
  const setActiveChild = useChildContextStore((s) => s.setActiveChild);

  const children = query.data ?? [];

  if (query.isLoading) {
    return (
      <Stack gap="$3" accessibilityLabel={t('common.loading')}>
        <SkeletonRow />
        <SkeletonRow />
        <SkeletonRow />
      </Stack>
    );
  }

  if (query.isError) {
    return (
      <Stack alignItems="center" gap="$4" marginTop="$8">
        <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
          {t('parent.myChildren.loadError')}
        </Text>
        <Button variant="ghost" accessibilityLabel={t('common.retry')} onPress={() => query.refetch()}>
          {t('common.retry')}
        </Button>
      </Stack>
    );
  }

  if (children.length === 0) {
    return (
      <Stack alignItems="center" gap="$4" marginTop="$16">
        <Image source={assets.mascotOwl} style={{ width: 80, height: 80, resizeMode: 'contain', opacity: 0.6 }} accessibilityElementsHidden />
        <Text color="$fg3" fontSize={16} fontFamily="$body" textAlign="center" writingDirection={direction}>
          {t('parent.myChildren.empty')}
        </Text>
        <Button variant="ghost" accessibilityLabel={t('parent.myChildren.linkButton')} onPress={() => router.push('/(parent)/link-child')}>
          {t('parent.myChildren.linkButton')}
        </Button>
      </Stack>
    );
  }

  return (
    <Stack gap="$3">
      <Text color="$fg3" fontSize={12} fontWeight="600" fontFamily="$heading" textTransform="uppercase" letterSpacing={0.6} writingDirection={direction}>
        {t('parent.myChildren.sectionLabel', { count: children.length })}
      </Text>
      {children.map((child) => {
        const id = String(child.id);
        return (
          <ChildCard
            key={id}
            variant="selectable"
            child={{ fullName: child.fullName ?? '', meta: child.email }}
            isActive={activeChildId === id}
            onPress={() => setActiveChild(id)}
            direction={direction}
            accessibilityLabel={child.fullName ?? ''}
          />
        );
      })}

      <Stack height={1} backgroundColor="$border" marginVertical="$4" />

      <Button variant="ghost" size="full" accessibilityLabel={t('parent.myChildren.linkButton')} onPress={() => router.push('/(parent)/link-child')}>
        {t('parent.myChildren.linkButton')}
      </Button>
    </Stack>
  );
}
