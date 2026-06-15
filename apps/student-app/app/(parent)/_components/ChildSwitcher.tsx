/**
 * ChildSwitcher — compact pill that shows the active child and opens a dropdown
 * to select a different child. Lives in the shell header (logical START).
 *
 * State: reads from `useActiveChildStore` (new, lead-approved Zustand store that
 * mirrors `localeStore`). The resolved active child is the one whose id matches
 * `activeChildId` in the store, falling back to `children[0]`.
 *
 * Dropdown: `$card` popover with one row per child, footer "+ Add child" CTA that
 * opens the AddChildModal. Active child row highlighted with `$primarySoft`.
 *
 * RTL: `dir={isRtl?'rtl':'ltr'}` on the root pill + dropdown panel; rows use
 * plain `flexDirection="row"` (the proven pattern — browser flips once). Chevron
 * literal `‹` / `›`; avatar gradient NOT mirrored; Eastern-Arabic numerals via
 * Intl for grade/level meta.
 *
 * No new design pattern — mirrors the sidebar child-selector card grammar, driven
 * by the same Zustand store shape as `localeStore`. Lead-approved (spec §A.4).
 */
import { useMyChildren } from '@learnexia/api-client';
import { Avatar } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { getChildStatsStub } from './parentDashboardStubs';

export function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(value);
}

export interface ChildSwitcherProps {
  /** Called when the user taps "+ Add child" in the dropdown. */
  onAddChild: () => void;
  /**
   * Compact pill: avatar + name + chevron only (no meta subtitle), shrink-to-content.
   * Used in the responsive header. The dropdown is unchanged.
   */
  compact?: boolean;
}

export function ChildSwitcher({ onAddChild, compact = false }: ChildSwitcherProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const query = useMyChildren();
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const setActiveChildId = useActiveChildStore((s) => s.setActiveChildId);
  const [open, setOpen] = useState(false);

  const children = query.data ?? [];

  // Resolve the active child: stored id if still valid, else first child.
  const activeChild =
    (activeChildId ? children.find((c) => String(c.id) === activeChildId) : null) ??
    children[0] ??
    null;

  const activeStats = activeChild ? getChildStatsStub(String(activeChild.id)) : null;

  const pillLabel = activeChild
    ? activeChild.fullName
    : t('parent.childSwitcher.noChildren');

  const metaLabel = activeStats
    ? t('parent.childSwitcher.metaLabel', {
        grade: formatNumber(activeStats.grade, locale),
        level: formatNumber(activeStats.level, locale),
      })
    : null;

  function handleSelect(childId: string) {
    setActiveChildId(childId);
    setOpen(false);
  }

  return (
    <Stack position="relative" dir={isRtl ? 'rtl' : 'ltr'}>
      {/* Pill — closed state */}
      <Stack
        testID="child-switcher-pill"
        flexDirection="row"
        alignItems="center"
        gap={compact ? 8 : '$2'}
        paddingVertical={compact ? 8 : 12}
        paddingHorizontal={compact ? 10 : 12}
        // Compact = slim capsule (design `PDChildSwitcher compact`); full = rounded card.
        borderRadius={compact ? 9999 : 16}
        backgroundColor="$card"
        borderWidth={1}
        borderColor={open ? 'rgba(99,102,241,0.6)' : '$border'}
        cursor="pointer"
        hoverStyle={{ backgroundColor: '$cardSoft' }}
        pressStyle={{ scale: 0.95 }}
        onPress={() => setOpen((v) => !v)}
        accessibilityRole="button"
        accessible
        accessibilityLabel={pillLabel}
        aria-label={pillLabel}
        aria-expanded={open}
      >
        {activeChild ? (
          <Avatar name={activeChild.fullName ?? ''} size={compact ? 'xs' : 'sm'} />
        ) : (
          <Text fontSize={14} accessibilityElementsHidden>
            {'👶'}
          </Text>
        )}
        <Stack flexDirection="column" flex={compact ? undefined : 1}>
          <Text
            color="$fg1"
            fontSize={13}
            fontWeight="700"
            fontFamily="$heading"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
          >
            {pillLabel}
          </Text>
          {!compact && metaLabel ? (
            <Text color="$fg3" fontSize={11} fontFamily="$body" writingDirection={direction} textAlign={isRtl ? 'right' : 'left'}>
              {metaLabel}
            </Text>
          ) : null}
        </Stack>
        <Text
          color="$fg3"
          fontSize={13}
          accessibilityElementsHidden
          style={{ transform: [{ rotate: open ? (isRtl ? '-90deg' : '90deg') : '0deg' }] } as object}
        >
          {isRtl ? '‹' : '›'}
        </Text>
      </Stack>

      {/* Dropdown — open state */}
      {open ? (
        <>
          {/* Backdrop — closes dropdown on outside click */}
          <Stack
            position="fixed"
            top={0}
            left={0}
            right={0}
            bottom={0}
            zIndex={99}
            onPress={() => setOpen(false)}
            style={{ cursor: 'default' }}
          />
          <Stack
            dir={isRtl ? 'rtl' : 'ltr'}
            testID="child-switcher-dropdown"
            position="absolute"
            // Sits just below the compact pill (small gap), NOT pill-width: a fixed
            // comfortable width so rows don't wrap. Opens toward the inline-start to
            // stay on-screen (pill is at the header's inline-end).
            top={48}
            right={isRtl ? undefined : 0}
            left={isRtl ? 0 : undefined}
            minWidth={240}
            zIndex={100}
            backgroundColor="$card"
            borderRadius={16}
            borderWidth={1}
            borderColor="rgba(255,255,255,0.1)"
            style={{ boxShadow: '0 20px 50px rgba(0,0,0,0.55)' }}
            overflow="hidden"
            flexDirection="column"
            padding={8}
            gap={2}
          >
            {/* "SWITCH CHILD" section header */}
            <Stack paddingHorizontal={14} paddingTop={4} paddingBottom={6}>
              <Text
                color="$fg3"
                fontSize={10}
                fontWeight="800"
                fontFamily="$heading"
                textTransform="uppercase"
                letterSpacing={1.2}
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {t('parent.childSelector.switchChild')}
              </Text>
            </Stack>

            {children.length === 0 ? (
              <Stack padding={14}>
                <Text color="$fg3" fontSize={13} fontFamily="$body" writingDirection={direction}>
                  {t('parent.childSwitcher.noChildren')}
                </Text>
              </Stack>
            ) : (
              children.map((child) => {
                const childId = String(child.id);
                const isActive = childId === (activeChildId ?? String(children[0]?.id));
                const stats = getChildStatsStub(childId);
                return (
                  <Stack
                    key={childId}
                    flexDirection="row"
                    alignItems="center"
                    gap={10}
                    paddingVertical={8}
                    paddingHorizontal={10}
                    borderRadius={12}
                    backgroundColor={isActive ? '$primarySoft' : 'transparent'}
                    hoverStyle={{ backgroundColor: isActive ? '$primarySoft' : '$cardSoft' }}
                    cursor="pointer"
                    pressStyle={{ scale: 0.97 }}
                    onPress={() => handleSelect(childId)}
                    accessibilityRole="menuitem"
                    accessible
                    accessibilityState={{ selected: isActive }}
                    accessibilityLabel={child.fullName ?? ''}
                  >
                    <Avatar name={child.fullName ?? ''} size="sm" />
                    <Stack flexDirection="column" flex={1}>
                      <Text
                        color={isActive ? '$primaryLight' : '$fg1'}
                        fontSize={13}
                        fontWeight="700"
                        fontFamily="$heading"
                        writingDirection={direction}
                        textAlign={isRtl ? 'right' : 'left'}
                      >
                        {child.fullName ?? ''}
                      </Text>
                      <Text
                        color="$fg3"
                        fontSize={11}
                        fontFamily="$body"
                        writingDirection={direction}
                        textAlign={isRtl ? 'right' : 'left'}
                      >
                        {t('parent.childSwitcher.metaLabel', {
                          grade: formatNumber(stats.grade, locale),
                          level: formatNumber(stats.level, locale),
                        })}
                      </Text>
                    </Stack>
                    {isActive ? (
                      <Text color="$primaryLight" fontSize={14} accessibilityElementsHidden>
                        {'✓'}
                      </Text>
                    ) : null}
                  </Stack>
                );
              })
            )}

            {/* Divider */}
            <Stack height={1} backgroundColor="$borderSubtle" marginHorizontal={10} marginVertical={4} />

            {/* Add a child footer — dashed circle "+" + label */}
            <Stack
              flexDirection="row"
              alignItems="center"
              gap={10}
              paddingVertical={8}
              paddingHorizontal={10}
              borderRadius={12}
              cursor="pointer"
              hoverStyle={{ backgroundColor: '$cardSoft' }}
              pressStyle={{ scale: 0.97 }}
              onPress={() => {
                setOpen(false);
                onAddChild();
              }}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('parent.childSelector.addChild')}
            >
              {/* Dashed-circle "+" icon — 32px matching spec */}
              <Stack
                width={32}
                height={32}
                borderRadius={16}
                borderWidth={1.5}
                alignItems="center"
                justifyContent="center"
                style={{ borderStyle: 'dashed', borderColor: 'rgba(165,180,252,0.5)' } as object}
              >
                <Text color="$primaryLight" fontSize={18} fontWeight="700" accessibilityElementsHidden>
                  {'+'}
                </Text>
              </Stack>
              <Text
                color="$primaryLight"
                fontSize={13}
                fontWeight="700"
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
                flex={1}
              >
                {t('parent.childSelector.addChild')}
              </Text>
            </Stack>
          </Stack>
        </>
      ) : null}
    </Stack>
  );
}

ChildSwitcher.displayName = 'ChildSwitcher';
