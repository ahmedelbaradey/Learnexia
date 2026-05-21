/**
 * Node-env mock for `@shopify/react-native-skia`.
 *
 * The real Skia package ships native bindings that cannot load in a Node/jsdom
 * test environment. The UI components already guard Skia behind `tryLoadSkia()`
 * (see `src/internal/skia.ts`) and degrade gracefully, so in practice they never
 * import the real module under Node. This mock exists so that, if a test or
 * smoke harness DOES `require('@shopify/react-native-skia')`, it resolves to
 * harmless empty stubs instead of crashing.
 *
 * Wire it via a jest `moduleNameMapper` / `setupFiles` mock (see catalog/README.md).
 */
import React from 'react';

const Noop = (props: { children?: React.ReactNode }) =>
  React.createElement(React.Fragment, null, props?.children ?? null);

export const Canvas = Noop;
export const Group = Noop;
export const Circle = Noop;
export const Rect = Noop;
export const Blur = Noop;
export const BlurMask = Noop;
export const RadialGradient = Noop;
export const LinearGradient = Noop;
export const ColorMatrix = Noop;
export const Fill = Noop;

export const Skia = {} as Record<string, unknown>;
export const useValue = <T,>(v: T) => ({ current: v });
export const useComputedValue = <T,>(fn: () => T) => ({ current: fn() });

export default { Canvas, Group, Circle, Rect, Blur, RadialGradient, Skia };
