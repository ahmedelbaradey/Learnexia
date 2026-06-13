/**
 * (parent) route group — shared parent web shell + auth/role guard.
 *
 * SHARED SHELL (parent-dashboard-uiux workstream A — lead-approved pattern):
 * On wide (≥768): renders a shell header (56px) + a row of [Sidebar 240px] +
 * [content scroll container]. All (parent) pages render via <Slot> inside the
 * content scroll container — they no longer need their own row/sidebar.
 *
 * On narrow (<768): no shell header / sidebar — each page uses the mobile
 * ScreenHeader. The Slot is rendered in a plain full-height Stack.
 *
 * Shell header contains (logical-start → logical-end):
 *   [ChildSwitcher pill] .... [LocaleThemeControls (lang + theme toggle)]
 * In RTL the document `dir` flips the row automatically; the header inner row
 * uses `rowDir` (`row-reverse` for RTL) so the switcher and controls swap sides.
 *
 * Brand scrollbar: injected via a web-only <style> element in the shell.
 * The content ScrollView's `style={{ flex: 1 }}` + `contentContainerStyle flexGrow:1`
 * is the single vertical scroll region — fixes the clipped/dead-scroll AC.
 *
 * Sidebar nav reorder: NAV array now has Overview first (index 0), per spec A.2 /
 * lead AC. The Sidebar component reads the reordered NAV. See Sidebar.tsx.
 *
 * RTL: document `dir="rtl"` already flips the [sidebar | content] row once —
 * do NOT add `row-reverse` (would double-flip). The shell-header inner row uses
 * `rowDir` for its own children only.
 *
 * AddChildModal is mounted here (shell level) so it can be opened from any
 * page (Overview empty-state, Settings LinkedChildren, ChildSwitcher footer).
 * The `addChildOpen` flag and `openAddChild`/`closeAddChild` actions live in
 * `activeChildStore` (Zustand) — pages call `openAddChild()` directly from the
 * store instead of receiving a prop callback through `<Slot>` (Expo Router
 * layouts cannot forward props into rendered pages). This is UI state only
 * (no server data in Zustand — consistent with the project rules).
 */
import { useMyChildren } from '@learnexia/api-client';
import { Slot, useSegments } from 'expo-router';
import React from 'react';
import { Platform, ScrollView, useWindowDimensions } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Stack } from '@tamagui/core';

import { useGroupGuard } from '../../src/hooks/useGroupGuard';
import { useLocale } from '../../src/hooks/useLocale';
import { useActiveChildStore } from '../../src/providers/activeChildStore';
import { LocaleThemeControls } from '../(auth)/_components/LocaleThemeControls';
import { AddChildModal } from '../../src/components/AddChildModal';
import { ChildSwitcher } from './_components/ChildSwitcher';
import {
  NAV_ITEM,
  type NavItemKey,
  Sidebar,
} from './_components/Sidebar';
import { getChildStatsStub } from './_components/parentDashboardStubs';

/** Sidebar appears at the tablet breakpoint and up (design-system `media`). */
const WIDE_BREAKPOINT = 768;

/** Map the current route segment to the active nav key. */
function segmentToNavKey(segments: string[]): NavItemKey {
  const last = segments[segments.length - 1];
  switch (last) {
    case 'overview':
      return NAV_ITEM.Overview;
    case 'children':
      return NAV_ITEM.MyChildren;
    case 'reports':
      return NAV_ITEM.Reports;
    case 'settings':
      return NAV_ITEM.Settings;
    default:
      return NAV_ITEM.Overview;
  }
}

/**
 * Brand scrollbar CSS injected once on web. Hex values are sanctioned literals
 * (scrollbar pseudo-elements can't receive Tamagui tokens). Values = $primary /
 * $primaryHover / $bg. Per spec A.5 / index.html L65–75.
 */
const BRAND_SCROLLBAR_CSS = `
* { scrollbar-width: thin; scrollbar-color: #4F46E5 transparent; }
*::-webkit-scrollbar { width: 10px; height: 10px; }
*::-webkit-scrollbar-track { background: rgba(255,255,255,0.03); border-radius: 9999px; }
*::-webkit-scrollbar-thumb {
  background: linear-gradient(180deg, #6366F1, #4F46E5);
  border-radius: 9999px;
  border: 2px solid #0F172A;
}
*::-webkit-scrollbar-thumb:hover { background: linear-gradient(180deg, #818CF8, #6366F1); }
*::-webkit-scrollbar-corner { background: transparent; }
`;

/** Inject the brand scrollbar CSS into the document once. Web-only. */
function useBrandScrollbar() {
  React.useEffect(() => {
    if (Platform.OS !== 'web') return;
    const id = 'lx-brand-scrollbar';
    if (document.getElementById(id)) return;
    const style = document.createElement('style');
    style.id = id;
    style.textContent = BRAND_SCROLLBAR_CSS;
    document.head.appendChild(style);
  }, []);
}

export default function ParentLayout() {
  const { isResolving } = useGroupGuard('(parent)');
  const { direction, isRtl } = useLocale();
  const { width } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const query = useMyChildren();
  const segments = useSegments();
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const addChildModalVisible = useActiveChildStore((s) => s.addChildOpen);
  const openAddChild = useActiveChildStore((s) => s.openAddChild);
  const closeAddChild = useActiveChildStore((s) => s.closeAddChild);

  // Inject brand scrollbar CSS (web only, once).
  useBrandScrollbar();

  // Render nothing while the guard is resolving auth/role (prevents content flash).
  if (isResolving) return null;

  const isWide = width >= WIDE_BREAKPOINT;

  const activeNavKey = segmentToNavKey(segments);
  const children = query.data ?? [];

  // Resolve the active child for the sidebar display.
  const activeChild =
    (activeChildId ? children.find((c) => String(c.id) === activeChildId) : null) ??
    children[0] ??
    undefined;

  const sidebarChild = activeChild
    ? (() => {
        const id = String(activeChild.id);
        const stats = getChildStatsStub(id);
        return {
          id,
          fullName: activeChild.fullName ?? '',
          grade: stats.grade,
          level: stats.level,
        };
      })()
    : undefined;

  const handleAddChild = () => openAddChild();

  if (isWide) {
    // Wide layout: shell header + [sidebar | content scroll container].
    // Direction: `row` always — document `dir="rtl"` flips it once. No row-reverse.
    return (
      <>
        <Stack flex={1} flexDirection="column" backgroundColor="$bg">
          {/* Shell header (56px, sticky) */}
          <Stack
            height={56}
            flexDirection={isRtl ? 'row-reverse' : 'row'}
            alignItems="center"
            justifyContent="space-between"
            paddingHorizontal="$4"
            borderBottomWidth={1}
            borderBottomColor="$borderSubtle"
            backgroundColor="$bg"
            style={{ position: 'sticky', top: 0, zIndex: 40 }}
          >
            {/* Logical START: Child Switcher */}
            <ChildSwitcher onAddChild={handleAddChild} />

            {/* Logical END: Language + Theme controls */}
            <LocaleThemeControls direction={direction} />
          </Stack>

          {/* Body row: sidebar + content */}
          <Stack flex={1} flexDirection="row">
            <Sidebar
              activeChild={sidebarChild}
              activeKey={activeNavKey}
            />
            {/* Content scroll container — the single vertical scroll region */}
            <ScrollView
              style={{ flex: 1 }}
              contentContainerStyle={{ flexGrow: 1, paddingBottom: 48 }}
            >
              <Slot />
            </ScrollView>
          </Stack>
        </Stack>

        <AddChildModal
          visible={addChildModalVisible}
          onClose={() => closeAddChild()}
        />
      </>
    );
  }

  // Narrow layout: compact header (LocaleThemeControls + ChildSwitcher) above Slot.
  // Per spec §A.1: locale/theme controls trailing-aligned; child-switcher full-width
  // pill below; RTL-correct via rowDir.
  return (
    <>
      <Stack flex={1} flexDirection="column" backgroundColor="$bg" paddingTop={insets.top}>
        {/* Compact mobile header */}
        <Stack flexDirection="column" paddingHorizontal="$4" paddingBottom="$3" gap="$2">
          {/* Row 1: trailing-aligned LocaleThemeControls */}
          <Stack
            flexDirection={isRtl ? 'row-reverse' : 'row'}
            justifyContent="flex-end"
            alignItems="center"
            paddingTop="$2"
          >
            <LocaleThemeControls direction={direction} />
          </Stack>
          {/* Row 2: full-width ChildSwitcher pill */}
          <ChildSwitcher onAddChild={handleAddChild} />
        </Stack>
        <Slot />
      </Stack>

      <AddChildModal
        visible={addChildModalVisible}
        onClose={() => closeAddChild()}
      />
    </>
  );
}
