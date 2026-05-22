/**
 * Re-export the single source-of-truth Tamagui config from
 * `@learnexia/design-system`. The admin app does NOT define its own tokens or
 * themes — it consumes the design-system config so the dark palette, fonts
 * (Poppins/Cairo/Tajawal), spacing/radius scales, and media queries stay in
 * lock-step with the rest of the monorepo.
 *
 * The `@tamagui/next-plugin` reads this file (via `next.config.ts`) to extract
 * the config for build-time CSS generation; `tamagui.config.ts` therefore must
 * export the config as the default export.
 */
import { tamaguiConfig } from '@learnexia/design-system/config';

export type Conf = typeof tamaguiConfig;

declare module '@tamagui/core' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface TamaguiCustomConfig extends Conf {}
}

export default tamaguiConfig;
