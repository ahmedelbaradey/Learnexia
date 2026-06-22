/**
 * ConfettiLayer — celebratory particle burst for RewardPopup / reward moments.
 *
 * Salvaged from the retired `feat/P4-08-gamification-screens-motion` branch
 * (plan L5) and reconciled with the carryover Design Spec
 * `design-system/ui_kits/student-app/P4-08.md` §0.1 + motion table:
 *  - burst lifetime ≤ `durations.celebrate` (700ms) — never blocks input;
 *  - particle budget ≤ 24 (NFR-7): 4–8 for correct-answer bursts,
 *    12–24 for RewardPopup (caller passes `count`);
 *  - NATIVE: Skia canvas with gravity-driven circles (lazy-loaded peer dep);
 *  - WEB / no-Skia FALLBACK (spec §0.1): absolutely-positioned 8×12px
 *    rounded-2px rects (geometry per `preview/mobile-confetti-trophy.html`),
 *    palette = CONFETTI_COLORS family, rotation −40°…60°, animated fall+fade
 *    over 700ms via Moti. When Moti is also unavailable (Node/SSR smoke) the
 *    layer renders nothing — the celebration stays complete without particles.
 *
 * Reduce-motion: when `useReduceMotion()` is true the component returns null —
 * no particle layer at all (spec motion table: confetti fallback = "none").
 *
 * A11y: `accessible={false}` / accessibilityElementsHidden — purely decorative.
 */
import { colors, durations } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Dimensions } from 'react-native';

import { useReduceMotion } from '../hooks/useReduceMotion';
import { tryLoadMoti } from './moti';
import { tryLoadSkia } from './skia';

// ---------------------------------------------------------------------------
// Constants — Design Spec §0.1 + §9 (NFR-7 budget)
// ---------------------------------------------------------------------------

/** Hard particle budget (Design Spec §9 "confetti ≤24 particles"). */
const MAX_PARTICLES = 24;
/** Default count = RewardPopup band (12–24). Bursts pass 4–8 explicitly. */
const DEFAULT_COUNT = 18;
/** Burst lifetime — `--lx-dur-celebrate` (700ms). */
const LIFETIME_MS = durations.celebrate;
/** Opacity starts fading at this age (ms). */
const FADE_START_MS = Math.round(LIFETIME_MS * 0.45);
const GRAVITY = 980; // px/s² (native Skia physics)

const RADIUS_MIN = 4;
const RADIUS_MAX = 9;

// Web-fallback rect geometry — literals from preview/mobile-confetti-trophy.html.
const RECT_WIDTH = 8;
const RECT_HEIGHT = 12;
const RECT_RADIUS = 2;
const ROTATE_MIN_DEG = -40;
const ROTATE_MAX_DEG = 60;

// ---------------------------------------------------------------------------
// Palettes — token-derived color sets per celebration class
// ---------------------------------------------------------------------------

const PALETTES = {
  multicolor: [
    colors.xp, // #FACC15 gold-yellow
    colors.accent, // #F59E0B amber
    colors.primary, // #4F46E5 indigo
    colors.secondary, // #22C55E green
    colors.purple, // #A855F7 purple
    colors.heart, // #FB7185 pink/red
  ],
  rare: [
    colors.gem, // #38BDF8 sky-blue
    colors.primary, // #4F46E5
    colors.primaryLight, // #A5B4FC indigo-300
  ],
  epic: [
    colors.purple, // #A855F7
    colors.purpleLight, // #E9D5FF
    colors.primary, // #4F46E5
  ],
  legendary: [
    colors.xp, // #FACC15
    colors.gold, // #FBBF24
    colors.purple, // #A855F7
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
   * When true, spawns particles and runs the ≤700ms burst. The layer stops
   * drawing by itself when the burst expires; reset to false before
   * re-triggering to fire a fresh burst.
   */
  active: boolean;
  /**
   * Color palette variant — drives the particle color set.
   * @default 'multicolor'
   */
  paletteVariant?: ConfettiPaletteVariant;
  /**
   * Particle count — Design Spec §0.1: 4–8 for correct-answer bursts,
   * 12–24 for RewardPopup. Clamped to 24 (NFR-7).
   * @default 18
   */
  count?: number;
  /** Called once shortly after the burst expires. */
  onComplete?: () => void;
  /**
   * Stable test identifier — present in both animated and reduce-motion paths
   * (reduce-motion returns null so the tester asserts absence in that case).
   * @default 'confetti-layer'
   */
  testID?: string;
}

// ---------------------------------------------------------------------------
// Internal particle types
// ---------------------------------------------------------------------------

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  color: string;
  bornAtMs: number;
}

interface RectParticle {
  /** Initial position as % of the layer (per the preview card scatter). */
  leftPct: number;
  topPct: number;
  rotateDeg: number;
  color: string;
  /** Fall distance in px over the burst lifetime. */
  fallPx: number;
  /** Per-particle start delay (ms) so the burst doesn't read as one block. */
  delayMs: number;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function rand(min: number, max: number): number {
  return min + Math.random() * (max - min);
}

function clampCount(count: number): number {
  return Math.max(1, Math.min(Math.floor(count), MAX_PARTICLES));
}

function pickColor(palette: readonly string[]): string {
  // Index is bounded to [0, palette.length) — palette is a non-empty const.
  return palette[Math.floor(Math.random() * palette.length)] ?? palette[0]!;
}

function spawnParticles(palette: readonly string[], count: number): Particle[] {
  const { width, height } = Dimensions.get('window');
  const originX = width / 2;
  const originY = height * 0.45;
  const now = Date.now();

  return Array.from({ length: count }, (): Particle => ({
    x: originX + rand(-30, 30),
    y: originY + rand(-20, 20),
    vx: rand(-180, 180),
    vy: rand(-600, -300), // negative = upward
    radius: rand(RADIUS_MIN, RADIUS_MAX),
    color: pickColor(palette),
    bornAtMs: now,
  }));
}

function spawnRectParticles(palette: readonly string[], count: number): RectParticle[] {
  // Scatter band per preview/mobile-confetti-trophy.html (top 18–75%, left 15–85%).
  return Array.from({ length: count }, (): RectParticle => ({
    leftPct: rand(10, 90),
    topPct: rand(12, 55),
    rotateDeg: rand(ROTATE_MIN_DEG, ROTATE_MAX_DEG),
    color: pickColor(palette),
    fallPx: rand(60, 140),
    delayMs: rand(0, 120),
  }));
}

// ---------------------------------------------------------------------------
// Main export
// ---------------------------------------------------------------------------

/**
 * Renders the celebratory particle layer. Returns null when: reduce-motion is
 * active, or `active` is false. Uses Skia on capable hosts, the Moti rect
 * fallback elsewhere (web PWA), and nothing when neither is available.
 */
export function ConfettiLayer({
  active,
  paletteVariant = 'multicolor',
  count = DEFAULT_COUNT,
  onComplete,
  testID = 'confetti-layer',
}: ConfettiLayerProps) {
  const reduceMotion = useReduceMotion();

  // Reduce-motion: no confetti at all (spec motion table fallback = "none").
  if (reduceMotion || !active) return null;

  const clamped = clampCount(count);
  const skia = tryLoadSkia();
  if (skia) {
    return (
      <SkiaCanvas
        skia={skia}
        paletteVariant={paletteVariant}
        count={clamped}
        onComplete={onComplete}
        testID={testID}
      />
    );
  }

  // Web / no-Skia fallback (Design Spec §0.1).
  return (
    <RectFallback paletteVariant={paletteVariant} count={clamped} onComplete={onComplete} testID={testID} />
  );
}

ConfettiLayer.displayName = 'ConfettiLayer';

// ---------------------------------------------------------------------------
// Web fallback — Moti-animated 8×12 rounded rects (fall + fade, 700ms)
// ---------------------------------------------------------------------------

interface RectFallbackProps {
  paletteVariant: ConfettiPaletteVariant;
  count: number;
  onComplete?: () => void;
  testID?: string;
}

function RectFallback({ paletteVariant, count, onComplete, testID }: RectFallbackProps) {
  const [done, setDone] = useState(false);

  const particles = useMemo(
    () => spawnRectParticles(PALETTES[paletteVariant], count),
    [paletteVariant, count],
  );

  useEffect(() => {
    const timer = setTimeout(() => {
      setDone(true);
      onComplete?.();
    }, LIFETIME_MS + 150);
    return () => clearTimeout(timer);
  }, [onComplete]);

  const moti = tryLoadMoti();
  const MotiView = moti?.MotiView as
    | React.ComponentType<Record<string, unknown>>
    | undefined;

  // Decorative-only: without Moti there is no fall+fade animation, so render
  // nothing rather than freezing static rects on screen.
  if (done || !MotiView) return null;

  return (
    <Stack
      testID={testID}
      position="absolute"
      top={0}
      left={0}
      right={0}
      bottom={0}
      pointerEvents="none"
      accessible={false}
      accessibilityElementsHidden
    >
      {particles.map((p, i) => (
        <MotiView
          key={i}
          from={{ opacity: 1, translateY: 0 }}
          animate={{ opacity: 0, translateY: p.fallPx }}
          transition={{
            type: 'timing',
            duration: LIFETIME_MS,
            delay: p.delayMs,
          }}
          style={{
            position: 'absolute',
            top: `${p.topPct}%`,
            left: `${p.leftPct}%`,
            width: RECT_WIDTH,
            height: RECT_HEIGHT,
            borderRadius: RECT_RADIUS,
            backgroundColor: p.color,
            transform: [{ rotate: `${p.rotateDeg}deg` }],
          }}
        />
      ))}
    </Stack>
  );
}

// ---------------------------------------------------------------------------
// Native Skia canvas — gravity-driven circles
// Separated so ConfettiLayer can return null before any hooks run.
// ---------------------------------------------------------------------------

interface SkiaCanvasProps {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  skia: Record<string, any>;
  paletteVariant: ConfettiPaletteVariant;
  count: number;
  onComplete?: () => void;
  testID?: string;
}

function SkiaCanvas({ skia, paletteVariant, count, onComplete, testID }: SkiaCanvasProps) {
  const { width, height } = Dimensions.get('window');

  // Mutable physics state — never stored in React state to avoid re-renders
  const particlesRef = useRef<Particle[]>([]);
  const lastTimestampRef = useRef<number | null>(null);
  const rafRef = useRef<number | null>(null);

  // A tick counter incremented each frame to trigger a React re-render, which
  // causes Skia's Canvas to re-draw its children with updated particle positions.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const [_tick, setTick] = useState(0);
  const [done, setDone] = useState(false);

  // Spawn particles on mount
  useEffect(() => {
    particlesRef.current = spawnParticles(PALETTES[paletteVariant], count);
    lastTimestampRef.current = null;
  }, [paletteVariant, count]);

  // Animation loop — `requestAnimationFrame` is bridged correctly on both
  // iOS + Android by RN, and is native on web.
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
      setDone(true);
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
    }, LIFETIME_MS + 150);
    return () => clearTimeout(timer);
  }, [onComplete]);

  // Resolve Skia components from the lazily-loaded module
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const Canvas = skia.Canvas as React.ComponentType<any>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const Circle = skia.Circle as React.ComponentType<any>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const Group = skia.Group as React.ComponentType<any> | undefined;

  if (done || !Canvas || !Circle) return null;

  const nowMs = Date.now();

  return (
    <Canvas
      testID={testID}
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
 * @deprecated Prefer `PALETTES.multicolor` via the `paletteVariant` prop.
 */
export const CONFETTI_COLORS = PALETTES.multicolor;
