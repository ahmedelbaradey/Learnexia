/**
 * NATIVE font loading helpers.
 *
 * On native, expo-font registers each `.ttf` under a distinct family key
 * (`Poppins-400`, `Cairo-700`, …). Tamagui maps its numeric weight scale onto
 * those keys via a per-family `face` object: `{ [weight]: { normal: key } }`.
 *
 * The app shell calls `useFonts(nativeFontMap)` (expo-font) to load the assets,
 * and the Tamagui config can attach `nativeFaceFor(family)` to each createFont so
 * weights resolve to the right physical face on iOS/Android.
 *
 * This file is NOT imported on web (web uses `@font-face` via `webFontFace.ts`),
 * but it is platform-safe to import (no native-only module is referenced).
 */
import { fontFaces, type FontFaceDescriptor } from './assets';

/** expo-font asset map: `{ 'Poppins-400': require(...), ... }`. */
export const nativeFontMap: Record<string, string | number> = fontFaces.reduce(
  (acc, f) => {
    acc[f.key] = f.asset;
    return acc;
  },
  {} as Record<string, string | number>,
);

/** Tamagui `face` mapping for one family: weight → registered face key. */
export function nativeFaceFor(
  family: FontFaceDescriptor['family'],
): Record<number, { normal: string }> {
  return fontFaces
    .filter((f) => f.family === family)
    .reduce(
      (acc, f) => {
        acc[f.weight] = { normal: f.key };
        return acc;
      },
      {} as Record<number, { normal: string }>,
    );
}
