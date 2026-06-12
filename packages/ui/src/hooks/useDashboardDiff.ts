/**
 * useDashboardDiff — detects reward transitions on dashboard data.
 *
 * Compares the current `DashboardLike` snapshot against the previously-seen
 * value and emits a `DashboardDiff` describing what changed. All celebration
 * triggers (RewardOverlay, confetti, streak ignite, hearts depletion sheet,
 * league promotion sheet) derive from this diff.
 *
 * CRITICAL INVARIANT (R3 — cold-start safety):
 *   The prior-value ref is initialised to `undefined`, NOT to a zero/empty
 *   DashboardLike. When prior is `undefined` (first successful load), the hook
 *   returns `ZERO_DIFF` unconditionally — no celebrations fire on cold start.
 *   The ref is set to the live value only AFTER the first successful load.
 *
 * Design Spec §3, Plan Batch 1 — P4-08-FE-7.
 *
 * Usage:
 *   const diff = useDashboardDiff(dashboardQuery.data);
 *   // diff.xpDelta > 0  → XP bar fill + (optional) XP reward overlay
 *   // diff.levelDelta > 0 → level-up overlay + confetti
 *   // diff.newBadges.length > 0 → badge pop-in + overlay
 *   // diff.completedMissions.length > 0 → mission-complete overlay
 *   // diff.tierChange !== null → league promotion/demotion sheet
 *   // diff.streakDelta > 0 → streak-flame ignite
 *   // diff.heartsDelta < 0 && current.hearts === 0 → hearts depletion sheet
 */
import { useEffect, useRef, useState } from 'react';

// ---------------------------------------------------------------------------
// Input shape
// ---------------------------------------------------------------------------

/**
 * Minimal subset of DashboardDto consumed by this hook.
 * Using a structural type so the hook is decoupled from the generated client
 * and remains unit-testable without importing the api-client package.
 */
export interface DashboardLike {
  xp?: number | null;
  level?: number | null;
  streak?: number | null;
  hearts?: number | null;
  inPracticeMode?: boolean | null;
  /** Top-N recently earned badges from the dashboard summary. */
  recentBadges?: ReadonlyArray<{ code?: string | null }> | null;
  /** Daily mission instances from the dashboard summary. */
  dailyMissions?: ReadonlyArray<{ code?: string | null; status?: string | null }> | null;
  /** Weekly mission instance from the dashboard summary. */
  weeklyMission?: { code?: string | null; status?: string | null } | null;
  /** Current league tier from the league preview strip. */
  leaguePreview?: { tierName?: string | null } | null;
}

// ---------------------------------------------------------------------------
// Output shape
// ---------------------------------------------------------------------------

export interface DashboardDiff {
  /** Signed XP delta (positive = gained XP). */
  xpDelta: number;
  /** Signed level delta (positive = levelled up). */
  levelDelta: number;
  /** Signed streak delta (positive = streak grew). */
  streakDelta: number;
  /** Signed hearts delta (negative = hearts lost). */
  heartsDelta: number;
  /** Badge codes appearing in recentBadges that were NOT in the prior snapshot. */
  newBadges: string[];
  /** Mission codes whose status transitioned to 'Completed' vs the prior snapshot. */
  completedMissions: string[];
  /** League tier transition, or null when the tier did not change. */
  tierChange: { from: string; to: string } | null;
  /** true when inPracticeMode flipped false → true. */
  enteredPracticeMode: boolean;
  /** true when inPracticeMode flipped true → false. */
  exitedPracticeMode: boolean;
}

// ---------------------------------------------------------------------------
// Zero diff — returned on cold start (prior === undefined) and on no-change
// ---------------------------------------------------------------------------

/**
 * The zero-delta sentinel returned on cold start (prior === undefined) and
 * when the dashboard data has not changed. Not mutated at runtime — treated as
 * effectively readonly by all callers.
 */
export const ZERO_DIFF: DashboardDiff = {
  xpDelta: 0,
  levelDelta: 0,
  streakDelta: 0,
  heartsDelta: 0,
  newBadges: [],
  completedMissions: [],
  tierChange: null,
  enteredPracticeMode: false,
  exitedPracticeMode: false,
};

// ---------------------------------------------------------------------------
// Helper functions (exported for unit tests)
// ---------------------------------------------------------------------------

/**
 * Returns codes that appear in `next` but not in `prev`.
 * Both inputs may be null / undefined.
 *
 * @internal Exported for unit testing only. Callers should use `useDashboardDiff`.
 */
export function diffNewCodes(
  prev: ReadonlyArray<{ code?: string | null }> | null | undefined,
  next: ReadonlyArray<{ code?: string | null }> | null | undefined,
): string[] {
  const prevSet = new Set(
    (prev ?? []).map((item) => item.code).filter((c): c is string => typeof c === 'string' && c.length > 0),
  );
  return (next ?? [])
    .map((item) => item.code)
    .filter((c): c is string => typeof c === 'string' && c.length > 0 && !prevSet.has(c));
}

/**
 * Returns mission codes whose status is 'Completed' in `next` but was not
 * 'Completed' in `prev`. Covers both daily missions array and the single
 * weekly mission.
 *
 * @internal Exported for unit testing only. Callers should use `useDashboardDiff`.
 */
export function diffCompletedMissions(
  prevDaily: ReadonlyArray<{ code?: string | null; status?: string | null }> | null | undefined,
  nextDaily: ReadonlyArray<{ code?: string | null; status?: string | null }> | null | undefined,
  prevWeekly: { code?: string | null; status?: string | null } | null | undefined,
  nextWeekly: { code?: string | null; status?: string | null } | null | undefined,
): string[] {
  const completed: string[] = [];

  // Build a lookup: code → status for the PREVIOUS snapshot
  const prevStatusMap = new Map<string, string | null | undefined>();
  for (const m of prevDaily ?? []) {
    if (m.code) prevStatusMap.set(m.code, m.status);
  }
  if (prevWeekly?.code) {
    prevStatusMap.set(prevWeekly.code, prevWeekly.status);
  }

  // Check next daily missions
  for (const m of nextDaily ?? []) {
    if (!m.code) continue;
    if (m.status === 'Completed' && prevStatusMap.get(m.code) !== 'Completed') {
      completed.push(m.code);
    }
  }

  // Check next weekly mission
  if (
    nextWeekly?.code &&
    nextWeekly.status === 'Completed' &&
    prevStatusMap.get(nextWeekly.code) !== 'Completed'
  ) {
    completed.push(nextWeekly.code);
  }

  return completed;
}

// ---------------------------------------------------------------------------
// Hook
// ---------------------------------------------------------------------------

/**
 * Computes the reward diff between consecutive dashboard snapshots.
 *
 * @param data - Current dashboard snapshot from `useDashboard().data`.
 *               Pass `undefined` while the query is loading.
 * @returns `DashboardDiff` — always `ZERO_DIFF` on the first successful load
 *          (cold-start safety invariant).
 */
export function useDashboardDiff(data: DashboardLike | undefined): DashboardDiff {
  // prevRef is intentionally `undefined` on mount — NEVER initialised to zero
  // or empty-object. This is the cold-start safety invariant.
  const prevRef = useRef<DashboardLike | undefined>(undefined);

  const [diff, setDiff] = useState<DashboardDiff>(ZERO_DIFF);

  useEffect(() => {
    // Query not yet resolved → nothing to diff
    if (data === undefined) return;

    const prev = prevRef.current;

    if (prev === undefined) {
      // First successful load — store as baseline, emit zero diff.
      prevRef.current = data;
      setDiff(ZERO_DIFF);
      return;
    }

    // Both prev and data are defined — compute the real diff.
    const xpDelta = (data.xp ?? 0) - (prev.xp ?? 0);
    const levelDelta = (data.level ?? 0) - (prev.level ?? 0);
    const streakDelta = (data.streak ?? 0) - (prev.streak ?? 0);
    const heartsDelta = (data.hearts ?? 0) - (prev.hearts ?? 0);

    const newBadges = diffNewCodes(prev.recentBadges, data.recentBadges);

    const completedMissions = diffCompletedMissions(
      prev.dailyMissions,
      data.dailyMissions,
      prev.weeklyMission,
      data.weeklyMission,
    );

    const prevTier = prev.leaguePreview?.tierName;
    const nextTier = data.leaguePreview?.tierName;
    const tierChange =
      prevTier && nextTier && prevTier !== nextTier
        ? { from: prevTier, to: nextTier }
        : null;

    const enteredPracticeMode = !prev.inPracticeMode && !!data.inPracticeMode;
    const exitedPracticeMode = !!prev.inPracticeMode && !data.inPracticeMode;

    const computed: DashboardDiff = {
      xpDelta,
      levelDelta,
      streakDelta,
      heartsDelta,
      newBadges,
      completedMissions,
      tierChange,
      enteredPracticeMode,
      exitedPracticeMode,
    };

    // Advance the cursor only after computing the diff
    prevRef.current = data;

    setDiff(computed);
  }, [data]);

  return diff;
}
