/**
 * Lazy Moti loader.
 *
 * Moti depends on `react-native-reanimated`, whose native bindings are not safe
 * to import in a Node/SSR smoke environment. We load Moti lazily and guard so
 * components degrade to a static (but still visible) state when it is absent.
 * On a real Expo/RN host the peer deps are present and Moti loads normally.
 */
import type React from 'react';

export interface MotiModuleLike {
  MotiView?: React.ComponentType<Record<string, unknown>>;
  AnimatePresence?: React.ComponentType<Record<string, unknown>>;
  [key: string]: unknown;
}

let cached: MotiModuleLike | null | undefined;

export function tryLoadMoti(): MotiModuleLike | null {
  if (cached !== undefined) return cached;
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    cached = require('moti') as MotiModuleLike;
  } catch {
    cached = null;
  }
  return cached;
}

export function hasMoti(): boolean {
  return tryLoadMoti() !== null;
}
