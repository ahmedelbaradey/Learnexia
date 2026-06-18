import type { ReactNode } from 'react';

/**
 * Authenticated route group layout (FE-3 + FE-4).
 *
 * This is now a pass-through: each section layout (dashboard, users, …) renders
 * its own `AdminShell` so it can pass the correct per-section title to
 * `AdminTopBar`.  The admin guard (`useAdminGuard`) lives inside `AdminShell`
 * so every section is still fully protected.
 *
 * P7-06 change: moved `AdminShell` down into section layouts so nested layouts
 * can supply distinct page titles without double-wrapping the shell.
 */
export default function AdminGroupLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
