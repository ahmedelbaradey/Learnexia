/**
 * Asset module declarations for the Node/`tsc` type-check.
 *
 * The admin dashboard consumes `@learnexia/design-system` as source via the
 * workspace path alias, so `tsc` pulls in the design-system font loaders
 * (`src/fonts/assets.ts`), which statically `import` the brand `.ttf` faces.
 * design-system carries its own ambient `*.ttf` declaration, but ambient
 * `declare module` is global only within the program that includes it — it does
 * not flow across to this app's separate `tsc` program. Without this file every
 * `.ttf` import resolves to TS2307 here. Mirrors `apps/student-app/types/assets.d.ts`.
 *
 * The bundlers (Next.js / Metro) resolve these at build time; this declaration
 * only satisfies the type-checker.
 */
declare module '*.ttf' {
  // URL string on web, numeric module id on native (the design-system font
  // loaders narrow per platform). Must match the package's own `*.ttf` decl.
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
