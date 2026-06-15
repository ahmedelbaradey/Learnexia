/**
 * ActivityWeb — the parent "Activity" timeline body (Batch D, design
 * `PMScreensExtra.jsx` → `PMActivityScreen` / preview `pm-activity-timeline.html`).
 *
 * Composition:
 *   - A sticky, frosted, horizontally scroll-snapping filter-chip rail
 *     (All / 🏅 Badges / ⚡ Energy / 🔔 Alerts) — reuses the SettingsWeb chip
 *     pattern. Selecting a chip filters the list client-side.
 *   - An event timeline: each row is an icon chip + (bold child name + action) +
 *     a relative-time label.
 *   - An empty state when the active filter matches no events.
 *
 * DATA: no activity/notification endpoint exists yet, so events come from a
 * typed, deterministic stub (`getActivityEventsStub`). TODO(P-activity): swap
 * for the real activity-feed query when it lands — the row shape is data-shaped.
 *
 * BUILD CONTRACT: numeric border radii; tokens for colors where they exist;
 * RTL via explicit `dir` (NO row-reverse); Eastern-Arabic numerals in AR prose
 * via Intl. Reuses ParentHeader + the SettingsWeb chip rail — no new pattern.
 */
import { Stack, Text, type StackProps } from '@tamagui/core';
import React, { useMemo, useState } from 'react';
import { Platform, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { ParentHeader } from './ParentHeader';
import {
  ACTIVITY_CATEGORY,
  ACTIVITY_KIND,
  getActivityEventsStub,
  type ActivityCategory,
  type ActivityEventStub,
  type ActivityKind,
} from './parentDashboardStubs';

const IS_WEB = Platform.OS === 'web';

/** Filter taxonomy: "all" (pseudo) + the three real categories. */
const FILTER = {
  All: 'all',
  ...ACTIVITY_CATEGORY,
} as const;

type FilterValue = (typeof FILTER)[keyof typeof FILTER];

const FILTER_LABEL_KEY: Record<FilterValue, string> = {
  [FILTER.All]: 'parent.activity.filters.all',
  [FILTER.Badge]: 'parent.activity.filters.badges',
  [FILTER.Energy]: 'parent.activity.filters.energy',
  [FILTER.Alert]: 'parent.activity.filters.alerts',
};

/** Per-kind icon + accent (decorative; message text comes from i18n). */
const KIND_META: Record<ActivityKind, { icon: string; color: string }> = {
  [ACTIVITY_KIND.BadgeEarned]: { icon: '🏅', color: '#FACC15' },
  [ACTIVITY_KIND.LevelUp]: { icon: '⭐', color: '#A855F7' },
  [ACTIVITY_KIND.LessonCompleted]: { icon: '✅', color: '#22C55E' },
  [ACTIVITY_KIND.EnergyUsed]: { icon: '⚡', color: '#2DD4BF' },
  [ACTIVITY_KIND.EnergyLow]: { icon: '🪫', color: '#2DD4BF' },
  [ACTIVITY_KIND.StreakReached]: { icon: '🔥', color: '#FB923C' },
  [ACTIVITY_KIND.Inactive]: { icon: '🔔', color: '#EF4444' },
};

const KIND_MESSAGE_KEY: Record<ActivityKind, string> = {
  [ACTIVITY_KIND.BadgeEarned]: 'parent.activity.events.badgeEarned',
  [ACTIVITY_KIND.LevelUp]: 'parent.activity.events.levelUp',
  [ACTIVITY_KIND.LessonCompleted]: 'parent.activity.events.lessonCompleted',
  [ACTIVITY_KIND.EnergyUsed]: 'parent.activity.events.energyUsed',
  [ACTIVITY_KIND.EnergyLow]: 'parent.activity.events.energyLow',
  [ACTIVITY_KIND.StreakReached]: 'parent.activity.events.streakReached',
  [ACTIVITY_KIND.Inactive]: 'parent.activity.events.inactive',
};

function intlLocale(locale: string): string {
  return locale === 'ar' ? 'ar-EG' : 'en-US';
}

function fmtNum(value: number, locale: string): string {
  return new Intl.NumberFormat(intlLocale(locale)).format(value);
}

/** Relative time label from minutes-ago, localized via Intl + i18n plurals. */
function relativeTime(
  minutesAgo: number,
  t: ReturnType<typeof useTranslation>['t'],
  locale: string,
): string {
  if (minutesAgo < 1) return t('parent.activity.time.justNow');
  if (minutesAgo < 60) {
    return t('parent.activity.time.minutes', { count: minutesAgo, value: fmtNum(minutesAgo, locale) });
  }
  const hours = Math.floor(minutesAgo / 60);
  if (hours < 24) {
    return t('parent.activity.time.hours', { count: hours, value: fmtNum(hours, locale) });
  }
  const days = Math.floor(hours / 24);
  return t('parent.activity.time.days', { count: days, value: fmtNum(days, locale) });
}

export function ActivityWeb() {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();

  const [filter, setFilter] = useState<FilterValue>(FILTER.All);
  const allEvents = getActivityEventsStub();

  const events = useMemo(
    () =>
      filter === FILTER.All
        ? allEvents
        : allEvents.filter((e) => e.category === (filter as ActivityCategory)),
    [allEvents, filter],
  );

  const align = isRtl ? 'right' : 'left';
  const filterValues = Object.values(FILTER) as FilterValue[];

  return (
    <Stack testID="activity-root" flexDirection="column" width="100%">
      <ParentHeader title={t('parent.activity.title')} subtitle={t('parent.activity.subtitle')} />

      {/* ---- Sticky frosted filter-chip rail (SettingsWeb pattern) ---- */}
      <Stack
        dir={isRtl ? 'rtl' : 'ltr'}
        borderBottomWidth={1}
        borderBottomColor="$borderSubtle"
        style={
          IS_WEB
            ? ({
                position: 'sticky',
                top: 0,
                zIndex: 30,
                backgroundColor: 'rgba(15,23,42,0.72)',
                backdropFilter: 'blur(20px)',
                WebkitBackdropFilter: 'blur(20px)',
              } as object)
            : undefined
        }
      >
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={{ flexDirection: 'row', gap: 8, paddingHorizontal: 14, paddingVertical: 12 }}
          style={IS_WEB ? ({ scrollSnapType: 'x mandatory' } as object) : undefined}
          accessibilityRole="tablist"
          aria-label={t('parent.activity.filters.navLabel')}
        >
          {filterValues.map((value) => {
            const active = value === filter;
            const label = t(FILTER_LABEL_KEY[value]);
            return (
              <Stack
                key={value}
                testID={`activity-filter-${value}`}
                flexDirection="row"
                alignItems="center"
                gap={6}
                paddingVertical={8}
                paddingHorizontal={14}
                borderRadius={9999}
                flexShrink={0}
                backgroundColor={active ? '$primary' : '$card'}
                borderWidth={1}
                borderColor={active ? '$primary' : '$border'}
                cursor="pointer"
                pressStyle={{ scale: 0.96 }}
                onPress={() => setFilter(value)}
                style={IS_WEB ? ({ scrollSnapAlign: 'start' } as object) : undefined}
                accessibilityRole="tab"
                accessible
                accessibilityState={{ selected: active }}
                accessibilityLabel={label}
                aria-label={label}
              >
                <Text
                  color={active ? '$fg1' : '$fg3'}
                  fontSize={13}
                  fontWeight={active ? '800' : '600'}
                  fontFamily="$heading"
                  writingDirection={direction}
                >
                  {label}
                </Text>
              </Stack>
            );
          })}
        </ScrollView>
      </Stack>

      {/* ---- Event timeline / empty state ---- */}
      <Stack flexDirection="column" gap={10} padding="$5">
        {events.length === 0 ? (
          <Stack
            testID="activity-empty"
            flexDirection="column"
            alignItems="center"
            gap="$2"
            paddingVertical="$8"
          >
            <Text fontSize={32} accessibilityElementsHidden>{'📭'}</Text>
            <Text
              color="$fg1"
              fontSize={16}
              fontWeight="800"
              fontFamily="$heading"
              textAlign="center"
              writingDirection={direction}
            >
              {t('parent.activity.empty.title')}
            </Text>
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
            >
              {t('parent.activity.empty.body')}
            </Text>
          </Stack>
        ) : (
          events.map((event) => (
            <ActivityRow
              key={event.id}
              event={event}
              t={t}
              locale={locale}
              direction={direction}
              isRtl={isRtl}
              align={align}
            />
          ))
        )}
      </Stack>
    </Stack>
  );
}

ActivityWeb.displayName = 'ActivityWeb';

interface ActivityRowProps {
  event: ActivityEventStub;
  t: ReturnType<typeof useTranslation>['t'];
  locale: string;
  direction: 'ltr' | 'rtl';
  isRtl: boolean;
  align: 'left' | 'right';
}

function ActivityRow({ event, t, locale, direction, isRtl, align }: ActivityRowProps) {
  const meta = KIND_META[event.kind];
  // `count` (raw) drives any plural selection; `{{value}}` carries the
  // locale-formatted digits (Eastern-Arabic in AR). Keys without a number
  // simply ignore both.
  const action = t(KIND_MESSAGE_KEY[event.kind], {
    count: event.amount,
    value: event.amount != null ? fmtNum(event.amount, locale) : undefined,
  });
  const timeLabel = relativeTime(event.minutesAgo, t, locale);
  const a11yLabel = t('parent.activity.events.a11y', {
    name: event.childName,
    action,
    time: timeLabel,
  });

  return (
    <Stack
      testID={`activity-row-${event.id}`}
      dir={isRtl ? 'rtl' : 'ltr'}
      flexDirection="row"
      gap={12}
      padding={13}
      borderRadius={16}
      backgroundColor="$card"
      borderWidth={1}
      borderColor="$borderSubtle"
      accessible
      accessibilityLabel={a11yLabel}
    >
      <Stack
        width={38}
        height={38}
        borderRadius={12}
        flexShrink={0}
        alignItems="center"
        justifyContent="center"
        // Dynamic per-kind tint (literal hex + alpha) — cast like FocusAreasCard's
        // chipBg since it isn't a theme token.
        backgroundColor={`${meta.color}22` as StackProps['backgroundColor']}
        accessibilityElementsHidden
      >
        <Text fontSize={17}>{meta.icon}</Text>
      </Stack>
      <Stack flex={1} minWidth={0} gap={3} accessibilityElementsHidden>
        <Text fontSize={13.5} lineHeight={19} writingDirection={direction} textAlign={align}>
          <Text color="$fg1" fontSize={13.5} fontWeight="800" fontFamily="$heading">
            {event.childName}
          </Text>
          <Text color="$fg1" fontSize={13.5} fontFamily="$body">
            {` ${action}`}
          </Text>
        </Text>
        <Text color="$fg4" fontSize={11} fontFamily="$body" writingDirection={direction} textAlign={align}>
          {timeLabel}
        </Text>
      </Stack>
    </Stack>
  );
}
