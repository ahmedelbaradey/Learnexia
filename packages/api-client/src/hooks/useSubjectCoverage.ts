/**
 * useSubjectCoverage — GET /api/learning/Subjects/Coverage?gradeId= (AdminOnly).
 *
 * Returns the 6-slot (SubjectCode × Language) coverage grid for a single grade.
 * Used by the LanguageCoveragePanel in the subjects list page.
 *
 * Backend contract (P7-01):
 *   GET /api/learning/Subjects/Coverage?gradeId={n}
 *   Returns: BaseResponse<SubjectLanguageCoverageReportDto>
 *   Auth: AdminOnly
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { SubjectLanguageCoverageReportDto } from './curriculum.types';

export function useSubjectCoverage(
  gradeId: number | undefined,
): UseQueryResult<SubjectLanguageCoverageReportDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.coverage(gradeId ?? 0),
    enabled: gradeId !== undefined && gradeId > 0,
    queryFn: ({ signal }) =>
      client.get<SubjectLanguageCoverageReportDto>(
        `/api/learning/Subjects/Coverage`,
        { query: { gradeId }, signal },
      ),
  });
}
