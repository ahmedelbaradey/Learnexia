/**
 * Asset module declarations for the Node/`tsc` type-check.
 *
 * Metro resolves `require('*.svg' | '*.png' | …)` to a numeric module id (native)
 * or a URL (web). Outside Metro, `tsc` needs these ambient declarations so the
 * brand asset requires in `src/assets.ts` type-check cleanly.
 */
declare module '*.svg' {
  const content: number;
  export default content;
}
declare module '*.png' {
  const content: number;
  export default content;
}
declare module '*.jpg' {
  const content: number;
  export default content;
}
declare module '*.ttf' {
  // URL string on web, numeric module id on native (the design-system font
  // loaders narrow per platform). Must match the package's own `*.ttf` decl.
  const content: string | number;
  export default content;
}
