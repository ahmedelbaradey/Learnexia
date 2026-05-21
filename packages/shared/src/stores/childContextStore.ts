/**
 * childContextStore — the active child a parent is currently viewing
 * (Zustand v5).
 *
 * Parent-driven, multi-child product: a parent can have several children and
 * switches which one the UI is scoped to. This store holds only the SELECTED
 * child id (UI/client state). The list of children and each child's data are
 * server data, fetched via TanStack Query in api-client — NOT stored here.
 */

import { create } from 'zustand';

export interface ChildContextState {
  /** The id of the child the parent is currently acting on; null = none. */
  activeChildId: string | null;
  setActiveChild: (childId: string | null) => void;
  clear: () => void;
}

export const useChildContextStore = create<ChildContextState>((set) => ({
  activeChildId: null,
  setActiveChild: (childId) => set({ activeChildId: childId }),
  clear: () => set({ activeChildId: null }),
}));
