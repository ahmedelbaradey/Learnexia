/**
 * BarChart — app-local pure Tamagui bar chart primitive (P5-05).
 *
 * Built entirely from Tamagui `Stack` and `Text` (no SVG, no charting lib).
 * Covers all three shape variants used in the parent dashboard:
 *   Shape A — 7-bar weekly activity (DailyActivityCard, Overview)
 *   Shape B — 20-bar XP trend (Reports, showValueLabels=false)
 *   Shape C — 8-bar time-of-day (Reports, Reward gradient peak bar)
 *
 * RTL note: the bar LAYOUT always renders LTR (Brand Law RTL rule 6) — an
 * explicit `direction: ltr` style override is applied on web regardless of
 * the app locale. Value labels and axis labels inside the chart are always
 * positional/technical strings (Latin day abbrevs, hour labels, numeric XP).
 *
 * Animation: bar-grow uses CSS `transition` on web (no Reanimated dependency).
 * Active bar glow + peak bar shadow are always-on, no looping pulse.
 *
 * Accessibility: the outermost Stack is a single a11y node (role=image) with
 * the caller-supplied `accessibilityLabel`. All inner nodes carry
 * `accessibilityElementsHidden` so assistive tech reads the summary once.
 */
import { Stack, Text } from '@tamagui/core';
import React, { useEffect, useRef, useState } from 'react';
import { Platform } from 'react-native';

const IS_WEB = Platform.OS === 'web';

export interface BarChartDataPoint {
  /**
   * Label shown below the bar — already formatted for the locale.
   * Day abbrevs stay Latin even in AR; hour strings are always Latin.
   */
  label: string;
  /** Raw numeric value for proportional height calculation. */
  value: number;
  /** When true, renders with the "active/today" Level-Up gradient + indigo glow. */
  isActive?: boolean;
}

export interface BarChartProps {
  data: BarChartDataPoint[];
  /**
   * Maximum value for proportional scaling.
   * Defaults to Math.max(...data.map(d => d.value)).
   */
  maxValue?: number;
  /**
   * Pixel height of the tallest bar (the chart "well" height).
   * Shape A: 180. Shape B: 200. Shape C: 160.
   */
  chartHeight?: number;
  /** Show a numeric value label above each bar. Default true. */
  showValueLabels?: boolean;
  /**
   * Controls text direction of value/axis labels. Bar layout is always LTR.
   * 'ltr' | 'rtl' — passed from the locale context.
   */
  direction?: 'ltr' | 'rtl';
  /**
   * Base (inactive) bar fill variant:
   *  "muted"  — `#334155` → `#1E293B` gradient (default)
   *  "purple" — solid `#A855F7`
   */
  barVariant?: 'muted' | 'purple';
  /**
   * Locale string for Intl.NumberFormat on value labels.
   * "ar" → ar-EG (Eastern-Arabic numerals); "en" → en-US.
   */
  locale?: string;
  /** testID applied to the outermost wrapper for E2E queries. */
  testID?: string;
  /** Required — read by screen readers instead of iterating bars. */
  accessibilityLabel: string;
  /**
   * Shape C peak/secondary styling overrides.
   * When provided, each bar entry can carry extra fill metadata.
   * shape="C" enables the peak (Reward gradient) + secondary (purple) logic.
   */
  shape?: 'A' | 'B' | 'C';
  /**
   * Gap between bars in px. Defaults: Shape A = 12, Shape B = 6, Shape C = 6.
   * When not supplied, derived from shape.
   */
  barGap?: number;
  /**
   * For Shape C: per-bar color override.
   * 'peak' = Reward gradient; 'secondary' = purple solid; 'muted' = default.
   * Indexed to match data[].
   */
  barColors?: Array<'peak' | 'secondary' | 'muted'>;
  /**
   * For Shape C: custom value suffix (e.g. "m" / "د").
   * If provided, the value label renders as "{value}{suffix}".
   */
  valueSuffix?: string;
}

/** Bar height (px) clamped to a 4px minimum so zero values stay visible. */
function calcBarHeight(value: number, max: number, chartHeight: number): number {
  if (max === 0) return 4;
  return Math.max(4, Math.round((value / max) * chartHeight));
}

/** Format a value with Intl.NumberFormat. */
function formatValue(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

/**
 * BarChart primitive. Renders Tamagui Stack columns.
 *
 * On web: bar heights animate via CSS `transition: height 600ms …` applied via
 * an inline style on mount. On native: static (no animation dependency).
 */
export function BarChart({
  data,
  maxValue,
  chartHeight = 180,
  showValueLabels = true,
  direction = 'ltr',
  barVariant = 'muted',
  locale = 'en',
  testID,
  accessibilityLabel,
  shape,
  barGap,
  barColors,
  valueSuffix,
}: BarChartProps) {
  const resolvedMax =
    maxValue ?? Math.max(1, ...data.map((d) => d.value));

  // Resolve gap: explicit > shape default > 12
  const resolvedGap = barGap ?? (shape === 'B' || shape === 'C' ? 6 : 12);

  // On web animate bars from 0 → target height after first render.
  const [mounted, setMounted] = useState(false);
  const rafRef = useRef<number | undefined>(undefined);
  useEffect(() => {
    if (!IS_WEB) return;
    rafRef.current = requestAnimationFrame(() => setMounted(true));
    return () => {
      if (rafRef.current !== undefined) cancelAnimationFrame(rafRef.current);
    };
  }, []);

  // Container: chart well height + 28px for value-label row above bars
  const containerHeight = chartHeight + 28;

  return (
    <Stack
      testID={testID}
      accessible
      accessibilityRole="image"
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      // The outer container respects the app direction for surrounding layout,
      // but the bar-row itself overrides to LTR (see bar-row below).
      width="100%"
    >
      {/* Bar row — ALWAYS LTR per Brand Law RTL rule 6 */}
      <Stack
        flexDirection="row"
        alignItems="flex-end"
        height={containerHeight}
        // LTR override for web; native layout is already LTR for row.
        style={IS_WEB ? ({ direction: 'ltr' } as object) : undefined}
        accessibilityElementsHidden
        aria-hidden
      >
        {data.map((point, index) => {
          const barH = calcBarHeight(point.value, resolvedMax, chartHeight);
          const isActive = Boolean(point.isActive);

          // Determine fill style
          const fillStyle = getBarFillStyle(
            isActive,
            barVariant,
            shape,
            barColors?.[index],
          );

          // Value label color
          const valueLabelColor = isActive || barColors?.[index] === 'peak'
            ? '#A5B4FC'
            : '#64748B';

          // Axis label color
          const axisLabelColor = isActive ? '#F8FAFC' : '#94A3B8';

          // Formatted value label text
          const valueText = point.value === 0
            ? formatValue(0, locale)
            : valueSuffix
            ? `${formatValue(point.value, locale)}${valueSuffix}`
            : formatValue(point.value, locale);

          // Border radius — Shape C uses tighter 6/3, others use 10/4
          const borderRadiusStyle = shape === 'C'
            ? '6px 6px 3px 3px'
            : '10px 10px 4px 4px';

          // Bar height: animate on web (0 → target)
          const barHeightStyle = IS_WEB
            ? mounted
              ? barH
              : 4 // start at 4px, animate up
            : barH;

          // Stagger delay: 40ms per bar for Shape A (7 bars), 20ms for B, 50ms for C
          const staggerDelay =
            shape === 'B' ? index * 20 :
            shape === 'C' ? index * 50 :
            index * 40;

          return (
            <Stack
              key={index}
              flex={1}
              alignItems="center"
              justifyContent="flex-end"
              height={containerHeight}
              paddingHorizontal={resolvedGap / 2}
              accessibilityElementsHidden
            >
              {/* Value label above bar. writingDirection from caller (ltr/rtl). */}
              {showValueLabels ? (
                <Text
                  writingDirection={direction}
                  style={{
                    fontSize: 11,
                    fontWeight: '800',
                    color: valueLabelColor,
                    marginBottom: 4,
                    fontVariant: ['tabular-nums'],
                    textAlign: 'center',
                  } as object}
                  accessibilityElementsHidden
                >
                  {valueText}
                </Text>
              ) : null}

              {/* Bar */}
              <Stack
                testID={
                  isActive
                    ? 'bar-chart-bar-active'
                    : `bar-chart-bar-${index}`
                }
                width="100%"
                style={IS_WEB
                  ? ({
                      ...fillStyle,
                      height: barHeightStyle,
                      borderRadius: borderRadiusStyle,
                      transition: `height 600ms cubic-bezier(0.16, 1, 0.3, 1) ${staggerDelay}ms`,
                      flexShrink: 0,
                    } as object)
                  : ({
                      ...fillStyle,
                      height: barH,
                      borderRadius: 4,
                      flexShrink: 0,
                    } as object)
                }
                accessibilityElementsHidden
              />

              {/* Axis label below bar */}
              <Text
                style={{
                  fontSize: 11,
                  fontWeight: '700',
                  color: axisLabelColor,
                  textTransform: 'uppercase',
                  letterSpacing: 0.66,
                  marginTop: 4,
                  textAlign: 'center',
                  // Axis labels are always LTR (Latin technical strings)
                  direction: 'ltr',
                } as object}
                accessibilityElementsHidden
              >
                {point.label}
              </Text>
            </Stack>
          );
        })}
      </Stack>
    </Stack>
  );
}

/**
 * Returns an inline style object for the bar fill.
 * Active bars use the Level-Up gradient (purple→indigo) with indigo glow.
 * Peak bars (Shape C) use Reward gradient (orange→red) with orange glow.
 * Secondary bars (Shape C) use solid purple.
 * Inactive muted bars use dark gradient.
 * Purple variant uses solid purple.
 */
function getBarFillStyle(
  isActive: boolean,
  barVariant: 'muted' | 'purple',
  shape?: 'A' | 'B' | 'C',
  barColor?: 'peak' | 'secondary' | 'muted',
): object {
  if (IS_WEB) {
    // Shape C peak bar
    if (shape === 'C' && barColor === 'peak') {
      return {
        background: 'linear-gradient(180deg, #FB923C, #EF4444)',
        boxShadow: '0 4px 14px rgba(251,146,60,0.35)',
      };
    }
    // Shape C secondary bar
    if (shape === 'C' && barColor === 'secondary') {
      return {
        background: '#A855F7',
      };
    }
    // Active bar (Level-Up gradient)
    if (isActive) {
      return {
        background: 'linear-gradient(180deg, #A855F7, #4F46E5)',
        boxShadow: '0 6px 18px rgba(99,102,241,0.4)',
      };
    }
    // Purple variant
    if (barVariant === 'purple') {
      return {
        background: '#A855F7',
      };
    }
    // Muted (default)
    return {
      background: 'linear-gradient(180deg, #334155, #1E293B)',
    };
  }

  // Native — no gradients/box-shadow; approximate with solid colors
  if (shape === 'C' && barColor === 'peak') {
    return { backgroundColor: '#FB923C' };
  }
  if (shape === 'C' && barColor === 'secondary') {
    return { backgroundColor: '#A855F7' };
  }
  if (isActive) {
    return { backgroundColor: '#A855F7' };
  }
  if (barVariant === 'purple') {
    return { backgroundColor: '#A855F7' };
  }
  return { backgroundColor: '#334155' };
}

BarChart.displayName = 'BarChart';
