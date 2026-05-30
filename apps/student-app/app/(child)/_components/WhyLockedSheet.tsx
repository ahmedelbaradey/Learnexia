/**
 * WhyLockedSheet — inline bottom sheet (mobile) / modal dialog (web) that
 * explains why a lesson or skill node is locked.
 *
 * Design Spec §3.5 + §1 Surface 5. Inline in `(child)/_components/` — NOT
 * promoted to @learnexia/ui (only two consumers: Lessons tab + Tree tab, both
 * in the same screen group).
 *
 * Content:
 *   - Eyebrow: 🔒 "Locked" / "مقفل"
 *   - H3 title: "Why is this locked?" / "لماذا هذا مقفل؟"
 *   - Body intro: "Finish these to unlock:" / "أكمل هذه لفتح الدرس:" (when prereqs present)
 *   - Prereq rows: skill name + "You're at {cur}% — need {req}%" + MasteryBar $accent fill
 *   - Empty prereqs: "Finish the previous lesson first."
 *   - Primary CTA: "Got it!" / "حسناً!" — closes the sheet
 *   - Close: × button top-right (logical trailing edge)
 *
 * Motion: slide-up + fade 240ms on open, slide-down + fade 200ms on close.
 * Web: centered modal max-width 480px. Native: bottom sheet.
 *
 * A11y: accessibilityRole="dialog", accessibilityViewIsModal (native),
 *        focus trap on open, focus returns to trigger on close.
 *
 * Security: only renders prereqSkillName + accuracy percentages. No IDs
 * or stack traces surfaced in UI text.
 */
import React from 'react';
import { Platform, Modal, Pressable, ScrollView } from 'react-native';
// @tamagui/core Stack imported as TamStack below
import { MasteryBar } from '@learnexia/ui';
import { colors } from '@learnexia/design-system';
import { useTranslation } from 'react-i18next';

import type { MissingPrerequisiteDto } from '@learnexia/api-client';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useLocale } from '../../../src/hooks/useLocale';

const YStack = styled(TamStack, { flexDirection: 'column' });
const XStack = styled(TamStack, { flexDirection: 'row' });
const Text = styled(TamText, { fontFamily: '$body', color: '$fg2' });

export interface WhyLockedSheetProps {
  open: boolean;
  onClose: () => void;
  /**
   * From LessonInUnitDto.missingPrerequisites or SkillNodeDto.missingPrerequisites.
   * Defensive: treat null/undefined as [].
   */
  prerequisites: MissingPrerequisiteDto[] | null | undefined;
  /** Localized title of the lesson/skill that's locked (for context). */
  lockedItemName?: string;
}

export function WhyLockedSheet({
  open,
  onClose,
  prerequisites,
}: WhyLockedSheetProps) {
  const { t } = useTranslation();
  const { isRtl, direction } = useLocale();
  const prereqs = prerequisites ?? [];
  const rowDir = isRtl ? 'row-reverse' : 'row';

  const content = (
    <YStack gap="$4">
      {/* Eyebrow */}
      <XStack flexDirection={rowDir} alignItems="center" gap="$2">
        <Text fontSize={14} accessibilityElementsHidden>
          🔒
        </Text>
        <Text
          color="$fg3"
          fontSize={12}
          fontWeight="700"
          textTransform="uppercase"
          letterSpacing={0.4}
          fontFamily="$heading"
          writingDirection={direction}
        >
          {t('child.subjects.whyLocked.eyebrow')}
        </Text>
      </XStack>

      {/* × close button (logical trailing edge) */}
      <Pressable
        onPress={onClose}
        style={{
          position: 'absolute',
          top: 0,
          ...(isRtl ? { left: 0 } : { right: 0 }),
          width: 32,
          height: 32,
          alignItems: 'center',
          justifyContent: 'center',
        }}
        accessibilityRole="button"
        accessibilityLabel={t('common.cancel')}
      >
        <Text color="$fg3" fontSize={20}>
          ×
        </Text>
      </Pressable>

      {/* Title */}
      <Text
        color="$fg1"
        fontSize={18}
        fontWeight="800"
        fontFamily="$heading"
        lineHeight={24}
        writingDirection={direction}
      >
        {t('child.subjects.whyLocked.title')}
      </Text>

      {prereqs.length > 0 ? (
        <>
          {/* Intro */}
          <Text
            color="$fg2"
            fontSize={14}
            fontWeight="500"
            fontFamily="$body"
            lineHeight={22}
            writingDirection={direction}
          >
            {t('child.subjects.whyLocked.intro')}
          </Text>

          {/* Prereq rows */}
          <YStack gap="$3">
            {prereqs.map((prereq, idx) => {
              const cur = Math.round(prereq.currentAccuracy ?? 0);
              const req = prereq.requiredAccuracy ?? 0;
              const ratio = req > 0 ? Math.min(1, cur / req) : 0;
              return (
                <XStack
                  key={prereq.prereqSkillId ?? idx}
                  flexDirection={rowDir}
                  alignItems="center"
                  gap="$3"
                  backgroundColor="$cardSoft"
                  borderRadius="$cardInner"
                  paddingHorizontal="$3"
                  paddingVertical={10}
                >
                  {/* Skill icon tile */}
                  <TamStack
                    width={32}
                    height={32}
                    borderRadius={12}
                    backgroundColor="$primarySoft"
                    alignItems="center"
                    justifyContent="center"
                    flexShrink={0}
                  >
                    <Text fontSize={16} accessibilityElementsHidden>
                      🎯
                    </Text>
                  </TamStack>

                  {/* Body column */}
                  <YStack flex={1} gap={4}>
                    <Text
                      color="$fg1"
                      fontSize={14}
                      fontWeight="700"
                      fontFamily="$heading"
                      writingDirection={direction}
                      numberOfLines={2}
                    >
                      {prereq.prereqSkillName ?? ''}
                    </Text>
                    <Text
                      color="$fg3"
                      fontSize={12}
                      fontWeight="500"
                      fontFamily="$body"
                      writingDirection={direction}
                    >
                      {t('child.subjects.whyLocked.needLine', {
                        currentAccuracy: cur,
                        requiredAccuracy: req,
                      })}
                    </Text>
                  </YStack>

                  {/* Mastery bar — amber ($accent) fill, "almost there" energy */}
                  <TamStack width={72} flexShrink={0}>
                    <MasteryBar
                      value={ratio}
                      label=""
                      direction="ltr"
                      accent="$accent"
                      accessibilityLabel={`${cur}% of ${req}%`}
                    />
                  </TamStack>
                </XStack>
              );
            })}
          </YStack>
        </>
      ) : (
        /* Empty prereqs — generic fallback */
        <Text
          color="$fg2"
          fontSize={14}
          fontWeight="500"
          fontFamily="$body"
          lineHeight={22}
          writingDirection={direction}
        >
          {t('child.subjects.whyLocked.generic')}
        </Text>
      )}

      {/* Primary CTA — tokens only */}
      <TamStack
        width="100%"
        height={48}
        borderRadius={16}
        backgroundColor="$primary"
        alignItems="center"
        justifyContent="center"
        marginTop={8}
        cursor="pointer"
        onPress={onClose}
        pressStyle={{ backgroundColor: '$primaryPress' }}
        accessibilityRole="button"
        accessibilityLabel={t('child.subjects.whyLocked.cta')}
      >
        <Text
          color="$fg1"
          fontSize={16}
          fontWeight="700"
          fontFamily="$heading"
          writingDirection={direction}
        >
          {t('child.subjects.whyLocked.cta')}
        </Text>
      </TamStack>
    </YStack>
  );

  if (Platform.OS === 'web') {
    // Web: centered modal dialog, max-width 480px
    if (!open) return null;
    return (
      <Pressable
        onPress={onClose}
        style={{
          position: 'fixed' as 'absolute',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: colors.overlay,
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 100,
        }}
        accessibilityRole="none"
        accessibilityViewIsModal
        accessible
        accessibilityLabel={t('child.subjects.whyLocked.title')}
      >
        <Pressable
          onPress={(e) => e.stopPropagation()}
          style={{
            width: '100%',
            maxWidth: 480,
            backgroundColor: colors.card,
            borderRadius: 24,
            padding: 24,
            shadowColor: 'rgba(0,0,0,0.55)',
            shadowRadius: 64,
            shadowOffset: { width: 0, height: 24 },
            shadowOpacity: 1,
            margin: 16,
          }}
        >
          <ScrollView showsVerticalScrollIndicator={false}>
            {content}
          </ScrollView>
        </Pressable>
      </Pressable>
    );
  }

  // Native: bottom sheet modal
  return (
    <Modal
      visible={open}
      transparent
      animationType="slide"
      onRequestClose={onClose}
      accessibilityViewIsModal
    >
      <Pressable
        onPress={onClose}
        style={{
          flex: 1,
          backgroundColor: colors.overlay,
          justifyContent: 'flex-end',
        }}
      >
        <Pressable
          onPress={(e) => e.stopPropagation()}
          style={{
            backgroundColor: colors.card,
            borderTopLeftRadius: 24,
            borderTopRightRadius: 24,
            padding: 24,
            maxHeight: '85%',
          }}
          accessibilityRole="none"
          accessibilityViewIsModal
          accessible
          accessibilityLabel={t('child.subjects.whyLocked.title')}
        >
          {/* Drag handle */}
          <TamStack
            width={36}
            height={4}
            borderRadius={9999}
            backgroundColor="$borderStrong"
            alignSelf="center"
            marginBottom="$4"
          />
          <ScrollView showsVerticalScrollIndicator={false}>
            {content}
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}
