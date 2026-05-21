/**
 * Optional Skia access for native glow / confetti / radial gradients.
 *
 * GAP 5 + GAP 9 RESOLUTION: backdrop blur, badge glow rings, the legendary
 * hue-rotate, and RewardPopup confetti use `@shopify/react-native-skia` on
 * NATIVE. Skia is a PEER dependency and is not safe to import in a Node/SSR/
 * type-only environment, so it is loaded lazily and guarded. When Skia is
 * unavailable (web SSR, Node smoke test, or a host that omitted the peer dep),
 * components fall back to a non-blurred semi-transparent surface / CSS box-shadow
 * glow / static (non-animated) legendary badge.
 *
 * A Node-env mock lives at `__mocks__/@shopify/react-native-skia.ts` for the
 * smoke test (see catalog/README.md).
 */

export interface SkiaModuleLike {
  Canvas: unknown;
  [key: string]: unknown;
}

let cached: SkiaModuleLike | null | undefined;

/**
 * Returns the Skia module if it is installed and importable, else `null`.
 * Never throws. Components MUST treat `null` as "render the CSS / RN fallback".
 */
export function tryLoadSkia(): SkiaModuleLike | null {
  if (cached !== undefined) return cached;
  try {
    // eslint-disable-next-line @typescript-eslint/no-require-imports
    const mod = require('@shopify/react-native-skia') as SkiaModuleLike;
    cached = mod ?? null;
  } catch {
    cached = null;
  }
  return cached;
}

/** True when Skia-backed effects (blur, confetti, glow ring) are available. */
export function hasSkia(): boolean {
  return tryLoadSkia() !== null;
}
