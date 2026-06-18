import type { ReactNode } from 'react';

/**
 * User detail segment layout — pass-through (P7-06).
 *
 * In Next.js App Router, child-segment layouts nest INSIDE their parent —
 * they do NOT replace or override it.  This layout is deliberately a
 * pass-through so that each leaf page under `/users/[id]/` can self-wrap
 * in `<AdminShell>` with its own per-page title, rather than inheriting a
 * generic title from a parent layout.
 *
 * CONVENTION (security): every leaf route under `/users/[id]/` MUST
 * self-wrap in `<AdminShell>`.  `AdminShell` runs `useAdminGuard()`
 * internally, which is the client-side auth gate.  If a new route is added
 * here without an `<AdminShell>` wrap, the client guard will not run for
 * that route.  The backend is the real gate ([Authorize(AdminOnly)]) but
 * the client guard must not be dropped by omission at the layout level.
 */
export default function UserDetailLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
