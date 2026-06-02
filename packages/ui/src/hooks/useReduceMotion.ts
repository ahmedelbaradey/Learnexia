/**
 * useReduceMotion — cross-platform reduce-motion flag.
 *
 * Returns `true` when the user has requested reduced motion in their system
 * accessibility settings. Every animation entry-point MUST check this and
 * provide an instantaneous / opacity-only fallback when true.
 *
 * Implementation:
 *  - On React Native (iOS / Android): wraps Reanimated 3's `useReducedMotion()`,
 *    which reads `AccessibilityInfo.isReduceMotionEnabled()` on the UI thread.
 *  - On web: reads `window.matchMedia('(prefers-reduced-motion: reduce)')` and
 *    subscribes to changes.
 *
 * Fallback: if `react-native-reanimated` is not importable (Node / SSR / test),
 * the hook returns `false` so component snapshots still render without crashing.
 * Production bundles always have Reanimated available.
 *
 * Design Spec §4 "Reduce-motion strategy". Plan Batch 1 — P4-08-FE-7.
 */
import { useEffect, useState } from 'react';
import { Platform } from 'react-native';

// ---------------------------------------------------------------------------
// Reanimated lazy-load (module-level, evaluated once, never changes)
// ---------------------------------------------------------------------------

type UseReducedMotionHook = () => boolean;

function loadReanimatedHook(): UseReducedMotionHook | null {
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const rn = require('react-native-reanimated') as {
      useReducedMotion?: UseReducedMotionHook;
    };
    return rn?.useReducedMotion ?? null;
  } catch {
    return null;
  }
}

const reanimatedHook: UseReducedMotionHook | null = loadReanimatedHook();

/** Stable fallback that always returns false (used when Reanimated not available). */
function useFalse(): boolean {
  return false;
}

/**
 * The Reanimated hook or the stable fallback — selected once at module load.
 * Assigning at module level ensures the SAME function reference is used on
 * every render, satisfying the Rules of Hooks (no conditional hook calls).
 */
const useNativeReduceMotion: UseReducedMotionHook = reanimatedHook ?? useFalse;

// ---------------------------------------------------------------------------
// Web hook
// ---------------------------------------------------------------------------

function useWebReduceMotion(): boolean {
  const [value, setValue] = useState<boolean>(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return false;
    return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  });

  useEffect(() => {
    if (typeof window === 'undefined' || !window.matchMedia) return;
    const mq = window.matchMedia('(prefers-reduced-motion: reduce)');
    const listener = (e: MediaQueryListEvent) => setValue(e.matches);
    mq.addEventListener('change', listener);
    return () => mq.removeEventListener('change', listener);
  }, []);

  return value;
}

// ---------------------------------------------------------------------------
// Public export — platform-selected at module load, stable reference
// ---------------------------------------------------------------------------

/**
 * Returns `true` when the device / browser "reduce motion" setting is active.
 *
 * Call unconditionally at the top of any component or hook.
 *
 * @example
 *   const reduceMotion = useReduceMotion();
 *   if (reduceMotion) { // skip animation, show static state }
 */
export const useReduceMotion: UseReducedMotionHook =
  Platform.OS === 'web' ? useWebReduceMotion : useNativeReduceMotion;
