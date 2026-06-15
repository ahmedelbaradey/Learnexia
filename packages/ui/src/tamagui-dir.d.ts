/**
 * Type augmentation: allow the HTML `dir` attribute on Tamagui Stack/View props.
 *
 * `dir` is forwarded to the DOM on web (it drives our RTL layout mirroring — the
 * dir-based RTL pattern) but is absent from Tamagui's prop types. The design-system
 * package declares the same augmentation for app/design-system consumers; this copy
 * exists so `@learnexia/ui` typechecks standalone (its isolated `tsc` program does
 * not include the design-system config file). Identical open-interface members
 * merge with no conflict where both are in scope.
 */
import '@tamagui/core';

declare module '@tamagui/core' {
  interface RNTamaguiViewNonStyleProps {
    dir?: 'ltr' | 'rtl' | 'auto';
  }
}
