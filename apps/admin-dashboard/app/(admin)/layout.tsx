import type { ReactNode } from 'react';

import { AdminShell } from '../../components/AdminShell';

/**
 * Authenticated route group layout (FE-3 + FE-4).
 *
 * Server component that wraps the client `AdminShell`. The shell applies the
 * client-side admin guard (`useAdminGuard`): unauthenticated or non-admin
 * visitors are redirected to `/login`; only signed-in admins see the chrome +
 * children.
 */
export default function AdminGroupLayout({ children }: { children: ReactNode }) {
  return <AdminShell>{children}</AdminShell>;
}
