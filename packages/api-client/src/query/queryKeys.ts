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
} as const;

export type QueryKeys = typeof queryKeys;
