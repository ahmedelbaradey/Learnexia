/**
 * SubjectsListSection — W13 P2-09-FE
 *
 * Inline app component (NOT promoted to @learnexia/ui — owns app-specific
 * grade resolution + routing per design spec §3.4).
 *
 * Extracted from `(child)/index.tsx` (W11 logic intact):
 *   - `useSubjectsForGrade(grade)` query orchestration.
 *   - `filterSubjects()` defensive 4-subject filter (subjects.ts helper).
 *   - Shimmer skeletons gated on loading union.
 *   - Error + empty states with retry.
 *   - Subject tap → `router.push('/(child)/subjects/{id}')`.
 *
 * The W11 H1 "Subjects" is DEMOTED to a section eyebrow ("Your subjects" /
 * "موادك") so the dashboard greeting H1 owns the H1 visual slot (design
 * spec §3.4, brief §0).
 *
 * Token-only, RTL-aware, kid-a11y baseline.
 */

import React, { useMemo } from 'react';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useSubjectsForGrade } from '@learnexia/api-client';
import { SubjectRow } from '@learnexia/ui';
import { useTranslation } from 'react-i18next';
import { useRouter } from 'expo-router';

import { filterSubjects } from './subjects';

const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

export interface SubjectsListSectionProps {
  /** Grade from useMe — 1..6. When null/undefined the subjects query is disabled. */
  grade?: number | null;
  direction?: 'ltr' | 'rtl';
  /** When true, the whole section renders shimmer skeletons (parent's loading union). */
  loading?: boolean;
  testID?: string;
}

export function SubjectsListSection({
  grade,
  direction = 'ltr',
  loading: parentLoading = false,
  testID,
}: SubjectsListSectionProps) {
  const { t } = useTranslation();
  const router = useRouter();

  const gradeKnown = grade !== null && grade !== undefined && grade >= 1 && grade <= 6;
  const subjectsQuery = useSubjectsForGrade(gradeKnown ? grade : undefined);

  const filteredSubjects = useMemo(
    () => filterSubjects(subjectsQuery.data ?? []),
    [subjectsQuery.data],
  );

  const isLoading = parentLoading || subjectsQuery.isLoading;

  return (
    <YStack gap={12} testID={testID}>
      {/* Section eyebrow — "Your subjects" / "موادك" (W11 H1 demoted per spec §3.4) */}
      <Text
        color="$fg3"
        fontSize={12}
        fontWeight="700"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.48}
        writingDirection={direction}
        alignSelf="flex-start"
        accessibilityRole="header"
        accessible
      >
        {t('child.home.yourSubjects')}
      </Text>

      {isLoading ? (
        /* Shimmer placeholders — 4 rows (W11 pattern) */
        <YStack testID="subjects-loading" gap={10}>
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
        /* Error state — W11 path unchanged */
        <YStack testID="subjects-error" gap="$4" alignItems="center" paddingVertical="$8">
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
      ) : filteredSubjects.length === 0 ? (
        /* Empty state — W11 path (preserved; parent screen handles the combined
           empty + continue===null welcome-tile case at the dashboard level). */
        <YStack testID="subjects-empty" gap="$3" alignItems="center" paddingVertical="$8">
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
              testID={`subject-row-${key}`}
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
    </YStack>
  );
}

SubjectsListSection.displayName = 'SubjectsListSection';
