import type { ReactNode } from 'react';

/**
 * Curriculum section layout (P7-01).
 *
 * Pass-through: every leaf route under `/curriculum/*` self-wraps in
 * `<AdminShell>` with its own page title (per the Wave-1 convention established
 * in `users/layout.tsx`). This layout must NOT add a shell — that would create
 * a double shell when the leaf page also renders one.
 *
 *   /curriculum/subjects       → page.tsx self-wraps <AdminShell title={curriculumPageTitle}>
 *   /curriculum/subjects/[id]  → page.tsx self-wraps <AdminShell title={subject.name}>
 *
 * This file is created ONCE by batch 2a-A and never edited again (it is a
 * pass-through; per-page AdminShell does all the work).
 */
export default function CurriculumLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
