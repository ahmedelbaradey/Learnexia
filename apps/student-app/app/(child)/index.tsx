/**
 * Child Subjects list screen — replaces the P1-09 mascot placeholder.
 *
 * Design Spec §1 Surface 1. The signed-in student sees exactly 4 grade-scoped
 * subject rows (Math / Science / Arabic / English). Social Studies never renders.
 *
 * Grade is read from `useMe().data?.grade`. When null/undefined:
 *   - `useSubjectsForGrade` is disabled (enabled: false).
 *   - The header omits the "Grade {n}" caption.
 *   - A neutral empty-state is shown (not an error).
 *
 * Sign-out and `childName` derivation from `authStore` are preserved.
 * All four product subjects are filtered defensively on the FE even if the
 * BE returns more rows.
 */
import React, { useMemo } from 'react';
import { ScrollView, Image } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useAuthStore } from '@learnexia/shared';
import { useMe, useSubjectsForGrade, type StudentSubjectDto } from '@learnexia/api-client';
import { SubjectRow } from '@learnexia/ui';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';
import { useRouter } from 'expo-router';

import { assets } from '../../src/assets';
import { useLocale } from '../../src/hooks/useLocale';
import { useSignOutAction } from '../../src/hooks/useSignOutAction';
import type { SubjectKey } from '@learnexia/ui';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

// Defensive filter: normalize the API subject name to a known SubjectKey.
// Matches case-insensitively against canonical English + Arabic names from seeder.
const SUBJECT_NAME_MAP: Record<string, SubjectKey> = {
  math: 'math',
  mathematics: 'math',
  الرياضيات: 'math',
  science: 'science',
  العلوم: 'science',
  arabic: 'arabic',
  العربية: 'arabic',
  english: 'english',
  الإنجليزية: 'english',
};

const ALLOWED_KEYS: Set<SubjectKey> = new Set(['math', 'science', 'arabic', 'english']);

function resolveSubjectKey(name: string | undefined): SubjectKey | null {
  if (!name) return null;
  const normalized = name.trim().toLowerCase();
  const key = SUBJECT_NAME_MAP[normalized];
  return key && ALLOWED_KEYS.has(key) ? key : null;
}

function filterSubjects(subjects: StudentSubjectDto[]): Array<{ dto: StudentSubjectDto; key: SubjectKey }> {
  const result: Array<{ dto: StudentSubjectDto; key: SubjectKey }> = [];
  const seen = new Set<SubjectKey>();
  for (const dto of subjects) {
    const key = resolveSubjectKey(dto.name);
    if (key && !seen.has(key)) {
      seen.add(key);
      result.push({ dto, key });
    }
  }
  // Sort by canonical order: Math, Science, Arabic, English
  const ORDER: SubjectKey[] = ['math', 'science', 'arabic', 'english'];
  result.sort((a, b) => ORDER.indexOf(a.key) - ORDER.indexOf(b.key));
  return result;
}

export default function ChildSubjectsScreen() {
  const { t } = useTranslation();
  const { isRtl, direction } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const fullName = useAuthStore((s) => s.user?.fullName);
  const { signOut, isPending } = useSignOutAction();

  const childName = (fullName ?? '').split(/\s+/)[0] || '';

  // Grade from /api/Users/Me (additive field — grade?: number | undefined)
  const meQuery = useMe();
  const grade = meQuery.data?.grade ?? null;
  const gradeKnown = grade !== null && grade !== undefined && grade >= 1 && grade <= 6;

  const subjectsQuery = useSubjectsForGrade(gradeKnown ? grade : undefined);

  const filteredSubjects = useMemo(
    () => filterSubjects(subjectsQuery.data ?? []),
    [subjectsQuery.data],
  );

  const rowDir = isRtl ? 'row-reverse' : 'row';

  return (
    <TamStack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      {/* Header bar */}
      <XStack
        flexDirection={rowDir}
        height={56}
        paddingHorizontal="$6"
        alignItems="center"
        justifyContent="space-between"
      >
        <Image
          source={assets.logoMark}
          style={{ width: 32, height: 32, resizeMode: 'contain' }}
          accessibilityElementsHidden
        />
        {/* Sign out — ghost/icon-button styling (not a primary action) */}
        <TamStack
          minHeight={48}
          justifyContent="center"
          cursor="pointer"
          onPress={isPending ? undefined : signOut}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('child.subjects.signOut')}
          aria-label={t('child.subjects.signOut')}
        >
          <Text color="$fg3" fontSize={13} fontFamily="$body">
            {t('child.subjects.signOut')}
          </Text>
        </TamStack>
      </XStack>

      <ScrollView
        contentContainerStyle={{ paddingHorizontal: 24, paddingBottom: insets.bottom + 24 }}
        showsVerticalScrollIndicator={false}
      >
        {/* Screen header block */}
        <YStack gap={4} marginTop="$6" marginBottom="$6">
          <Text
            color="$fg1"
            fontSize={32}
            fontWeight="800"
            fontFamily="$heading"
            lineHeight={37}
            letterSpacing={-0.02 * 32}
            writingDirection={direction}
            accessibilityRole="header"
          >
            {t('child.subjects.title')}
          </Text>
          {gradeKnown ? (
            <Text
              color="$fg3"
              fontSize={14}
              fontWeight="500"
              fontFamily="$body"
              writingDirection={direction}
            >
              {t('child.subjects.gradeLabel', { grade })}
            </Text>
          ) : childName ? (
            <Text
              color="$fg3"
              fontSize={14}
              fontWeight="500"
              fontFamily="$body"
              writingDirection={direction}
            >
              {childName}
            </Text>
          ) : null}
        </YStack>

        {/* Loading state — shimmer placeholders. Includes the brief pre-/Me window
            so the empty-state never flashes before the grade resolves. */}
        {meQuery.isLoading || subjectsQuery.isLoading ? (
          <YStack gap={10}>
            {[0, 1, 2, 3].map((i) => (
              <SubjectRow
                key={i}
                subjectId={i}
                name=""
                subjectKey="math"
                onPress={() => {}}
                accessibilityLabel=""
                loading
              />
            ))}
          </YStack>
        ) : subjectsQuery.isError ? (
          /* Error state */
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
              onPress={() => subjectsQuery.refetch()}
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
        ) : !gradeKnown || filteredSubjects.length === 0 ? (
          /* Empty state — grade null or no subjects returned */
          <YStack gap="$3" alignItems="center" paddingVertical="$8">
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
          /* Subject rows — exactly 4 product subjects, defensively filtered */
          <YStack gap={10}>
            {filteredSubjects.map(({ dto, key }) => (
              <SubjectRow
                key={dto.id}
                subjectId={dto.id ?? 0}
                name={dto.name ?? ''}
                subjectKey={key}
                onPress={() =>
                  router.push(`/(child)/subjects/${dto.id}` as `/${string}`)
                }
                direction={direction}
                accessibilityLabel={`${dto.name ?? ''}`}
              />
            ))}
          </YStack>
        )}
      </ScrollView>
    </TamStack>
  );
}
