/**
 * restartPromptStore — controls the native "restart to change language" dialog
 * (Zustand v5).
 *
 * On native, switching the app between Arabic (RTL) and English (LTR) requires
 * `I18nManager.forceRTL` + an app restart for the direction to fully apply
 * (Design Spec Screen 10). This store holds whether the prompt is visible and
 * which locale is pending. Web never triggers it (direction flips instantly via
 * `applyWebDirection`). Purely client/UI state.
 */

import { create } from 'zustand';

import type { Locale } from '../constants';

export interface RestartPromptState {
  isVisible: boolean;
  /** The locale the user wants to switch to (applied after restart). */
  pendingLocale: Locale | null;
  /** Show the prompt for a given target locale. */
  show: (locale: Locale) => void;
  /** Dismiss the prompt ("Later") without applying the locale change. */
  dismiss: () => void;
}

export const useRestartPromptStore = create<RestartPromptState>((set) => ({
  isVisible: false,
  pendingLocale: null,
  show: (locale) => set({ isVisible: true, pendingLocale: locale }),
  dismiss: () => set({ isVisible: false, pendingLocale: null }),
}));
