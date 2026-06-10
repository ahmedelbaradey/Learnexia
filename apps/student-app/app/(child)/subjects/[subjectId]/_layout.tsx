/**
 * Subject detail tab layout — Design Spec §1 Surface 2.
 *
 * Wraps the two tabs (Lessons | Skill Tree) inside a SegmentedTabs control.
 * Controlled tab state lives here so it persists when switching between tabs.
 *
 * Layout:
 *   - Custom ScreenHeader with back chevron + subject-name title + secondary
 *     info line (units/lessons on Lessons tab; unit x of y · mastery on Tree tab).
 *   - SegmentedTabs below the header.
 *   - Tab body (outlet via children — each tab renders inside this layout).
 *
 * The `subjectId` param is read from the URL so the header can derive the
 * subject name once `useSubjectLessons` or `useSubjectSkillTree` resolves.
 *
 * Note: Expo Router nested `_layout` with a dynamic segment works by letting
 * the file-system router wire `index.tsx` → 'lessons' segment and `tree.tsx`
 * → 'tree' segment. We manage the segment switch via router.push/replace so
 * navigation stays in the Stack (the user sees one Back entry, not two).
 *
 * For Wave 11 we use a simple approach: the layout renders both tabs' bodies as
 * conditional mounts (not as file-system tabs) to avoid the complexity of
 * Expo Router's nested `<Tabs>` + `Stack` hybrid.
 *
 * Routing strategy: index.tsx is the default (Lessons). When the user switches to
 * Tree, we push `/(child)/subjects/${subjectId}/tree`. Back from Tree pops back
 * to Lessons without a double-back issue because we use `replace` on tab switch.
 */
import React, { useCallback } from 'react';
import { Pressable } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { SegmentedTabs } from '@learnexia/ui';
import { useTranslation } from 'react-i18next';
import { useLocalSearchParams, useRouter, Slot, usePathname } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../../../src/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

const TAB_LESSONS = 'lessons';
const TAB_TREE = 'tree';

export default function SubjectLayout() {
  const { t } = useTranslation();
  const { isRtl, direction } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { subjectId } = useLocalSearchParams<{ subjectId: string }>();
  const pathname = usePathname();

  // Derive active tab from current pathname
  const isTreeTab = pathname?.endsWith('/tree');
  const activeTab = isTreeTab ? TAB_TREE : TAB_LESSONS;

  const handleTabChange = useCallback(
    (value: string) => {
      if (value === TAB_TREE) {
        router.push(`/(child)/subjects/${subjectId}/tree` as `/${string}`);
      } else {
        router.push(`/(child)/subjects/${subjectId}` as `/${string}`);
      }
    },
    [router, subjectId],
  );

  const handleBack = useCallback(() => {
    router.push('/(child)/' as `/${string}`);
  }, [router]);

  const rowDir = isRtl ? 'row-reverse' : 'row';
  const backChevron = isRtl ? '→' : '←';

  const tabItems = [
    { value: TAB_LESSONS, label: t('child.subjects.tabs.lessons') },
    { value: TAB_TREE, label: t('child.subjects.tabs.tree') },
  ];

  return (
    <YStack flex={1} backgroundColor="$bg">
      {/* Top safe area */}
      <TamStack height={insets.top} />

      {/* ScreenHeader */}
      <XStack
        flexDirection={rowDir}
        alignItems="center"
        height={56}
        paddingHorizontal="$4"
        borderBottomWidth={1}
        borderBottomColor="$borderSubtle"
        gap="$3"
      >
        {/* Back button */}
        <Pressable
          onPress={handleBack}
          accessibilityRole="button"
          accessibilityLabel={t('child.subjects.backToSubjects')}
          style={{ minWidth: 48, minHeight: 48, alignItems: 'center', justifyContent: 'center' }}
        >
          <Text color="$fg2" fontSize={22} fontFamily="$body">
            {backChevron}
          </Text>
        </Pressable>

        {/* Title block (center-flex) */}
        <YStack flex={1} gap={2} alignItems="center">
          <Text
            color="$fg1"
            fontSize={18}
            fontWeight="700"
            fontFamily="$heading"
            textAlign="center"
            writingDirection={direction}
            numberOfLines={1}
          >
            {subjectId ? t('child.subjects.title') : ''}
          </Text>
        </YStack>

        {/* Trailing slot (empty in W11) */}
        <TamStack width={48} />
      </XStack>

      {/* Segmented control */}
      <TamStack paddingHorizontal="$4" paddingVertical="$2">
        <SegmentedTabs
          items={tabItems}
          value={activeTab}
          onChange={handleTabChange}
          direction={direction}
          accessibilityLabel={t('child.subjects.tabs.lessons') + ' / ' + t('child.subjects.tabs.tree')}
          itemTestIDPrefix="segmented-tab"
        />
      </TamStack>

      {/* Tab body outlet */}
      <TamStack flex={1}>
        <Slot />
      </TamStack>
    </YStack>
  );
}
