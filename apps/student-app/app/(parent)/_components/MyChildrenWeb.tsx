/**
 * MyChildrenWeb — the parent-dashboard "My Children" main content (captures
 * `web/04-my-children.png` + `web-ar/04`): a page header row (title + subtitle
 * + period select + Send Report) with its own padding and a hairline bottom
 * border, the family-summary hero, a "pick a child" row with "+ Add Child", a
 * 3-column grid of child cards, and the trailing dashed add card.
 *
 * Children come from `useMyChildren` (P1-04); per-child + family stats are
 * Phase-5 stubs (TODO(P5)). "Send Report" and the period select are no-op stubs
 * (Phase 5 — analytics). RTL + ar/en throughout; tokens only.
 *
 * Layout (align-my-children C-43): the card grid is a CSS Grid `repeat(3,1fr)`
 * on web (in AR the grid container is direction:rtl so the columns reverse and
 * the add-card lands at the visual left); native falls back to a wrapping flex
 * row.
 */
import { useMyChildren } from '@learnexia/api-client';
import { Button, Select } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { MotiView } from 'moti';
import React, { useState } from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { AddChildCard } from './AddChildCard';
import { ChildDashboardCard } from './ChildDashboardCard';
import { EditLinkedChildSheet } from './EditLinkedChildSheet';
import { FamilySummaryStrip } from './FamilySummaryStrip';
import { getChildStatsStub, getFamilyTotalsStub } from './parentDashboardStubs';

/** Fixed reporting-period set (enum-style; only "this week" wired in P1-11). */
const REPORTING_PERIOD = {
  ThisWeek: 'thisWeek',
} as const;

const IS_WEB = Platform.OS === 'web';

function CardSkeleton() {
  return (
    <MotiView
      from={{ opacity: 0.4 }}
      animate={{ opacity: 0.7 }}
      transition={{ type: 'timing', duration: 400, loop: true, repeatReverse: true }}
      style={{ flex: 1, minWidth: 300 }}
    >
      <Stack height={260} borderRadius="$modal" backgroundColor="$card" borderWidth={1} borderColor="rgba(255,255,255,0.06)" />
    </MotiView>
  );
}

export function MyChildrenWeb() {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const router = useRouter();
  const query = useMyChildren();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);

  // Edit-child sheet state.
  const [editTarget, setEditTarget] = useState<{ id: number; fullName: string } | null>(null);

  const rowDir = isRtl ? 'row-reverse' : 'row';
  const children = query.data ?? [];
  const childIds = children.map((c) => String(c.id));
  const totals = getFamilyTotalsStub(childIds);

  // 3-column CSS Grid on web (C-43); AR reverses column order via direction:rtl.
  const gridStyle = IS_WEB
    ? ({
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: 16,
        alignItems: 'stretch',
        direction: isRtl ? 'rtl' : 'ltr',
      } as object)
    : undefined;

  return (
    <Stack flexDirection="column" width="100%">
      {/* Header row — its own 20/32 padding + hairline bottom border (H-1/H-2) */}
      <Stack
        flexDirection={rowDir}
        alignItems="flex-start"
        justifyContent="space-between"
        gap="$4"
        flexWrap="wrap"
        paddingVertical={20}
        paddingHorizontal={32}
        borderBottomWidth={1}
        borderBottomColor="$border"
      >
        <Stack flexDirection="column" gap="$1">
          <Text color="$fg1" fontSize={22} fontWeight="800" fontFamily="$heading" accessibilityRole="header" writingDirection={direction}>
            {t('parent.myChildren.title')}
          </Text>
          <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
            {t('parent.myChildren.subtitle', { count: children.length })}
          </Text>
        </Stack>

        <Stack flexDirection={rowDir} alignItems="center" gap="$3">
          <Stack width={150}>
            <Select
              label={t('parent.myChildren.periodLabel')}
              hideLabel
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

      {/* Content area — 28px gutter, 20px section gap (L-2/L-3). */}
      <Stack flexDirection="column" gap={20} padding={28}>
        {/* Family summary (TODO(P5) stub totals) */}
        <FamilySummaryStrip totals={totals} />

        {/* Pick-a-child row */}
        <Stack flexDirection={rowDir} alignItems="center" justifyContent="space-between" gap="$4" flexWrap="wrap">
          <Text color="$fg1" fontSize={18} fontWeight="800" fontFamily="$heading" writingDirection={direction}>
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
          /* Card grid — 3-col CSS grid on web, wrapping flex on native */
          <Stack
            {...(IS_WEB
              ? { style: gridStyle }
              : { flexDirection: rowDir, flexWrap: 'wrap' as const, gap: '$4', alignItems: 'stretch' })}
          >
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
                      childId={child.id}
                      fullName={child.fullName ?? ''}
                      stats={getChildStatsStub(id)}
                      onViewDashboard={() => router.push('/(parent)')}
                      onEdit={() => setEditTarget({ id: child.id ?? 0, fullName: child.fullName ?? '' })}
                    />
                  );
                })}
                <AddChildCard onPress={() => router.push('/(onboarding)/add-child')} />
              </>
            )}
          </Stack>
        )}
      </Stack>

      {/* Edit-child bottom sheet — mounts once, toggles visibility per editTarget. */}
      {editTarget ? (
        <EditLinkedChildSheet
          visible={Boolean(editTarget)}
          childId={editTarget.id}
          initialFullName={editTarget.fullName}
          onClose={() => setEditTarget(null)}
          onSaved={() => setEditTarget(null)}
        />
      ) : null}
    </Stack>
  );
}

MyChildrenWeb.displayName = 'MyChildrenWeb';
