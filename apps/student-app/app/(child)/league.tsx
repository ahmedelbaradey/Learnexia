/**
 * League tab root — STUB (B0-nav batch 2b).
 *
 * Batch 3b (B6 / P4-07-FE) replaces this body with the real league standings
 * screen (`GET /api/Gamification/Leagues/Me` — plural). Route name `league`
 * is final — only the screen body changes.
 */
import React from 'react';
import { useTranslation } from 'react-i18next';

import { ChildTabRoute, CHILD_TAB_ICONS } from './_components/ChildTabBar';
import { TabStubScreen } from './_components/TabStubScreen';

export default function LeagueScreen() {
  const { t } = useTranslation();
  return (
    <TabStubScreen
      glyph={CHILD_TAB_ICONS[ChildTabRoute.League]}
      title={t('nav.tabs.league')}
      testID="league-stub"
    />
  );
}
