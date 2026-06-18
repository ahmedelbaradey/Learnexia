import { notFound } from 'next/navigation';
import { Suspense } from 'react';
import Link from 'next/link';
import { AdminShell } from '../../../../../../components/AdminShell';
import { CurriculumPreview } from '../../../../../../components/CurriculumPreview';
import { getStrings } from '../../../../../../lib/strings';
import { VERSIONED_ENTITY_TYPE, type VersionedEntityTypeValue } from '@learnexia/shared/constants';

/**
 * /curriculum/preview/[type]/[id] — Read-only curriculum entity snapshot viewer.
 *
 * [type] ∈ subject | unit | lesson | quiz   → maps to VERSIONED_ENTITY_TYPE ints.
 * [id] — entity integer id.
 *
 * Both params are validated server-side (404 for unknown type, invalid id).
 * force-dynamic: preview data is admin-only and entity-specific — do not cache.
 *
 * Design Spec P7-05 §7.
 */
export const dynamic = 'force-dynamic';

/** Slug → VERSIONED_ENTITY_TYPE value map. */
const SLUG_TO_ENTITY_TYPE: Record<string, VersionedEntityTypeValue> = {
  subject: VERSIONED_ENTITY_TYPE.Subject,
  unit:    VERSIONED_ENTITY_TYPE.Unit,
  lesson:  VERSIONED_ENTITY_TYPE.Lesson,
  quiz:    VERSIONED_ENTITY_TYPE.QuizQuestion,
};

interface PreviewPageProps {
  params: Promise<{ type: string; id: string }>;
}

export default async function CurriculumPreviewPage({ params }: PreviewPageProps) {
  const { type, id } = await params;

  // Validate [type]
  const entityType = SLUG_TO_ENTITY_TYPE[type.toLowerCase()];
  if (entityType == null) {
    notFound();
  }

  // Validate [id] — must be a positive integer
  const entityId = parseInt(id, 10);
  if (isNaN(entityId) || entityId <= 0) {
    notFound();
  }

  const strings = getStrings();

  // Back URL: attempt to go back to the entity's own page
  // (handled client-side in the component via browser history)

  return (
    <AdminShell>
      <div
        data-testid="curriculum-preview-page"
        style={{
          paddingBlock: 'var(--lx-spacing-page-v, 32px)',
          paddingInline: 'var(--lx-spacing-page-h, 24px)',
          maxWidth: 800,
        }}
      >
        {/* Breadcrumb-style back link */}
        <div style={{ marginBottom: 20 }}>
          <Link
            href="/curriculum/subjects"
            data-testid="preview-breadcrumb"
            style={{
              fontSize: 13,
              color: '#94A3B8',
              textDecoration: 'none',
              display: 'inline-flex',
              alignItems: 'center',
              gap: 4,
            }}
          >
            {strings.subjectsDetailBreadcrumb}
          </Link>
        </div>

        <Suspense fallback={null}>
          <CurriculumPreviewClient
            entityType={entityType}
            entityId={entityId}
            strings={strings}
          />
        </Suspense>
      </div>
    </AdminShell>
  );
}

/**
 * Thin client-only wrapper that injects the router back action.
 * Declared in this file to keep the route module self-contained.
 */
function CurriculumPreviewClient({
  entityType,
  entityId,
  strings,
}: {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  strings: ReturnType<typeof getStrings>;
}) {
  return (
    <CurriculumPreview
      entityType={entityType}
      entityId={entityId}
      strings={strings}
    />
  );
}
