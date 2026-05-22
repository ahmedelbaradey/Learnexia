/**
 * flashMessageStore — a tiny transient UI-message channel (Zustand v5).
 *
 * Used to hand a one-shot message from any-to-any screen without coupling it to
 * routing. The canonical case (Design Spec §3): the api-client interceptor fires
 * `onSignOut` on an unrecoverable 401 → `authStore.signOut()` flips the status →
 * the routing guard redirects to login, and login reads + clears the message
 * ("Your session expired…").
 *
 * It holds an i18n KEY (not a rendered string) so the message localizes with the
 * active locale at display time. Purely client/UI state — no server data.
 */

import { create } from 'zustand';

export interface FlashMessageState {
  /** An i18n key (e.g. 'auth.sessionExpired'), or null when nothing pending. */
  messageKey: string | null;
  /** Set the pending message key (or clear with null). */
  setMessage: (key: string | null) => void;
  /** Read-and-clear in one step (returns the key that was pending). */
  consume: () => string | null;
}

export const useFlashMessageStore = create<FlashMessageState>((set, get) => ({
  messageKey: null,
  setMessage: (key) => set({ messageKey: key }),
  consume: () => {
    const current = get().messageKey;
    if (current !== null) set({ messageKey: null });
    return current;
  },
}));
