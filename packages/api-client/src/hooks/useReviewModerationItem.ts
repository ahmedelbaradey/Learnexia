/**
 * useReviewModerationItem — POST /api/Admin/Moderation/{id}/Review (AdminOnly).
 *
 * Submits a review decision (Approve / Reject / Flag) on a moderation item.
 * Body: { decision: int, reason?: string }
 * Returns: BaseResponse<ModerationItemDto> — the updated item.
 *
 * Enum wire form: INTEGERS (no JsonStringEnumConverter on this backend).
 *   decision=1 → Approved, decision=2 → Rejected, decision=3 → Flagged.
 *   Pending (0) is rejected by FluentValidation as an invalid decision.
 *
 * Business rules (enforced server-side, also enforced in FE to prevent bad requests):
 *   - `reason` is REQUIRED when `decision === 2` (Rejected), max 2000 chars.
 *   - `reason` is optional for Approved (1) and Flagged (3).
 *   - Pending → Approved | Rejected | Flagged (all ok)
 *   - Flagged → Approved | Rejected (ok) | Flagged (→ 400 AlreadyFlagged)
 *   - Approved / Rejected → any review (→ 400 AlreadyTerminal)
 *
 * On success: invalidates `adminModeration.item(id)` + `adminModeration.all`
 *   so both the detail page and the queue list refresh.
 * NO optimistic update — the new status comes from the refetch only.
 *
 * Mirrors `useSuspendUser` pattern: useMutation + queryClient.invalidateQueries.
 *
 * The review actor (admin user id) is taken from the JWT server-side — the FE
 * sends ONLY { decision, reason }, never an admin id in the body.
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { ModerationItemDto, ModerationStatus } from './useModerationQueue';

// ── Integer wire values for the ModerationStatus enum ────────────────────────
// Backend: no JsonStringEnumConverter → enums are ints on the wire.
const MODERATION_STATUS_INT: Record<ModerationStatus, number> = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
  Flagged: 3,
};

export interface ReviewModerationItemInput {
  /** Moderation item primary key. */
  id: number;
  /**
   * Review decision (string union for FE type-safety).
   * Serialised to int before sending to the API.
   * Must NOT be 'Pending' — the backend validator rejects Pending as a decision.
   */
  decision: Exclude<ModerationStatus, 'Pending'>;
  /**
   * Reason text.
   * REQUIRED when decision === 'Rejected' (max 2000 chars, server-side + FE).
   * Optional for Approved and Flagged.
   */
  reason?: string;
}

export function useReviewModerationItem(): UseMutationResult<
  ModerationItemDto,
  Error,
  ReviewModerationItemInput
> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<ModerationItemDto, Error, ReviewModerationItemInput>({
    mutationFn: ({ id, decision, reason }) =>
      client.post<ModerationItemDto>(`/api/Admin/Moderation/${id}/Review`, {
        body: {
          // Send the enum as an integer — backend has NO JsonStringEnumConverter.
          decision: MODERATION_STATUS_INT[decision],
          ...(reason ? { reason } : {}),
        },
      }),
    onSuccess: async (_data, { id }) => {
      // Invalidate the specific item + every list cache (bust queue pagination).
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminModeration.item(id),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminModeration.all,
        }),
      ]);
    },
  });
}
