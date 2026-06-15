/**
 * RoleCard — a selectable role option for the Role Select screen (Batch A).
 *
 * Visual anatomy (natural source order):
 *   LTR: [ icon tile (56×56) ][ flex-1 text block ][ chevron › ]
 *   RTL: same source order — `dir="rtl"` on the row causes browser/RN to
 *        visually reverse the layout automatically: chevron ‹ at reading-START
 *        (right edge), icon tile at reading-END (left edge). NO `flexDirection:
 *        'row-reverse'` is used — mirroring is driven entirely by writingDirection.
 *
 * Touch target: padding 18px top/bottom + 56px icon = well above the 44px min.
 *
 * States:
 *   - default: $card bg, $borderSubtle border
 *   - hover (web): #243349 bg, $primary border, scale(1.02) spring
 *   - press: scale(0.95) 80ms ease-out
 *   - focus (web): $focusRing shadow (2px indigo + 6px glow)
 *   - disabled: opacity 0.4, pointerEvents none
 *
 * Token gaps (Design Spec §6):
 *   DS-A-01: label fontSize 17/19px — no exact token; use raw px (flagged for
 *            token-scale review if these sizes recur).
 *   DS-A-07: hover bg #243349 — not a current token; use raw hex (spec-exact).
 */
import { type Direction } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';

export interface RoleCardProps {
  id: 'parent' | 'student';
  emoji: string;
  /** Localized label (resolved by the caller via i18n). */
  label: string;
  /** Localized subtitle (resolved by the caller via i18n). */
  sub: string;
  onPress: () => void;
  direction?: Direction;
  testID?: string;
  /** Accessibility label from i18n (e.g. "Sign in as Parent"). */
  accessibilityLabel?: string;
  disabled?: boolean;
}

export function RoleCard({
  emoji,
  label,
  sub,
  onPress,
  direction = 'ltr',
  testID,
  accessibilityLabel,
  disabled = false,
}: RoleCardProps) {
  // RTL: natural writing direction reverses the row visually. The chevron character
  // flips between single guillemets to match reading direction.
  const chevron = direction === 'rtl' ? '‹' : '›'; // ‹ or ›
  const textAlign = direction === 'rtl' ? 'right' : 'left';

  return (
    <Stack
      testID={testID}
      // Row layout — source order: icon → text → chevron.
      // On web, `dir` drives the RTL visual flip (browser renders row right-to-left
      // when dir="rtl"). On native, each Text child gets writingDirection for copy;
      // native layout direction follows the locale automatically (I18nManager).
      // NO flexDirection:'row-reverse' per RTL build contract.
      flexDirection="row"
      // `dir` drives the RTL visual flip on web (typed via the @tamagui/core augmentation).
      dir={direction}
      alignItems="center"
      // Native padding: 18px; spec §3.4
      padding={18}
      // Border radius: 20px (spec §3.4). Numeric literal, NOT the `$card` radius
      // token — on native New-Arch the token string isn't resolved (renders square).
      borderRadius={20}
      backgroundColor="$card"
      borderWidth={2}
      borderColor="$borderSubtle"
      // shadow: DS shadow-soft (spec §3.4) — expressed as a boxShadow-equivalent via native shadow props
      shadowColor="rgba(0,0,0,0.15)"
      shadowOffset={{ width: 0, height: 4 }}
      shadowRadius={12}
      shadowOpacity={1}
      // gap between icon tile, text block, chevron: 16px native (spec §3.4)
      gap="$4"
      cursor={disabled ? 'default' : 'pointer'}
      // Hover (web): slightly lighter bg + primary border + spring scale (DS-A-07 for hover bg)
      hoverStyle={
        disabled
          ? undefined
          : {
              // DS-A-07: #243349 — midpoint between $card and $cardSoft; no token exists
              backgroundColor: '#243349',
              borderColor: '$primary',
              scale: 1.02,
            }
      }
      // Press: scale down 5% for 80ms (brand law)
      pressStyle={disabled ? undefined : { scale: 0.95 }}
      // Focus ring (web keyboard nav) — applied via focusStyle
      focusStyle={
        disabled
          ? undefined
          : {
              // $focusRing: 2px indigo + 6px indigo glow (spec §5)
              outlineWidth: 2,
              outlineColor: '$primary',
              outlineStyle: 'solid',
            }
      }
      onPress={disabled ? undefined : onPress}
      opacity={disabled ? 0.4 : 1}
      pointerEvents={disabled ? 'none' : 'auto'}
      accessibilityRole="button"
      accessible
      accessibilityLabel={accessibilityLabel ?? label}
      aria-label={accessibilityLabel ?? label}
      aria-disabled={disabled}
    >
      {/* Icon tile — 56×56px, indigo-soft tinted background */}
      <Stack
        width={56}
        height={56}
        borderRadius={16}
        backgroundColor="$primarySoft"
        alignItems="center"
        justifyContent="center"
        // flexShrink:0 so the tile never collapses when the text is long
        flexShrink={0}
      >
        <Text
          fontSize={28}
          accessibilityElementsHidden
          aria-hidden
        >
          {emoji}
        </Text>
      </Stack>

      {/* Text block — label + subtitle */}
      <Stack flex={1} gap={2}>
        <Text
          color="$fg1"
          // DS-A-01: 17px — no token step; raw px per design spec gap note
          fontSize={17}
          fontWeight="800"
          fontFamily="$heading"
          textAlign={textAlign}
          writingDirection={direction}
        >
          {label}
        </Text>
        <Text
          color="$fg3"
          // DS-A-01 sub: 12px = $small
          fontSize={12}
          fontFamily="$body"
          textAlign={textAlign}
          writingDirection={direction}
          marginTop={2}
        >
          {sub}
        </Text>
      </Stack>

      {/* Chevron — trailing element; in RTL this renders at the visual right (reading-start)
          because writingDirection="rtl" on the row reverses the visual order. */}
      <Text
        color="$primaryLight"
        fontSize={22}
        flexShrink={0}
        accessibilityElementsHidden
        aria-hidden
      >
        {chevron}
      </Text>
    </Stack>
  );
}
