/**
 * LessonCard — student-facing lesson card with three state variants + boss overlay.
 *
 * Design Spec §3.2 + §2. Composed from `preview/components-lesson-card.html`.
 *
 * States (sourced from LessonInUnitDto.state — NOT the obsolete isLocked field):
 *   0 = Locked   → dimmed, lock glyph badge, non-navigable, onLockTap fires.
 *   1 = Available → primary border + glow, active CTA chrome.
 *   2 = Completed → success border, completed treatment.
 *
 * Boss decoration (isBoss prop, orthogonal to state):
 *   Renders a BossBadge pill at the trailing-top corner of the card.
 *   Coexists with lock badge: boss sits below lock (top: 38, end: 14 on Locked;
 *   top: 14, end: 14 on Available/Completed).
 *
 * Motion:
 *   Available hover: brighten 8% + scale 1.02, 160ms.
 *   Press: scale 0.95, 80ms (except Locked).
 *   Locked: no scale, no pulse. Tap → onLockTap only.
 *
 * A11y: accessibilityRole="button", accessibilityState.disabled on Locked.
 *       Caller provides fully-localized accessibilityLabel.
 */
import React from 'react';
import { Stack, type StackProps } from '@tamagui/core';

import { YStack, XStack, Text } from '../../internal/primitives';
import { Badge } from '../Badge';

/** 0=Locked, 1=Available, 2=Completed — mirrors NodeState enum values. */
export type LessonState = 0 | 1 | 2;

export interface LessonCardProps {
  lessonId: number;
  /** Eyebrow tag — e.g. "Math · Numbers". Caller localizes. */
  tag: string;
  /** Lesson title (localized). */
  title: string;
  /** Meta strings (caller localizes; rendered with • separator). Pass [] to hide. */
  meta?: string[];
  /** 0=Locked, 1=Available, 2=Completed. From LessonInUnitDto.state. */
  state: LessonState;
  /** Optional progress 0..100; only rendered when state===1 OR state===2. */
  progressPercent?: number;
  /** Boss overlay. Orthogonal to state. */
  isBoss?: boolean;
  /** Stars earned (0..3) — rendered when state===2. */
  masteryStars?: number;
  /** Open the lesson player (Available/Completed). */
  onPress?: () => void;
  /** Open WhyLockedSheet (Locked). Caller passes a function that surfaces missingPrerequisites. */
  onLockTap?: () => void;
  direction?: 'ltr' | 'rtl';
  /** Already-localized a11y label. */
  accessibilityLabel: string;
  /** Localized tag label strings for each state. */
  tagLabel?: string;
  testID?: string;
}

const STATE_TAG_BG: Record<LessonState, StackProps['backgroundColor']> = {
  0: 'rgba(255,255,255,0.06)',
  1: '$primarySoft',
  2: '$successSoft',
};

// Use string type for tag text color since Stack doesn't have color prop
const STATE_TAG_COLOR: Record<LessonState, string> = {
  0: '$fg3',
  1: '$primary',
  2: '$success',
};

export function LessonCard({
  lessonId: _lessonId, // eslint-disable-line @typescript-eslint/no-unused-vars
  tag,
  title,
  meta,
  state,
  progressPercent,
  isBoss = false,
  masteryStars,
  onPress,
  onLockTap,
  direction = 'ltr',
  accessibilityLabel,
  tagLabel,
  testID,
}: LessonCardProps) {
  const isRtl = direction === 'rtl';
  const isLocked = state === 0;
  const isAvailable = state === 1;
  const isCompleted = state === 2;

  const pct = progressPercent !== undefined
    ? Math.max(0, Math.min(100, progressPercent))
    : undefined;

  const handlePress = () => {
    if (isLocked) {
      onLockTap?.();
    } else {
      onPress?.();
    }
  };

  const borderColor = isAvailable ? '$primary' : isCompleted ? '$success' : '$border';
  const borderWidth = isAvailable ? 2 : 1;

  const hasShadow = isAvailable;

  return (
    <Stack
      testID={testID}
      width="100%"
      borderRadius="$modal"
      backgroundColor="$card"
      borderWidth={borderWidth}
      borderColor={borderColor}
      shadowColor={hasShadow ? 'rgba(99,102,241,0.45)' : 'rgba(0,0,0,0.15)'}
      shadowRadius={hasShadow ? 24 : 12}
      shadowOffset={{ width: 0, height: hasShadow ? 8 : 4 }}
      shadowOpacity={1}
      overflow="visible"
      cursor={isLocked ? 'not-allowed' : 'pointer'}
      hoverStyle={
        isAvailable
          ? { backgroundColor: '$cardSoft', scale: 1.02 }
          : isCompleted
          ? { backgroundColor: '$cardSoft' }
          : undefined
      }
      pressStyle={isLocked ? undefined : { scale: 0.95 }}
      onPress={handlePress}
      accessibilityRole="button"
      accessible
      accessibilityState={{ disabled: isLocked }}
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
    >
      {/* Content with conditional opacity on Locked */}
      <YStack
        padding={18}
        gap={12}
        opacity={isLocked ? 0.55 : 1}
      >
        {/* Tag pill (eyebrow) */}
        <XStack flexDirection={isRtl ? 'row-reverse' : 'row'} alignItems="center">
          <Stack
            paddingHorizontal={10}
            paddingVertical={4}
            borderRadius={9999}
            backgroundColor={STATE_TAG_BG[state]}
            alignSelf="flex-start"
          >
            <Text
              // eslint-disable-next-line @typescript-eslint/no-explicit-any
              color={STATE_TAG_COLOR[state] as any}
              fontSize={10}
              fontWeight="700"
              textTransform="uppercase"
              letterSpacing={0.4}
              fontFamily="$heading"
            >
              {tagLabel ?? tag}
            </Text>
          </Stack>
        </XStack>

        {/* Title */}
        <Text
          color={isLocked ? '$fg3' : '$fg1'}
          fontSize={18}
          fontWeight="800"
          fontFamily="$heading"
          lineHeight={22}
          writingDirection={direction}
          numberOfLines={2}
        >
          {title}
        </Text>

        {/* Meta row */}
        {meta && meta.length > 0 ? (
          <XStack
            flexDirection={isRtl ? 'row-reverse' : 'row'}
            alignItems="center"
            gap={10}
            flexWrap="wrap"
          >
            {meta.map((item, idx) => (
              <React.Fragment key={idx}>
                {idx > 0 ? (
                  <Stack
                    width={3}
                    height={3}
                    borderRadius={9999}
                    backgroundColor="$fg3"
                    opacity={0.6}
                    accessibilityElementsHidden
                  />
                ) : null}
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontWeight="500"
                  fontFamily="$body"
                  writingDirection={direction}
                >
                  {item}
                </Text>
              </React.Fragment>
            ))}
          </XStack>
        ) : null}

        {/* Stars (Completed) */}
        {isCompleted && masteryStars !== undefined ? (
          <XStack flexDirection={isRtl ? 'row-reverse' : 'row'} gap={2}>
            {[0, 1, 2].map((i) => (
              <Text
                key={i}
                fontSize={12}
                color={i < (masteryStars ?? 0) ? '$xp' : '$fg4'}
                accessibilityElementsHidden
              >
                ⭐
              </Text>
            ))}
          </XStack>
        ) : null}

        {/* Progress bar (Available or Completed, only when progress is provided) */}
        {!isLocked && pct !== undefined ? (
          <Stack
            height={8}
            borderRadius={9999}
            backgroundColor="$bg"
            overflow="hidden"
            flexDirection="row"
          >
            <Stack
              width={`${isCompleted ? 100 : pct}%` as StackProps['width']}
              height="100%"
              borderRadius={9999}
              backgroundColor={isCompleted ? '$success' : undefined}
            >
              {!isCompleted ? (
                /* Fill gradient from preview: indigo→purple (components-lesson-card.html) */
                <Stack
                  flex={1}
                  style={{ background: 'linear-gradient(90deg, #4F46E5, #A855F7)' } as object}
                />
              ) : null}
            </Stack>
          </Stack>
        ) : null}
      </YStack>

      {/* Lock badge — floats at trailing-top corner (logical) */}
      {isLocked ? (
        <Text
          position="absolute"
          top={14}
          end={14}
          fontSize={18}
          color="$fg3"
          accessibilityElementsHidden
        >
          🔒
        </Text>
      ) : null}

      {/* Boss badge — trailing-top, shifts down if lock badge is also present */}
      {isBoss ? (
        <Stack
          position="absolute"
          top={isLocked ? 38 : 14}
          end={14}
        >
          <Badge
            variant="boss"
            label="Boss"
            accessibilityLabel=""
          />
        </Stack>
      ) : null}
    </Stack>
  );
}

LessonCard.displayName = 'LessonCard';
