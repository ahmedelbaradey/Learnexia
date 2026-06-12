/**
 * Badges tab root — STUB (B0-nav batch 2b).
 *
 * Batch 3b (B4 / P4-05-FE) replaces this body with the real badge-collection
 * screen (`GET /api/Gamification/Badges/Me`). Route name `badges` is final —
 * only the screen body changes.
 */
import React from 'react';
import { useTranslation } from 'react-i18next';

import { ChildTabRoute, CHILD_TAB_ICONS } from './_components/ChildTabBar';
import { TabStubScreen } from './_components/TabStubScreen';

export default function BadgesScreen() {
  const { t } = useTranslation();
  return (
    <TabStubScreen
      glyph={CHILD_TAB_ICONS[ChildTabRoute.Badges]}
      title={t('nav.tabs.badges')}
      testID="badges-stub"
    />
  );
}
