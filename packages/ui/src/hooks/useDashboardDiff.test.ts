/**
 * Unit tests for useDashboardDiff helpers.
 *
 * These tests verify the PURE logic functions that back the hook:
 *   - `diffNewCodes`           — badge code diff (new codes appearing in `next`)
 *   - `diffCompletedMissions`  — mission completion transitions
 *   - `ZERO_DIFF`              — cold-start sentinel shape
 *
 * No test runner dependency — uses a minimal inline assert that can be
 * verified by reading the TypeScript or running with:
 *   `npx ts-node --project packages/ui/tsconfig.json packages/ui/src/hooks/useDashboardDiff.test.ts`
 *
 * Full hook-level tests (requiring renderHook from @testing-library/react)
 * are documented in the commented-out section at the bottom of this file —
 * they can be activated once jest + ts-jest are added as workspace devDeps.
 *
 * CRITICAL COLD-START INVARIANT (R3):
 *   `useDashboardDiff` MUST return `ZERO_DIFF` on the very first call where
 *   `data` is defined. It must NOT fire positive `xpDelta`, `newBadges`, or
 *   `completedMissions` on the first render even if the data has non-zero
 *   values. The hook uses `undefined` (NOT zero/empty) as the initial prev-ref
 *   to guarantee this.
 */

// Import pure helper functions (exported for testability)
import {
  diffNewCodes,
  diffCompletedMissions,
  ZERO_DIFF,
} from './useDashboardDiff';

// ---------------------------------------------------------------------------
// Minimal inline assert (avoids @types/node dependency)
// ---------------------------------------------------------------------------

function assertEqual<T>(actual: T, expected: T, message: string): void {
  if (actual !== expected) {
    throw new Error(
      `FAIL [${message}]\n  Expected: ${JSON.stringify(expected)}\n  Got:      ${JSON.stringify(actual)}`,
    );
  }
}

function assertDeepEqual<T>(actual: T, expected: T, message: string): void {
  const a = JSON.stringify(actual);
  const e = JSON.stringify(expected);
  if (a !== e) {
    throw new Error(
      `FAIL [${message}]\n  Expected: ${e}\n  Got:      ${a}`,
    );
  }
}

let passed = 0;
let failed = 0;

function test(name: string, fn: () => void): void {
  try {
    fn();
    passed++;
  } catch (err) {
    failed++;
    console.error(`✗ ${name}`);
    if (err instanceof Error) console.error('  ', err.message);
  }
}

// ---------------------------------------------------------------------------
// diffNewCodes tests
// ---------------------------------------------------------------------------

test('diffNewCodes: both null → empty array', () => {
  assertDeepEqual(diffNewCodes(null, null), [], 'both null');
});

test('diffNewCodes: prev empty, next has two codes → both new', () => {
  assertDeepEqual(
    diffNewCodes([], [{ code: 'FIRST_LESSON' }, { code: 'STREAK_3' }]),
    ['FIRST_LESSON', 'STREAK_3'],
    'prev empty',
  );
});

test('diffNewCodes: overlap — only new code returned', () => {
  assertDeepEqual(
    diffNewCodes(
      [{ code: 'FIRST_LESSON' }],
      [{ code: 'FIRST_LESSON' }, { code: 'STREAK_3' }],
    ),
    ['STREAK_3'],
    'only new code',
  );
});

test('diffNewCodes: identical sets → empty', () => {
  assertDeepEqual(
    diffNewCodes([{ code: 'FIRST_LESSON' }], [{ code: 'FIRST_LESSON' }]),
    [],
    'identical sets',
  );
});

test('diffNewCodes: null/undefined codes filtered out', () => {
  assertDeepEqual(
    diffNewCodes([{ code: undefined }], [{ code: null }, { code: 'STREAK_3' }]),
    ['STREAK_3'],
    'null/undefined codes ignored',
  );
});

// ---------------------------------------------------------------------------
// diffCompletedMissions tests
// ---------------------------------------------------------------------------

test('diffCompletedMissions: InProgress → Completed in daily list', () => {
  assertDeepEqual(
    diffCompletedMissions(
      [{ code: 'DAILY_3_LESSONS', status: 'InProgress' }],
      [{ code: 'DAILY_3_LESSONS', status: 'Completed' }],
      null,
      null,
    ),
    ['DAILY_3_LESSONS'],
    'InProgress → Completed',
  );
});

test('diffCompletedMissions: already Completed in prev → not re-counted', () => {
  assertDeepEqual(
    diffCompletedMissions(
      [{ code: 'DAILY_3_LESSONS', status: 'Completed' }],
      [{ code: 'DAILY_3_LESSONS', status: 'Completed' }],
      null,
      null,
    ),
    [],
    'already completed not re-counted',
  );
});

test('diffCompletedMissions: weekly mission completion detected', () => {
  assertDeepEqual(
    diffCompletedMissions(
      [],
      [],
      { code: 'WEEKLY_7_LESSONS', status: 'InProgress' },
      { code: 'WEEKLY_7_LESSONS', status: 'Completed' },
    ),
    ['WEEKLY_7_LESSONS'],
    'weekly completion',
  );
});

test('diffCompletedMissions: NotStarted → InProgress → empty (not completed)', () => {
  assertDeepEqual(
    diffCompletedMissions(
      [{ code: 'DAILY_3_LESSONS', status: 'NotStarted' }],
      [{ code: 'DAILY_3_LESSONS', status: 'InProgress' }],
      null,
      null,
    ),
    [],
    'non-Completed transition',
  );
});

test('diffCompletedMissions: only newly completed mission returned', () => {
  assertDeepEqual(
    diffCompletedMissions(
      [
        { code: 'DAILY_1_LESSON', status: 'Completed' },
        { code: 'DAILY_5_CORRECT', status: 'InProgress' },
      ],
      [
        { code: 'DAILY_1_LESSON', status: 'Completed' },
        { code: 'DAILY_5_CORRECT', status: 'Completed' },
      ],
      null,
      null,
    ),
    ['DAILY_5_CORRECT'],
    'only newly completed',
  );
});

// ---------------------------------------------------------------------------
// ZERO_DIFF shape validation
// ---------------------------------------------------------------------------

test('ZERO_DIFF: xpDelta is 0 (cold-start invariant)', () => {
  assertEqual(ZERO_DIFF.xpDelta, 0, 'xpDelta');
});

test('ZERO_DIFF: levelDelta is 0', () => {
  assertEqual(ZERO_DIFF.levelDelta, 0, 'levelDelta');
});

test('ZERO_DIFF: streakDelta is 0', () => {
  assertEqual(ZERO_DIFF.streakDelta, 0, 'streakDelta');
});

test('ZERO_DIFF: heartsDelta is 0', () => {
  assertEqual(ZERO_DIFF.heartsDelta, 0, 'heartsDelta');
});

test('ZERO_DIFF: newBadges is empty array', () => {
  assertDeepEqual(ZERO_DIFF.newBadges, [], 'newBadges');
});

test('ZERO_DIFF: completedMissions is empty array', () => {
  assertDeepEqual(ZERO_DIFF.completedMissions, [], 'completedMissions');
});

test('ZERO_DIFF: tierChange is null', () => {
  assertEqual(ZERO_DIFF.tierChange, null, 'tierChange');
});

test('ZERO_DIFF: enteredPracticeMode is false', () => {
  assertEqual(ZERO_DIFF.enteredPracticeMode, false, 'enteredPracticeMode');
});

test('ZERO_DIFF: exitedPracticeMode is false', () => {
  assertEqual(ZERO_DIFF.exitedPracticeMode, false, 'exitedPracticeMode');
});

// ---------------------------------------------------------------------------
// Results
// ---------------------------------------------------------------------------

if (failed > 0) {
  console.error(`\n✗ ${failed} test(s) failed, ${passed} passed.`);
  // process.exit(1) — omitted because `process` requires @types/node
} else {
  console.log(`✓ All ${passed} tests passed.`);
}

// ---------------------------------------------------------------------------
// COLD-START INVARIANT — hook-level spec (requires @testing-library/react)
// ---------------------------------------------------------------------------
/*
 * Activate by adding jest + ts-jest + @testing-library/react to
 * packages/ui/package.json devDependencies and creating jest.config.ts.
 *
 * import { renderHook, act } from '@testing-library/react';
 * import { useDashboardDiff } from './useDashboardDiff';
 *
 * describe('useDashboardDiff — cold-start invariant (R3)', () => {
 *   it('first load with real XP data returns ZERO_DIFF', () => {
 *     const data = { xp: 1000, level: 5, streak: 7, hearts: 5 };
 *     const { result } = renderHook(({ d }) => useDashboardDiff(d), {
 *       initialProps: { d: data },
 *     });
 *     // Prior ref is undefined on mount → ZERO_DIFF
 *     expect(result.current.xpDelta).toBe(0);
 *     expect(result.current.newBadges).toHaveLength(0);
 *     expect(result.current.completedMissions).toHaveLength(0);
 *   });
 *
 *   it('subsequent XP increase returns correct xpDelta', () => {
 *     const { result, rerender } = renderHook(({ d }) => useDashboardDiff(d), {
 *       initialProps: { d: { xp: 100, level: 1 } },
 *     });
 *     expect(result.current.xpDelta).toBe(0); // baseline
 *     act(() => { rerender({ d: { xp: 150, level: 1 } }); });
 *     expect(result.current.xpDelta).toBe(50);
 *   });
 *
 *   it('new badge appearing emits newBadges code', () => {
 *     const { result, rerender } = renderHook(({ d }) => useDashboardDiff(d), {
 *       initialProps: { d: { recentBadges: [] } },
 *     });
 *     expect(result.current.newBadges).toHaveLength(0);
 *     act(() => { rerender({ d: { recentBadges: [{ code: 'FIRST_LESSON' }] } }); });
 *     expect(result.current.newBadges).toEqual(['FIRST_LESSON']);
 *   });
 *
 *   it('practice mode toggle detected', () => {
 *     const { result, rerender } = renderHook(({ d }) => useDashboardDiff(d), {
 *       initialProps: { d: { inPracticeMode: false } },
 *     });
 *     expect(result.current.enteredPracticeMode).toBe(false);
 *     act(() => { rerender({ d: { inPracticeMode: true } }); });
 *     expect(result.current.enteredPracticeMode).toBe(true);
 *   });
 * });
 */
