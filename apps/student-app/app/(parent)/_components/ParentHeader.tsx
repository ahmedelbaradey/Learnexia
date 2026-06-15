/**
 * ParentHeader — the unified, responsive parent-surface page header
 * (design `DashboardComponents.jsx` → `PDHeader`).
 *
 * Layout (single row, `space-between`):
 *   - inline-START: page title (22/800, `$fg1`) + optional subtitle (13, `$fg3`).
 *   - inline-END (cluster, gap 10, center):
 *       · narrow (<768): `ChildSwitcher` (compact) + `AccountMenu`.
 *       · wide   (≥768): wide-only `actions` + `AccountMenu` — NO ChildSwitcher
 *         (on desktop the child switcher lives in the Sidebar).
 *
 * Responsive: ChildSwitcher renders ONLY on narrow (< `WIDE_BREAKPOINT` = 768);
 * the `actions` slot (e.g. period select + Send Report) renders ONLY on wide
 * (≥768) — matching the design's `pd-hide-sm` class. AccountMenu renders on
 * every width so account controls stay reachable.
 *
 * This component REPLACES the per-page header blocks that each parent page used
 * to render itself, AND the slim shell header rows the `_layout` used to carry
 * (ChildSwitcher / AccountMenu). It is the one place the controls live now.
 *
 * State: `ChildSwitcher`'s "+ Add child" is wired to `useActiveChildStore`'s
 * `openAddChild` so the AddChildModal (mounted in `_layout`) opens from any page.
 *
 * BUILD CONTRACT:
 *   - Numeric border radii (none needed here — only a bottom border).
 *   - RTL: NO `flexDirection:'row-reverse'`. An explicit `dir={isRtl?'rtl':'ltr'}`
 *     on the header root flips the row once deterministically;
 *     `justifyContent="space-between"` keeps title at inline-start and the
 *     action cluster at inline-end in both directions. Title/subtitle mirror via
 *     `writingDirection` + `textAlign`.
 *   - Right-side action cluster: `flexShrink={0}`. Title column: `flex={1}` +
 *     truncates (`numberOfLines={1}`).
 *   - Colors via tokens; spec literals where no token exists.
 *   - No new design pattern — composes existing ChildSwitcher + AccountMenu.
 */
import { Stack, Text } from '@tamagui/core';
import React, { type ReactNode } from 'react';
import { useWindowDimensions } from 'react-native';

import { useLocale } from '../../../src/hooks/useLocale';
import { useActiveChildStore } from '../../../src/providers/activeChildStore';
import { AccountMenu } from './AccountMenu';
import { ChildSwitcher } from './ChildSwitcher';

/** Sidebar/actions appear at the tablet breakpoint and up (matches the shell). */
const WIDE_BREAKPOINT = 768;

export interface ParentHeaderProps {
  /** Page title (inline-start, 22/800). */
  title: string;
  /** Optional page subtitle / date-range line (inline-start, 13, `$fg3`). */
  subtitle?: string;
  /**
   * Wide-only trailing extras (e.g. the reporting-period select + "Send Report"
   * button). Rendered ONLY at ≥768 — hidden on narrow/mobile, matching the
   * design's `pd-hide-sm`. ChildSwitcher + AccountMenu always render.
   */
  actions?: ReactNode;
}

export function ParentHeader({ title, subtitle, actions }: ParentHeaderProps) {
  const { direction, isRtl } = useLocale();
  const { width } = useWindowDimensions();
  const isWide = width >= WIDE_BREAKPOINT;

  const openAddChild = useActiveChildStore((s) => s.openAddChild);

  return (
    <Stack
      dir={isRtl ? 'rtl' : 'ltr'}
      testID="parent-header"
      flexDirection="row"
      alignItems="center"
      justifyContent="space-between"
      gap="$3"
      paddingVertical={20}
      paddingHorizontal={isWide ? 32 : 16}
      borderBottomWidth={1}
      borderBottomColor="$borderSubtle"
    >
      {/* inline-START: title + subtitle column (truncates so the action
          cluster never gets pushed off-screen on narrow). */}
      <Stack flexDirection="column" flex={1} gap={2} minWidth={0}>
        <Text
          color="$fg1"
          fontSize={22}
          fontWeight="800"
          fontFamily="$heading"
          accessibilityRole="header"
          writingDirection={direction}
          textAlign={isRtl ? 'right' : 'left'}
          numberOfLines={1}
        >
          {title}
        </Text>
        {subtitle ? (
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={isRtl ? 'right' : 'left'}
            numberOfLines={1}
          >
            {subtitle}
          </Text>
        ) : null}
      </Stack>

      {/* inline-END: action cluster — never shrinks. Narrow: ChildSwitcher +
          AccountMenu. Wide: wide-only actions + AccountMenu (no ChildSwitcher —
          it lives in the Sidebar on desktop). `dir` flips the visual order in
          RTL; we keep a natural source order (no row-reverse). */}
      <Stack flexDirection="row" alignItems="center" gap={10} flexShrink={0}>
        {isWide ? actions : <ChildSwitcher compact onAddChild={openAddChild} />}
        <AccountMenu />
      </Stack>
    </Stack>
  );
}

ParentHeader.displayName = 'ParentHeader';
