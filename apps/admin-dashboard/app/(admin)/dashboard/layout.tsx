import type { ReactNode } from 'react';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

/**
 * Dashboard section layout.
 * Wraps the dashboard route in `AdminShell` with the Dashboard page title.
 */
export default function DashboardLayout({ children }: { children: ReactNode }) {
  return <AdminShell title={strings.pageTitleDashboard}>{children}</AdminShell>;
}
