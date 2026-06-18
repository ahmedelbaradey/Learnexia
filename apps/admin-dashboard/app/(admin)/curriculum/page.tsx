import { Suspense } from 'react';
import Link from 'next/link';
import { AdminShell } from '../../../components/AdminShell';
import { PublicationCoverage } from '../../../components/PublicationCoverage';
import { getStrings } from '../../../lib/strings';

/**
 * /curriculum — Publication Coverage landing page.
 *
 * Hosts the PublicationCoverage component as the entry point to the curriculum
 * section, giving admins a grade-scoped view of publication status before
 * navigating to Subjects. Also links to /curriculum/subjects.
 *
 * force-dynamic: coverage data is admin-only and grade-dependent — do not cache.
 *
 * Design Spec P7-05 §8.
 */
export const dynamic = 'force-dynamic';

export default function CurriculumPage() {
  const strings = getStrings();

  return (
    <AdminShell>
      <div
        data-testid="curriculum-landing-page"
        style={{
          paddingBlock: 'var(--lx-spacing-page-v, 32px)',
          paddingInline: 'var(--lx-spacing-page-h, 24px)',
          maxWidth: 960,
        }}
      >
        {/* Go to Subjects link */}
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 24 }}>
          <Link
            href="/curriculum/subjects"
            data-testid="curriculum-go-to-subjects"
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
              height: 36,
              paddingInline: 16,
              borderRadius: 'var(--lx-radius-button)',
              border: '1px solid var(--lx-border)',
              backgroundColor: 'transparent',
              color: '#CBD5E1',
              fontSize: 13,
              fontWeight: 500,
              textDecoration: 'none',
              fontFamily: 'inherit',
            }}
          >
            {strings.clCoverageGoToSubjects}
          </Link>
        </div>

        {/* Publication coverage matrix (client component — Suspense boundary) */}
        <Suspense fallback={null}>
          <PublicationCoverage strings={strings} />
        </Suspense>
      </div>
    </AdminShell>
  );
}
