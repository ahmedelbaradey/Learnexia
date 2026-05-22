/**
 * Brand asset requires. Metro resolves these to module ids at bundle time; in a
 * Node type-check the `*.svg` requires are typed via `types/assets.d.ts`.
 *
 * NOTE: `expo-image` / RN `Image` render SVGs only through `react-native-svg`
 * transformer config in a full setup. For P1-09 the runtime render is deferred
 * (documented); these requires keep the screens declarative and ready.
 */
export const assets = {
  logo: require('../../../design-system/assets/logo.svg'),
  logoMark: require('../../../design-system/assets/logo-mark.svg'),
  mascotOwl: require('../../../design-system/assets/mascot-owl.svg'),
} as const;
