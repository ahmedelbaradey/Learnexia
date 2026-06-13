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
 *
 * Edit mode (bug #4 fix): pressing ✎ on a child card opens AddChildModal in
 * edit mode (childId + initialValues) — same centered overlay as add mode,
 * pre-filled with the child's real data, submitting via useUpdateChild.
 * EditChildSheet (bottom-slide) is kept for the onboarding flow only.
 *
 * Bug #3 / #6 fix: pick-a-child row uses `flexWrap="nowrap"` so that in
 * RTL `row-reverse` the button stays in-row and does not wrap to an unexpected
 * position where pointer-events fail. Text alignment follows locale direction.
 */
import { useLocalizedRouter } from '@/hooks/useLocalizedRouter';
import { useMyChildren } from '@learnexia/api-client';
import { Button, Select } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { MotiView } from 'moti';
import React, { useState } from 'react';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocale } from '@/hooks/useLocale';
import { useActiveChildStore } from '@/providers/activeChildStore';
import {
  AddChildModal,
  type EditChildInitialValues,
} from '@/components/AddChildModal';
import { AddChildCard } from './AddChildCard';
import { ChildDashboardCard } from './ChildDashboardCard';
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

/** Shape of the child being edited — seeded from useMyChildren data. */
interface EditingChild {
  id: number;
  initialValues: EditChildInitialValues;
}

export function MyChildrenWeb() {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const router = useLocalizedRouter();
  const query = useMyChildren();
  const [period, setPeriod] = useState<string>(REPORTING_PERIOD.ThisWeek);
  /**
   * Edit-child state (bug #4 fix):
   * null = edit modal closed; non-null = edit modal open for this child.
   * Uses AddChildModal in edit mode (childId + initialValues) so the same
   * centered overlay that handles Add also handles Edit — pre-filled,
   * correct title, and submitting via useUpdateChild.
   */
  const [editingChild, setEditingChild] = useState<EditingChild | null>(null);

  const openAddChild = useActiveChildStore((s) => s.openAddChild);
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
          <Text
            color="$fg1"
            fontSize={22}
            fontWeight="800"
            fontFamily="$heading"
            accessibilityRole="header"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
          >
            {t('parent.myChildren.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
          >
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

        {/* Pick-a-child row (bug #3 / #6 fix):
            `flexWrap="nowrap"` prevents RTL `row-reverse` from wrapping the
            Add Child button to an unexpected position where it becomes
            un-clickable. Both children stay on one line; the Button sits at
            the logical start (left in RTL) and the label at the logical end
            (right in RTL). */}
        <Stack
          flexDirection={rowDir}
          alignItems="center"
          justifyContent="space-between"
          gap="$4"
          flexWrap="nowrap"
        >
          <Text
            color="$fg1"
            fontSize={18}
            fontWeight="800"
            fontFamily="$heading"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
            flex={1}
          >
            {t('parent.myChildren.pickChild')}
          </Text>
          <Button
            variant="primary"
            size="sm"
            accessibilityLabel={t('parent.myChildren.addChild')}
            onPress={() => openAddChild()}
            testID="my-children-add-button"
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
            testID="my-children-list"
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
                  const childId = child.id ?? 0;
                  // Card stats (XP/mastery) remain Phase-5 stubs. The edit modal,
                  // however, pre-fills the child's REAL grade/language (returned on
                  // LinkedChildResponse) so a save can't silently overwrite them
                  // with placeholders.
                  const stub = getChildStatsStub(id);
                  return (
                    <ChildDashboardCard
                      key={id}
                      testID={`child-card-${id}`}
                      fullName={child.fullName ?? ''}
                      stats={stub}
                      onViewDashboard={() => router.push('/(parent)')}
                      onEdit={() =>
                        setEditingChild({
                          id: childId,
                          initialValues: {
                            fullName: child.fullName ?? '',
                            grade: child.grade ?? stub.grade,
                            language: child.language ?? 'ar',
                            learningLanguage: child.learningLanguage ?? undefined,
                          },
                        })
                      }
                    />
                  );
                })}
                <AddChildCard onPress={() => openAddChild()} />
              </>
            )}
          </Stack>
        )}
      </Stack>

      {/*
       * Edit-child modal (bug #4 fix) — AddChildModal in edit mode.
       * Opens when a child card's Edit button is pressed. On save, the hook
       * invalidates queryKeys.family.myChildren() so the list refreshes.
       * Using the same centered modal as Add mode keeps the UX consistent;
       * the modal title and CTA label change, email/password are hidden,
       * and useUpdateChild is used for the API call.
       */}
      <AddChildModal
        visible={editingChild !== null}
        childId={editingChild?.id}
        initialValues={editingChild?.initialValues}
        onClose={() => setEditingChild(null)}
      />
    </Stack>
  );
}

MyChildrenWeb.displayName = 'MyChildrenWeb';
