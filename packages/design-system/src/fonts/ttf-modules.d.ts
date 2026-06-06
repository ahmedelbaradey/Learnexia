/**
 * Ambient module declaration for `.ttf` font asset imports.
 *
 * Metro resolves `import x from '*.ttf'` to a URL (web) or a numeric module id
 * (native). Outside Metro (plain `tsc --build` type-check) we need this ambient
 * type so the static `.ttf` imports in `assets.ts` type-check cleanly.
 *
 * NOTE: this file must NOT be named `assets.d.ts` — a `.d.ts` whose basename
 * matches a sibling `.ts` (`assets.ts`) is treated by TypeScript as that file's
 * emitted declaration and is dropped as a program input, so the ambient wildcard
 * never loads (causes TS2307 on every `.ttf` import). Keep it a distinct name.
 *
 * On web the resolved value is a string URL; on native it's a number. We type it
 * as the union and the loaders narrow per platform.
 */
declare module '*.ttf' {
  const content: string | number;
  export default content;
}
