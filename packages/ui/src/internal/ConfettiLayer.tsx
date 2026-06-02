/**
 * ConfettiLayer — celebratory particle burst for RewardPopup / RewardOverlay.
 *
 * Real Skia particle system (P4-08-FE-7). Replaces the P1-08 stub.
 *
 * Design Spec §3 M3:
 *  - 40 particles on Android, 60 on iOS / web (Platform.OS guard).
 *  - 2 000 ms lifetime per particle.
 *  - Gravity 980 px/s² (downward).
 *  - Initial velocity: vx ∈ [-180, 180], vy ∈ [-600, -300] px/s (upward burst).
 *  - Opacity fades from 1 → 0 over the last 500 ms of lifetime.
 *  - Particle shape: filled circle, radius 4–9 px (randomised per particle).
 *  - Colors: rarity-tinted palette (see PALETTES below).
 *
 * Lazy-loading:
 *  Skia is a PEER dependency and may not be available in Node/SSR/test
 *  environments. We load it via `tryLoadSkia()` — returns null when unavailable.
 *  When null the component renders nothing (parent celebration still renders).
 *
 * Reduce-motion:
 *  When `useReduceMotion()` is true the component returns null — no particle
 *  layer at all.
 *
 * Animation strategy:
 *  Particle positions are held in a mutable ref (no React state updates per
 *  frame). A `requestAnimationFrame` loop on web, and Reanimated's
 *  `useFrameCallback` on native (when available), drive the physics each frame
 *  and batch-update a React state tick counter that triggers a Skia
 *  `<Canvas>` re-draw. Because Skia renders to a native canvas off the main
 *  thread, the JS-thread cost is only the physics math, not layout.
 *
 * A11y: `accessible={false}` / accessibilityElementsHidden — purely decorative.
 *
 * Performance (Design Spec R2):
 *  - `active` MUST be reset to false ~2100 ms after being set true.
 *    The caller schedules this via a `setTimeout` + unmount cleanup.
 *  - Android cap: 40 particles vs 60 on iOS/web.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Dimensions, Platform } from 'react-native';

import { colors } from '@learnexia/design-system';
import { useReduceMotion } from '../hooks/useReduceMotion';
import { tryLoadSkia } from './skia';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const PARTICLE_COUNT = Platform.OS === 'android' ? 40 : 60;
const LIFETIME_MS = 2000;
/** Opacity starts fading at this age (ms). */
const FADE_START_MS = 1500;
const GRAVITY = 980; // px/s²

const RADIUS_MIN = 4;
const RADIUS_MAX = 9;

// ---------------------------------------------------------------------------
// Palettes — Design Spec §3 M3 "Confetti colors"
// ---------------------------------------------------------------------------

const PALETTES = {
  multicolor: [
    colors.xp,          // #FACC15 gold-yellow
    colors.accent,      // #F59E0B amber
    colors.primary,     // #4F46E5 indigo
    colors.secondary,   // #22C55E green
    colors.purple,      // #A855F7 purple
    colors.heart,       // #FB7185 pink/red
  ],
  rare: [
    colors.gem,         // #38BDF8 sky-blue
    colors.primary,     // #4F46E5
    colors.primaryLight,// #A5B4FC indigo-300
  ],
  epic: [
    colors.purple,      // #A855F7
    colors.purpleLight, // #E9D5FF
    colors.primary,     // #4F46E5
  ],
  legendary: [
    colors.xp,          // #FACC15
    colors.gold,        // #FBBF24
    colors.purple,      // #A855F7
    colors.purpleLight, // #E9D5FF
  ],
  levelup: [
    '#A855F7', // $gradLevelup stop 1
    '#6366F1', // $gradLevelup stop 2
  ],
} as const;

export type ConfettiPaletteVariant = keyof typeof PALETTES;

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface ConfettiLayerProps {
  /**
   * When true, spawns particles and runs the animation.
   * MUST be reset to false ~2100 ms after setting true to stop the Skia canvas
   * draw and free GPU resources (Design Spec §3 M3 "active gate" rule).
   */
  active: boolean;
  /**
   * Color palette variant — drives the particle color set.
   * @default 'multicolor'
   */
  paletteVariant?: ConfettiPaletteVariant;
  /**
   * Called once approximately 2100 ms after `active` becomes true —
   * after all particles have expired.
   */
  onComplete?: () => void;
}

// ---------------------------------------------------------------------------
// Internal particle type (lives in a ref, never in React state)
// ---------------------------------------------------------------------------

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  /** Pre-parsed Skia color (hex string). */
  color: string;
  bornAtMs: number;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function rand(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

function spawnParticles(palette: readonly string[]): Particle[] {
  const { width, height } = Dimensions.get('window');
  const originX = width / 2;
  const originY = height * 0.45;
  const now = Date.now();

  return Array.from({ length: PARTICLE_COUNT }, (): Particle => ({
    x: originX + rand(-30, 30),
    y: originY + rand(-20, 20),
    vx: rand(-180, 180),
    vy: rand(-600, -300), // negative = upward
    radius: rand(RADIUS_MIN, RADIUS_MAX),
    // Non-null assertion is safe because index is bounded to [0, palette.length)
    // eslint-disable-next-line @typescript-eslint/no-non-null-assertion
    color: palette[Math.floor(Math.random() * palette.length)]!,
    bornAtMs: now,
  }));
}

// ---------------------------------------------------------------------------
// Main export
// ---------------------------------------------------------------------------

/**
 * Renders a Skia canvas with gravity-driven confetti particles.
 * Returns null when: reduce-motion is active, Skia is unavailable, or
 * `active` is false.
 */
export function ConfettiLayer({
  active,
  paletteVariant = 'multicolor',
  onComplete,
}: ConfettiLayerProps) {
  const reduceMotion = useReduceMotion();

  // Reduce-motion: no confetti at all (Design Spec §4 "ConfettiLayer").
  if (reduceMotion || !active) return null;

  const skia = tryLoadSkia();
  if (!skia) return null;

  return (
    <SkiaCanvas
      skia={skia}
      paletteVariant={paletteVariant}
      onComplete={onComplete}
    />
  );
}

ConfettiLayer.displayName = 'ConfettiLayer';

// ---------------------------------------------------------------------------
// Inner Skia-drawing component
// Separated so ConfettiLayer can return null before any hooks run.
// ---------------------------------------------------------------------------

interface SkiaCanvasProps {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  skia: Record<string, any>;
  paletteVariant: ConfettiPaletteVariant;
  onComplete?: () => void;
}

function SkiaCanvas({ skia, paletteVariant, onComplete }: SkiaCanvasProps) {
  const { width, height } = Dimensions.get('window');

  // Mutable physics state — never stored in React state to avoid re-renders
  const particlesRef = useRef<Particle[]>([]);
  const lastTimestampRef = useRef<number | null>(null);
  const rafRef = useRef<number | null>(null);

  // A tick counter incremented each frame to trigger a React re-render, which
  // causes Skia's Canvas to re-draw its children with updated particle positions.
  // The value itself is not used in JSX — the state change is the trigger.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const [_tick, setTick] = useState(0);

  // Spawn particles on mount
  useEffect(() => {
    const palette = PALETTES[paletteVariant];
    particlesRef.current = spawnParticles(palette);
    lastTimestampRef.current = null;
  }, [paletteVariant]);

  // Animation loop — runs `requestAnimationFrame` on web; on native we use the
  // same mechanism (RN bridges rAF correctly on both iOS + Android).
  const tick_ = useCallback(() => {
    const nowMs = Date.now();
    const nowSec = nowMs / 1000;

    if (lastTimestampRef.current === null) {
      lastTimestampRef.current = nowSec;
    }

    const dt = Math.min(nowSec - lastTimestampRef.current, 0.05); // cap at 50ms
    lastTimestampRef.current = nowSec;

    // Physics update
    for (const p of particlesRef.current) {
      p.vy += GRAVITY * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      // Mild air resistance
      p.vx *= Math.max(0, 1 - 1.2 * dt);
    }

    // Prune expired particles
    particlesRef.current = particlesRef.current.filter(
      (p) => nowMs - p.bornAtMs < LIFETIME_MS,
    );

    // Signal Skia canvas to re-draw
    setTick((t) => t + 1);

    // Keep looping if particles remain
    if (particlesRef.current.length > 0) {
      rafRef.current = requestAnimationFrame(tick_);
    } else {
      rafRef.current = null;
    }
  }, []);

  // Start / stop the animation loop
  useEffect(() => {
    rafRef.current = requestAnimationFrame(tick_);
    return () => {
      if (rafRef.current !== null) {
        cancelAnimationFrame(rafRef.current);
        rafRef.current = null;
      }
    };
  }, [tick_]);

  // Completion callback
  useEffect(() => {
    if (!onComplete) return;
    const timer = setTimeout(() => {
      onComplete();
    }, LIFETIME_MS + 100);
    return () => clearTimeout(timer);
  }, [onComplete]);

  // Resolve Skia components from the lazily-loaded module
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const Canvas = skia.Canvas as React.ComponentType<any>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const Circle = skia.Circle as React.ComponentType<any>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const Group = skia.Group as React.ComponentType<any> | undefined;

  if (!Canvas || !Circle) return null;

  const nowMs = Date.now();

  return (
    <Canvas
      style={{
        position: 'absolute' as const,
        top: 0,
        left: 0,
        width,
        height,
        pointerEvents: 'none' as const,
      }}
      accessible={false}
      accessibilityElementsHidden={true}
    >
      {particlesRef.current.map((p, i) => {
        const age = nowMs - p.bornAtMs;
        const clampedOpacity =
          age < FADE_START_MS
            ? 1
            : Math.max(0, 1 - (age - FADE_START_MS) / (LIFETIME_MS - FADE_START_MS));

        if (clampedOpacity <= 0) return null;

        // Skia v1.x JSX API: <Group> wraps <Circle> for opacity + transform.
        // Fall back to <Circle> with opacity prop when Group is unavailable.
        if (Group) {
          return (
            <Group key={i} opacity={clampedOpacity}>
              <Circle cx={p.x} cy={p.y} r={p.radius} color={p.color} />
            </Group>
          );
        }
        return (
          <Circle
            key={i}
            cx={p.x}
            cy={p.y}
            r={p.radius}
            color={p.color}
            opacity={clampedOpacity}
          />
        );
      })}
    </Canvas>
  );
}

// ---------------------------------------------------------------------------
// Backward-compat export (RewardPopup imports CONFETTI_COLORS from here)
// ---------------------------------------------------------------------------

/**
 * Token-derived confetti palette (multicolor variant).
 * Used by RewardPopup — kept for backward compatibility.
 *
 * @deprecated Prefer `PALETTES.multicolor` from this module.
 */
export const CONFETTI_COLORS = PALETTES.multicolor;
