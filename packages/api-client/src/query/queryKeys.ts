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
    // A5 attempt history (CO-FE-7) — per-student attempt list.
    studentAttempts: (studentId: number | null) =>
      [...queryKeys.learning.all, 'student-attempts', studentId] as const,
  },
  // P4-08 carryover (batch 2a) — gamification /Me hooks.
  gamification: {
    all: ['gamification'] as const,
    /** GET /api/Gamification/Badges/Me — full badge catalog with earned state. */
    badges: () => [...queryKeys.gamification.all, 'badges'] as const,
    /** GET /api/Gamification/Missions/Me — daily + weekly mission state for the current period. */
    missions: () => [...queryKeys.gamification.all, 'missions'] as const,
    /** GET /api/Gamification/Leagues/Me — cohort standings + tier + week boundaries. */
    league: () => [...queryKeys.gamification.all, 'league'] as const,
  },

  /**
   * P7-06/07/08 admin user-management hooks.
   *
   * DISTINCT from the legacy `users.*` namespace (which targets the old
   * `/api/Users/UserManagement/…` endpoints and is NOT used by these stories).
   * P7-07/08 mutation hooks MUST invalidate the keys in this namespace — never
   * the legacy `users.*` keys.
   *
   * PascalCase filter params mirror the backend's `SearchUsersQuery` binding
   * (`Role`, `Status`, `Q`, `PageNumber`, `PageSize`, `OrderBy`) — this is
   * intentional, not a typo. The query params sent over the wire are lowercase-
   * mapped by `ApiClient.buildUrl`; the key object just carries the raw params.
   */
  adminUsers: {
    all: ['adminUsers'] as const,
    /**
     * List key — includes all paginated/filter params so distinct filter
     * combinations are cached separately and the list is invalidated cleanly on
     * any mutation (by passing only `queryKeys.adminUsers.all`).
     */
    list: (filters?: object) =>
      [...queryKeys.adminUsers.all, 'list', filters ?? {}] as const,
    /** Single-profile key — used by detail page + invalidated by lifecycle mutations. */
    profile: (userId: number) =>
      [...queryKeys.adminUsers.all, 'profile', userId] as const,
    /** Family linkage key for a single user. */
    family: (userId: number) =>
      [...queryKeys.adminUsers.all, 'family', userId] as const,
    /** Activity summary key for a single user. */
    activity: (userId: number) =>
      [...queryKeys.adminUsers.all, 'activity', userId] as const,
  },
} as const;

export type QueryKeys = typeof queryKeys;
