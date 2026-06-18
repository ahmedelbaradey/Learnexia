/**
 * P7-05 Curriculum Lifecycle hooks — five hand-written admin hooks.
 *
 * All target GET/POST /api/learning/ContentLifecycle/* (AdminOnly).
 * Mirror the useSuspendUser / useAdminUserProfile patterns:
 *   - raw `client.get`/`client.post` (NOT NSwag generated client)
 *   - `adminCurriculum.*` query-key namespace
 *   - BaseResponse<T> unwrapped by the api-client envelope layer
 *   - No optimistic updates — invalidate-and-refetch on mutation success
 *
 * FE-local DTO types are co-located here per the curriculum.types.ts convention.
 * The reason field in RollbackInput is a FE-ONLY guardrail — it is NOT sent to
 * the backend (the POST /Rollback body has no reason field). See DG-4.
 *
 * Security note: preview URLs are validated at the rendering layer
 * (CurriculumPreview.tsx) — only https:// + image-extension URLs rendered as
 * <img>; javascript:/data: schemes are blocked there.
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryResult,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { LifecycleStateValue, VersionedEntityTypeValue, ContentLanguageValue, SubjectCodeValue } from '@learnexia/shared/constants';

// ── FE-local DTO types ────────────────────────────────────────────────────────

/**
 * ContentVersionDto — one entry in the version history list.
 * Source: GET /api/learning/ContentLifecycle/VersionHistory
 */
export interface ContentVersionDto {
  id: number;
  entityType: VersionedEntityTypeValue;
  entityId: number;
  versionNumber: number;
  /** ISO 8601 timestamp: when this version was published. */
  publishedAtUtc: string;
  publishedByUserId: number;
  language: ContentLanguageValue;
  /** Raw serialised JSON string of the entity snapshot. */
  snapshot: string;
}

/**
 * PreviewDto — current state snapshot for an entity (Draft or Published).
 * Source: GET /api/learning/ContentLifecycle/Preview
 */
export interface PreviewDto {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  currentState: LifecycleStateValue;
  language: ContentLanguageValue;
  /** Raw serialised JSON string of the entity snapshot. */
  snapshot: string;
}

/**
 * PublicationCoverageSlotDto — one (SubjectCode × Language) slot in the coverage grid.
 * Source: GET /api/learning/ContentLifecycle/PublicationCoverage
 */
export interface PublicationCoverageSlotDto {
  subjectCode: SubjectCodeValue;
  language: ContentLanguageValue;
  exists: boolean;
  subjectId: number | null;
  name: string | null;
  isActive: boolean | null;
  lifecycleState: LifecycleStateValue | null;
  isPublished: boolean;
}

/**
 * PublicationCoverageReportDto — full coverage report for one grade.
 * Source: GET /api/learning/ContentLifecycle/PublicationCoverage
 */
export interface PublicationCoverageReportDto {
  gradeId: number;
  gradeNumber: number;
  coverage: PublicationCoverageSlotDto[];
}

// ── Input shapes ──────────────────────────────────────────────────────────────

export interface TransitionLifecycleInput {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  targetState: LifecycleStateValue;
  /** Unit/lesson/question parent IDs — used for granular invalidation only. */
  gradeId?: number;
  unitId?: number;
  lessonId?: number;
}

export interface RollbackVersionInput {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  versionNumber: number;
  /** FE-only guardrail — NOT sent to the backend. Required by the dialog UX. */
  _feOnlyReason: string;
  /** Parent IDs for invalidation. */
  gradeId?: number;
  unitId?: number;
  lessonId?: number;
}

// ── Hooks ─────────────────────────────────────────────────────────────────────

/**
 * useContentVersionHistory — GET /api/learning/ContentLifecycle/VersionHistory
 *
 * Returns the published version history for one entity, newest-first.
 * Empty list = entity has never been published (renders empty state, not error).
 * staleTime: 0 — always fresh (version rows are append-only but we need latest).
 */
export function useContentVersionHistory(
  entityType: VersionedEntityTypeValue,
  entityId: number,
): UseQueryResult<ContentVersionDto[]> {
  const client = useApiClient();

  return useQuery<ContentVersionDto[]>({
    queryKey: queryKeys.adminCurriculum.versions(entityType, entityId),
    queryFn: () =>
      client.get<ContentVersionDto[]>(
        `/api/learning/ContentLifecycle/VersionHistory?entityType=${entityType}&entityId=${entityId}`,
      ),
    enabled: entityId > 0,
    staleTime: 0,
  });
}

/**
 * usePreviewContent — GET /api/learning/ContentLifecycle/Preview
 *
 * Returns the current (possibly Draft) entity snapshot + lifecycle state.
 * Does NOT mutate state. Reachable only by admins.
 */
export function usePreviewContent(
  entityType: VersionedEntityTypeValue,
  entityId: number,
): UseQueryResult<PreviewDto> {
  const client = useApiClient();

  return useQuery<PreviewDto>({
    queryKey: queryKeys.adminCurriculum.preview(entityType, entityId),
    queryFn: () =>
      client.get<PreviewDto>(
        `/api/learning/ContentLifecycle/Preview?entityType=${entityType}&entityId=${entityId}`,
      ),
    enabled: entityId > 0,
    staleTime: 30_000,
  });
}

/**
 * usePublicationCoverage — GET /api/learning/ContentLifecycle/PublicationCoverage
 *
 * Returns per-(SubjectCode × Language) lifecycle state for a grade.
 * Used by the /curriculum landing page (PublicationCoverage component).
 */
export function usePublicationCoverage(gradeId: number): UseQueryResult<PublicationCoverageReportDto> {
  const client = useApiClient();

  return useQuery<PublicationCoverageReportDto>({
    queryKey: queryKeys.adminCurriculum.pubCoverage(gradeId),
    queryFn: () =>
      client.get<PublicationCoverageReportDto>(
        `/api/learning/ContentLifecycle/PublicationCoverage?gradeId=${gradeId}`,
      ),
    enabled: gradeId > 0,
    staleTime: 30_000,
  });
}

/**
 * useTransitionLifecycle — POST /api/learning/ContentLifecycle/Transition
 *
 * Single endpoint for Publish (→2), Unpublish (→1), Archive (→3), Restore (→1).
 * Returns BaseResponse<string> (success message) — the caller refetches via
 * invalidation; the mutation result message may be surfaced as a success banner.
 *
 * Invalidations on success:
 *   - versions(entityType, entityId)
 *   - preview(entityType, entityId)
 *   - pubCoverage(gradeId) if gradeId is known
 *   - adminCurriculum.all (busts all list/detail caches for the cluster)
 */
export function useTransitionLifecycle(): UseMutationResult<string, Error, TransitionLifecycleInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, TransitionLifecycleInput>({
    mutationFn: ({ entityType, entityId, targetState }) =>
      client.post<string>('/api/learning/ContentLifecycle/Transition', {
        body: { entityType, entityId, targetState },
      }),
    onSuccess: async (_data, { entityType, entityId, gradeId }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.versions(entityType, entityId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.preview(entityType, entityId),
        }),
        // Invalidate all curriculum caches (subjects/units/lessons lists pick up
        // any state changes; pubCoverage needs fresh data too)
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.all,
        }),
        // Fine-grained pubCoverage invalidation if gradeId is known
        ...(gradeId != null
          ? [queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.pubCoverage(gradeId) })]
          : []),
      ]);
    },
  });
}

/**
 * useRollbackToVersion — POST /api/learning/ContentLifecycle/Rollback
 *
 * Restores entity editorial fields from a historical snapshot and re-publishes.
 * A new version row is appended to the history.
 *
 * IMPORTANT: the `_feOnlyReason` field in the input is a FE-only UX guardrail.
 * It is NOT included in the POST body sent to the backend. The backend does
 * not persist a reason field for rollbacks. (DG-4)
 *
 * Same invalidation strategy as useTransitionLifecycle.
 */
export function useRollbackToVersion(): UseMutationResult<string, Error, RollbackVersionInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, RollbackVersionInput>({
    mutationFn: ({ entityType, entityId, versionNumber }) =>
      // Note: _feOnlyReason is intentionally excluded from the request body.
      client.post<string>('/api/learning/ContentLifecycle/Rollback', {
        body: { entityType, entityId, versionNumber },
      }),
    onSuccess: async (_data, { entityType, entityId, gradeId }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.versions(entityType, entityId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.preview(entityType, entityId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.all,
        }),
        ...(gradeId != null
          ? [queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.pubCoverage(gradeId) })]
          : []),
      ]);
    },
  });
}
