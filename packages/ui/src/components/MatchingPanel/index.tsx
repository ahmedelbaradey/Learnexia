/**
 * MatchingPanel — tap-to-pair Matching question renderer (C5 / CO-FE-5).
 *
 * Replaces the W12 "coming soon" stub with the real pairing interaction per
 * Design Spec `design-system/ui_kits/student-app/C5-matching-panel.md`:
 *
 *  - Two columns inside the QuestionCard body: prompt ("left") + answer
 *    ("right", rendered as served — the seed stores it in non-aligned order).
 *  - Tap a card to *arm* it; tap a card in the other column to form a pair.
 *    Tapping a paired card unpairs it. Tapping a second card in the same
 *    column moves the armed state. Order-agnostic (answer-first also pairs).
 *  - Pair identity = a number chip on both members (Eastern-Arabic in ar) —
 *    color-blind-safe, no per-pair color palette.
 *  - States: default / armed / paired / correct / wrong (uniform — the server
 *    returns attempt-level isCorrect only, spec G-2). Wrong = whole-panel
 *    shake (60ms ±6px ×2, RN Animated — skipped under reduce-motion).
 *
 * Controlled component: `pairs` + `onPairsChange`. The *armed* selection is
 * transient UI state and stays internal. The panel never builds the submit
 * payload — the lesson player owns the locked L1 JSON shape.
 *
 * Stub fallback: when `left`/`right` are missing/empty (malformed content),
 * renders the previous "coming soon" tile chrome (spec §4).
 *
 * A11y: every card `accessibilityRole="button"` with composed armed/paired
 * labels (label builders passed in — this package has no i18n dependency);
 * pair/unpair announced via a visually-hidden polite live region.
 */
import React, { useEffect, useRef, useState } from 'react';
import { Animated, Platform, Pressable } from 'react-native';

import { useReduceMotion } from '../../hooks/useReduceMotion';
import { Stack, XStack, YStack, Text } from '../../internal/primitives';

// ── Public types ────────────────────────────────────────────────────────────

/** One item of the prompt ("left") or answer ("right") column. */
export interface MatchingItem {
  /** Stable item id from the question content (CO-BE-1 contract — string). */
  id: string;
  /** Localized display text. */
  text: string;
}

/** One formed pair — mirrors the locked wire-contract pair entry. */
export interface MatchingPairValue {
  leftId: string;
  rightId: string;
}

/** Interaction phase — mirrors the lesson player's answer state machine. */
export type MatchingPanelPhase = 'answering' | 'locked' | 'correct' | 'wrong';

export interface MatchingPanelProps {
  /** Prompt-column items. Empty/missing → stub fallback tile. */
  left?: MatchingItem[];
  /** Answer-column items, rendered as served (pre-shuffled by the seed). */
  right?: MatchingItem[];
  /** Formed pairs (controlled). */
  pairs?: MatchingPairValue[];
  /** Called with the next pairs array on every pair / unpair. */
  onPairsChange?: (pairs: MatchingPairValue[]) => void;
  /** Drives chrome + interactivity. Default `answering`. */
  phase?: MatchingPanelPhase;
  direction?: 'ltr' | 'rtl';
  locale?: 'en' | 'ar';
  /** Localized one-line how-to shown above the columns (spec §6). */
  instructionText?: string;
  /** Localized column headers — only rendered when >3 pairs (spec §2). */
  promptHeaderText?: string;
  answerHeaderText?: string;
  /** Localized a11y label for an armed card: "{text}, selected, …". */
  armedA11yLabel?: (text: string) => string;
  /** Localized a11y label for a paired card: "{text}, paired with {partner}, …". */
  pairedA11yLabel?: (text: string, partner: string) => string;
  /** Localized live-region announcement on pair: "Paired: {a} with {b}". */
  pairedAnnounceText?: (a: string, b: string) => string;
  /** Localized live-region announcement on unpair: "Unpaired: {a} and {b}". */
  unpairedAnnounceText?: (a: string, b: string) => string;
  /** Stub-fallback copy (already localized) — used when content is malformed. */
  stubTitle?: string;
  stubSubTitle?: string;
  testID?: string;
}

// ── Internals ───────────────────────────────────────────────────────────────

type Side = 'left' | 'right';

const SIDES = { left: 'left', right: 'right' } as const;

type CardState = 'default' | 'armed' | 'paired' | 'correct' | 'wrong';

// Token deltas per spec §3 (literals transcribed from components-quiz.html).
const CARD_BG: Record<CardState, string> = {
  default: '$card',
  armed: '$primarySoft',
  paired: '$card',
  correct: '$successSoft',
  wrong: '$dangerSoft',
};

const CARD_BORDER: Record<CardState, string> = {
  default: '$border',
  armed: '$primary',
  paired: '$primary',
  correct: '$success',
  wrong: '$danger',
};

const CARD_TEXT: Record<CardState, string> = {
  default: '$fg1',
  armed: '$fg1',
  paired: '$fg1',
  correct: '$success',
  wrong: '$danger',
};

const ARABIC_DIGITS = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'] as const;

function toLocaleDigits(n: number, locale: 'en' | 'ar'): string {
  const s = String(n);
  if (locale !== 'ar') return s;
  return s.replace(/[0-9]/g, (d) => ARABIC_DIGITS[parseInt(d, 10)] ?? d);
}

export function MatchingPanel({
  left = [],
  right = [],
  pairs = [],
  onPairsChange,
  phase = 'answering',
  direction = 'ltr',
  locale = 'en',
  instructionText,
  promptHeaderText,
  answerHeaderText,
  armedA11yLabel,
  pairedA11yLabel,
  pairedAnnounceText,
  unpairedAnnounceText,
  stubTitle,
  stubSubTitle,
  testID,
}: MatchingPanelProps) {
  const isRtl = direction === 'rtl';
  const reduceMotion = useReduceMotion();

  // Armed (selected, awaiting partner) — transient, never leaves the panel.
  const [armed, setArmed] = useState<{ side: Side; id: string } | null>(null);

  // Polite live-region announcement on pair / unpair (spec §6).
  const [announcement, setAnnouncement] = useState('');

  // Wrong-answer shake: 60ms ±6px ×2 (spec §3). RN Animated like
  // AnswerFeedbackStrip (no Reanimated per W12). Skipped under reduce-motion.
  const shakeX = useRef(new Animated.Value(0)).current;
  useEffect(() => {
    if (phase !== 'wrong') return;
    setArmed(null);
    if (reduceMotion) return;
    const step = (toValue: number) =>
      Animated.timing(shakeX, {
        toValue,
        duration: 60,
        useNativeDriver: Platform.OS !== 'web',
      });
    Animated.sequence([step(6), step(-6), step(6), step(-6), step(0)]).start();
  }, [phase, reduceMotion, shakeX]);

  // Drop the armed state whenever interaction locks (submit fired).
  useEffect(() => {
    if (phase !== 'answering' && armed !== null) setArmed(null);
  }, [phase, armed]);

  // ── Stub fallback (malformed/empty content — spec §4) ────────────────────
  if (left.length === 0 || right.length === 0) {
    return (
      <YStack
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
        accessibilityLabel={`${stubTitle ?? ''}. ${stubSubTitle ?? ''}`}
        testID={testID}
      >
        <Text fontSize={32} accessibilityElementsHidden>
          {'🧩'}
        </Text>
        <Text
          fontSize={16}
          fontWeight="700"
          color="$fg2"
          fontFamily="$heading"
          textAlign="center"
          writingDirection={direction}
        >
          {stubTitle ?? ''}
        </Text>
        <Text
          fontSize={12}
          fontWeight="500"
          color="$fg3"
          fontFamily="$body"
          textAlign="center"
          writingDirection={direction}
        >
          {stubSubTitle ?? ''}
        </Text>
      </YStack>
    );
  }

  // ── Pair lookups ──────────────────────────────────────────────────────────
  const pairIndexOf = (side: Side, id: string): number =>
    pairs.findIndex((p) => (side === SIDES.left ? p.leftId === id : p.rightId === id));

  const partnerTextOf = (side: Side, id: string): string | null => {
    const idx = pairIndexOf(side, id);
    if (idx < 0) return null;
    const pair = pairs[idx]!;
    const partner =
      side === SIDES.left
        ? right.find((r) => r.id === pair.rightId)
        : left.find((l) => l.id === pair.leftId);
    return partner?.text ?? null;
  };

  // ── Tap logic (answering only) ────────────────────────────────────────────
  const handleTap = (side: Side, item: MatchingItem) => {
    if (phase !== 'answering') return;

    const existingIdx = pairIndexOf(side, item.id);
    if (existingIdx >= 0) {
      // Paired → unpair (both members return to default).
      const removed = pairs[existingIdx]!;
      const a = left.find((l) => l.id === removed.leftId)?.text ?? '';
      const b = right.find((r) => r.id === removed.rightId)?.text ?? '';
      onPairsChange?.(pairs.filter((_, i) => i !== existingIdx));
      setArmed(null);
      if (unpairedAnnounceText) setAnnouncement(unpairedAnnounceText(a, b));
      return;
    }

    if (armed === null || armed.side === side) {
      // Nothing armed, or same column → (re-)arm this card.
      setArmed({ side, id: item.id });
      return;
    }

    // Opposite column armed → form the pair (order-agnostic).
    const leftId = side === SIDES.left ? item.id : armed.id;
    const rightId = side === SIDES.right ? item.id : armed.id;
    // If the armed partner was somehow paired meanwhile, drop its pair first.
    const next = pairs.filter((p) => p.leftId !== leftId && p.rightId !== rightId);
    next.push({ leftId, rightId });
    onPairsChange?.(next);
    setArmed(null);
    if (pairedAnnounceText) {
      const a = left.find((l) => l.id === leftId)?.text ?? '';
      const b = right.find((r) => r.id === rightId)?.text ?? '';
      setAnnouncement(pairedAnnounceText(a, b));
    }
  };

  // ── Card renderer ─────────────────────────────────────────────────────────
  const renderCard = (side: Side, item: MatchingItem, index: number) => {
    const pairIdx = pairIndexOf(side, item.id);
    const isPaired = pairIdx >= 0;
    const isArmed = armed?.side === side && armed.id === item.id;

    const state: CardState =
      phase === 'correct'
        ? 'correct'
        : phase === 'wrong'
          ? 'wrong'
          : isArmed
            ? 'armed'
            : isPaired
              ? 'paired'
              : 'default';

    const interactive = phase === 'answering';

    // Pair-number chip (both members show the same number) — 20×20 $pill,
    // $primarySoft bg, $primaryLight 11/800 (spec §2). Correct: "✓" appended.
    const chip = isPaired ? (
      <Stack
        minWidth={20}
        height={20}
        paddingHorizontal={4}
        borderRadius="$pill"
        backgroundColor={state === 'correct' ? '$successSoft' : state === 'wrong' ? '$dangerSoft' : '$primarySoft'}
        alignItems="center"
        justifyContent="center"
        accessibilityElementsHidden
      >
        <Text
          fontSize={11}
          fontWeight="800"
          color={state === 'correct' ? '$success' : state === 'wrong' ? '$danger' : '$primaryLight'}
          fontFamily="$heading"
          style={{ fontVariant: ['tabular-nums'] }}
        >
          {`${toLocaleDigits(pairIdx + 1, locale)}${state === 'correct' ? '✓' : ''}`}
        </Text>
      </Stack>
    ) : (
      // Empty placeholder dot — 8×8 $borderStrong (spec §2).
      <Stack
        width={8}
        height={8}
        borderRadius="$pill"
        backgroundColor="$borderStrong"
        accessibilityElementsHidden
      />
    );

    const partnerText = isPaired ? partnerTextOf(side, item.id) : null;
    const a11yLabel = isArmed
      ? (armedA11yLabel?.(item.text) ?? item.text)
      : isPaired && partnerText
        ? (pairedA11yLabel?.(item.text, partnerText) ?? item.text)
        : item.text;

    const card = (
      <Stack
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        backgroundColor={CARD_BG[state] as any}
        borderWidth={2}
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        borderColor={CARD_BORDER[state] as any}
        borderRadius="$button"
        paddingVertical={14}
        paddingHorizontal={16}
        minHeight={48}
        $tablet={{ minHeight: 56 }}
        justifyContent="center"
        pointerEvents={interactive ? 'auto' : 'none'}
        {...(interactive && state === 'default'
          ? { hoverStyle: { backgroundColor: '$cardSoft' } }
          : null)}
        testID={`${testID ?? 'quiz-matching'}-${side}-${index}`}
      >
        <XStack
          flexDirection={isRtl ? 'row-reverse' : 'row'}
          alignItems="center"
          justifyContent="space-between"
          gap={10}
        >
          <Text
            flex={1}
            fontSize={16}
            fontWeight="600"
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            color={CARD_TEXT[state] as any}
            fontFamily="$heading"
            numberOfLines={2}
            textAlign={isRtl ? 'right' : 'left'}
            writingDirection={direction}
          >
            {item.text}
          </Text>
          {chip}
        </XStack>
      </Stack>
    );

    if (!interactive) {
      // Locked / feedback: non-interactive but still readable by SRs.
      return (
        <Stack
          key={item.id}
          accessible
          accessibilityRole="button"
          accessibilityState={{ disabled: true, selected: isPaired }}
          accessibilityLabel={a11yLabel}
          aria-label={a11yLabel}
          aria-disabled
        >
          {card}
        </Stack>
      );
    }

    return (
      <Pressable
        key={item.id}
        onPress={() => handleTap(side, item)}
        accessible
        accessibilityRole="button"
        accessibilityState={{ selected: isArmed || isPaired }}
        accessibilityLabel={a11yLabel}
        aria-label={a11yLabel}
        style={({ pressed }) => ({
          transform: [{ scale: pressed && !reduceMotion ? 0.95 : 1 }],
        })}
      >
        {card}
      </Pressable>
    );
  };

  const showHeaders = left.length > 3 && Boolean(promptHeaderText) && Boolean(answerHeaderText);

  const renderHeader = (label: string | undefined) =>
    label ? (
      <Text
        fontSize={11}
        fontWeight="700"
        color="$fg3"
        fontFamily="$heading"
        textTransform="uppercase"
        letterSpacing={0.44}
        writingDirection={direction}
        textAlign={isRtl ? 'right' : 'left'}
      >
        {label}
      </Text>
    ) : null;

  return (
    <YStack gap={12} testID={testID}>
      {/* One-sentence how-to — 12/500 $fg3 (spec §5/§6). */}
      {instructionText ? (
        <Text
          fontSize={12}
          fontWeight="500"
          color="$fg3"
          fontFamily="$body"
          writingDirection={direction}
          textAlign={isRtl ? 'right' : 'left'}
        >
          {instructionText}
        </Text>
      ) : null}

      <Animated.View style={{ transform: [{ translateX: shakeX }] }}>
        <XStack flexDirection={isRtl ? 'row-reverse' : 'row'} gap={10} alignItems="flex-start">
          {/* Prompt column (logical start — right side in AR). */}
          <YStack flex={1} gap={10}>
            {showHeaders ? renderHeader(promptHeaderText) : null}
            {left.map((item, i) => renderCard(SIDES.left, item, i))}
          </YStack>
          {/* Answer column — rendered as served (non-aligned order). */}
          <YStack flex={1} gap={10}>
            {showHeaders ? renderHeader(answerHeaderText) : null}
            {right.map((item, i) => renderCard(SIDES.right, item, i))}
          </YStack>
        </XStack>
      </Animated.View>

      {/* Visually-hidden polite live region — pair/unpair announcements. */}
      <Text
        accessibilityLiveRegion="polite"
        aria-live="polite"
        style={{ position: 'absolute', width: 1, height: 1, opacity: 0 }}
      >
        {announcement}
      </Text>
    </YStack>
  );
}

MatchingPanel.displayName = 'MatchingPanel';
