/**
 * Lesson player stub — placeholder until P2-05-FE (Wave 12) ships.
 *
 * Design Spec §10 (stub note). Accepts `lessonId` from URL params.
 * Does NOT crash on unknown/invalid lessonId.
 * Renders friendly "Lesson player coming soon" in AR/EN.
 * Back button returns to the subject screen.
 *
 * P2-05-FE will replace this file entirely.
 */
import React from 'react';
import { Pressable } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../../src/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

export default function LessonStubScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { lessonId } = useLocalSearchParams<{ lessonId: string }>();

  return (
    <TamStack
      flex={1}
      backgroundColor="$bg"
      paddingTop={insets.top}
      paddingBottom={insets.bottom}
    >
      {/* Header bar */}
      <TamStack
        height={56}
        paddingHorizontal="$4"
        justifyContent="center"
        borderBottomWidth={1}
        borderBottomColor="$borderSubtle"
      >
        <Pressable
          onPress={() => router.back()}
          accessibilityRole="button"
          accessibilityLabel={t('child.lessons.stub.back')}
          style={{ width: 48, height: 48, alignItems: 'flex-start', justifyContent: 'center' }}
        >
          <Text color="$fg2" fontSize={22} fontFamily="$body">
            {direction === 'rtl' ? '→' : '←'}
          </Text>
        </Pressable>
      </TamStack>

      {/* Body */}
      <YStack flex={1} alignItems="center" justifyContent="center" paddingHorizontal="$6" gap="$4">
        <Text
          color="$fg1"
          fontSize={24}
          fontWeight="800"
          fontFamily="$heading"
          textAlign="center"
          writingDirection={direction}
          accessibilityRole="header"
        >
          {t('child.lessons.stub.title')}
        </Text>
        <Text
          color="$fg2"
          fontSize={16}
          fontWeight="500"
          fontFamily="$body"
          textAlign="center"
          writingDirection={direction}
        >
          {t('child.lessons.stub.body')}
        </Text>
        {lessonId ? (
          <Text
            color="$fg4"
            fontSize={12}
            fontWeight="500"
            fontFamily="$body"
            textAlign="center"
          >
            {`lesson: ${lessonId}`}
          </Text>
        ) : null}

        {/* Back CTA */}
        <TamStack
          marginTop="$4"
          paddingHorizontal="$6"
          paddingVertical="$3"
          borderRadius="$button"
          backgroundColor="$primary"
          cursor="pointer"
          onPress={() => router.back()}
          accessibilityRole="button"
          accessibilityLabel={t('child.lessons.stub.back')}
          minHeight={48}
          justifyContent="center"
          alignItems="center"
        >
          <Text color="$fg1" fontSize={15} fontWeight="700" fontFamily="$heading">
            {t('child.lessons.stub.back')}
          </Text>
        </TamStack>
      </YStack>
    </TamStack>
  );
}
