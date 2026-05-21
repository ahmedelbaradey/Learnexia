/**
 * Media query breakpoints (Design Spec §2d).
 *   sm     — phone (390px portrait reference)
 *   tablet — tablet portrait / small laptop
 *   laptop — laptop / desktop
 *
 * Tamagui v1 takes the media map as a plain object on `createTamagui({ media })`
 * (there is no `createMedia` helper in `@tamagui/core`); the object is typed
 * `as const` so breakpoint keys are preserved in the config type.
 */
export const media = {
  sm: { maxWidth: 767 },
  tablet: { minWidth: 768 },
  laptop: { minWidth: 1024 },
} as const;

export type Media = typeof media;
