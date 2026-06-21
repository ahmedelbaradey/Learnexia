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
    /** GET /api/Gamification/TimedEventParticipations/Me — P4-12 per-child participation snapshots. */
    timedEventParticipations: () =>
      [...queryKeys.gamification.all, 'timed-event-participations'] as const,
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

  /**
   * P7-01..P7-05 admin curriculum hooks.
   *
   * DISTINCT from the student-facing `learning.*` namespace (different endpoints,
   * different DTOs, admin-only access). All 5 curriculum stories use this single
   * namespace — no per-story sub-namespaces are permitted.
   *
   * Coverage key note: `coverage(gradeId)` = P7-01 subject-language coverage
   * (6-slot ar/en presence grid); `pubCoverage(gradeId)` = P7-05 publication
   * coverage (Draft/Published status per entity). They are intentionally distinct.
   *
   * Invalidation convention: mutations invalidate `adminCurriculum.all` to bust
   * all list/detail caches; fine-grained invalidation (e.g. `units(subjectId)`)
   * is used where only one subtree needs refresh.
   */
  adminCurriculum: {
    all: ['adminCurriculum'] as const,

    // P7-01 — subjects & units
    subjects: (filters?: object) =>
      [...queryKeys.adminCurriculum.all, 'subjects', filters ?? {}] as const,
    subject: (id: number) =>
      [...queryKeys.adminCurriculum.all, 'subject', id] as const,
    /** Subject-language coverage 6-slot report for a grade (P7-01). */
    coverage: (gradeId: number) =>
      [...queryKeys.adminCurriculum.all, 'coverage', gradeId] as const,
    units: (subjectId: number, filters?: object) =>
      [...queryKeys.adminCurriculum.all, 'units', subjectId, filters ?? {}] as const,
    grades: () =>
      [...queryKeys.adminCurriculum.all, 'grades'] as const,

    // P7-02 — lessons & content blocks
    lessons: (unitId: number, filters?: object) =>
      [...queryKeys.adminCurriculum.all, 'lessons', unitId, filters ?? {}] as const,
    lesson: (lessonId: number) =>
      [...queryKeys.adminCurriculum.all, 'lesson', lessonId] as const,
    blocks: (lessonId: number) =>
      [...queryKeys.adminCurriculum.all, 'blocks', lessonId] as const,

    // P7-04 — questions
    questions: (lessonId: number) =>
      [...queryKeys.adminCurriculum.all, 'questions', lessonId] as const,
    question: (id: number) =>
      [...queryKeys.adminCurriculum.all, 'question', id] as const,

    // P7-03 — skills & graph
    skills: (filters?: object) =>
      [...queryKeys.adminCurriculum.all, 'skills', filters ?? {}] as const,
    skill: (id: number) =>
      [...queryKeys.adminCurriculum.all, 'skill', id] as const,
    graph: (subjectId: number) =>
      [...queryKeys.adminCurriculum.all, 'graph', subjectId] as const,
    prerequisites: (nodeId: number) =>
      [...queryKeys.adminCurriculum.all, 'prerequisites', nodeId] as const,
    unlockedBy: (nodeId: number) =>
      [...queryKeys.adminCurriculum.all, 'unlocked-by', nodeId] as const,

    // P7-05 — lifecycle (entityType + entityId)
    versions: (entityType: number, entityId: number) =>
      [...queryKeys.adminCurriculum.all, 'versions', entityType, entityId] as const,
    preview: (entityType: number, entityId: number) =>
      [...queryKeys.adminCurriculum.all, 'preview', entityType, entityId] as const,
    /** Publication coverage (Draft/Published status) for a grade (P7-05). */
    pubCoverage: (gradeId: number) =>
      [...queryKeys.adminCurriculum.all, 'pub-coverage', gradeId] as const,
  },

  /**
   * P7-13 admin gamification hooks.
   *
   * DISTINCT from the student-facing `gamification.*` namespace (different
   * endpoints, different DTOs, AdminOnly access).
   *
   * League-tier override + streak-freeze grant success invalidates
   * `adminUsers.activity(childId)` (existing namespace) — the read lives there.
   */
  adminGamification: {
    all: ['adminGamification'] as const,
    badges: () => [...queryKeys.adminGamification.all, 'badges'] as const,
    missions: () => [...queryKeys.adminGamification.all, 'missions'] as const,
    timedEvents: () => [...queryKeys.adminGamification.all, 'timed-events'] as const,
  },

  /**
   * P7-12 admin audit-log hooks.
   *
   * DISTINCT from all other admin namespaces. Read-only — there are no
   * invalidation consumers in v1 (no mutation touches the audit log). The
   * namespace is added now so future invalidation can be wired without
   * changing the key shape.
   *
   * PascalCase filter params mirror the backend's `GetAuditLogQuery` binding
   * (`AdminUserId`, `ActionType`, `TargetEntityType`, `DateFrom`, `DateTo`,
   * `PageNumber`, `PageSize`).
   */
  adminAudit: {
    all: ['adminAudit'] as const,
    /**
     * List key — includes all paginated/filter params so distinct filter
     * combinations are cached separately.
     */
    list: (filters?: object) =>
      [...queryKeys.adminAudit.all, 'list', filters ?? {}] as const,
  },

  /**
   * P7-09 admin moderation hooks.
   *
   * DISTINCT from all other admin namespaces. Covers the queue list, single
   * item detail, and the review mutation.
   *
   * `list(filters)` — includes all filter/pagination params so distinct
   *   combinations are cached separately.
   * `item(id)`     — single-item detail; invalidated by a successful review.
   * Invalidation convention: mutations invalidate both `adminModeration.all`
   *   (busts every list cache) and `adminModeration.item(id)`.
   */
  adminModeration: {
    all: ['adminModeration'] as const,
    list: (filters?: object) =>
      [...queryKeys.adminModeration.all, 'list', filters ?? {}] as const,
    item: (id: number) =>
      [...queryKeys.adminModeration.all, 'item', id] as const,
  },
  /**
   * P5-05 parent analytics hooks.
   *
   * All per-child queries include childId so child-switching invalidates
   * correctly. The namespace is `parentAnalytics` — distinct from both
   * `family.*` (children list) and `learning.*` (student-facing).
   *
   * Key design: `reports(childId)` covers ALL THREE chart panels (dailyXpSeries,
   * xpTrend20Day, timeOfDayBuckets) — a single GET /api/Parent/Children/{id}/Reports
   * call. The old `weeklyActivity`, `twentyDayActivity`, `timeOfDay` keys are
   * REMOVED — those endpoints do not exist.
   *
   * `familySummary()` is NOT keyed by childId — it covers all children in the family.
   */
  parentAnalytics: {
    all: ['parentAnalytics'] as const,
    /** GET api/Parent/Children/{id}/WeeklyKpis */
    weeklyKpis: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'weekly-kpis', childId] as const,
    /**
     * GET api/Parent/Children/{id}/Reports — feeds daily-activity, 20-day trend,
     * and time-of-day charts. ONE key for all three panels.
     */
    reports: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'reports', childId] as const,
    /** GET api/Parent/Children/{id}/SubjectMastery */
    subjectMastery: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'subject-mastery', childId] as const,
    /** GET api/Parent/Children/{id}/WeakAreas */
    weakAreas: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'weak-areas', childId] as const,
    /** GET api/Parent/Children/{id}/Recommendations */
    recommendations: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'recommendations', childId] as const,
    /** GET api/Parent/Children/{id}/Progress */
    childProgress: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'child-progress', childId] as const,
    /** GET api/Parent/Family/Summary (not per-child) */
    familySummary: () =>
      [...queryKeys.parentAnalytics.all, 'family-summary'] as const,
    /** GET api/Parent/Children/{id}/Energy */
    childEnergy: (childId: string) =>
      [...queryKeys.parentAnalytics.all, 'child-energy', childId] as const,
    },

  /**
   * P7-10 admin platform analytics hooks.
   *
   * Read-only aggregate endpoints in the Identity module.
   * Params for kpis: camelCase `from`/`to` (controller binding).
   * No time-series endpoint in v1 — only summary + categorical breakdowns.
   */
  adminAnalytics: {
    all: ['adminAnalytics'] as const,
    kpis: (from?: string, to?: string) =>
      [...queryKeys.adminAnalytics.all, 'kpis', { from, to }] as const,
  },

  /**
   * P7-11 admin AI-safety monitoring hooks.
   *
   * Read-only aggregate endpoints in the Ai module.
   * Params for signals/trend/usage: PascalCase `From`/`To` (controller binding).
   * `evals` takes NO params. `flagged` adds PascalCase paging/filter params.
   */
  adminAiSafety: {
    all: ['adminAiSafety'] as const,
    signals: (From?: string, To?: string) =>
      [...queryKeys.adminAiSafety.all, 'signals', { From, To }] as const,
    trend: (From?: string, To?: string) =>
      [...queryKeys.adminAiSafety.all, 'trend', { From, To }] as const,
    usage: (From?: string, To?: string) =>
      [...queryKeys.adminAiSafety.all, 'usage', { From, To }] as const,
    evals: () =>
      [...queryKeys.adminAiSafety.all, 'evals'] as const,
    flagged: (filters?: object) =>
      [...queryKeys.adminAiSafety.all, 'flagged', filters ?? {}] as const,
  },
} as const;

export type QueryKeys = typeof queryKeys;
