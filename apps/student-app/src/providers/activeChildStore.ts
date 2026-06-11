/**
 * activeChildStore — the currently-selected child in the parent dashboard
 * + the Add-Child Modal open/close signal.
 *
 * The parent dashboard shows data for one active child at a time. This store
 * holds the selected child id and persists it to `localStorage` on web so the
 * parent returns to the same child after a hard reload or route change.
 *
 * Also exposes `addChildOpen` / `openAddChild` / `closeAddChild` so that any
 * page inside the (parent) group can trigger the AddChildModal that lives in
 * `_layout.tsx` without prop-drilling through `<Slot>` (Expo Router layouts
 * cannot forward props into rendered pages).
 *
 * Mirrors `localeStore` exactly:
 *  - Zustand create() — no Zustand middleware (same as localeStore).
 *  - Web-only localStorage persistence (key `lx_active_child`) for childId.
 *  - Falls back to null (= "use first child from useMyChildren") when nothing
 *    is persisted or when not on web.
 *
 * NOTE: this is UI/client state only. The actual child list comes from
 * `useMyChildren` (TanStack Query). Callers resolve the active child by
 * checking `activeChildId` first, then falling back to `children[0]`. When a
 * persisted id is no longer in the current children list the fallback applies
 * automatically — no explicit cleanup needed.
 *
 * Lead-approved pattern (parent-dashboard-uiux design spec §A.4 "BUILD A
 * CHILD-SWITCHER"). Not a new abstraction — identical shape to localeStore.
 */
import { Platform } from 'react-native';
import { create } from 'zustand';

/** localStorage key for the persisted active-child id (non-user-facing technical identifier). */
const ACTIVE_CHILD_STORAGE_KEY = 'lx_active_child';

/**
 * Read the persisted active-child id from localStorage (web only).
 * Returns null when not on web, unavailable, or empty.
 */
function readPersistedChildId(): string | null {
  if (Platform.OS !== 'web') return null;
  try {
    return globalThis.localStorage?.getItem(ACTIVE_CHILD_STORAGE_KEY) ?? null;
  } catch {
    // localStorage blocked — degrade silently.
  }
  return null;
}

/**
 * Write the active-child id to localStorage (web only). Errors are swallowed
 * so a storage restriction never crashes the app.
 */
function persistChildId(id: string | null): void {
  if (Platform.OS !== 'web') return;
  try {
    if (id === null) {
      globalThis.localStorage?.removeItem(ACTIVE_CHILD_STORAGE_KEY);
    } else {
      globalThis.localStorage?.setItem(ACTIVE_CHILD_STORAGE_KEY, id);
    }
  } catch {
    // Storage blocked — degrade silently.
  }
}

export interface ActiveChildState {
  /** The id of the currently selected child, or null to use the first child. */
  activeChildId: string | null;
  setActiveChildId: (id: string | null) => void;

  /**
   * Controls the AddChildModal mounted in `(parent)/_layout.tsx`.
   * Pages call `openAddChild()` instead of receiving a prop callback through
   * Expo Router's <Slot> (which cannot forward props into rendered pages).
   */
  addChildOpen: boolean;
  openAddChild: () => void;
  closeAddChild: () => void;
}

export const useActiveChildStore = create<ActiveChildState>((set) => ({
  activeChildId: readPersistedChildId(),
  setActiveChildId: (id) => {
    persistChildId(id);
    set({ activeChildId: id });
  },

  addChildOpen: false,
  openAddChild: () => set({ addChildOpen: true }),
  closeAddChild: () => set({ addChildOpen: false }),
}));
