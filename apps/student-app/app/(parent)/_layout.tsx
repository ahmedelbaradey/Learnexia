/**
 * (parent) route group — shared parent web shell + auth/role guard.
 *
 * SHARED SHELL (parent-dashboard-uiux workstream A — lead-approved pattern):
 * On wide (≥768): renders a [Sidebar 240px] + [content scroll container] row
 * with NO shell header. Locale/theme controls now live at the bottom of the
 * Sidebar. All (parent) pages render via <Slot> inside the content scroll
 * container — they no longer need their own row/sidebar.
 *
 * On narrow (<768): no sidebar — compact header has LocaleThemeControls +
 * ChildSwitcher above the Slot. The Slot is rendered in a plain full-height Stack.
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
import { AddChildModal } from '../../src/components/AddChildModal';
import {
  NAV_ITEM,
  type NavItemKey,
  Sidebar,
} from './_components/Sidebar';
import { getChildStatsStub } from './_components/parentDashboardStubs';
import { ParentTabBar, PARENT_TAB_BAR_CLEARANCE } from './_components/ParentTabBar';

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
      // Per-child drill-down (`child/[id]`) is reached from My Children — keep
      // that nav item highlighted while the pushed sub-screen is open. The last
      // segment for the dynamic route is the id, so we match on the `child`
      // parent segment rather than `last`.
      if (segments.includes('child')) return NAV_ITEM.MyChildren;
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
  const { isRtl } = useLocale();
  const { width } = useWindowDimensions();
  const insets = useSafeAreaInsets();
  const query = useMyChildren();
  const segments = useSegments();
  const activeChildId = useActiveChildStore((s) => s.activeChildId);
  const addChildModalVisible = useActiveChildStore((s) => s.addChildOpen);
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

  if (isWide) {
    // Wide layout: [sidebar | content]. No slim shell header — the per-page
    // ParentHeader now carries the ChildSwitcher + AccountMenu (and the
    // wide-only period select + Send Report). row-reverse in RTL puts the
    // sidebar (first in DOM) on the RIGHT and content on the LEFT.
    return (
      <>
        <Stack flex={1} flexDirection="column" backgroundColor="$bg">
          {/* Body row: sidebar + content. row-reverse in RTL puts sidebar RIGHT, content LEFT. */}
          <Stack flex={1} flexDirection={isRtl ? 'row-reverse' : 'row'}>
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

  // Narrow layout (<768) AND native: content area + floating ParentTabBar.
  //
  // Design rule: controls MUST remain reachable on narrow (design-author hard
  // requirement). They now live in the per-page ParentHeader (ChildSwitcher +
  // AccountMenu inline-end of every page) — the shell no longer renders its own
  // slim header row. The tab bar floats over the content via `position:fixed`
  // (web) / `position:absolute` (native), so the content Stack needs a bottom
  // padding equal to PARENT_TAB_BAR_CLEARANCE + safe-area bottom to prevent
  // content being obscured by the bar.
  //
  // The outer Stack is `position:relative` on native so the `absolute`-positioned
  // tab bar is contained within it.
  return (
    <>
      <Stack
        flex={1}
        flexDirection="column"
        backgroundColor="$bg"
        paddingTop={insets.top}
        // Native: relative positioning so the absolute tab bar is contained.
        position="relative"
      >
        {/* Content area — bottom padding clears the floating tab bar. */}
        <Stack
          flex={1}
          // paddingBottom prevents content scrolling under the bar.
          // Uses safe-area bottom + bar clearance so content is never obscured.
          paddingBottom={PARENT_TAB_BAR_CLEARANCE + insets.bottom}
          overflow="hidden"
        >
          <Slot />
        </Stack>

        {/* Floating glass tab bar — position:fixed (web) / absolute (native). */}
        <ParentTabBar />
      </Stack>

      <AddChildModal
        visible={addChildModalVisible}
        onClose={() => closeAddChild()}
      />
    </>
  );
}
