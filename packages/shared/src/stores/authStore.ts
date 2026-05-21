/**
 * authStore — client/UI auth state (Zustand v5).
 *
 * Holds ONLY auth/UI state: the access/refresh tokens, the current user
 * identity, and the derived sign-in status. It is NOT a cache for server data
 * — profile, curriculum, gamification, etc. are owned by TanStack Query in
 * api-client (per the state-split rule).
 *
 * Token PERSISTENCE is delegated to the `TokenStorage` abstraction (Keychain
 * on native, sessionStorage/cookie on web). The store keeps an in-memory copy
 * for fast synchronous reads (e.g. the request interceptor) and writes through
 * to secure storage on change. Call `hydrate()` once at app start to load
 * persisted tokens.
 */

import { create } from 'zustand';

import type { CurrentUser } from '../types/auth';
import type { AuthTokens, TokenStorage } from '../storage/tokenStorage';
import { createInMemoryTokenStorage } from '../storage/tokenStorage';

export type AuthStatus = 'unknown' | 'signed-out' | 'signed-in';

export interface AuthState {
  status: AuthStatus;
  accessToken: string | null;
  refreshToken: string | null;
  user: CurrentUser | null;

  /** The storage strategy in use; swapped by the app at startup. */
  storage: TokenStorage;

  /** Inject the platform token storage (native/web). Call before `hydrate`. */
  setStorage: (storage: TokenStorage) => void;
  /** Load persisted tokens from storage into memory (call once at app start). */
  hydrate: () => Promise<void>;
  /** Persist tokens + flip to signed-in. */
  setTokens: (tokens: AuthTokens) => Promise<void>;
  /** Set/replace the current user identity. */
  setUser: (user: CurrentUser | null) => void;
  /** Clear everything (sign-out / refresh failure). */
  signOut: () => Promise<void>;
}

export const useAuthStore = create<AuthState>((set, get) => ({
  status: 'unknown',
  accessToken: null,
  refreshToken: null,
  user: null,
  storage: createInMemoryTokenStorage(),

  setStorage: (storage) => set({ storage }),

  hydrate: async () => {
    const persisted = await get().storage.getTokens();
    if (persisted) {
      set({
        accessToken: persisted.accessToken,
        refreshToken: persisted.refreshToken,
        status: 'signed-in',
      });
    } else {
      set({ status: 'signed-out' });
    }
  },

  setTokens: async (tokens) => {
    await get().storage.setTokens(tokens);
    set({
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      status: 'signed-in',
    });
  },

  setUser: (user) => set({ user }),

  signOut: async () => {
    await get().storage.clear();
    set({
      accessToken: null,
      refreshToken: null,
      user: null,
      status: 'signed-out',
    });
  },
}));
