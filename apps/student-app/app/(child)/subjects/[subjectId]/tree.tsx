/**
 * Skill Tree tab — Design Spec §1 Surface 4 (P2-03-FE-1, -2, -3, -4, -5).
 *
 * Route: (child)/subjects/[subjectId]/tree.tsx
 * Uses:
 *   useSubjectSkillTree(subjectId) → ConceptNodeDto[]
 *   useSubjectLessons(subjectId)   → UnitWithLessonsDto[] (TanStack cache; cheap)
 *
 * Boss derivation (design spec §7 / brief §7):
 *   For each SkillNodeDto.skillId, derive hasBoss = true if any LessonInUnitDto
 *   with skillId === skill.skillId has isBoss === true.
 *   This is an in-memory join — no extra network request (same query key as the
 *   Lessons tab → served from TanStack cache after first visit).
 *
 * Mastery % header (AC-03-FE-4):
 *   z = Math.round(completedCount / totalSkillCount * 100).
 *   Concept name of the first non-completed concept shown in the secondary line.
 *   Unit x of y: concept index (1-based) of first in-progress concept.
 *
 * Connector strips: plain 4×24 Tamagui Stack (design spec §4, no Skia/Reanimated).
 *   Between concepts: NO connector.
 *   Between skills WITHIN a concept: connector below upper node.
 *   Connector color: $borderStrong (reachable) or $border (locked).
 *
 * RTL: logical flex throughout. Labels right-aligned in AR via writingDirection.
 * Connectors: stay centered (no x-flip — they are vertical strips).
 *
 * Available skill pulse: CSS @keyframes on web; no-op on native (W11 degradation).
 */
import React, { useState, useMemo } from 'react';
import { ScrollView } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import {
  useSubjectSkillTree,
  useSubjectLessons,
  NodeState,
  type SkillNodeDto,
  type MissingPrerequisiteDto,
} from '@learnexia/api-client';
import { SkillTreeNode } from '@learnexia/ui';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { useLocale } from '../../../../src/hooks/useLocale';
import { WhyLockedSheet } from '../../_components/WhyLockedSheet';

const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

interface LockedSkill {
  name: string;
  prereqs: MissingPrerequisiteDto[];
}

// Map NodeState enum to the LessonState union (0|1|2)
function nodeStateValue(state: NodeState | undefined): 0 | 1 | 2 {
  if (state === NodeState._1) return 1;
  if (state === NodeState._2) return 2;
  return 0;
}

// CSS @keyframes injection for Available node pulse (web-only, once)
let pulseStyleInjected = false;
function injectPulseStyle() {
  if (typeof document === 'undefined' || pulseStyleInjected) return;
  pulseStyleInjected = true;
  const style = document.createElement('style');
  style.textContent = `
    @keyframes lx-skill-pulse {
      0%, 100% { transform: scale(1); }
      50%       { transform: scale(1.04); }
    }
  `;
  document.head.appendChild(style);
}

export default function SkillTreeTab() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const { subjectId: subjectIdParam } = useLocalSearchParams<{ subjectId: string }>();
  const subjectId = parseInt(subjectIdParam ?? '0', 10);

  const [lockedSkill, setLockedSkill] = useState<LockedSkill | null>(null);

  const treeQuery = useSubjectSkillTree(subjectId);
  const lessonsQuery = useSubjectLessons(subjectId);

  // Inject CSS pulse animation on web (once)
  React.useEffect(() => { injectPulseStyle(); }, []);

  // Build a Set of skillIds that have a boss lesson (in-memory join)
  const bossSkillIds = useMemo<Set<number>>(() => {
    const set = new Set<number>();
    const units = lessonsQuery.data ?? [];
    for (const unit of units) {
      for (const lesson of unit.lessons ?? []) {
        if (lesson.isBoss && lesson.skillId !== undefined && lesson.skillId !== null) {
          set.add(lesson.skillId);
        }
      }
    }
    return set;
  }, [lessonsQuery.data]);

  const concepts = treeQuery.data ?? [];

  // Mastery header computation
  const { totalSkills, completedSkills } = useMemo(() => {
    let total = 0;
    let completed = 0;
    for (const concept of concepts) {
      for (const skill of concept.skills ?? []) {
        total++;
        if (skill.state === NodeState._2) completed++;
      }
    }
    return { totalSkills: total, completedSkills: completed };
  }, [concepts]);

  const masteryPct = totalSkills > 0 ? Math.round((completedSkills / totalSkills) * 100) : 0;

  // Find the first in-progress concept index (for "Unit x of y")
  const firstInProgressConceptIdx = useMemo(() => {
    for (let i = 0; i < concepts.length; i++) {
      const skills = concepts[i]?.skills ?? [];
      if (skills.some((s) => s.state === NodeState._1)) return i;
    }
    return 0;
  }, [concepts]);

  const currentConceptName =
    concepts.length > 0 ? (concepts[firstInProgressConceptIdx]?.name ?? '') : '';

  const isLoading = treeQuery.isLoading || lessonsQuery.isLoading;
  const isError = treeQuery.isError;

  const subCaptionMap = {
    locked: t('child.skillTree.subLocked'),
    available: t('child.skillTree.subInProgress'),
    completed: t('child.skillTree.subCompleted'),
    bossChallenge: t('child.skillTree.subBossChallenge'),
    bossBeaten: t('child.skillTree.subBossBeaten'),
  };

  return (
    <TamStack flex={1} backgroundColor="$bg">
      {/* Mastery header strip */}
      {!isLoading && !isError && concepts.length > 0 ? (
        <TamStack
          paddingHorizontal="$4"
          paddingVertical="$2"
          borderBottomWidth={1}
          borderBottomColor="$borderSubtle"
        >
          <Text
            color="$fg3"
            fontSize={12}
            fontWeight="500"
            fontFamily="$body"
            textAlign="center"
            writingDirection={direction}
          >
            {currentConceptName
              ? `${currentConceptName} · ${t('child.skillTree.unitOf', {
                  current: firstInProgressConceptIdx + 1,
                  total: concepts.length,
                })} · ${t('child.skillTree.mastery', { percent: masteryPct })}`
              : t('child.skillTree.mastery', { percent: masteryPct })}
          </Text>
        </TamStack>
      ) : null}

      <ScrollView
        contentContainerStyle={{
          paddingHorizontal: 16,
          paddingBottom: insets.bottom + 24,
          paddingTop: 24,
          alignItems: 'center',
        }}
        showsVerticalScrollIndicator={false}
      >
        {/* Loading skeleton */}
        {isLoading ? (
          <YStack gap="$8" alignItems="center" width="100%">
            {[0, 1].map((ci) => (
              <YStack key={ci} alignItems="center" gap="$6">
                <TamStack height={16} width={120} borderRadius="$card" backgroundColor="$cardSoft" />
                {[0, 1, 2].map((si) => (
                  <TamStack
                    key={si}
                    width={72}
                    height={72}
                    borderRadius={36}
                    backgroundColor="$cardSoft"
                  />
                ))}
              </YStack>
            ))}
          </YStack>
        ) : isError ? (
          /* Error + retry */
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
              onPress={() => treeQuery.refetch()}
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
        ) : concepts.length === 0 ? (
          /* Empty */
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
          /* Tree: concepts → skills */
          <YStack gap="$8" alignItems="center" width="100%">
            {concepts.map((concept, conceptIdx) => {
              const skills = concept.skills ?? [];
              return (
                <YStack
                  key={concept.conceptId ?? conceptIdx}
                  alignItems="center"
                  gap={0}
                  width="100%"
                >
                  {/* Concept eyebrow */}
                  <Text
                    color="$primaryLight"
                    fontSize={12}
                    fontWeight="700"
                    textTransform="uppercase"
                    letterSpacing={0.04 * 12}
                    fontFamily="$heading"
                    textAlign="center"
                    marginBottom="$2"
                    writingDirection={direction}
                  >
                    {t('child.skillTree.conceptEyebrow', { name: concept.name ?? '' })}
                  </Text>

                  {/* Skills column (centered, with connector strips) */}
                  <YStack alignItems="center" gap={0}>
                    {skills.map((skill: SkillNodeDto, skillIdx) => {
                      const stateVal = nodeStateValue(skill.state);
                      const hasBoss = bossSkillIds.has(skill.skillId ?? -1);
                      const isLast = skillIdx === skills.length - 1;
                      const nextState =
                        !isLast ? nodeStateValue(skills[skillIdx + 1]?.state) : 0;
                      const connectorState: 'reachable' | 'locked' =
                        nextState > 0 ? 'reachable' : 'locked';

                      const isLocked = stateVal === 0;

                      const a11yLabel = isLocked
                        ? `${skill.name ?? ''}, ${t('child.skillTree.subLocked')}, ${t('child.skillTree.a11y.lockedHint')}`
                        : hasBoss
                        ? `${skill.name ?? ''}, ${t('child.skillTree.bossLabel')}`
                        : `${skill.name ?? ''}`;

                      return (
                        <SkillTreeNode
                          key={skill.skillId ?? skillIdx}
                          skillId={skill.skillId ?? 0}
                          name={skill.name ?? ''}
                          state={stateVal}
                          hasBoss={hasBoss}
                          showConnectorBelow={!isLast}
                          connectorState={connectorState}
                          direction={direction}
                          accessibilityLabel={a11yLabel}
                          subCaptionMap={subCaptionMap}
                          onPress={() => {
                            const lessonIds = skill.lessonIds ?? [];
                            if (lessonIds.length > 0) {
                              router.push(
                                `/(child)/lessons/${lessonIds[0]}` as `/${string}`,
                              );
                            }
                          }}
                          onLockTap={() => {
                            setLockedSkill({
                              name: skill.name ?? '',
                              prereqs: skill.missingPrerequisites ?? [],
                            });
                          }}
                        />
                      );
                    })}
                  </YStack>
                </YStack>
              );
            })}
          </YStack>
        )}
      </ScrollView>

      {/* Why Locked Sheet */}
      <WhyLockedSheet
        open={lockedSkill !== null}
        onClose={() => setLockedSkill(null)}
        prerequisites={lockedSkill?.prereqs ?? []}
        lockedItemName={lockedSkill?.name ?? ''}
      />
    </TamStack>
  );
}
