/**
 * SkillTreeNode — circular disc node for the skill tree, with 4 visual states.
 *
 * Design Spec §3.3 + §2. Composed from `preview/components-skill-node.html` +
 * `preview/mobile-skill-path.html` + `screenshots/mobile/09-skill-tree.png`.
 *
 * States:
 *   Locked (state=0): grey $cardSoft disc + 🔒 glyph + "Locked" sub-caption.
 *   Available (state=1): purple-gradient disc + ✏️ glyph + "In progress" sub-caption + pulse loop.
 *   Completed (state=2): green-gradient disc + ✓ glyph + 3-star row + "Mastered" sub-caption.
 *   Boss (hasBoss=true): boss chrome overrides disc color, state determines glyph.
 *     Boss+Locked → 🔒 glyph, boss disc, no pulse, no glow.
 *     Boss+Available → 🔥 glyph, boss disc, pulse + boss glow.
 *     Boss+Completed → ✓ glyph, boss disc, 3-star row.
 *
 * Connector strip (optional, rendered BELOW the node):
 *   showConnectorBelow=true → 4×24 Stack centered horizontally.
 *   connectorState='reachable' → $borderStrong. 'locked' → $border.
 *
 * Motion (Available pulse): CSS @keyframes on web, Reanimated on native.
 *   Acceptable W11 degradation: if Reanimated is not available, no pulse on native.
 *
 * A11y: accessibilityRole="button", accessibilityState.disabled on Locked.
 */
import React from 'react';
import { Platform } from 'react-native';
import { Stack } from '@tamagui/core';

import { YStack, Text } from '../../internal/primitives';

export type SkillNodeState = 0 | 1 | 2;

export interface SkillTreeNodeProps {
  skillId: number;
  /** Localized name. */
  name: string;
  /** 0=Locked, 1=Available, 2=Completed. From SkillNodeDto.state. */
  state: SkillNodeState;
  /** Derived on FE by joining subjectLessons × skillTree on skillId. */
  hasBoss?: boolean;
  /** 0..3 stars; rendered as ⭐ row when state===2 (or Boss+Completed). */
  masteryStars?: number;
  /** Open the lesson player (Available/Completed/Boss-X). */
  onPress?: () => void;
  /** Open WhyLockedSheet (Locked / Boss+Locked). */
  onLockTap?: () => void;
  /** Render a connector strip BELOW this node. The last node in a Concept passes false. */
  showConnectorBelow?: boolean;
  /** Token-aware connector tint based on the NEXT node's state. */
  connectorState?: 'reachable' | 'locked';
  direction?: 'ltr' | 'rtl';
  accessibilityLabel: string;
  /** Already-localized sub-captions for state display. */
  subCaptionMap?: {
    locked: string;
    available: string;
    completed: string;
    bossChallenge: string;
    bossBeaten: string;
  };
  testID?: string;
}

// Disc visual specs per state + boss composition (design spec §2)
function getDiscStyle(state: SkillNodeState, hasBoss: boolean) {
  if (hasBoss) {
    // Boss chrome: danger/red family gradient
    return {
      bg: 'rgba(239,68,68,0.55)' as const, // approximation of radial — flat fallback
      glyph: state === 0 ? '🔒' : state === 2 ? '✓' : '🔥',
      glyphColor: '#FFFFFF' as const,
      glyphSize: state === 1 ? 32 : 30,
      shadowColor: state === 0 ? 'transparent' : 'rgba(239,68,68,0.55)',
      shadowRadius: 28,
      hasPulse: state === 1,
    };
  }
  switch (state) {
    case 0:
      return {
        bg: '$cardSoft' as const,
        glyph: '🔒',
        glyphColor: '$fg4' as string,
        glyphSize: 28,
        shadowColor: 'transparent',
        shadowRadius: 0,
        hasPulse: false,
      };
    case 1:
      return {
        bg: '#4F46E5' as const, // $primary — flat approximation of radial gradient
        glyph: '✏️',
        glyphColor: '#FFFFFF' as const,
        glyphSize: 30,
        shadowColor: 'rgba(99,102,241,0.6)',
        shadowRadius: 28,
        hasPulse: true,
      };
    case 2:
    default:
      return {
        bg: '#22C55E' as const, // $success — flat approximation of radial gradient
        glyph: '✓',
        glyphColor: '#FFFFFF' as const,
        glyphSize: 30,
        shadowColor: 'rgba(34,197,94,0.45)',
        shadowRadius: 20,
        hasPulse: false,
      };
  }
}

const DEFAULT_SUBCAPTIONS = {
  locked: 'Locked',
  available: 'In progress',
  completed: 'Mastered',
  bossChallenge: 'Boss Challenge',
  bossBeaten: 'Boss Beaten',
};

// Map numeric state to the string used by data-state attribute.
function stateToDataState(state: SkillNodeState, hasBoss: boolean): string {
  if (hasBoss) return state === 0 ? 'locked' : state === 2 ? 'completed' : 'boss';
  if (state === 0) return 'locked';
  if (state === 2) return 'completed';
  return 'available';
}

export function SkillTreeNode({
  skillId: _skillId,
  name,
  state,
  hasBoss = false,
  masteryStars,
  onPress,
  onLockTap,
  showConnectorBelow = false,
  connectorState = 'locked',
  direction = 'ltr',
  accessibilityLabel,
  subCaptionMap,
  testID,
}: SkillTreeNodeProps) {
  const isLocked = state === 0;
  const isCompleted = state === 2;

  // Web-only data attributes for E2E state assertions (non-user-facing).
  const dataStateAttr = Platform.OS === 'web' ? stateToDataState(state, hasBoss) : undefined;
  const dataBossAttr = Platform.OS === 'web' && hasBoss ? 'true' : undefined;

  const captions = subCaptionMap ?? DEFAULT_SUBCAPTIONS;
  const discStyle = getDiscStyle(state, hasBoss);

  const subCaption = hasBoss
    ? isCompleted
      ? captions.bossBeaten
      : isLocked
      ? captions.locked
      : captions.bossChallenge
    : state === 0
    ? captions.locked
    : state === 2
    ? captions.completed
    : captions.available;

  const subCaptionColor = hasBoss
    ? isLocked
      ? '$fg3'
      : '$streak'
    : state === 0
    ? '$fg3'
    : state === 1
    ? '$primaryLight'
    : '$fg1';

  const labelColor = hasBoss
    ? isLocked
      ? '$fg3'
      : '$streak'
    : state === 0
    ? '$fg3'
    : '$fg1';

  const handlePress = () => {
    if (isLocked) {
      onLockTap?.();
    } else {
      onPress?.();
    }
  };

  // CSS pulse animation style for web (Available state, not Boss+Locked)
  const pulseStyle =
    Platform.OS === 'web' && discStyle.hasPulse
      ? ({
          animationName: 'lx-skill-pulse',
          animationDuration: '2000ms',
          animationTimingFunction: 'ease-out',
          animationIterationCount: 'infinite',
        } as Record<string, unknown>)
      : undefined;

  return (
    <YStack
      testID={testID}
      alignItems="center"
      gap={6}
      // @ts-ignore — data-* attributes are web-only, non-user-facing E2E hooks
      data-state={dataStateAttr}
      // @ts-ignore — data-* attributes are web-only, non-user-facing E2E hooks
      data-boss={dataBossAttr}
    >
      {/* Disc */}
      <Stack
        width={72}
        height={72}
        borderRadius={36}
        backgroundColor={discStyle.bg}
        alignItems="center"
        justifyContent="center"
        // @ts-ignore — raw shadowColor string not typed as a token
      shadowColor={discStyle.shadowColor}
        shadowRadius={discStyle.shadowRadius}
        shadowOffset={{ width: 0, height: discStyle.hasPulse ? 0 : 8 }}
        shadowOpacity={discStyle.shadowColor === 'transparent' ? 0 : 1}
        borderWidth={hasBoss && !isLocked ? 2 : 0}
        borderColor={hasBoss && !isLocked ? '$streak' : 'transparent'}
        cursor={isLocked ? 'not-allowed' : 'pointer'}
        hoverStyle={isLocked ? undefined : { scale: 1.02 }}
        pressStyle={isLocked ? undefined : { scale: 0.95 }}
        onPress={handlePress}
        accessibilityRole="button"
        accessible
        accessibilityState={{ disabled: isLocked }}
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
        // @ts-ignore — platform-specific style for CSS keyframe pulse
        style={pulseStyle}
      >
        <Text
          fontSize={discStyle.glyphSize}
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          color={discStyle.glyphColor as any}
          accessibilityElementsHidden
        >
          {discStyle.glyph}
        </Text>
      </Stack>

      {/* Label */}
      <Text
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        color={labelColor as any}
        fontSize={12}
        fontWeight="700"
        fontFamily="$heading"
        textAlign="center"
        maxWidth={96}
        numberOfLines={2}
        writingDirection={direction}
      >
        {name}
      </Text>

      {/* Sub-caption */}
      <Text
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        color={subCaptionColor as any}
        fontSize={10}
        fontWeight={state === 1 ? '700' : '500'}
        fontFamily="$body"
        textAlign="center"
        writingDirection={direction}
      >
        {subCaption}
      </Text>

      {/* Stars row (Completed or Boss+Completed) */}
      {(isCompleted || (hasBoss && isCompleted)) ? (
        <Stack flexDirection="row" gap={2}>
          {[0, 1, 2].map((i) => (
            <Text
              key={i}
              fontSize={10}
              color={i < (masteryStars ?? 0) ? '$xp' : '$fg4'}
              accessibilityElementsHidden
            >
              ⭐
            </Text>
          ))}
        </Stack>
      ) : null}

      {/* Connector strip below (last node in concept does not render this) */}
      {showConnectorBelow ? (
        <Stack
          width={4}
          height={24}
          borderRadius={2}
          backgroundColor={connectorState === 'reachable' ? '$borderStrong' : '$border'}
          marginTop={12}
          marginBottom={12}
          alignSelf="center"
        />
      ) : null}
    </YStack>
  );
}

SkillTreeNode.displayName = 'SkillTreeNode';
