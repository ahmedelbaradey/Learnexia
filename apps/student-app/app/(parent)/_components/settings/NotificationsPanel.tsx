/**
 * NotificationsPanel — Settings → Notifications tab (P2-12a).
 *
 * 4-category × 2-channel Switch grid (Weekly Report / Streak At Risk /
 * Product Announcement / Achievement) × (Email / Push). Optimistic toggle:
 * mutates local state immediately, PUTs the full 4-item array (BE requires
 * all 4; partial updates are rejected). On error: rollback + ServerErrorBanner.
 *
 * AC: N1 — 4 rows always shown (defaults from BE, never empty).
 *     N2 — loads via useNotificationPreferences; loading state shown.
 *     N3 — optimistic toggle, full PUT, rollback on error.
 *     N4 — localized labels in en + ar; accessibilityLabel composed.
 *
 * RTL: outer row uses flexDirection={rowDir}; switch pair also flexDirection={rowDir}.
 * Tokens only — no raw hex (rgba(255,255,255,0.06) border is sibling-consistent).
 * Security: no passwords / session IDs in this component.
 */
import {
  useNotificationPreferences,
  useUpdateNotificationPreferences,
  NotificationCategory,
  type NotificationPreferenceItemDto,
} from '@learnexia/api-client';
import { Switch } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../../src/components/ServerErrorBanner';
import { useServerError } from '../../../../src/hooks/useServerError';

interface NotificationsPanelProps {
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
}

/**
 * Numeric NotificationCategory → i18n key segment.
 * Values: 0 = WeeklyReport, 1 = StreakAtRisk, 2 = ProductAnnouncement, 3 = Achievement.
 * The generated enum uses _0/_1/_2/_3 names — we map positionally.
 */
const CATEGORY_KEY: Record<number, string> = {
  [NotificationCategory._0]: 'weeklyReport',
  [NotificationCategory._1]: 'streakAtRisk',
  [NotificationCategory._2]: 'productAnnouncement',
  [NotificationCategory._3]: 'achievement',
};

/** Canonical ordering of categories (matches BE enum 0..3). */
const CATEGORY_ORDER = [
  NotificationCategory._0,
  NotificationCategory._1,
  NotificationCategory._2,
  NotificationCategory._3,
] as const;

/** Default preferences used when BE returns empty or on first paint. */
const DEFAULT_PREFERENCES: NotificationPreferenceItemDto[] = CATEGORY_ORDER.map((cat) => ({
  category: cat,
  emailEnabled: false,
  pushEnabled: false,
}));

function PanelSurface({ children }: { children: React.ReactNode }) {
  return (
    <Stack
      flexDirection="column"
      gap={18}
      borderRadius="$modal"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      padding={22}
    >
      {children}
    </Stack>
  );
}

function PanelHeader({
  title,
  subtitle,
  direction,
}: {
  title: string;
  subtitle: string;
  direction: 'ltr' | 'rtl';
}) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text
        color="$fg1"
        fontSize={16}
        fontWeight="800"
        fontFamily="$heading"
        writingDirection={direction}
        textAlign={direction === 'rtl' ? 'right' : 'left'}
      >
        {title}
      </Text>
      <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction} textAlign={direction === 'rtl' ? 'right' : 'left'}>
        {subtitle}
      </Text>
    </Stack>
  );
}

export function NotificationsPanel({ direction, rowDir }: NotificationsPanelProps) {
  const { t } = useTranslation();
  const prefsQuery = useNotificationPreferences();
  const updateMutation = useUpdateNotificationPreferences();
  const resolveError = useServerError();

  // Local preferences array — seeded from the query, mutated optimistically.
  const [localPrefs, setLocalPrefs] = useState<NotificationPreferenceItemDto[]>(DEFAULT_PREFERENCES);

  useEffect(() => {
    if (prefsQuery.data?.preferences && prefsQuery.data.preferences.length > 0) {
      setLocalPrefs(
        CATEGORY_ORDER.map((cat) => {
          const match = prefsQuery.data!.preferences!.find((p) => p.category === cat);
          return match ?? { category: cat, emailEnabled: false, pushEnabled: false };
        }),
      );
    }
  }, [prefsQuery.data]);

  // Success strip auto-dismiss
  const [showSuccess, setShowSuccess] = useState(false);
  const successTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const serverMessage = updateMutation.isError
    ? resolveError(updateMutation.error, {
        byStatus: { 400: 'parent.settings.notifications.saveError' },
      })
    : null;

  function handleToggle(
    category: NotificationCategory,
    channel: 'emailEnabled' | 'pushEnabled',
    next: boolean,
  ) {
    // Clone the full array with the toggled item mutated.
    const previous = [...localPrefs];
    const updated = localPrefs.map((p) =>
      p.category === category ? { ...p, [channel]: next } : p,
    );
    // Optimistic update.
    setLocalPrefs(updated);

    // Fire the full-array PUT.
    updateMutation.mutate(
      { preferences: updated },
      {
        onError: () => {
          // Rollback.
          setLocalPrefs(previous);
        },
        onSuccess: () => {
          // Show success strip + auto-dismiss after 3 s.
          setShowSuccess(true);
          if (successTimerRef.current) clearTimeout(successTimerRef.current);
          successTimerRef.current = setTimeout(() => setShowSuccess(false), 3000);
        },
      },
    );
  }

  if (prefsQuery.isPending) {
    return (
      <PanelSurface>
        <PanelHeader
          title={t('parent.settings.notifications.title')}
          subtitle={t('parent.settings.notifications.subtitle')}
          direction={direction}
        />
        <Stack alignItems="center" paddingVertical="$8">
          <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
            {t('common.loading')}
          </Text>
        </Stack>
      </PanelSurface>
    );
  }

  const emailLabel = t('parent.settings.notifications.channel.email');
  const pushLabel = t('parent.settings.notifications.channel.push');

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.notifications.title')}
        subtitle={t('parent.settings.notifications.subtitle')}
        direction={direction}
      />

      {/* Success strip */}
      {showSuccess ? (
        <Stack
          backgroundColor="$successSoft"
          borderRadius="$sm"
          padding={12}
          accessibilityLiveRegion="polite"
        >
          <Text
            color="$success"
            fontSize={14}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.settings.notifications.saveSuccess')}
          </Text>
        </Stack>
      ) : null}

      {/* Error banner */}
      <ServerErrorBanner message={serverMessage} direction={direction} />

      {/* 4-category rows */}
      <Stack flexDirection="column" gap={14}>
        {CATEGORY_ORDER.map((cat) => {
          const pref = localPrefs.find((p) => p.category === cat) ?? {
            category: cat,
            emailEnabled: false,
            pushEnabled: false,
          };
          const catKey = CATEGORY_KEY[cat as number];
          const categoryLabel = t(`parent.settings.notifications.category.${catKey}.label`);
          const helperText = t(`parent.settings.notifications.category.${catKey}.helper`);

          return (
            <Stack
              key={cat}
              flexDirection={rowDir}
              alignItems="center"
              gap={16}
              paddingVertical={14}
              paddingHorizontal={16}
              backgroundColor="$bgElevated"
              borderRadius={14}
              borderWidth={1}
              borderColor="rgba(255,255,255,0.06)"
              accessibilityLabel={categoryLabel}
            >
              {/* Category label + helper */}
              <Stack flex={1} gap={2}>
                <Text
                  color="$fg1"
                  fontSize={14}
                  fontWeight="700"
                  fontFamily="$body"
                  writingDirection={direction}
                  textAlign={direction === 'rtl' ? 'right' : 'left'}
                >
                  {categoryLabel}
                </Text>
                <Text
                  color="$fg3"
                  fontSize={12}
                  fontFamily="$body"
                  writingDirection={direction}
                  textAlign={direction === 'rtl' ? 'right' : 'left'}
                >
                  {helperText}
                </Text>
              </Stack>

              {/* Email + Push switches */}
              <Stack
                flexDirection={rowDir}
                gap={24}
                alignItems="center"
              >
                {/* Email switch */}
                <Stack alignItems="center" gap={6}>
                  <Text
                    color="$fg3"
                    fontSize={11}
                    fontWeight="600"
                    fontFamily="$body"
                    // AR: no uppercase — drop textTransform
                    textTransform={direction === 'ltr' ? 'uppercase' : undefined}
                    letterSpacing={direction === 'ltr' ? 0.04 : undefined}
                    writingDirection={direction}
                  >
                    {emailLabel}
                  </Text>
                  <Switch
                    value={pref.emailEnabled ?? false}
                    onValueChange={(next) => handleToggle(cat, 'emailEnabled', next)}
                    accessibilityLabel={`${categoryLabel} — ${emailLabel}`}
                    direction={direction}
                    disabled={updateMutation.isPending}
                    testID={`notification-${catKey}-email`}
                  />
                </Stack>

                {/* Push switch */}
                <Stack alignItems="center" gap={6}>
                  <Text
                    color="$fg3"
                    fontSize={11}
                    fontWeight="600"
                    fontFamily="$body"
                    textTransform={direction === 'ltr' ? 'uppercase' : undefined}
                    letterSpacing={direction === 'ltr' ? 0.04 : undefined}
                    writingDirection={direction}
                  >
                    {pushLabel}
                  </Text>
                  <Switch
                    value={pref.pushEnabled ?? false}
                    onValueChange={(next) => handleToggle(cat, 'pushEnabled', next)}
                    accessibilityLabel={`${categoryLabel} — ${pushLabel}`}
                    direction={direction}
                    disabled={updateMutation.isPending}
                    testID={`notification-${catKey}-push`}
                  />
                </Stack>
              </Stack>
            </Stack>
          );
        })}
      </Stack>
    </PanelSurface>
  );
}

NotificationsPanel.displayName = 'NotificationsPanel';
