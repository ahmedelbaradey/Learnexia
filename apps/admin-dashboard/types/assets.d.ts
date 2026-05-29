/**
 * Asset module declarations for the Node/`tsc` type-check.
 *
 * Metro/Next/webpack resolve `import x from '*.ttf' | '*.svg' | …'` to a URL
 * (web) or numeric module id (native). Outside the bundler (plain `tsc`
 * type-check) we need these ambient declarations so the brand asset imports in
 * `@learnexia/design-system/src/fonts/assets.ts` (which the admin-dashboard
 * type-checks against via path mapping) resolve cleanly.
 *
 * On web the resolved value is a string URL; design-system narrows per platform.
 */
declare module '*.ttf' {
  const content: string | number;
  export default content;
}
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
