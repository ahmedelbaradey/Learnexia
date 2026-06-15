/**
 * RoleBadge — a centered read-only pill showing the chosen login role (Batch A).
 *
 * Renders: [ role emoji ][ "Signing in as Parent/Student" ][ "Change" link ]
 * All three on a single centered row, gap 8px.
 *
 * The "Change" link calls `onChangePress` which should `router.replace('/(auth)/role-select')`.
 * The badge itself has no toggle state — it is presentation-only.
 *
 * RTL: source order is unchanged. `dir="rtl"` on the container reverses the
 * visual order automatically. No `flexDirection:'row-reverse'` is used.
 *
 * Token gaps (Design Spec §6):
 *   DS-A-02: container bg #15161D ("bgDeepest") — now added as $bgDeepest in
 *            packages/design-system/src/tokens/colors.ts.
 *
 * Accessibility:
 *   - Badge container: accessibilityRole="text", accessibilityLabel = signingInAs key
 *   - "Change" pressable: accessibilityRole="button", tabIndex=0, hitSlop 44px
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { Pressable } from 'react-native';
import { useTranslation } from 'react-i18next';

export interface RoleBadgeProps {
  role: 'parent' | 'student';
  onChangePress: () => void;
  direction?: Direction;
  disabled?: boolean;
  testID?: string;
}

const ROLE_EMOJI: Record<'parent' | 'student', string> = {
  parent: '👨‍👩‍👦',
  student: '🎓',
};

export function RoleBadge({
  role,
  onChangePress,
  direction = 'ltr',
  disabled = false,
  testID,
}: RoleBadgeProps) {
  const { t } = useTranslation();

  const labelKey =
    role === 'parent' ? 'auth.login.signingInAsParent' : 'auth.login.signingInAsStudent';
  const label = t(labelKey);
  const changeLabel = t('auth.login.change');
  const emoji = ROLE_EMOJI[role];

  return (
    <Stack
      testID={testID ?? 'role-badge'}
      // DS-A-02: $bgDeepest (#15161D) — promoted to design-system tokens
      backgroundColor="$bgDeepest"
      borderRadius="$nav"
      borderWidth={1}
      borderColor="$borderSubtle"
      paddingVertical="$2"
      paddingHorizontal="$3"
      flexDirection="row"
      // `dir` drives the RTL visual flip on web (typed via the @tamagui/core augmentation).
      dir={direction}
      alignItems="center"
      justifyContent="center"
      gap="$2"
      opacity={disabled ? 0.4 : 1}
      pointerEvents={disabled ? 'none' : 'auto'}
      // Container a11y: announces the signing-in-as label for screen readers
      accessibilityRole="text"
      accessible
      accessibilityLabel={label}
      aria-label={label}
    >
      {/* Role emoji — does not mirror (brand rule) */}
      <Text
        fontSize={16}
        accessibilityElementsHidden
        aria-hidden
      >
        {emoji}
      </Text>

      {/* "Signing in as …" label */}
      <Text
        color="$fg2"
        fontSize={13}
        fontWeight="700"
        fontFamily="$heading"
        writingDirection={direction}
      >
        {label}
      </Text>

      {/* "Change" link — tap target minimum 44px via hitSlop */}
      <Pressable
        testID="role-badge-change"
        onPress={disabled ? undefined : onChangePress}
        accessibilityRole="button"
        accessibilityLabel={changeLabel}
        aria-label={changeLabel}
        accessibilityHint="Tap to go back to role selection"
        // Extend touch target to meet 44px minimum (hitSlop adds invisible padding)
        hitSlop={{ top: 14, bottom: 14, left: 8, right: 8 }}
        style={({ pressed }) => ({
          // Press: opacity 0.7 for 80ms (text link — no scale)
          opacity: pressed ? 0.7 : 1,
          marginStart: 4,
        })}
      >
        <Text
          color="$primaryLight"
          fontSize={12}
          fontWeight="700"
          fontFamily="$body"
          writingDirection={direction}
        >
          {changeLabel}
        </Text>
      </Pressable>
    </Stack>
  );
}
