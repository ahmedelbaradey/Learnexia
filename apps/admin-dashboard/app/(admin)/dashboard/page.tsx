import type { Metadata } from 'next';

import { DashboardLanding } from './DashboardLanding';

export const metadata: Metadata = {
  title: 'Dashboard — Learnexia Admin',
};

/**
 * Dashboard landing (FE-3, Design Spec §4d). Pure static placeholder — no data
 * fetched, no loading state. Admin features arrive in Phase 2+.
 */
export default function DashboardPage() {
  return <DashboardLanding />;
}
