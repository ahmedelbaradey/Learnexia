/**
 * ChildTabBar — floating glass bottom TabBar for the (child) route group.
 *
 * B0-nav (carryover plan batch 2b) — the ONE lead-approved new visual pattern
 * (CLAUDE.md rule 8, plan decision L3). Built to the Design Spec
 * `design-system/ui_kits/student-app/B0-nav-tabbar.md`; the pixel authority is
 * `design-system/preview/mobile-tabbar.html` — its card literals (22px radius,
 * 0.85-alpha glass bg, 0 8px 32px shadow) are transcribed verbatim per spec §2/§8.2
 * and deliberately NOT rounded to token buckets.
 *
 * Behaviour (spec §3):
 *   - Custom `tabBar` for the expo-router `<Tabs>` navigator in `(child)/_layout.tsx`.
 *   - VISIBLE only on the routes in `TAB_BAR_VISIBLE_ROUTES` (the 4 tab roots +
 *     subject detail). Every other route registered with `href: null`
 *     (lesson player today; streak/hearts/xp/attempts in Wave B) hides the bar
 *     automatically — later batches add push screens with zero re-architecture.
 *   - Web PWA: `position: fixed` overlay; native: `position: absolute`.
 *     Bottom offset = max(safe-area inset, 12px) on both.
 *   - RTL: the row stays `row` — document `dir="rtl"` (web) / I18nManager (native)
 *     flips it once, so Home sits at the right edge in Arabic. NO `row-reverse`
 *     (double-flip — spec §4, same rule as the parent shell).
 *
 * Motion (spec §2): active-change icon pop 1 → 1.15 → 1 (~240ms, spring easing);
 * suppressed under reduced motion (color change only). Press scale 0.95 via
 * Tamagui `pressStyle` (existing Tabs-component precedent).
 *
 * Deviation note for reviewer: spec §2 asks for Twemoji rendering of the emoji
 * glyphs on web/Android — no Twemoji infra exists in the repo yet, so glyphs
 * render as system emoji like every existing component (MatchingPanel, ui/Tabs).
 */
import { motion, zIndex } from '@learnexia/design-system';
import { Stack as TamStack, Text as TamText, styled } from '@tamagui/core';
import type { Tabs } from 'expo-router';
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

const Text = styled(TamText, { fontFamily: '$heading', color: '$fg4' });

// ---------------------------------------------------------------------------
// Route names (technical identifiers — fixed value set, spec §1/§3).
// `(child)/_layout.tsx` registers screens from these consts so batch 3c (B-int)
// and the Wave-B screens reference one source of truth.
// ---------------------------------------------------------------------------

/** The 4 tab roots (spec §1 — logical order; RTL flips visually). */
export const ChildTabRoute = {
  Home: 'index',
  Missions: 'missions',
  League: 'league',
  Badges: 'badges',
} as const;
export type ChildTabRouteName = (typeof ChildTabRoute)[keyof typeof ChildTabRoute];

/** Non-tab push screens registered with `href: null` (spec §3). */
export const ChildPushRoute = {
  SubjectDetail: 'subjects/[subjectId]',
  LessonPlayer: 'lessons/[lessonId]',
} as const;

/**
 * Allowlist of routes the bar is VISIBLE on (spec §3 visibility rules; mirrors
 * the mock kit's `showsTabBar` allowlist). Any route NOT listed here — the
 * lesson player and all future `href: null` push screens (streak, hearts, xp,
 * attempts) — hides the bar with no further wiring.
 */
const TAB_BAR_VISIBLE_ROUTES: readonly string[] = [
  ChildTabRoute.Home,
  ChildTabRoute.Missions,
  ChildTabRoute.League,
  ChildTabRoute.Badges,
  ChildPushRoute.SubjectDetail,
];

// ---------------------------------------------------------------------------
// Preview-card literals — transcribed from design-system/preview/mobile-tabbar.html
// (spec §2). Kept verbatim per the pixel rule (spec §8.2): 22px radius and the
// 0 8px 32px shadow sit outside the token buckets on purpose.
// ---------------------------------------------------------------------------
/** Bar height (px) — also the per-item ≥48px touch-target provider (spec §5). */
export const CHILD_TAB_BAR_HEIGHT = 64;
/**
 * Bottom clearance tab screens must add to their scroll content:
 * `paddingBottom: CHILD_TAB_BAR_CLEARANCE + insets.bottom` (spec §3).
 * B-int (batch 3c) applies this on Home; new screens bake it in from day one.
 */
export const CHILD_TAB_BAR_CLEARANCE = CHILD_TAB_BAR_HEIGHT + 24;

const TABBAR_MAX_WIDTH = 380;
const TABBAR_SIDE_INSET = 20;
const TABBAR_MIN_BOTTOM = 12;
const TABBAR_RADIUS = 22;
/** `$bg` (#0F172A) at 0.85 alpha — glass overlay derivation per spec §2. */
const TABBAR_BG = 'rgba(15, 23, 42, 0.85)';
const TABBAR_BLUR = 'blur(20px)';
const TABBAR_SHADOW = '0 8px 32px rgba(0, 0, 0, 0.5)';
const TABBAR_SHADOW_NATIVE = {
  color: 'rgba(0, 0, 0, 0.5)',
  offsetY: 8,
  radius: 32,
} as const;
const ICON_SIZE = 22;
const LABEL_SIZE = 10;
/** Inactive emoji treatment — web filter per spec §2; native fallback opacity 0.55. */
const INACTIVE_ICON_STYLE: TextStyle | ViewStyle =
  Platform.OS === 'web'
    ? ({ filter: 'grayscale(0.6) opacity(0.7)' } as unknown as TextStyle)
    : { opacity: 0.55 };

/** Tab inventory (spec §1) — icon glyphs from the preview's semantic emoji set. */
const TAB_ITEMS = [
  { route: ChildTabRoute.Home, icon: '🏠' },
  { route: ChildTabRoute.Missions, icon: '🎯' },
  { route: ChildTabRoute.League, icon: '🏆' },
  { route: ChildTabRoute.Badges, icon: '🏅' },
] as const;

/** Tab glyph per route — exported for the stub screens (removed by B-int). */
export const CHILD_TAB_ICONS: Record<ChildTabRouteName, string> = {
  [ChildTabRoute.Home]: '🏠',
  [ChildTabRoute.Missions]: '🎯',
  [ChildTabRoute.League]: '🏆',
  [ChildTabRoute.Badges]: '🏅',
};

/** Localized label key per route (typed `nav.tabs.*` namespace, owned by 2b). */
const TAB_LABEL_KEYS: Record<ChildTabRouteName, string> = {
  [ChildTabRoute.Home]: 'nav.tabs.home',
  [ChildTabRoute.Missions]: 'nav.tabs.missions',
  [ChildTabRoute.League]: 'nav.tabs.league',
  [ChildTabRoute.Badges]: 'nav.tabs.badges',
};

// Derive the custom-tabBar props type from expo-router itself (no direct
// dependency on @react-navigation/bottom-tabs).
type ExpoTabsProps = React.ComponentProps<typeof Tabs>;
type TabBarProps = Parameters<NonNullable<ExpoTabsProps['tabBar']>>[0];

// ---------------------------------------------------------------------------
// TabBarItem — one icon-over-label column (spec §2 item anatomy + §5 a11y).
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

  // Active-change icon pop: 1 → 1.15 → 1, ~240ms total with the spring bezier
  // (spec §2 active-change motion). Reduced-motion: color change only.
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
      gap={2}
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
        color={focused ? '$primaryLight' : '$fg4'}
        // Hover (web): brighten, never darken (spec §2 / brand law 10).
        hoverStyle={focused ? undefined : { color: '$fg2' }}
        numberOfLines={1}
      >
        {label}
      </Text>
    </TamStack>
  );
}

// ---------------------------------------------------------------------------
// ChildTabBar — the floating glass pill (custom tabBar for <Tabs>).
// ---------------------------------------------------------------------------
export function ChildTabBar({ state, navigation }: TabBarProps) {
  const { t } = useTranslation();
  const insets = useSafeAreaInsets();

  const activeRouteName = state.routes[state.index]?.name ?? '';

  // Visibility allowlist (spec §3): hidden on the lesson player and any future
  // href:null push screen not explicitly allowlisted.
  if (!TAB_BAR_VISIBLE_ROUTES.includes(activeRouteName)) return null;

  const bottomOffset = Math.max(insets.bottom, TABBAR_MIN_BOTTOM);

  // Web: fixed overlay (PWA keeps the same floating bar at every breakpoint —
  // spec §3 explicitly forbids a sidebar conversion). Native: absolute overlay.
  const wrapperStyle: ViewStyle = {
    // RN's ViewStyle has no 'fixed'; react-native-web passes it through to CSS.
    position:
      Platform.OS === 'web'
        ? ('fixed' as unknown as ViewStyle['position'])
        : 'absolute',
    bottom: bottomOffset,
    left: 0,
    right: 0,
    alignItems: 'center',
    paddingHorizontal: TABBAR_SIDE_INSET,
    zIndex: zIndex[3],
    pointerEvents: 'box-none',
  };

  const glassStyle: ViewStyle =
    Platform.OS === 'web'
      ? ({
          backgroundColor: TABBAR_BG,
          backdropFilter: TABBAR_BLUR,
          WebkitBackdropFilter: TABBAR_BLUR,
          boxShadow: TABBAR_SHADOW,
        } as unknown as ViewStyle)
      : {
          backgroundColor: TABBAR_BG,
          shadowColor: TABBAR_SHADOW_NATIVE.color,
          shadowOffset: { width: 0, height: TABBAR_SHADOW_NATIVE.offsetY },
          shadowOpacity: 1,
          shadowRadius: TABBAR_SHADOW_NATIVE.radius,
          elevation: 16,
        };

  return (
    <View style={wrapperStyle}>
      <TamStack
        testID="child-tab-bar"
        width="100%"
        maxWidth={TABBAR_MAX_WIDTH}
        height={CHILD_TAB_BAR_HEIGHT}
        borderRadius={TABBAR_RADIUS}
        borderWidth={1}
        borderColor="$border"
        // Logical row — document dir / I18nManager flips it once in RTL (spec §4).
        flexDirection="row"
        alignItems="center"
        justifyContent="space-around"
        overflow="hidden"
        style={glassStyle}
        accessibilityRole="tablist"
        accessible
        accessibilityLabel={t('nav.tabs.barLabel')}
        aria-label={t('nav.tabs.barLabel')}
      >
        {TAB_ITEMS.map((item) => {
          const route = state.routes.find((r) => r.name === item.route);
          if (!route) return null;
          const focused = activeRouteName === item.route;

          const handlePress = () => {
            const event = navigation.emit({
              type: 'tabPress',
              target: route.key,
              canPreventDefault: true,
            });
            if (!focused && !event.defaultPrevented) {
              navigation.navigate(route.name, route.params as never);
            }
          };

          return (
            <TabBarItem
              key={item.route}
              icon={item.icon}
              label={t(TAB_LABEL_KEYS[item.route])}
              focused={focused}
              onPress={handlePress}
              testID={`child-tab-${item.route}`}
            />
          );
        })}
      </TamStack>
    </View>
  );
}
