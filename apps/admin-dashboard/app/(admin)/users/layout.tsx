import type { ReactNode } from 'react';

/**
 * Users section layout (P7-06).
 *
 * Pass-through: every leaf route under `/users/*` self-wraps in `<AdminShell>`
 * so it can supply the correct per-page title to `AdminTopBar` without nesting
 * shells inside each other.
 *
 * In Next.js App Router, child-segment layouts nest INSIDE their parent —
 * they do NOT replace or override it.  A shell in this layout would therefore
 * be rendered around EVERY route under `/users/*`, creating a double shell when
 * the leaf page also renders one.  The correct pattern is to keep this layout
 * as a pass-through and let each page own its single shell.
 *
 *   /users           → page.tsx self-wraps <AdminShell title={pageTitleUsers}>
 *   /users/[id]      → page.tsx self-wraps <AdminShell title={user.fullName}>
 *   /users/[id]/edit → page.tsx self-wraps <AdminShell title={childEditPageTitle}>
 */
export default function UsersLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
