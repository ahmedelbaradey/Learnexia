/**
 * MyChildrenWeb — the parent-dashboard "My Children" main content (capture
 * `web/04-my-children.png`): page header (title + subtitle + period select +
 * Send Report), the family-summary strip, a "pick a child" row with "+ Add
 * Child", the responsive grid of child cards, and the trailing dashed add card.
 *
 * Children come from `useMyChildren` (P1-04); per-child + family stats are
 * Phase-5 stubs (TODO(P5)). "Send Report" and the period select are no-op stubs
 * (Phase 5 — analytics). RTL + ar/en throughout; tokens only.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Button, Select } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { MotiView } from 'moti';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { AddChildCard } from './AddChildCard';
import { ChildDashboardCard } from './ChildDashboardCard';
import { FamilySummaryStrip } from './FamilySummaryStrip';
import { getChildStatsStub, getFamilyTotalsStub } from './parentDashboardStubs';

/** Fixed reporting-period set (enum-style; only "this week" wired in P1-11). */
const REPORTING_PERIOD = {
  ThisWeek: 'thisWeek',
} as const;

function CardSkeleton() {
  return (
    <MotiView
      from={{ opacity: 0.4 }}
      animate={{ opacity: 0.7 }}
      transition={{ type: 'timing', duration: 400, loop: true, repeatReverse: true }}
      style={{ flex: 1, minWidth: 300 }}
    >
      <Stack height={260} borderRadius="$card" backgroundColor="$card" borderWidth={1} borderColor="$border" />
    </MotiView>
  );
}

export function MyChildrenWeb() {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const router = useRouter();
  const query = useMyChildren();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);

  const rowDir = isRtl ? 'row-reverse' : 'row';
  const children = query.data ?? [];
  const childIds = children.map((c) => String(c.id));
  const totals = getFamilyTotalsStub(childIds);

  return (
    <Stack flexDirection="column" gap="$6" padding="$6" maxWidth={1200} width="100%" alignSelf="center">
      {/* Header */}
      <Stack flexDirection={rowDir} alignItems="flex-start" justifyContent="space-between" gap="$4" flexWrap="wrap">
        <Stack flexDirection="column" gap="$1">
          <Text color="$fg1" fontSize={26} fontWeight="800" fontFamily="$heading" accessibilityRole="header" writingDirection={direction}>
            {t('parent.myChildren.title')}
          </Text>
          <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
            {t('parent.myChildren.subtitle', { count: children.length })}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Stack width={150}>
            <Select
              label={t('parent.myChildren.periodLabel')}
              value={period}
              onChange={(v) => setPeriod(String(v))}
              options={[{ value: REPORTING_PERIOD.ThisWeek, label: t('parent.myChildren.periodThisWeek') }]}
              direction={direction}
              accessibilityLabel={t('parent.myChildren.periodLabel')}
            />
          </Stack>
          {/* Send Report — Phase-5 stub (no-op until analytics ship). */}
          <Button
            variant="primary"
            size="sm"
            accessibilityLabel={t('parent.myChildren.sendReport')}
            onPress={() => {
              /* TODO(P5): wire to the reports endpoint. */
            }}
          >
            {t('parent.myChildren.sendReport')}
          </Button>
        </Stack>
      </Stack>

      {/* Family summary (TODO(P5) stub totals) */}
      <FamilySummaryStrip totals={totals} />

      {/* Pick-a-child row */}
      <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap="$4" flexWrap="wrap">
        <Text color="$fg1" fontSize={18} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
          {t('parent.myChildren.pickChild')}
        </Text>
        <Button
          variant="primary"
          size="sm"
          accessibilityLabel={t('parent.myChildren.addChild')}
          onPress={() => router.push('/(onboarding)/add-child')}
        >
          {t('parent.myChildren.addChild')}
        </Button>
      </Stack>

      {/* Error state */}
      {query.isError ? (
        <Stack alignItems="center" gap="$4" paddingVertical="$8">
          <Text color="$fg3" fontSize={15} fontFamily="$body" textAlign="center" writingDirection={direction}>
            {t('parent.myChildren.loadError')}
          </Text>
          <Button variant="ghost" accessibilityLabel={t('common.retry')} onPress={() => query.refetch()}>
            {t('common.retry')}
          </Button>
        </Stack>
      ) : (
        /* Card grid (wraps responsively: 3-across on wide, stacks down) */
        <Stack flexDirection={rowDir} flexWrap="wrap" gap="$4" alignItems="stretch">
          {query.isLoading ? (
            <>
              <CardSkeleton />
              <CardSkeleton />
              <CardSkeleton />
            </>
          ) : (
            <>
              {children.map((child) => {
                const id = String(child.id);
                return (
                  <ChildDashboardCard
                    key={id}
                    fullName={child.fullName ?? ''}
                    stats={getChildStatsStub(id)}
                    onViewDashboard={() => router.push('/(parent)')}
                  />
                );
              })}
              <AddChildCard onPress={() => router.push('/(onboarding)/add-child')} />
            </>
          )}
        </Stack>
      )}
    </Stack>
  );
}

MyChildrenWeb.displayName = 'MyChildrenWeb';
