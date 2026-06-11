/**
 * ChildCard — child row used in the onboarding list editor and My-Children.
 * Design Spec §2.4.
 *
 * Variants:
 *  - `editable`   — edit + remove icon buttons (before submit).
 *  - `status`     — success/error badge (per-child submit result).
 *  - `selectable` — chevron / active-accent (child switcher).
 *  - `linking`    — read-only with a success badge (Link-Child result).
 *
 * Avatar: initials (first char of up to 2 words), or `avatarUri` Image if given
 * (Design Spec Gap 3). RTL: content row reverses; active accent uses the logical
 * START side. Token-only styling.
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { Stack } from '@tamagui/core';
import React from 'react';
import { Image } from 'react-native';

import { GradientFill } from '../../internal/GradientFill';
import { tryLoadMoti } from '../../internal/moti';
import { XStack, YStack, Text } from '../../internal/primitives';

export type ChildCardVariant = 'editable' | 'status' | 'selectable' | 'linking';

export interface ChildCardChild {
  fullName: string;
  /** Grade + language meta line — already localized by the caller. */
  meta?: string;
}

export interface ChildCardProps {
  variant: ChildCardVariant;
  child: ChildCardChild;
  status?: 'idle' | 'success' | 'error';
  errorMessage?: string;
  isActive?: boolean;
  avatarUri?: string;
  onPress?: () => void;
  onEdit?: () => void;
  onRemove?: () => void;
  direction?: Direction;
  locale?: string;
  accessibilityLabel: string;
  testID?: string;
  /** testID for the edit icon button (editable variant). */
  editTestID?: string;
  /** testID for the remove icon button (editable variant). */
  removeTestID?: string;
  /**
   * Border radius token for the card surface.
   * Default: "$card" (20px — standalone cards).
   * Use "$cardInner" (14px) for linked-children rows inside a panel per spec §D.4.
   */
  borderRadius?: React.ComponentProps<typeof Stack>['borderRadius'];
}

function resolveDirection(direction?: Direction, locale?: string): Direction {
  if (direction) return direction;
  if (locale) return directionForLocale(locale);
  return 'ltr';
}

function initials(fullName: string): string {
  return fullName
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((w) => w.charAt(0))
    .join('')
    .toUpperCase();
}

function StatusBadge({ status }: { status: 'success' | 'error' }) {
  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as React.ComponentType<Record<string, unknown>> | undefined;
  const inner = (
    <Stack
      width={24}
      height={24}
      borderRadius={9999}
      alignItems="center"
      justifyContent="center"
      backgroundColor={status === 'success' ? '$successSoft' : '$dangerSoft'}
      accessibilityElementsHidden
    >
      <Text fontSize={13} color={status === 'success' ? '$success' : '$danger'}>
        {status === 'success' ? '✓' : '✕'}
      </Text>
    </Stack>
  );
  if (MotiView) {
    return (
      <MotiView
        from={{ scale: 0, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ type: 'spring', damping: 10, stiffness: 200 }}
      >
        {inner}
      </MotiView>
    );
  }
  return inner;
}

type TextColor = React.ComponentProps<typeof Text>['color'];

function IconButton({
  glyph,
  color,
  label,
  onPress,
  testID,
}: {
  glyph: string;
  color: TextColor;
  label: string;
  onPress?: () => void;
  testID?: string;
}) {
  return (
    <Stack
      testID={testID}
      minWidth={48}
      minHeight={48}
      alignItems="center"
      justifyContent="center"
      cursor="pointer"
      pressStyle={{ scale: 0.92 }}
      onPress={onPress}
      accessibilityRole="button"
      accessible
      accessibilityLabel={label}
      aria-label={label}
    >
      <Text fontSize={18} color={color}>
        {glyph}
      </Text>
    </Stack>
  );
}

export function ChildCard({
  variant,
  child,
  status = 'idle',
  errorMessage,
  isActive = false,
  avatarUri,
  onPress,
  onEdit,
  onRemove,
  direction,
  locale,
  accessibilityLabel,
  testID,
  editTestID,
  removeTestID,
  borderRadius = '$card',
}: ChildCardProps) {
  const dir = resolveDirection(direction, locale);
  const rowDir = dir === 'rtl' ? 'row-reverse' : 'row';
  const isPressable = variant === 'selectable' || variant === 'editable';

  const showError = variant === 'status' && status === 'error' && Boolean(errorMessage);

  return (
    <YStack gap="$1">
      <Stack
        testID={testID}
        minHeight={72}
        borderRadius={borderRadius}
        borderWidth={1}
        borderColor="$border"
        backgroundColor={isActive ? '$bgElevated' : '$card'}
        borderStartWidth={isActive ? 4 : 1}
        borderStartColor={isActive ? '$primary' : '$border'}
        cursor={isPressable ? 'pointer' : 'default'}
        pressStyle={isPressable ? { scale: 0.98 } : undefined}
        onPress={isPressable ? onPress : undefined}
        accessibilityRole={isPressable ? 'button' : undefined}
        accessible
        accessibilityState={variant === 'selectable' ? { selected: isActive } : undefined}
        accessibilityLabel={accessibilityLabel}
        aria-label={accessibilityLabel}
      >
        <XStack gap="$3" alignItems="center" padding="$4" flexDirection={rowDir}>
          {/* Avatar */}
          <Stack
            width={44}
            height={44}
            borderRadius={9999}
            overflow="hidden"
            alignItems="center"
            justifyContent="center"
            accessibilityElementsHidden
          >
            {avatarUri ? (
              <Image source={{ uri: avatarUri }} style={{ width: 44, height: 44 }} />
            ) : (
              <>
                <GradientFill
                  stops={['#A78BFA', '#334155']}
                  angle={135}
                  position="absolute"
                  top={0}
                  left={0}
                  right={0}
                  bottom={0}
                />
                <Text color="$fg1" fontSize={18} fontWeight="700">
                  {initials(child.fullName)}
                </Text>
              </>
            )}
          </Stack>

          {/* Info */}
          <YStack flex={1} gap="$1">
            <Text color="$fg1" fontSize={16} fontWeight="600" textAlign="left" writingDirection={dir}>
              {child.fullName}
            </Text>
            {child.meta ? (
              <Text color="$fg3" fontSize={13} fontFamily="$body" textAlign="left" writingDirection={dir}>
                {child.meta}
              </Text>
            ) : null}
          </YStack>

          {/* Actions */}
          {variant === 'editable' ? (
            <XStack gap="$2" flexDirection={rowDir}>
              <IconButton glyph="✎" color="$fg3" label="Edit child" onPress={onEdit} testID={editTestID} />
              <IconButton glyph="🗑" color="$danger" label="Remove child" onPress={onRemove} testID={removeTestID} />
            </XStack>
          ) : null}

          {variant === 'status' && status !== 'idle' ? (
            <StatusBadge status={status} />
          ) : null}

          {variant === 'linking' ? <StatusBadge status="success" /> : null}

          {variant === 'selectable' ? (
            isActive ? (
              <Text color="$primaryLight" fontSize={18} accessibilityElementsHidden>
                {'✓'}
              </Text>
            ) : (
              <Text
                color="$fg3"
                fontSize={16}
                accessibilityElementsHidden
                scaleX={dir === 'rtl' ? -1 : 1}
              >
                {'›'}
              </Text>
            )
          ) : null}
        </XStack>
      </Stack>

      {showError ? (
        <Text color="$danger" fontSize={12} fontFamily="$body" paddingHorizontal="$2" textAlign="left" writingDirection={dir}>
          {errorMessage}
        </Text>
      ) : null}
    </YStack>
  );
}

ChildCard.displayName = 'ChildCard';
