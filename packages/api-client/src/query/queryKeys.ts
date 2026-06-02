/**
 * Query-key conventions.
 *
 * Keys are tuples: `[domain, scope, ...params]`. Use the factory below so keys
 * stay consistent and invalidation is predictable. Example:
 *   queryKeys.users.profile(userId)  →  ['users', 'profile', userId]
 *   queryKeys.users.list(filters)    →  ['users', 'list', { ...filters }]
 *
 * Hooks others write should add their domain here rather than inlining arrays.
 */

export const queryKeys = {
  auth: {
    all: ['auth'] as const,
    currentUser: () => [...queryKeys.auth.all, 'current-user'] as const,
    me: () => [...queryKeys.auth.all, 'me'] as const,
  },
  account: {
    all: ['account'] as const,
    profile: () => [...queryKeys.account.all, 'profile'] as const,
    // P2-12 — Security tab
    sessions: () => [...queryKeys.account.all, 'sessions'] as const,
    // P2-12 — Plan & billing tab
    plan: () => [...queryKeys.account.all, 'plan'] as const,
  },
  family: {
    all: ['family'] as const,
    myChildren: () => [...queryKeys.family.all, 'my-children'] as const,
  },
  // P2-12 — Notifications tab
  notifications: {
    all: ['notifications'] as const,
    preferences: () => [...queryKeys.notifications.all, 'preferences'] as const,
  },
  users: {
    all: ['users'] as const,
    profile: (userId: number) =>
      [...queryKeys.users.all, 'profile', userId] as const,
    list: (filters?: object) =>
      [...queryKeys.users.all, 'list', filters ?? {}] as const,
  },
  learning: {
    all: ['learning'] as const,
    subjectsForGrade: (grade: number | undefined) =>
      [...queryKeys.learning.all, 'subjects-for-grade', grade] as const,
    subjectLessons: (subjectId: number) =>
      [...queryKeys.learning.all, 'subject-lessons', subjectId] as const,
    subjectSkillTree: (subjectId: number) =>
      [...queryKeys.learning.all, 'subject-skill-tree', subjectId] as const,
    // W12 quiz hooks — lesson detail by id.
    lesson: (lessonId: number) =>
      [...queryKeys.learning.all, 'lesson', lessonId] as const,
    dashboard: () => [...queryKeys.learning.all, 'dashboard'] as const,
  },
  // P4-08 Gamification hooks — 4 endpoints added as hand-written hooks.
  gamification: {
    all: ['gamification'] as const,
    /** GET /api/Gamification/Profile — XP profile snapshot (level, totalXp, xpToNextLevel). */
    profile: () => [...queryKeys.gamification.all, 'profile'] as const,
    /** GET /api/Gamification/Badges/Me — full badge catalog with earned state. */
    badges: () => [...queryKeys.gamification.all, 'badges'] as const,
    /** GET /api/Gamification/Missions/Me — daily + weekly mission state for the current period. */
    missions: () => [...queryKeys.gamification.all, 'missions'] as const,
    /** GET /api/Gamification/Leagues/Me — full cohort standings + tier + week boundaries. */
    league: () => [...queryKeys.gamification.all, 'league'] as const,
  },
} as const;

export type QueryKeys = typeof queryKeys;
