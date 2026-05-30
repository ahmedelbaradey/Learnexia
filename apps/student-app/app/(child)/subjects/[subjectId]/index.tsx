/**
 * Lessons tab — Design Spec §1 Surface 3 (P2-02-FE-1, -2, -3, -4, -5).
 *
 * Route: (child)/subjects/[subjectId]/index.tsx
 * Uses: useSubjectLessons(subjectId) → UnitWithLessonsDto[]
 *
 * Layout:
 *   Units sorted by sequenceOrder ASC. Within each unit, lessons sorted by
 *   sequenceOrder ASC. Each lesson rendered as a LessonCard with state, isBoss,
 *   and missingPrerequisites.
 *
 * Interactions:
 *   Tap Available/Completed → router.push('/(child)/lessons/' + lessonId).
 *   Tap Locked → open WhyLockedSheet with missingPrerequisites.
 *
 * State variants (AC-02-FE-5):
 *   Loading: shimmer skeleton rows.
 *   Error: retry button.
 *   Empty units / all empty lessons: "Coming soon" empty state.
 *   404 (ApiEnvelopeError status 404): "Subject not found" + Back action.
 *
 * Tokens only. Logical RTL flex. Arabic/English i18n keys.
 */
import React, { useState, useMemo } from 'react';
import { ScrollView } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useSubjectLessons, NodeState, type LessonInUnitDto, type MissingPrerequisiteDto } from '@learnexia/api-client';
import { LessonCard } from '@learnexia/ui';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../../../src/hooks/useLocale';
import { WhyLockedSheet } from '../../_components/WhyLockedSheet';

const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

interface LockedState {
  name: string;
  prereqs: MissingPrerequisiteDto[];
}

// Sort helper
function bySequence<T extends { sequenceOrder?: number }>(arr: T[]): T[] {
  return [...arr].sort((a, b) => (a.sequenceOrder ?? 0) - (b.sequenceOrder ?? 0));
}

export default function LessonsTab() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { subjectId: subjectIdParam } = useLocalSearchParams<{ subjectId: string }>();
  const subjectId = parseInt(subjectIdParam ?? '0', 10);

  const [lockedState, setLockedState] = useState<LockedState | null>(null);

  const { data: units, isLoading, isError, error, refetch } = useSubjectLessons(subjectId);

  const sortedUnits = useMemo(() => bySequence(units ?? []), [units]);

  // Check for 404 — ApiEnvelopeError with statusCode 404
  const is404 =
    isError &&
    error &&
    typeof error === 'object' &&
    'statusCode' in error &&
    (error as { statusCode?: number }).statusCode === 404;

  const allEmpty =
    !isLoading &&
    !isError &&
    sortedUnits.length === 0;

  function lessonStateValue(lesson: LessonInUnitDto): 0 | 1 | 2 {
    const s = lesson.state ?? NodeState._0;
    if (s === NodeState._1) return 1;
    if (s === NodeState._2) return 2;
    return 0;
  }

  return (
    <TamStack flex={1} backgroundColor="$bg">
      <ScrollView
        contentContainerStyle={{
          paddingHorizontal: 16,
          paddingBottom: insets.bottom + 24,
          paddingTop: 16,
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* Loading skeleton */}
        {isLoading ? (
          <YStack gap="$6">
            {[0, 1, 2].map((unitIdx) => (
              <YStack key={unitIdx} gap="$3">
                <TamStack height={20} width="60%" borderRadius="$card" backgroundColor="$cardSoft" />
                {[0, 1, 2].map((lessonIdx) => (
                  <TamStack
                    key={lessonIdx}
                    height={120}
                    borderRadius="$modal"
                    backgroundColor="$cardSoft"
                  />
                ))}
              </YStack>
            ))}
          </YStack>
        ) : is404 ? (
          /* 404 — Subject not found */
          <YStack gap="$4" alignItems="center" paddingVertical="$8">
            <Text
              color="$fg2"
              fontSize={18}
              fontWeight="700"
              fontFamily="$heading"
              textAlign="center"
              writingDirection={direction}
            >
              {t('child.subjects.subjectNotFound')}
            </Text>
            <TamStack
              paddingHorizontal="$6"
              paddingVertical="$3"
              borderRadius="$button"
              backgroundColor="transparent"
              borderWidth={1}
              borderColor="$border"
              cursor="pointer"
              onPress={() => router.push('/(child)/' as `/${string}`)}
              accessibilityRole="button"
              accessibilityLabel={t('child.subjects.backToSubjects')}
              minHeight={48}
              justifyContent="center"
            >
              <Text color="$fg2" fontSize={14} fontWeight="500" fontFamily="$body">
                {t('child.subjects.backToSubjects')}
              </Text>
            </TamStack>
          </YStack>
        ) : isError ? (
          /* Generic error + retry */
          <YStack gap="$4" alignItems="center" paddingVertical="$8">
            <Text
              color="$fg2"
              fontSize={16}
              fontWeight="600"
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
            >
              {t('child.subjects.errorRetry')}
            </Text>
            <TamStack
              paddingHorizontal="$6"
              paddingVertical="$3"
              borderRadius="$button"
              backgroundColor="$primary"
              cursor="pointer"
              onPress={() => refetch()}
              accessibilityRole="button"
              accessibilityLabel={t('child.subjects.errorRetry')}
              minHeight={48}
              justifyContent="center"
            >
              <Text color="$fg1" fontSize={15} fontWeight="700" fontFamily="$heading">
                {t('common.retry')}
              </Text>
            </TamStack>
          </YStack>
        ) : allEmpty ? (
          /* Coming-soon empty state */
          <YStack gap="$3" alignItems="center" paddingVertical="$8" paddingHorizontal="$8">
            <Text
              color="$fg2"
              fontSize={18}
              fontWeight="700"
              fontFamily="$heading"
              textAlign="center"
              writingDirection={direction}
            >
              {t('child.subjects.empty')}
            </Text>
          </YStack>
        ) : (
          /* Units + lessons */
          <YStack gap="$6">
            {sortedUnits.map((unit, unitIdx) => {
              const sortedLessons = bySequence(unit.lessons ?? []);
              return (
                <YStack key={unit.unitId ?? unitIdx} gap="$3">
                  {/* Unit eyebrow + name */}
                  <YStack gap={8}>
                    <Text
                      color="$fg3"
                      fontSize={12}
                      fontWeight="700"
                      textTransform="uppercase"
                      letterSpacing={0.04 * 12}
                      fontFamily="$heading"
                      writingDirection={direction}
                    >
                      {t('child.subjects.lessons.unitLabel', {
                        n: unit.sequenceOrder ?? unitIdx + 1,
                      })}
                    </Text>
                    <Text
                      color="$fg1"
                      fontSize={18}
                      fontWeight="800"
                      fontFamily="$heading"
                      lineHeight={22}
                      writingDirection={direction}
                    >
                      {unit.name ?? ''}
                    </Text>
                  </YStack>

                  {/* Lessons or empty unit placeholder */}
                  {sortedLessons.length === 0 ? (
                    <TamStack
                      padding="$4"
                      borderRadius="$card"
                      borderWidth={1}
                      borderColor="$borderStrong"
                      borderStyle="dashed"
                      alignItems="center"
                      justifyContent="center"
                    >
                      <Text
                        color="$fg3"
                        fontSize={14}
                        fontWeight="700"
                        fontFamily="$body"
                        writingDirection={direction}
                      >
                        {t('child.subjects.lessons.emptyUnit')}
                      </Text>
                    </TamStack>
                  ) : (
                    <YStack gap="$3">
                      {sortedLessons.map((lesson) => {
                        const stateVal = lessonStateValue(lesson);
                        const tag = t('child.subjects.title'); // simplified tag: subject name
                        const tagLabel =
                          stateVal === 2
                            ? t('child.subjects.lessons.tagCompleted')
                            : stateVal === 0
                            ? t('child.subjects.lessons.tagLocked')
                            : undefined;

                        const a11yState =
                          stateVal === 0
                            ? `${lesson.name ?? ''}, ${t('child.subjects.lessons.tagLocked')}${lesson.isBoss ? ', boss' : ''}`
                            : stateVal === 2
                            ? `${lesson.name ?? ''}, ${t('child.subjects.lessons.tagCompleted')}${lesson.isBoss ? ', boss' : ''}`
                            : `${lesson.name ?? ''}${lesson.isBoss ? ', boss' : ''}`;

                        return (
                          <LessonCard
                            key={lesson.lessonId}
                            lessonId={lesson.lessonId ?? 0}
                            tag={tag}
                            tagLabel={tagLabel}
                            title={lesson.name ?? ''}
                            state={stateVal}
                            isBoss={lesson.isBoss ?? false}
                            direction={direction}
                            accessibilityLabel={a11yState}
                            onPress={() => {
                              if (lesson.lessonId) {
                                router.push(
                                  `/(child)/lessons/${lesson.lessonId}?subjectId=${subjectId}` as `/${string}`,
                                );
                              }
                            }}
                            onLockTap={() => {
                              setLockedState({
                                name: lesson.name ?? '',
                                prereqs: lesson.missingPrerequisites ?? [],
                              });
                            }}
                          />
                        );
                      })}
                    </YStack>
                  )}
                </YStack>
              );
            })}
          </YStack>
        )}
      </ScrollView>

      {/* Why Locked Sheet */}
      <WhyLockedSheet
        open={lockedState !== null}
        onClose={() => setLockedState(null)}
        prerequisites={lockedState?.prereqs ?? []}
        lockedItemName={lockedState?.name ?? ''}
      />
    </TamStack>
  );
}
