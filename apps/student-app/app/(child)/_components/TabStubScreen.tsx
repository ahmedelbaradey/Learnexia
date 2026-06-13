/**
 * TabStubScreen — temporary body for the Missions / League / Badges tab roots.
 *
 * B0-nav (batch 2b) ships the TabBar + route stubs; the real screens are
 * Wave-B batches 3a/3b (B4/B5/B6) and B-int (3c) removes this component.
 *
 * Chrome per Design Spec §5 "Stub route": centered 40px glyph + lx-h3 title
 * (18px/600 `$fg1`) + lx-body-sm `$fg3` "Coming soon" line, on the exact
 * MatchingPanel-stub tile (dashed `$borderStrong` border, `$cardSoft` bg,
 * 14px radius).
 *
 * Bottom padding bakes in the TabBar clearance from day one (spec §3):
 * `CHILD_TAB_BAR_CLEARANCE + insets.bottom`.
 */
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../../src/hooks/useLocale';
import { CHILD_TAB_BAR_CLEARANCE } from './ChildTabBar';

const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

export interface TabStubScreenProps {
  /** Decorative emoji glyph (from CHILD_TAB_ICONS). */
  glyph: string;
  /** Already-localized screen name (the tab label, `t('nav.tabs.*')`). */
  title: string;
  testID?: string;
}

export function TabStubScreen({ glyph, title, testID }: TabStubScreenProps) {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const insets = useSafeAreaInsets();

  const comingSoon = t('nav.stub.comingSoon');

  return (
    <TamStack
      flex={1}
      backgroundColor="$bg"
      paddingTop={insets.top}
      paddingHorizontal={24}
      paddingBottom={CHILD_TAB_BAR_CLEARANCE + insets.bottom}
      alignItems="center"
      justifyContent="center"
      testID={testID}
    >
      {/* MatchingPanel-stub chrome (spec §5) */}
      <YStack
        width="100%"
        maxWidth={380}
        padding={24}
        gap={8}
        borderRadius={14}
        borderWidth={1}
        borderStyle="dashed"
        borderColor="$borderStrong"
        backgroundColor="$cardSoft"
        alignItems="center"
        accessible
        accessibilityRole="text"
        accessibilityLabel={`${title}. ${comingSoon}`}
        aria-label={`${title}. ${comingSoon}`}
      >
        <Text fontSize={40} lineHeight={48} accessibilityElementsHidden>
          {glyph}
        </Text>
        {/* lx-h3: display family, 18px, semibold, $fg1 */}
        <Text
          fontSize={18}
          fontWeight="600"
          fontFamily="$heading"
          color="$fg1"
          textAlign="center"
          writingDirection={direction}
        >
          {title}
        </Text>
        {/* lx-body-sm: body family, 14px, regular, $fg3 */}
        <Text
          fontSize={14}
          fontWeight="400"
          fontFamily="$body"
          color="$fg3"
          textAlign="center"
          writingDirection={direction}
        >
          {comingSoon}
        </Text>
      </YStack>
    </TamStack>
  );
}
