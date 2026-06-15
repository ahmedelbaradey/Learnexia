/**
 * ParentTabBar — floating glass bottom tab bar for the (parent) route group.
 *
 * Design source: `design-system/ui_kits/parent-mobile/PMComponents.jsx` →
 * `PMTabBar` spec. 5 tabs: Children · Reports · Energy · Activity · Settings.
 *
 * Build contract:
 *   - Numeric border radii (22) — Tamagui token `$card` etc. do NOT resolve on
 *     native (render square). Spec values are literals per pixel rule.
 *   - RTL: row stays `flexDirection:"row"` and an explicit `dir={isRtl?'rtl':'ltr'}`
 *     on the bar root flips it once (web). NO `row-reverse` (double-flip).
 *   - Position: `fixed` on web (overlay over content), `absolute` on native.
 *   - a11y: `tablist` / `tab` roles; each tab ≥44px touch target via minHeight
 *     on the full-height 66px bar (all tabs fill height = container touch target).
 *   - Active color #A5B4FC ($primaryLight); inactive #64748B ($fg4).
 *   - Inactive icon: web filter grayscale(0.6) opacity(0.7); native opacity 0.55.
 *   - Active-change icon pop: 1→1.15→1 ~240ms via Reanimated; suppressed under
 *     reduced motion (color only — mirrors ChildTabBar pattern exactly).
 *
 * Mirrors ChildTabBar.tsx pattern for consistency — same structure, same motion
 * hook shape, same glass card literal values.
 */
import { motion, zIndex } from '@learnexia/design-system';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import { useRouter, useSegments } from 'expo-router';
import React, { useEffect } from 'react';
import { Platform, View, type TextStyle, type ViewStyle } from 'react-native';
import Animated, {
  Easing,
  useAnimatedStyle,
  useReducedMotion,
  useSharedValue,
  withSequence,
  withTiming,
} from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';

const Text = styled(TamText, { fontFamily: '$heading', color: '$fg4' });

// ---------------------------------------------------------------------------
// Route constants (fixed value set — technical identifiers, not user-facing text).
// ---------------------------------------------------------------------------

/** The 5 parent tab routes (spec §PMTabBar). */
export const ParentTabRoute = {
  Children: 'children',
  Reports: 'reports',
  Energy: 'energy',
  Activity: 'activity',
  Settings: 'settings',
} as const;
export type ParentTabRouteName = (typeof ParentTabRoute)[keyof typeof ParentTabRoute];

// ---------------------------------------------------------------------------
// Glass card literals — transcribed from PMComponents.jsx PMTabBar spec.
// Kept verbatim per the pixel rule: 22px radius and 0 8px 32px shadow sit
// outside token buckets intentionally.
// ---------------------------------------------------------------------------

/** Bar height (px) — also provides the ≥44px touch-target area per tab item. */
export const PARENT_TAB_BAR_HEIGHT = 66;

/**
 * Bottom clearance screens must add to their scroll content:
 * `paddingBottom: PARENT_TAB_BAR_CLEARANCE + insets.bottom`
 * so content is not covered by the floating bar.
 */
export const PARENT_TAB_BAR_CLEARANCE = PARENT_TAB_BAR_HEIGHT + 24;

const TABBAR_SIDE_INSET = 12;
const TABBAR_BOTTOM_OFFSET = 24;
const TABBAR_MIN_BOTTOM = 12;
const TABBAR_RADIUS = 22;

/** $bg (#0F172A) at 0.82 alpha — glass overlay per PMTabBar spec. */
const TABBAR_BG = 'rgba(15,23,42,0.82)';
const TABBAR_BLUR = 'blur(20px)';
const TABBAR_SHADOW = '0 8px 32px rgba(0,0,0,0.5)';
const TABBAR_SHADOW_NATIVE = {
  color: 'rgba(0,0,0,0.5)',
  offsetY: 8,
  radius: 32,
} as const;

/** Active tab color (spec: #A5B4FC = $primaryLight). */
const ACTIVE_COLOR = '#A5B4FC';
/** Inactive tab color (spec: #64748B = $fg4). */
const INACTIVE_COLOR = '#64748B';

const ICON_SIZE = 21;
const LABEL_SIZE = 10;

/** Inactive icon treatment — web filter per spec; native fallback opacity 0.55. */
const INACTIVE_ICON_STYLE: TextStyle | ViewStyle =
  Platform.OS === 'web'
    ? ({ filter: 'grayscale(0.6) opacity(0.7)' } as unknown as TextStyle)
    : { opacity: 0.55 };

/** Tab inventory (spec PMTabBar). Label keys from the parent.nav namespace. */
const TAB_ITEMS = [
  { route: ParentTabRoute.Children, icon: '👨‍👩‍👦', labelKey: 'parent.nav.children' },
  { route: ParentTabRoute.Reports, icon: '📈', labelKey: 'parent.nav.reports' },
  { route: ParentTabRoute.Energy, icon: '⚡', labelKey: 'parent.nav.energy' },
  // Reuses the existing parent.nav.activity sidebar key (same word, same locale).
  { route: ParentTabRoute.Activity, icon: '🔔', labelKey: 'parent.nav.activity' },
  { route: ParentTabRoute.Settings, icon: '⚙️', labelKey: 'parent.nav.settings' },
] as const;

/** Full Expo Router paths per tab — used by router.replace(). */
const TAB_ROUTES: Record<ParentTabRouteName, `/(parent)/${ParentTabRouteName}`> = {
  [ParentTabRoute.Children]: '/(parent)/children',
  [ParentTabRoute.Reports]: '/(parent)/reports',
  [ParentTabRoute.Energy]: '/(parent)/energy',
  [ParentTabRoute.Activity]: '/(parent)/activity',
  [ParentTabRoute.Settings]: '/(parent)/settings',
} as const;

// ---------------------------------------------------------------------------
// Active-tab detection — derive from useSegments.
// ---------------------------------------------------------------------------

/**
 * Map the last route segment to the matching ParentTabRouteName.
 * Returns `null` when on a non-tab route (e.g. overview, link-child).
 */
function segmentToTabRoute(segments: readonly string[]): ParentTabRouteName | null {
  const last = segments[segments.length - 1];
  switch (last) {
    case ParentTabRoute.Children:
      return ParentTabRoute.Children;
    case ParentTabRoute.Reports:
      return ParentTabRoute.Reports;
    case ParentTabRoute.Energy:
      return ParentTabRoute.Energy;
    case ParentTabRoute.Activity:
      return ParentTabRoute.Activity;
    case ParentTabRoute.Settings:
      return ParentTabRoute.Settings;
    default:
      return null;
  }
}

// ---------------------------------------------------------------------------
// TabBarItem — one icon-over-label column.
// ---------------------------------------------------------------------------
interface TabBarItemProps {
  icon: string;
  label: string;
  focused: boolean;
  onPress: () => void;
  testID: string;
}

function TabBarItem({ icon, label, focused, onPress, testID }: TabBarItemProps) {
  const reduceMotion = useReducedMotion();
  const iconScale = useSharedValue(1);

  // Active-change icon pop: 1→1.15→1, ~240ms total (mirrors ChildTabBar pattern).
  // Reduced-motion: color change only, no scale animation.
  useEffect(() => {
    if (focused && !reduceMotion) {
      iconScale.value = withSequence(
        withTiming(1.15, {
          duration: motion.durations.fast,
          easing: Easing.bezier(...motion.bezier.easeSpring),
        }),
        withTiming(1, { duration: motion.durations.fast }),
      );
    }
  }, [focused, reduceMotion, iconScale]);

  const iconAnimatedStyle = useAnimatedStyle(() => ({
    transform: [{ scale: iconScale.value }],
  }));

  return (
    <TamStack
      testID={testID}
      flex={1}
      height="100%"
      flexDirection="column"
      alignItems="center"
      justifyContent="center"
      gap={3}
      flexShrink={0}
      cursor="pointer"
      pressStyle={{ scale: 0.95 }}
      hoverStyle={{ scale: 1.02 }}
      onPress={onPress}
      accessibilityRole="tab"
      accessible
      accessibilityState={{ selected: focused }}
      accessibilityLabel={label}
      aria-label={label}
      aria-selected={focused}
    >
      <Animated.View style={iconAnimatedStyle}>
        <TamText
          fontSize={ICON_SIZE}
          lineHeight={ICON_SIZE + 4}
          style={focused ? undefined : (INACTIVE_ICON_STYLE as TextStyle)}
          accessibilityElementsHidden
          importantForAccessibility="no-hide-descendants"
        >
          {icon}
        </TamText>
      </Animated.View>
      <Text
        fontSize={LABEL_SIZE}
        fontWeight="700"
        // Inline style for the color literal values (spec: #A5B4FC active, #64748B inactive).
        // Using inline style rather than token prop to match the exact spec hex values.
        style={{ color: focused ? ACTIVE_COLOR : INACTIVE_COLOR }}
        numberOfLines={1}
      >
        {label}
      </Text>
    </TamStack>
  );
}

// ---------------------------------------------------------------------------
// ParentTabBar — the floating glass pill.
// ---------------------------------------------------------------------------

/**
 * ParentTabBar renders the 5-tab glass bottom navigation bar for the parent
 * shell. It must be rendered inside the narrow layout path only (the caller in
 * `_layout.tsx` already gates this behind `!isWide`).
 *
 * The bar uses `position: fixed` on web and `position: absolute` on native so
 * it floats over the content scroll container without affecting layout flow.
 */
export function ParentTabBar() {
  const { t } = useTranslation();
  const { isRtl } = useLocale();
  const router = useRouter();
  const segments = useSegments();
  const insets = useSafeAreaInsets();

  const activeRoute = segmentToTabRoute(segments);
  const bottomOffset = Math.max(insets.bottom + TABBAR_BOTTOM_OFFSET, TABBAR_MIN_BOTTOM);

  // Web: `fixed` overlay (floats over content at every narrow width).
  // Native: `absolute` overlay.
  const wrapperStyle: ViewStyle = {
    position:
      Platform.OS === 'web'
        ? ('fixed' as unknown as ViewStyle['position'])
        : 'absolute',
    bottom: bottomOffset,
    left: TABBAR_SIDE_INSET,
    right: TABBAR_SIDE_INSET,
    zIndex: zIndex[3],
    pointerEvents: 'box-none',
  };

  // Spec border: rgba(255,255,255,0.08) — no design-system token; literal.
  // Including borderColor here because Tamagui's `borderWidth` prop + `style`
  // prop's borderColor resolve together correctly on both platforms.
  const TABBAR_BORDER_COLOR = 'rgba(255,255,255,0.08)';

  const glassStyle: ViewStyle =
    Platform.OS === 'web'
      ? ({
          backgroundColor: TABBAR_BG,
          backdropFilter: TABBAR_BLUR,
          WebkitBackdropFilter: TABBAR_BLUR,
          boxShadow: TABBAR_SHADOW,
          borderColor: TABBAR_BORDER_COLOR,
        } as unknown as ViewStyle)
      : {
          backgroundColor: TABBAR_BG,
          borderColor: TABBAR_BORDER_COLOR,
          shadowColor: TABBAR_SHADOW_NATIVE.color,
          shadowOffset: { width: 0, height: TABBAR_SHADOW_NATIVE.offsetY },
          shadowOpacity: 1,
          shadowRadius: TABBAR_SHADOW_NATIVE.radius,
          elevation: 16,
        };

  return (
    <View style={wrapperStyle}>
      <TamStack
        dir={isRtl ? 'rtl' : 'ltr'}
        testID="parent-tab-bar"
        width="100%"
        height={PARENT_TAB_BAR_HEIGHT}
        borderRadius={TABBAR_RADIUS}
        borderWidth={1}
        // Spec: rgba(255,255,255,0.08) border — no matching token; literal via style.
        // Glass background + border passed together; kept separate from Tamagui layout props
        // so Tamagui's style merge does not clobber the borderWidth prop above.
        style={glassStyle}
        // Logical row — explicit `dir` on this root flips it once in RTL. NO row-reverse.
        flexDirection="row"
        alignItems="center"
        justifyContent="space-around"
        overflow="hidden"
        flexShrink={0}
        accessibilityRole="tablist"
        accessible
        accessibilityLabel={t('parent.nav.barLabel')}
        aria-label={t('parent.nav.barLabel')}
      >
        {TAB_ITEMS.map((item) => {
          const focused = activeRoute === item.route;

          const handlePress = () => {
            if (!focused) {
              router.replace(TAB_ROUTES[item.route]);
            }
          };

          return (
            <TabBarItem
              key={item.route}
              icon={item.icon}
              label={t(item.labelKey)}
              focused={focused}
              onPress={handlePress}
              testID={`parent-tab-${item.route}`}
            />
          );
        })}
      </TamStack>
    </View>
  );
}

ParentTabBar.displayName = 'ParentTabBar';
