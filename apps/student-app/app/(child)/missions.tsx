/**
 * Missions tab root — STUB (B0-nav batch 2b).
 *
 * Batch 3b (B5 / P4-06-FE) replaces this body with the real missions screen
 * (`GET /api/Gamification/Missions/Me`). Route name `missions` is final —
 * only the screen body changes.
 */
import React from 'react';
import { useTranslation } from 'react-i18next';

import { ChildTabRoute, CHILD_TAB_ICONS } from './_components/ChildTabBar';
import { TabStubScreen } from './_components/TabStubScreen';

export default function MissionsScreen() {
  const { t } = useTranslation();
  return (
    <TabStubScreen
      glyph={CHILD_TAB_ICONS[ChildTabRoute.Missions]}
      title={t('nav.tabs.missions')}
      testID="missions-stub"
    />
  );
}
