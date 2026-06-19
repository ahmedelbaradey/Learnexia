/**
 * P7-13 Admin Gamification hooks.
 *
 * Targets:
 *   - Badge catalog: GET/POST/PUT/PATCH api/Admin/Gamification/Badges
 *   - Mission catalog: GET/POST/PUT/PATCH api/Admin/Gamification/Missions
 *   - Timed events (list): GET api/admin/timed-events  ← separate controller, lowercase route
 *   - Timed events (write): POST/PUT/PATCH api/Admin/Gamification/TimedEvents
 *   - League-tier override: POST api/Admin/Gamification/children/{childId}/league-tier
 *   - Streak-freeze grant: POST api/Admin/Gamification/children/{childId}/streak-freeze
 *
 * Pattern mirrors useSuspendUser / useSetSubjectActive / useAuditLog.
 * No optimistic mutations — every mutation onSuccess invalidates + lets TanStack
 * refetch. Never trust create-response ids (Q3 — may return 0 pre-fix).
 *
 * Enum values verified against the merged AdminGamificationController:
 *   LeagueTier:       Bronze=1, Silver=2, Gold=3, Diamond=4
 *   BadgeRarity:      Common=1, Rare=2, Epic=3, Legendary=4
 *   BadgeTriggerType: FirstLesson=1, StreakThreshold=2, LevelThreshold=3
 *   MissionType:      Daily=1, Weekly=2  (ONLY two values — no weekly-challenge)
 *   MissionTargetType: CompleteLessons=1, CorrectAnswers=2, MaintainStreak=3
 *   TimedEventScope:  AllXp=1, MissionXp=2, LeagueXp=3
 */

import {
  useQuery,
  useMutation,
  useQueryClient,
  type UseQueryResult,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from '../hooks/apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── Enum numeric types (sync with BE enums above) ─────────────────────────────

export type LeagueTier = 1 | 2 | 3 | 4;
export type BadgeRarity = 1 | 2 | 3 | 4;
export type BadgeTriggerType = 1 | 2 | 3;
export type MissionType = 1 | 2;
export type MissionTargetType = 1 | 2 | 3;
export type TimedEventScope = 1 | 2 | 3;

// ── DTO types (hand-written from merged controller) ────────────────────────────

export interface BadgeDefinitionDto {
  id: number;
  code: string;
  name: string;
  description: string;
  iconKey: string;
  rarity: BadgeRarity;
  sortOrder: number;
  triggerType: BadgeTriggerType;
  threshold: number | null;
  rewardXp: number;
  isActive: boolean;
}

export interface MissionDefinitionDto {
  id: number;
  code: string;
  iconKey: string;
  titleKey: string;
  cadence: MissionType;
  targetType: MissionTargetType;
  target: number;
  rewardXp: number;
  sortOrder: number;
  isActive: boolean;
}

export interface TimedEventListItemDto {
  id: number;
  code: string;
  nameEn: string;
  nameAr: string;
  multiplier: number;
  startUtc: string;
  endUtc: string;
  isActive: boolean;
  /** String name: "AllXp" | "MissionXp" | "LeagueXp" */
  scope: string;
}

// ── Create / Update body shapes ────────────────────────────────────────────────

export interface CreateBadgeBody {
  code: string;
  name: string;
  description: string;
  iconKey: string;
  rarity: BadgeRarity;
  sortOrder: number;
  triggerType: BadgeTriggerType;
  threshold: number | null;
  rewardXp: number;
}

export interface UpdateBadgeBody {
  name: string;
  description: string;
  iconKey: string;
  rarity: BadgeRarity;
  sortOrder: number;
  triggerType: BadgeTriggerType;
  threshold: number | null;
  rewardXp: number;
}

export interface CreateMissionBody {
  code: string;
  iconKey: string;
  titleKey: string;
  cadence: MissionType;
  targetType: MissionTargetType;
  target: number;
  rewardXp: number;
  sortOrder: number;
}

export interface UpdateMissionBody {
  iconKey: string;
  titleKey: string;
  cadence: MissionType;
  targetType: MissionTargetType;
  target: number;
  rewardXp: number;
  sortOrder: number;
}

export interface CreateTimedEventBody {
  code: string;
  nameEn: string;
  nameAr: string;
  descriptionEn?: string;
  descriptionAr?: string;
  startUtc: string;
  endUtc: string;
  multiplier: number;
  scope: TimedEventScope;
}

export interface UpdateTimedEventBody {
  nameEn: string;
  nameAr: string;
  descriptionEn?: string;
  descriptionAr?: string;
  startUtc: string;
  endUtc: string;
  multiplier: number;
  scope: TimedEventScope;
}

// ── Badge query hooks ──────────────────────────────────────────────────────────

export function useAdminBadges(): UseQueryResult<BadgeDefinitionDto[], Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminGamification.badges(),
    queryFn: ({ signal }) =>
      client.get<BadgeDefinitionDto[]>('/api/Admin/Gamification/Badges', { signal }),
  });
}

// ── Badge mutation hooks ───────────────────────────────────────────────────────

export function useCreateBadge(): UseMutationResult<number, Error, CreateBadgeBody> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<number, Error, CreateBadgeBody>({
    mutationFn: (body) =>
      client.post<number>('/api/Admin/Gamification/Badges', { body }),
    onSuccess: async () => {
      // Q3: never trust returned id — invalidate + refetch list
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.badges(),
      });
    },
  });
}

export function useUpdateBadge(): UseMutationResult<
  void,
  Error,
  UpdateBadgeBody & { id: number }
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, UpdateBadgeBody & { id: number }>({
    mutationFn: ({ id, ...body }) =>
      client.put<void>(`/api/Admin/Gamification/Badges/${id}`, { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.badges(),
      });
    },
  });
}

export function useSetBadgeActive(): UseMutationResult<
  void,
  Error,
  { id: number; isActive: boolean }
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, { id: number; isActive: boolean }>({
    mutationFn: ({ id, isActive }) =>
      client.patch<void>(`/api/Admin/Gamification/Badges/${id}/active`, {
        body: { isActive },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.badges(),
      });
    },
  });
}

// ── Mission query hooks ────────────────────────────────────────────────────────

export function useAdminMissions(): UseQueryResult<MissionDefinitionDto[], Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminGamification.missions(),
    queryFn: ({ signal }) =>
      client.get<MissionDefinitionDto[]>('/api/Admin/Gamification/Missions', { signal }),
  });
}

// ── Mission mutation hooks ─────────────────────────────────────────────────────

export function useCreateMission(): UseMutationResult<number, Error, CreateMissionBody> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<number, Error, CreateMissionBody>({
    mutationFn: (body) =>
      client.post<number>('/api/Admin/Gamification/Missions', { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.missions(),
      });
    },
  });
}

export function useUpdateMission(): UseMutationResult<
  void,
  Error,
  UpdateMissionBody & { id: number }
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, UpdateMissionBody & { id: number }>({
    mutationFn: ({ id, ...body }) =>
      client.put<void>(`/api/Admin/Gamification/Missions/${id}`, { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.missions(),
      });
    },
  });
}

export function useSetMissionActive(): UseMutationResult<
  void,
  Error,
  { id: number; isActive: boolean }
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, { id: number; isActive: boolean }>({
    mutationFn: ({ id, isActive }) =>
      client.patch<void>(`/api/Admin/Gamification/Missions/${id}/active`, {
        body: { isActive },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.missions(),
      });
    },
  });
}

// ── Timed events query hook ────────────────────────────────────────────────────

export function useTimedEvents(): UseQueryResult<TimedEventListItemDto[], Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminGamification.timedEvents(),
    queryFn: ({ signal }) =>
      client.get<TimedEventListItemDto[]>('/api/admin/timed-events', { signal }),
  });
}

// ── Timed event mutation hooks ─────────────────────────────────────────────────

export function useCreateTimedEvent(): UseMutationResult<number, Error, CreateTimedEventBody> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<number, Error, CreateTimedEventBody>({
    mutationFn: (body) =>
      client.post<number>('/api/Admin/Gamification/TimedEvents', { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.timedEvents(),
      });
    },
  });
}

export function useUpdateTimedEvent(): UseMutationResult<
  void,
  Error,
  UpdateTimedEventBody & { id: number }
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, UpdateTimedEventBody & { id: number }>({
    mutationFn: ({ id, ...body }) =>
      client.put<void>(`/api/Admin/Gamification/TimedEvents/${id}`, { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.timedEvents(),
      });
    },
  });
}

export function useActivateTimedEvent(): UseMutationResult<void, Error, { id: number }> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, { id: number }>({
    mutationFn: ({ id }) =>
      client.post<void>(`/api/Admin/Gamification/TimedEvents/${id}/activate`, {}),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.timedEvents(),
      });
    },
  });
}

export function useExpireTimedEvent(): UseMutationResult<void, Error, { id: number }> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<void, Error, { id: number }>({
    mutationFn: ({ id }) =>
      client.post<void>(`/api/Admin/Gamification/TimedEvents/${id}/expire`, {}),
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminGamification.timedEvents(),
      });
    },
  });
}

// ── Student override hooks ─────────────────────────────────────────────────────

export interface OverrideLeagueTierInput {
  childId: number;
  newTier: LeagueTier;
  reason: string;
  confirm: true;
}

export function useOverrideLeagueTier(): UseMutationResult<
  boolean,
  Error,
  OverrideLeagueTierInput
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<boolean, Error, OverrideLeagueTierInput>({
    mutationFn: ({ childId, newTier, reason, confirm }) =>
      client.post<boolean>(
        `/api/Admin/Gamification/children/${childId}/league-tier`,
        { body: { newTier, reason, confirm } },
      ),
    onSuccess: async (_data, { childId }) => {
      // Invalidate the activity key so the page panel refreshes with the new tier
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminUsers.activity(childId),
      });
    },
  });
}

export interface GrantStreakFreezeInput {
  childId: number;
  count: number;
  reason: string;
  confirm: true;
}

export function useGrantStreakFreeze(): UseMutationResult<
  boolean,
  Error,
  GrantStreakFreezeInput
> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  return useMutation<boolean, Error, GrantStreakFreezeInput>({
    mutationFn: ({ childId, count, reason, confirm }) =>
      client.post<boolean>(
        `/api/Admin/Gamification/children/${childId}/streak-freeze`,
        { body: { count, reason, confirm } },
      ),
    onSuccess: async (_data, { childId }) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminUsers.activity(childId),
      });
    },
  });
}
