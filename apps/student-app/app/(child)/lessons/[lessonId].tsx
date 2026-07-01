/**
 * Lesson Player — single route, 3-stage view-state machine.
 *
 * Stages: intro → quiz → summary.
 *
 * - Intro: lesson GET + Start CTA. On Start → useStartAttempt → quiz.
 * - Quiz: question-by-question walk. 4 question types via plain switch(questionType).
 *   Correct answer: auto-advance after 800ms. Incorrect: explicit "Next" CTA.
 *   On last question advance → useCompleteAttempt → summary.
 * - Summary: AttemptSummaryCard + "Back" + "Try again" CTAs.
 *
 * Back-gesture from quiz fires useAbandonAttempt (fire-and-forget, idempotent).
 * Back-gesture from summary does NOT fire Abandon (attempt is Completed).
 *
 * URL: /(child)/lessons/[lessonId]?subjectId={id}
 * The ?subjectId param is passed from the Lessons tab so "Back to subject" can
 * navigate without a unit→subject lookup (Pipeline Brief §5 OQ6).
 *
 * Design Spec §1 (Surfaces 1–3) + Pipeline Brief §3 (acceptance criteria).
 * P2-05-FE · P2-06-FE · P2-07-FE · Wave 12.
 */
import React, { useState, useEffect, useRef, useCallback } from 'react';
import { ScrollView, Pressable } from 'react-native';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import type { StackProps } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useQueryClient } from '@tanstack/react-query';

import {
  useLesson,
  useStartAttempt,
  useSubmitAnswer,
  useCompleteAttempt,
  useAbandonAttempt,
  QuestionType,
  queryKeys,
  type QuizQuestionDto,
  type AttemptSummaryDto,
  type SubmitAnswerResponse,
} from '@learnexia/api-client';

import { gradientStops } from '@learnexia/design-system';

import {
  Hearts,
  QuestionCard,
  MCQOption,
  TrueFalseChoice,
  FillInBlank,
  MatchingPanel,
  AnswerFeedbackStrip,
  AttemptSummaryCard,
  Button,
  GradientBox,
  type MatchingItem,
  type MatchingPairValue,
  type MatchingPanelPhase,
} from '@learnexia/ui';

import { useLocale } from '../../../src/hooks/useLocale';

// ── Local styled primitives ─────────────────────────────────────────────────
const YStack = styled(TamStack, { flexDirection: 'column' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

// ── State types (plain discriminated union — no XState/library) ─────────────

type AnswerState =
  | { phase: 'answering'; selectedValue: string | null }
  | { phase: 'submitting' }
  | { phase: 'feedback'; isCorrect: boolean; correctAnswer: string | null; selectedValue: string };

type Stage =
  | { kind: 'intro' }
  | {
      kind: 'quiz';
      attemptId: number;
      questions: QuizQuestionDto[];
      currentIndex: number;
      answerState: AnswerState;
    }
  | { kind: 'summary'; summary: AttemptSummaryDto };

// ── Helpers ─────────────────────────────────────────────────────────────────

function parseOptions(optionsJson: string | undefined | null): string[] {
  if (!optionsJson) return [];
  try {
    const parsed = JSON.parse(optionsJson);
    if (Array.isArray(parsed)) return parsed.map(String);
    return [];
  } catch {
    return [];
  }
}

// ── Matching (C5) helpers — CO-BE-1 wire contract ───────────────────────────
// Content shape: {"left":[{id,text}],"right":[{id,text}]} (stable string ids;
// "right" is served in non-aligned order — rendered as served, never realigned).

interface MatchingContent {
  left: MatchingItem[];
  right: MatchingItem[];
}

/** Defensive content parse (like parseOptions) — null → stub fallback tile. */
function parseMatchingContent(optionsJson: string | undefined | null): MatchingContent | null {
  if (!optionsJson) return null;
  try {
    const parsed: unknown = JSON.parse(optionsJson);
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) return null;
    const { left, right } = parsed as Record<string, unknown>;

    const mapItems = (value: unknown): MatchingItem[] | null => {
      if (!Array.isArray(value) || value.length === 0) return null;
      const items: MatchingItem[] = [];
      for (const entry of value) {
        if (typeof entry !== 'object' || entry === null) return null;
        const { id, text } = entry as Record<string, unknown>;
        if (typeof id !== 'string' && typeof id !== 'number') return null;
        if (typeof text !== 'string' && typeof text !== 'number') return null;
        items.push({ id: String(id), text: String(text) });
      }
      return items;
    };

    const leftItems = mapItems(left);
    const rightItems = mapItems(right);
    if (!leftItems || !rightItems) return null;
    return { left: leftItems, right: rightItems };
  } catch {
    return null;
  }
}

/**
 * Parse the pairs array carried in `AnswerState.selectedValue` for Matching
 * questions (the existing string slot holds `JSON.stringify(pairs)`; the full
 * locked L1 payload is only assembled at submit time in handleSubmit).
 */
function parseSelectedPairs(value: string | null | undefined): MatchingPairValue[] {
  if (!value) return [];
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];
    const pairs: MatchingPairValue[] = [];
    for (const entry of parsed) {
      if (typeof entry !== 'object' || entry === null) return [];
      const { leftId, rightId } = entry as Record<string, unknown>;
      if (typeof leftId !== 'string' || typeof rightId !== 'string') return [];
      pairs.push({ leftId, rightId });
    }
    return pairs;
  } catch {
    return [];
  }
}

/**
 * Format the server's Matching correctAnswer ({"pairs":[{leftId,rightId}]})
 * as kid-readable text ("1 – one, 2 – two") for the feedback strip reveal.
 * Returns null when it can't be resolved against the content (reveal omitted).
 */
function formatMatchingCorrectAnswer(
  correctJson: string,
  content: MatchingContent,
  t: (key: string, opts?: Record<string, unknown>) => string,
): string | null {
  try {
    const parsed: unknown = JSON.parse(correctJson);
    if (typeof parsed !== 'object' || parsed === null) return null;
    const { pairs } = parsed as Record<string, unknown>;
    if (!Array.isArray(pairs) || pairs.length === 0) return null;
    const parts: string[] = [];
    for (const entry of pairs) {
      if (typeof entry !== 'object' || entry === null) return null;
      const { leftId, rightId } = entry as Record<string, unknown>;
      const leftText = content.left.find((i) => i.id === String(leftId))?.text;
      const rightText = content.right.find((i) => i.id === String(rightId))?.text;
      if (!leftText || !rightText) return null;
      parts.push(t('quiz.matching.pairFormat', { left: leftText, right: rightText }));
    }
    return parts.join(t('quiz.matching.pairJoin'));
  } catch {
    return null;
  }
}

const ARABIC_DIGITS = ['٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩'] as const;

function toArabicNumerals(n: string): string {
  return n.replace(/[0-9]/g, (d) => ARABIC_DIGITS[parseInt(d)] ?? d);
}

// ── Main screen ─────────────────────────────────────────────────────────────

export default function LessonScreen() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const queryClient = useQueryClient();

  const { lessonId: lessonIdParam, subjectId: subjectIdParam } =
    useLocalSearchParams<{ lessonId: string; subjectId: string }>();

  const lessonId = parseInt(lessonIdParam ?? '0', 10);
  const subjectId = parseInt(subjectIdParam ?? '0', 10);

  // ── Stage state ──────────────────────────────────────────────────────────
  const [stage, setStage] = useState<Stage>({ kind: 'intro' });
  const [submitError, setSubmitError] = useState<string | null>(null);

  // Per-question timestamp for timeSpentSeconds (and Matching's payload timeMs).
  const questionStartRef = useRef<number>(Date.now());

  // Value snapshot while phase === 'submitting' (which carries no selectedValue)
  // so the MatchingPanel keeps its pair chips readable during the round-trip
  // (Design Spec §3 "Locked"), without changing the AnswerState shape.
  const submittedValueRef = useRef<string | null>(null);

  // Auto-advance timer ref — cleared on unmount / back.
  const advanceTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // ── API hooks ────────────────────────────────────────────────────────────
  const lessonQuery = useLesson(lessonId);
  const startAttemptMutation = useStartAttempt();
  const completeAttemptMutation = useCompleteAttempt();
  const abandonAttemptMutation = useAbandonAttempt();

  // submitAnswer hook is instantiated per-attempt; we keep the attemptId in
  // state and pass 0 initially — the mutation is only called after attemptId
  // is set, so this is safe.
  const currentAttemptId = stage.kind === 'quiz' ? stage.attemptId : 0;
  const submitAnswerMutation = useSubmitAnswer(currentAttemptId);

  // ── Abandon on unmount during quiz ───────────────────────────────────────
  const stageRef = useRef(stage);
  stageRef.current = stage;

  useEffect(() => {
    return () => {
      // Clear any pending auto-advance timer.
      if (advanceTimerRef.current) clearTimeout(advanceTimerRef.current);
      // Fire abandon only if mid-quiz (not summary — attempt is Completed).
      if (stageRef.current.kind === 'quiz') {
        const { attemptId } = stageRef.current;
        // Fire-and-forget — do not await (this is cleanup).
        abandonAttemptMutation.mutate({ attemptId });
      }
    };
  }, []);  // intentional empty deps — runs once on mount; cleanup fires on unmount only

  // ── advanceOrComplete ────────────────────────────────────────────────────
  const advanceOrComplete = useCallback(
    (quiz: Extract<Stage, { kind: 'quiz' }>) => {
      const { attemptId, questions, currentIndex } = quiz;

      if (currentIndex + 1 < questions.length) {
        // Advance to next question.
        questionStartRef.current = Date.now();
        setStage({
          kind: 'quiz',
          attemptId,
          questions,
          currentIndex: currentIndex + 1,
          answerState: { phase: 'answering', selectedValue: null },
        });
        setSubmitError(null);
      } else {
        // Last question — complete the attempt.
        completeAttemptMutation.mutate(
          { attemptId },
          {
            onSuccess: (summary) => {
              // Invalidate subject-lessons cache so the Lessons tab reflects
              // the now-Completed lesson on next focus.
              if (subjectId > 0) {
                queryClient.invalidateQueries({
                  queryKey: queryKeys.learning.subjectLessons(subjectId),
                });
              }
              // Also invalidate dashboard if it exists.
              queryClient.invalidateQueries({
                queryKey: queryKeys.learning.dashboard(),
              });
              setStage({ kind: 'summary', summary });
            },
          },
        );
      }
    },
    [completeAttemptMutation, queryClient, subjectId],
  );

  // ── handleSubmit ─────────────────────────────────────────────────────────
  const handleSubmit = useCallback(() => {
    if (stage.kind !== 'quiz') return;
    const { attemptId, questions, currentIndex, answerState } = stage;
    if (answerState.phase !== 'answering') return;

    const question = questions[currentIndex];
    if (!question?.id) return;

    // Matching (C5) carries pairs JSON in selectedValue and may legitimately be
    // null in the malformed-content fallback (submits an empty pair set).
    const isMatchingQuestion = question.questionType === QuestionType._3;
    if (!isMatchingQuestion && !answerState.selectedValue) return;

    const now = Date.now();
    const timeSpentSeconds = Math.max(
      0,
      Math.min(3600, Math.round((now - questionStartRef.current) / 1000)),
    );

    // Matching: wrap pairs into the LOCKED L1 payload shape (plan L1 —
    // byte-for-byte keys: pairs / attemptOrder / timeMs). Other types submit
    // selectedValue unchanged.
    const answerPayload = isMatchingQuestion
      ? JSON.stringify({
          pairs: parseSelectedPairs(answerState.selectedValue),
          attemptOrder: currentIndex + 1,
          timeMs: Math.max(0, now - questionStartRef.current),
        })
      : answerState.selectedValue!;

    submittedValueRef.current = answerState.selectedValue ?? '';
    setStage({
      kind: 'quiz',
      attemptId,
      questions,
      currentIndex,
      answerState: { phase: 'submitting' },
    });
    setSubmitError(null);

    submitAnswerMutation.mutate(
      {
        attemptId,
        questionId: question.id,
        answerPayload,
        timeSpentSeconds,
        hintUsed: false,
      },
      {
        onSuccess: (result: SubmitAnswerResponse) => {
          const isCorrect = result.isCorrect ?? false;
          const correctAnswer = result.correctAnswer ?? null;

          const nextAnswerState: AnswerState = {
            phase: 'feedback',
            isCorrect,
            correctAnswer,
            selectedValue: answerState.selectedValue ?? '',
          };

          setStage({
            kind: 'quiz',
            attemptId,
            questions,
            currentIndex,
            answerState: nextAnswerState,
          });

          if (isCorrect) {
            // Auto-advance after 800ms. Use stageRef to get the correct snapshot
            // at timer-fire time (avoids stale closure).
            if (advanceTimerRef.current) clearTimeout(advanceTimerRef.current);
            advanceTimerRef.current = setTimeout(() => {
              const currentStage = stageRef.current;
              if (currentStage.kind === 'quiz') {
                advanceOrComplete(currentStage);
              }
            }, 800);
          }
        },
        onError: () => {
          // Network error — revert to answering so user can retry.
          setStage({
            kind: 'quiz',
            attemptId,
            questions,
            currentIndex,
            answerState: { phase: 'answering', selectedValue: answerState.selectedValue },
          });
          setSubmitError(t('child.quiz.networkError'));
        },
      },
    );
  }, [stage, submitAnswerMutation, advanceOrComplete, t]);

  // ── handleStart ──────────────────────────────────────────────────────────
  const handleStart = useCallback(() => {
    startAttemptMutation.mutate(
      { lessonId },
      {
        onSuccess: (response) => {
          const questions = response.questions ?? [];
          if (questions.length === 0) {
            // Empty lesson — stay on intro but show empty state.
            // The intro renderer will detect no questions by checking a flag.
            // We keep stage as 'intro' and store a flag via a local state.
            setEmptyLesson(true);
            return;
          }
          questionStartRef.current = Date.now();
          setStage({
            kind: 'quiz',
            attemptId: response.attemptId ?? 0,
            questions,
            currentIndex: 0,
            answerState: { phase: 'answering', selectedValue: null },
          });
        },
      },
    );
  }, [lessonId, startAttemptMutation]);

  const [emptyLesson, setEmptyLesson] = useState(false);

  // ── handleRetry (Try Again) ──────────────────────────────────────────────
  const handleRetry = useCallback(() => {
    setStage({ kind: 'intro' });
    setEmptyLesson(false);
    setSubmitError(null);
    // Clear stale mutation state before re-firing.
    startAttemptMutation.reset();
    handleStart();
  }, [handleStart, startAttemptMutation]);

  // ── handleBack ───────────────────────────────────────────────────────────
  const handleBack = useCallback(() => {
    if (subjectId > 0) {
      router.replace(`/(child)/subjects/${subjectId}` as `/${string}`);
    } else if (router.canGoBack()) {
      router.back();
    } else {
      // No subjectId param AND no in-app history (e.g. direct deep-link without
      // ?subjectId — router.back() would silently no-op on web PWA). Fall back
      // to child home so the screen always exits (DEF-P205FE-02 guard).
      router.replace('/(child)/' as `/${string}`);
    }
  }, [router, subjectId]);

  // ── Render: progress label ────────────────────────────────────────────────
  const renderProgressLabel = (current: number, total: number) => {
    const currentStr = isRtl ? toArabicNumerals(String(current)) : String(current);
    const totalStr = isRtl ? toArabicNumerals(String(total)) : String(total);
    const raw = t('child.quiz.questionOf', { current: currentStr, total: totalStr });
    return raw;
  };

  // ── TopBar ───────────────────────────────────────────────────────────────
  const renderTopBar = (showProgress: boolean, progressCurrent?: number, progressTotal?: number) => {
    const hasProgressData =
      showProgress && progressCurrent !== undefined && progressTotal !== undefined;
    const progressRatio =
      hasProgressData && progressTotal! > 0
        ? Math.max(0, Math.min(1, progressCurrent! / progressTotal!))
        : 0;
    const progressPct = hasProgressData
      ? `${Math.round(progressRatio * 100)}%`
      : '';

    return (
      <YStack paddingHorizontal="$4" paddingBottom={hasProgressData ? '$2' : 0}>
        {/* Row 1: back chevron + hearts */}
        <TamStack
          height={56}
          flexDirection={isRtl ? 'row-reverse' : 'row'}
          justifyContent="space-between"
          alignItems="center"
        >
          {/* Back chevron */}
          <Pressable
            testID="lesson-back"
            onPress={() => {
              // quiz + summary use handleBack() (router.replace to the subject) so the
              // exit works even on a web deep-link / refresh where the in-app nav stack
              // is empty and router.back() would silently no-op (DEF-P205FE-02).
              // Abandon (quiz stage) still fires via the unmount cleanup.
              if (stage.kind === 'summary' || stage.kind === 'quiz') {
                handleBack();
              } else if (router.canGoBack()) {
                // Intro stage — guarded back: only call router.back() when there IS
                // a history entry; otherwise fall back to child home (deep-link guard).
                router.back();
              } else {
                router.replace('/(child)/' as `/${string}`);
              }
            }}
            accessibilityRole="button"
            accessibilityLabel={t('child.lessons.intro.back')}
            style={{ width: 44, height: 44, alignItems: 'center', justifyContent: 'center' }}
          >
            <Text color="$fg2" fontSize={22} fontFamily="$body">
              {isRtl ? '›' : '‹'}
            </Text>
          </Pressable>

          {/* Hearts — static count=3 this wave (Phase 3 wires live value). */}
          <Hearts
            current={3}
            maxHearts={3}
            locale={locale}
            accessibilityLabel={locale === 'ar' ? '٣ قلوب' : '3 of 3 hearts'}
          />
        </TamStack>

        {/* Row 2 + 3: gradient progress (quiz only) */}
        {hasProgressData ? (
          <YStack
            gap={6}
            accessible
            accessibilityRole="progressbar"
            accessibilityValue={{ now: progressCurrent, min: 1, max: progressTotal }}
            accessibilityLabel={renderProgressLabel(progressCurrent!, progressTotal!)}
            aria-label={renderProgressLabel(progressCurrent!, progressTotal!)}
            aria-valuenow={progressCurrent}
            aria-valuemin={1}
            aria-valuemax={progressTotal}
            testID="lesson-progress"
          >
            {/* Label row: "Question X of Y" + percentage */}
            <TamStack
              flexDirection={isRtl ? 'row-reverse' : 'row'}
              justifyContent="space-between"
              alignItems="center"
            >
              <Text
                color="$fg2"
                fontSize={13}
                fontWeight="700"
                fontFamily="$heading"
                writingDirection={direction}
              >
                {renderProgressLabel(progressCurrent!, progressTotal!)}
              </Text>
              <Text
                color="$fg3"
                fontSize={13}
                fontWeight="600"
                fontFamily="$heading"
              >
                {progressPct}
              </Text>
            </TamStack>

            {/* Gradient progress bar track */}
            {/* SKILL.md rule 4.6: progress bars stay LTR in all locales */}
            <TamStack
              height={4}
              borderRadius={9999}
              backgroundColor="$border"
              overflow="hidden"
              flexDirection="row"
            >
              <TamStack
                width={`${Math.round(progressRatio * 100)}%` as StackProps['width']}
                height="100%"
                borderRadius={9999}
                overflow="hidden"
              >
                <GradientBox
                  stops={gradientStops.gradReward.colors}
                  angle={90}
                  height="100%"
                  width="100%"
                />
              </TamStack>
            </TamStack>
          </YStack>
        ) : null}
      </YStack>
    );
  };

  // ── Render: MCQ renderer ─────────────────────────────────────────────────
  const renderMCQ = (
    question: QuizQuestionDto,
    answerState: AnswerState,
    onSelect: (val: string) => void,
  ) => {
    const options = parseOptions(question.options);
    const isLocked = answerState.phase !== 'answering';

    return (
      <YStack testID="quiz-renderer-mcq" gap="$3" accessibilityRole="radiogroup">
        {options.map((opt, idx) => {
          let optionState: 'default' | 'selected' | 'correct' | 'incorrect' | 'locked-default' = 'default';

          if (answerState.phase === 'answering') {
            optionState = answerState.selectedValue === opt ? 'selected' : 'default';
          } else if (answerState.phase === 'feedback') {
            const { isCorrect, correctAnswer, selectedValue } = answerState;
            if (opt === correctAnswer) {
              optionState = 'correct';
            } else if (opt === selectedValue && !isCorrect) {
              optionState = 'incorrect';
            } else {
              optionState = 'locked-default';
            }
          } else {
            // submitting
            optionState = 'locked-default';
          }

          const letter = String.fromCharCode(65 + idx); // A, B, C, D
          const stateLabel =
            optionState === 'selected'
              ? t('child.lessons.a11y.optionState.selected')
              : optionState === 'correct'
              ? t('child.lessons.a11y.optionState.correct')
              : optionState === 'incorrect'
              ? t('child.lessons.a11y.optionState.incorrect')
              : '';

          return (
            <MCQOption
              key={`${question.id}-${idx}`}
              testID={`quiz-mcq-option-${idx}`}
              label={opt}
              state={optionState}
              onPress={isLocked ? undefined : () => onSelect(opt)}
              direction={direction}
              locale={locale}
              letterKey={letter}
              accessibilityLabel={`${locale === 'ar' ? 'خيار' : 'Option'} ${letter}: ${opt}${stateLabel ? `, ${stateLabel}` : ''}`}
            />
          );
        })}
      </YStack>
    );
  };

  // ── Render: TrueFalse renderer ───────────────────────────────────────────
  const renderTrueFalse = (question: QuizQuestionDto, answerState: AnswerState, onSelect: (val: string) => void) => {
    const selectedBool =
      answerState.phase === 'answering'
        ? answerState.selectedValue === 'true'
          ? true
          : answerState.selectedValue === 'false'
          ? false
          : null
        : answerState.phase === 'feedback'
        ? answerState.selectedValue === 'true'
          ? true
          : false
        : null;

    const feedbackPhase = answerState.phase === 'feedback' ? answerState : null;
    const correctBool =
      feedbackPhase?.correctAnswer === 'true'
        ? true
        : feedbackPhase?.correctAnswer === 'false'
        ? false
        : null;
    const selectedFeedbackBool =
      feedbackPhase?.selectedValue === 'true'
        ? true
        : feedbackPhase?.selectedValue === 'false'
        ? false
        : null;

    return (
      <TrueFalseChoice
        testID="quiz-renderer-truefalse"
        trueTestID="quiz-truefalse-true"
        falseTestID="quiz-truefalse-false"
        value={selectedBool}
        onChange={(val) => onSelect(val ? 'true' : 'false')}
        phase={answerState.phase === 'answering' ? 'answering' : 'feedback'}
        correctAnswer={correctBool}
        selectedAnswer={selectedFeedbackBool}
        direction={direction}
        locale={locale}
        trueLabel={t('child.quiz.true')}
        falseLabel={t('child.quiz.false')}
      />
    );
  };

  // ── Render: FillInBlank renderer ─────────────────────────────────────────
  const renderFillInBlank = (question: QuizQuestionDto, answerState: AnswerState, onTextChange: (val: string) => void) => {
    const value =
      answerState.phase === 'answering'
        ? answerState.selectedValue ?? ''
        : answerState.phase === 'feedback'
        ? answerState.selectedValue
        : '';

    const fieldState: 'default' | 'correct' | 'incorrect' =
      answerState.phase === 'feedback'
        ? answerState.isCorrect
          ? 'correct'
          : 'incorrect'
        : 'default';

    return (
      <YStack testID="quiz-renderer-fillblank">
        <FillInBlank
          testID="quiz-fillblank-input"
          value={value}
          onChange={onTextChange}
          state={fieldState}
          locked={answerState.phase !== 'answering'}
          placeholder={t('child.quiz.fillPlaceholder')}
          direction={direction}
          locale={locale}
        />
      </YStack>
    );
  };

  // ── Render: Matching renderer (C5 tap-to-pair) ───────────────────────────
  const renderMatching = (
    question: QuizQuestionDto,
    answerState: AnswerState,
    onSelect: (val: string) => void,
  ) => {
    const content = parseMatchingContent(question.options);

    if (!content) {
      // Malformed/empty content — keep the W12 "coming soon" stub tile
      // (Design Spec §4). Submit shows "Next" and sends an empty pair set in
      // the locked L1 shape (valid JSON — never a 422).
      return (
        <MatchingPanel
          testID="quiz-renderer-matching"
          direction={direction}
          locale={locale}
          stubTitle={t('child.quiz.matchingStub')}
          stubSubTitle={t('child.quiz.matchingSkip')}
        />
      );
    }

    // 'submitting' carries no selectedValue — read the snapshot ref so pair
    // chips stay readable during the round-trip (Design Spec §3 "Locked").
    const rawPairsValue =
      answerState.phase === 'submitting' ? submittedValueRef.current : answerState.selectedValue;
    const pairs = parseSelectedPairs(rawPairsValue);

    const panelPhase: MatchingPanelPhase =
      answerState.phase === 'answering'
        ? 'answering'
        : answerState.phase === 'submitting'
          ? 'locked'
          : answerState.isCorrect
            ? 'correct'
            : 'wrong';

    return (
      <MatchingPanel
        testID="quiz-renderer-matching"
        left={content.left}
        right={content.right}
        pairs={pairs}
        onPairsChange={(next) => onSelect(JSON.stringify(next))}
        phase={panelPhase}
        direction={direction}
        locale={locale}
        instructionText={t('quiz.matching.instruction')}
        promptHeaderText={t('quiz.matching.promptHeader')}
        answerHeaderText={t('quiz.matching.answerHeader')}
        armedA11yLabel={(text) => t('quiz.matching.armedA11y', { text })}
        pairedA11yLabel={(text, partner) => t('quiz.matching.pairedA11y', { text, partner })}
        pairedAnnounceText={(a, b) => t('quiz.matching.pairedAnnounce', { a, b })}
        unpairedAnnounceText={(a, b) => t('quiz.matching.unpairedAnnounce', { a, b })}
      />
    );
  };

  // ── Render: question-type renderer switch ────────────────────────────────
  const renderQuestionRenderer = (
    question: QuizQuestionDto,
    answerState: AnswerState,
    onSelect: (val: string) => void,
  ) => {
    // Plain switch — no Strategy pattern (CLAUDE.md rule #8).
    switch (question.questionType) {
      case QuestionType._1: // MCQ
        return renderMCQ(question, answerState, onSelect);
      case QuestionType._2: // TrueFalse
        return renderTrueFalse(question, answerState, onSelect);
      case QuestionType._4: // FillInBlank
        return renderFillInBlank(question, answerState, onSelect);
      case QuestionType._3: // Matching (C5 tap-to-pair)
      default:
        return renderMatching(question, answerState, onSelect);
    }
  };

  // ── Render: Intro stage ──────────────────────────────────────────────────
  const renderIntro = () => {
    const lesson = lessonQuery.data;

    if (lessonQuery.isPending) {
      // Loading shimmer skeleton.
      return (
        <YStack testID="lesson-loading" flex={1} alignItems="center" justifyContent="center" paddingHorizontal="$6" gap="$6">
          <TamStack
            width="100%"
            height={220}
            backgroundColor="$cardSoft"
            borderRadius={24}
            opacity={0.5}
          />
        </YStack>
      );
    }

    if (lessonQuery.isError) {
      const errMsg = lessonQuery.error?.message ?? '';
      const is404 = errMsg.includes('404') || errMsg.toLowerCase().includes('not found');
      return (
        <YStack testID={is404 ? 'lesson-404' : 'lesson-error'} flex={1} alignItems="center" justifyContent="center" paddingHorizontal="$6" gap="$4">
          <Text color="$fg1" fontSize={18} fontWeight="700" fontFamily="$heading" textAlign="center" writingDirection={direction}>
            {is404 ? t('child.lessons.intro.notFound') : t('child.lessons.intro.errorRetry')}
          </Text>
          {!is404 ? (
            <Button
              testID="lesson-error-retry"
              variant="primary"
              size="md"
              accessibilityLabel={t('child.lessons.intro.errorRetry')}
              onPress={() => lessonQuery.refetch()}
            >
              {t('child.lessons.intro.errorRetry')}
            </Button>
          ) : null}
          <Button
            variant="ghost"
            size="md"
            accessibilityLabel={t('child.lessons.intro.back')}
            onPress={handleBack}
          >
            {t('child.lessons.intro.back')}
          </Button>
        </YStack>
      );
    }

    // Empty lesson state (StartAttempt resolved with 0 questions).
    if (emptyLesson) {
      return (
        <YStack testID="lesson-empty" flex={1} alignItems="center" justifyContent="center" paddingHorizontal="$6" gap="$4">
          <Text fontSize={40} accessibilityElementsHidden>{'📭'}</Text>
          <Text color="$fg2" fontSize={18} fontWeight="700" fontFamily="$heading" textAlign="center" writingDirection={direction}>
            {t('child.lessons.intro.noQuestions')}
          </Text>
          <Text color="$fg3" fontSize={14} fontWeight="500" fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('child.lessons.intro.noQuestionsSub')}
          </Text>
          <Button
            variant="ghost"
            size="md"
            accessibilityLabel={t('child.lessons.intro.back')}
            onPress={handleBack}
          >
            {t('child.lessons.intro.back')}
          </Button>
        </YStack>
      );
    }

    return (
      <ScrollView
        contentContainerStyle={{ flexGrow: 1, paddingHorizontal: 24, paddingBottom: 24, maxWidth: 720, width: '100%', alignSelf: 'center' }}
        showsVerticalScrollIndicator={false}
      >
        <YStack gap="$6">
          {/* Hero card */}
          <TamStack
            testID="lesson-intro"
            backgroundColor="$card"
            borderRadius={24}
            padding="$6"
            gap="$4"
            shadowColor="rgba(0,0,0,0.15)"
            shadowOpacity={1}
            shadowRadius={12}
            shadowOffset={{ width: 0, height: 4 }}
            alignItems="center"
          >
            {/* Hero glyph */}
            <TamStack
              width={80}
              height={80}
              borderRadius={9999}
              backgroundColor="$primarySoft"
              alignItems="center"
              justifyContent="center"
            >
              <Text fontSize={36} accessibilityElementsHidden>{'🎯'}</Text>
            </TamStack>

            {/* Eyebrow */}
            <Text
              color="$fg3"
              fontSize={12}
              fontWeight="700"
              fontFamily="$heading"
              textTransform="uppercase"
              letterSpacing={0.04}
              textAlign="center"
            >
              {t('child.lessons.intro.eyebrow')}
            </Text>

            {/* Lesson title */}
            <Text
              testID="lesson-title"
              color="$fg1"
              fontSize={24}
              fontWeight="800"
              fontFamily="$heading"
              textAlign="center"
              numberOfLines={2}
              writingDirection={direction}
            >
              {lesson?.name ?? ''}
            </Text>

            {/* Explanation (AI tutor bubble style — plain text per Pipeline Brief R5) */}
            {lesson?.explanation !== undefined ? (
              <TamStack
                backgroundColor="$primarySoft"
                borderRadius={14}
                padding="$4"
                borderWidth={1}
                borderColor="$primary"
                width="100%"
              >
                <Text
                  color="$fg2"
                  fontSize={15}
                  fontWeight="500"
                  fontFamily="$body"
                  writingDirection={direction}
                  textAlign={isRtl ? 'right' : 'left'}
                >
                  {lesson.explanation ?? t('child.lessons.intro.aiBubbleFallback')}
                </Text>
              </TamStack>
            ) : null}

            {/* Visual block (only when visual !== null) */}
            {lesson?.visual ? (
              <TamStack
                width="100%"
                height={160}
                borderRadius={14}
                backgroundColor="$cardSoft"
                overflow="hidden"
              />
            ) : null}
          </TamStack>

          {/* Start CTA */}
          <Button
            testID="lesson-start-cta"
            variant="primary"
            size="full"
            accessibilityLabel={t('child.lessons.intro.startCta')}
            loading={startAttemptMutation.isPending}
            disabled={startAttemptMutation.isPending}
            onPress={handleStart}
          >
            {t('child.lessons.intro.startCta')}
          </Button>
        </YStack>
      </ScrollView>
    );
  };

  // ── Render: Quiz stage ───────────────────────────────────────────────────
  const renderQuiz = (quiz: Extract<Stage, { kind: 'quiz' }>) => {
    const { attemptId, questions, currentIndex, answerState } = quiz;
    const question = questions[currentIndex];
    if (!question) return null;

    const isMatching = question.questionType === QuestionType._3;
    const matchingContent = isMatching ? parseMatchingContent(question.options) : null;
    const isAnswering = answerState.phase === 'answering';
    const isSubmitting = answerState.phase === 'submitting';
    const isFeedback = answerState.phase === 'feedback';

    // Narrow the feedback state for safe field access.
    type FeedbackState = Extract<AnswerState, { phase: 'feedback' }>;
    const feedbackState: FeedbackState | null = isFeedback ? (answerState as FeedbackState) : null;
    const isCorrectFeedback = feedbackState?.isCorrect ?? false;

    // Determine if Submit is enabled. Matching (C5): gated until EVERY prompt
    // item is paired (Design Spec §1). Malformed-content fallback: always
    // enabled ("Next" submits an empty pair set).
    const hasAnswer =
      isAnswering &&
      (isMatching
        ? matchingContent
          ? parseSelectedPairs(answerState.selectedValue).length === matchingContent.left.length
          : true
        : answerState.selectedValue !== null && answerState.selectedValue.trim().length > 0);

    const onSelectAnswer = (val: string) => {
      if (!isAnswering) return;
      setStage({
        kind: 'quiz',
        attemptId,
        questions,
        currentIndex,
        answerState: { phase: 'answering', selectedValue: val },
      });
    };

    // Correct-answer reveal for the feedback strip. Matching: the server's
    // correctAnswer is the pairs JSON — format it as kid-readable pair text
    // against the question content; omit the reveal if it can't be resolved.
    const revealText = (() => {
      if (!feedbackState || feedbackState.isCorrect || !feedbackState.correctAnswer) {
        return undefined;
      }
      if (isMatching) {
        if (!matchingContent) return undefined;
        const formatted = formatMatchingCorrectAnswer(
          feedbackState.correctAnswer,
          matchingContent,
          t,
        );
        return formatted
          ? t('child.feedback.correctAnswer', { answer: formatted })
          : undefined;
      }
      return t('child.feedback.correctAnswer', { answer: feedbackState.correctAnswer });
    })();

    // Feedback strip
    const feedbackNode = feedbackState ? (
      <AnswerFeedbackStrip
        testID="answer-feedback-strip"
        variant={feedbackState.isCorrect ? 'correct' : 'incorrect'}
        title={
          feedbackState.isCorrect
            ? t('child.feedback.correct')
            : t('child.feedback.incorrect')
        }
        revealText={revealText}
        direction={direction}
        locale={locale}
      />
    ) : null;

    // Submit / Next button
    const submitNode = (() => {
      if (isCorrectFeedback) return null; // auto-advance handles it
      if (isFeedback) {
        return (
          <Button
            testID="feedback-continue"
            variant="primary"
            size="md"
            accessibilityLabel={t('child.quiz.next')}
            onPress={() => advanceOrComplete(quiz)}
          >
            {t('child.quiz.next')}
          </Button>
        );
      }
      // Matching (C5) uses the standard Submit path: handleSubmit wraps the
      // pairs into the locked L1 payload. Malformed-content fallback only:
      // the CTA reads "Next" (stub tile, empty pair set).
      const submitLabel =
        isMatching && !matchingContent ? t('child.quiz.next') : t('child.quiz.submit');
      return (
        <Button
          testID="quiz-submit"
          variant="primary"
          size="md"
          accessibilityLabel={submitLabel}
          disabled={isSubmitting || !hasAnswer}
          loading={isSubmitting}
          onPress={handleSubmit}
        >
          {isSubmitting ? '' : submitLabel}
        </Button>
      );
    })();

    // Hint button (disabled this wave — P3-05).
    const hintNode = (
      <YStack gap={2} alignItems={isRtl ? 'flex-end' : 'flex-start'}>
        <Button
          variant="ghost"
          size="sm"
          accessibilityLabel={`${t('child.quiz.hint')}, ${t('child.quiz.hintComingSoon')}`}
          disabled
          accessibilityState={{ disabled: true }}
        >
          {`💡 ${t('child.quiz.hint')}`}
        </Button>
        <Text
          color="$fg4"
          fontSize={12}
          fontWeight="500"
          fontFamily="$body"
          writingDirection={direction}
        >
          {t('child.quiz.hintComingSoon')}
        </Text>
      </YStack>
    );

    return (
      <ScrollView
        contentContainerStyle={{ flexGrow: 1, paddingHorizontal: 24, paddingBottom: 24, maxWidth: 720, width: '100%', alignSelf: 'center' }}
        showsVerticalScrollIndicator={false}
      >
        <YStack gap="$3">
          {/* QuestionCard */}
          <QuestionCard
            testID="quiz-question-card"
            questionText={question.questionText ?? ''}
            feedback={feedbackNode}
            submitButton={submitNode}
            hintButton={hintNode}
            errorMessage={submitError}
            direction={direction}
            locale={locale}
          >
            {renderQuestionRenderer(question, answerState, onSelectAnswer)}
          </QuestionCard>
        </YStack>
      </ScrollView>
    );
  };

  // ── Render: Summary stage ────────────────────────────────────────────────
  const renderSummary = (summary: AttemptSummaryDto) => {
    const correct = summary.correctAnswers ?? 0;
    const total = summary.totalAnswers ?? 0;
    const accuracy = Math.round(summary.accuracyPercentage ?? 0);
    const duration = Math.round(summary.durationSeconds ?? 0);

    return (
      <ScrollView
        contentContainerStyle={{ flexGrow: 1, paddingHorizontal: 24, paddingBottom: 24, maxWidth: 720, width: '100%', alignSelf: 'center' }}
        showsVerticalScrollIndicator={false}
      >
        <AttemptSummaryCard
          testID="lesson-summary"
          backButtonTestID="lesson-summary-continue"
          retryButtonTestID="lesson-summary-retry"
          correct={correct}
          total={total}
          accuracyPercent={accuracy}
          durationSeconds={duration}
          onBack={handleBack}
          onRetry={handleRetry}
          direction={direction}
          locale={locale}
          titleText={t('child.summary.title')}
          backLabel={t('child.summary.backToSubject')}
          retryLabel={t('child.summary.tryAgain')}
          correctLabel={t('child.summary.scoreLabel')}
          accuracyLabel={t('child.summary.accuracyLabel')}
          durationLabel={t('child.summary.durationLabel')}
          xpStubText={t('child.summary.xpStub')}
        />
      </ScrollView>
    );
  };

  // ── Main render ──────────────────────────────────────────────────────────
  const isQuiz = stage.kind === 'quiz';
  const showProgress = isQuiz;

  return (
    <TamStack
      testID="lesson-screen"
      flex={1}
      backgroundColor="$bg"
      paddingTop={insets.top}
      paddingBottom={insets.bottom}
    >
      {/* TopBar */}
      {renderTopBar(
        showProgress,
        isQuiz ? stage.currentIndex + 1 : undefined,
        isQuiz ? stage.questions.length : undefined,
      )}

      {/* Stage content */}
      {stage.kind === 'intro' && renderIntro()}
      {stage.kind === 'quiz' && renderQuiz(stage)}
      {stage.kind === 'summary' && renderSummary(stage.summary)}
    </TamStack>
  );
}
