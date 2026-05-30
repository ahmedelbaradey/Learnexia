# Review — Wave 11 FE (P2-02-FE + P2-03-FE) Browse Subjects & Navigate Skill Tree

**Reviewer:** Claude Sonnet 4.6 (reviewer agent)
**Date:** 2026-05-30
**Branch:** `feat/W11-P2-02-P2-03-FE`

---

## VERDICT: PASS (with required fixes)

All acceptance criteria have code paths. All builds and type-checks pass. Three blocker-level issues require fixes before committing: two raw-hex violations in `WhyLockedSheet` (a CONVENTIONS.md non-negotiable) and one logical-prop violation in `LessonCard`. The remaining findings are should-fix or nit.

---

## Build / test results

| Check | Result |
|---|---|
| `pnpm --filter @learnexia/api-client type-check` | PASS (no output, clean exit) |
| `pnpm --filter @learnexia/ui type-check` | PASS (no output, clean exit) |
| `pnpm --filter @learnexia/shared type-check` | PASS (no output, clean exit) |
| `pnpm --filter student-app type-check` | PASS (no output, clean exit) |
| `pnpm --filter student-app lint` | PASS (no output, clean exit) |
| `dotnet build backend/Learnexia.Modular.sln` | PASS — 7 MSB3277 warnings (pre-existing EF version conflict, not introduced by this wave), 0 errors |

---

## Per-check results

### Check 1 — CONVENTIONS: BaseResponse, tokens, logical RTL, module isolation, ILoggerManager

**BaseResponse / `Successed`:** The integration test at line 217-220 of `P1_09_Me_Tests.cs` explicitly asserts `"successed"` (lowercase camelCase) in the envelope. `GetMeQueryHandler.cs` uses `Success(response)` via `BaseResponseHandler` — correct. `SubjectsController.cs` uses `NewResult(...)` via `AppControllerBase` — correct. `Successed` spelling preserved throughout.

**Module isolation:** `SubjectsController.cs` uses `using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos` — all references are within the Learning module's own projects. No cross-module project references introduced.

**ILoggerManager:** `GetMeQueryHandler.cs` line 22 injects `ILoggerManager _logger` and uses `_logger.LogError(...)` at line 90. Correct — no `ILogger<T>` introduced.

**Tokens:** Reviewed all new FE files. App screen files (`index.tsx`, `_layout.tsx`, `subjects/.../index.tsx`, `subjects/.../tree.tsx`) use exclusively `$`-prefixed token references. `packages/design-system/src/tokens/colors.ts` has the new `subjectMath/Science/Arabic/English` tokens and the three glow shadow tokens properly added.

**Logical RTL flex:** All `XStack`/`YStack` uses `flexDirection={isRtl ? 'row-reverse' : 'row'}`. `writingDirection={direction}` on user-language `Text`. `marginStart`/`marginEnd` not tested extensively but no `marginLeft`/`marginRight` found in new screen files. Progress bars wrapped in `direction: 'ltr'` override per brand law (SubjectRow).

**Design pattern check (rule #8):** `SegmentedTabs` is a plain Tamagui primitive composed from `XStack + Stack + Text` tokens. No Strategy/Factory/Decorator pattern. In-memory boss derivation in `tree.tsx` (loop + Set) is plain JavaScript — not a design pattern. No graph-layout library or Skia/Reanimated added for W11.

Result: **PARTIAL — see Findings 1, 2, 3 (blockers)**

---

### Check 2 — Rule #8: no unilateral design pattern

`SegmentedTabs` at `packages/ui/src/components/SegmentedTabs/index.tsx` is a thin horizontal-mode variant of the tab primitive. It composes `XStack + Stack + Text` from `../../internal/primitives`. No design pattern. Comment in file explicitly calls out "NOT a design pattern — it is an orientation variant that mirrors the existing Tabs shape. Per CLAUDE.md rule 8, mirroring existing shapes does not require approval." Verified correct.

Boss derivation in `tree.tsx`: `useMemo(() => { const set = new Set<number>(); for (const unit of units) { for (const lesson of unit.lessons ?? []) { if (lesson.isBoss && ...) set.add(lesson.skillId); } } return set; }, [lessonsQuery.data])` — plain in-memory join, no pattern.

Result: **PASS**

---

### Check 3 — AC traceability

**AC-02-FE-1 (Subject Selection list):** `(child)/index.tsx` reads `useMe().data?.grade`, calls `useSubjectsForGrade(grade)` with `enabled: gradeKnown`, filters defensively via `filterSubjects()` + `SUBJECT_NAME_MAP`, renders `SubjectRow` components. Grade caption rendered when `gradeKnown`. Sign-out preserved (line 80-128). Social Studies excluded by the `ALLOWED_KEYS` Set.

**AC-02-FE-2 (Lessons view):** `subjects/[subjectId]/index.tsx` sorts `units` by `sequenceOrder` via `bySequence()`, sorts `lessons` per unit, renders `LessonCard` with `state` from `lesson.state` (NOT `isLocked`), `isBoss={lesson.isBoss}`. Boss badge renders via `LessonCard`'s `isBoss` prop. State variants Active/Completed/Locked correct.

**AC-02-FE-3 (Empty state):** `allEmpty` check at line 72-74 renders "Coming soon" copy. 404 detection via `error.statusCode === 404` at line 63-69 renders "Subject not found" + Back action.

**AC-02-FE-4 (Tap behaviour):** Available/Completed cards fire `router.push('/(child)/lessons/' + lessonId)`. Locked cards call `onLockTap` which opens `WhyLockedSheet` — no navigation on lock tap.

**AC-02-FE-5 (Loading + error):** Loading: shimmer skeleton (3 units × 3 cards). Error: retry button. Both present.

**AC-03-FE-1 (Skill tree render):** `tree.tsx` renders `ConceptNodeDto[]` as concept section headers + `SkillTreeNode` per skill with `state` + `hasBoss` derived from `bossSkillIds` set. Four visual states implemented in `SkillTreeNode` via `getDiscStyle()`.

**AC-03-FE-2 (Tap behaviour):** Available/Completed node fires `router.push('/(child)/lessons/' + lessonIds[0])` with defensive `if (lessonIds.length > 0)`. Locked node fires `onLockTap` → `WhyLockedSheet`.

**AC-03-FE-3 (RTL/LTR):** All direction props threaded. `writingDirection={direction}` on Text. `flexDirection={isRtl ? 'row-reverse' : 'row'}` on XStacks. Connector strips stay centered (vertical, no x-flip).

**AC-03-FE-4 (Mastery % header):** `masteryPct = Math.round(completedSkills / totalSkills * 100)`. First in-progress concept index computed. Header renders `"{conceptName} · Unit {x} of {y} · Mastery {z}%"` using i18n keys.

**AC-03-FE-5 (Loading + error):** Loading skeleton (2 concept × 3 node discs). Error retry button present. 401 handled by existing auth guard (not bypassed).

Result: **PASS — all 10 ACs have code paths**

---

### Check 4 — Build / test

All green. See table above.

---

### Checks 5-10 — Feature behaviour spot checks

**Grade from useMe:** `meQuery.data?.grade ?? null` — correct. Defensive guard `grade >= 1 && grade <= 6` prevents out-of-range calls.

**4-subject defensive filter:** `SUBJECT_NAME_MAP` covers EN + AR names. `ALLOWED_KEYS` Set enforces exactly 4 subjects. De-duplication via `seen` Set. Ordering canonical (Math/Science/Arabic/English).

**Lessons tab state:** Uses `lesson.state` via `lessonStateValue()` mapping `NodeState._0/_1/_2` → `0/1/2`. Obsolete `isLocked` not used.

**MissingPrerequisites in WhyLockedSheet:** `prereq.prereqSkillName` and `cur`/`req` percentages rendered. No `prereqSkillId` or `prereqNodeId` in UI text — security light check passed.

**Boss derivation join:** `bossSkillIds` set built from `lessonsQuery.data` (same TanStack Query key = cached if Lessons tab was visited first, one HTTP request if cold). `hasBoss = bossSkillIds.has(skill.skillId ?? -1)` — defensive.

**i18n parity:** All new keys verified present in both `en` and `ar` objects in `packages/shared/src/i18n/resources.ts` (lines 381-436 EN, lines 797-852 AR). Eastern-Arabic numeral copy for AR locale: `"الوحدة {{current}} من {{total}}"`, `"الإتقان {{percent}}٪"` — `٪` glyph present.

**Lesson stub:** Accepts `lessonId` from params. Does not crash on unknown ids. Back button present (`router.back()`). AR + EN copy via `child.lessons.stub.*` i18n keys.

**Sign-out preserved:** `useSignOutAction` imported and wired at lines 80-127 of `(child)/index.tsx`.

---

## Findings

### Finding 1 — [blocker] Raw hex in `WhyLockedSheet.tsx` — CTA button colors

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/(child)/_components/WhyLockedSheet.tsx` lines 225-226

```
backgroundColor: pressed ? '#4338CA' : '#4F46E5',
```

Both `#4338CA` (press state) and `#4F46E5` (default) are raw hex. These must be token references. The design-system tokens are `$primary` (`#4F46E5`) and `$primaryPress` (`#4338CA`). The `Pressable` using React Native `style` prop does not accept Tamagui tokens directly, but the pattern used elsewhere in the codebase is to use `TamStack` with `onPress` for Tamagui-styled buttons. Use the existing `Button variant="primary"` from `@learnexia/ui` or replace with a `TamStack` using `backgroundColor="$primary" pressStyle={{ backgroundColor: '$primaryPress' }}`.

**Rule violated:** CONVENTIONS.md §5 "No raw hex anywhere — all colours are tokens from `@learnexia/design-system`."

---

### Finding 2 — [blocker] Raw hex in `WhyLockedSheet.tsx` — modal/sheet backgrounds

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/(child)/_components/WhyLockedSheet.tsx` lines 256, 271, 302, 309

```
backgroundColor: 'rgba(15,23,42,0.72)',  // overlay — should be $overlay
backgroundColor: '#1E293B',              // card — should be $card
backgroundColor: 'rgba(15,23,42,0.72)',  // overlay (native)
backgroundColor: '#1E293B',             // card (native)
```

These are raw hex/rgba values that have corresponding design-system tokens: `$overlay` = `rgba(15, 23, 42, 0.72)` and `$card` = `#1E293B`. The `Pressable` `style` prop does not resolve Tamagui tokens, but the container shells should be replaced with `TamStack` components using the tokens, or the values should be pulled from `colors` import from `@learnexia/design-system`.

**Rule violated:** CONVENTIONS.md §5 "No raw hex."

---

### Finding 3 — [blocker] Physical `left`/`right` props in `LessonCard` absolute-positioned overlays

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/packages/ui/src/components/LessonCard/index.tsx` lines 262-263, 277-278

```tsx
right={isRtl ? undefined : 14}
left={isRtl ? 14 : undefined}
```

The lock badge and boss badge use physical `left`/`right` props with conditional flipping. The correct pattern per CONVENTIONS / design spec §7 is to use logical `end` (and `start`) props. Tamagui supports logical positioning via `end` / `start` which auto-flip with RTL. The correct fix is:

```tsx
// Lock badge
end={14}
top={14}

// Boss badge (LessonCard)
end={14}
top={isLocked ? 38 : 14}
```

The design spec (§3.2) explicitly says: "positioned `top: 14, end: 14` (logical)." The implementation uses physical props instead.

**Rule violated:** Logical props convention (CLAUDE.md §7 RTL + theming, brief §7, design spec §7 "Logical flex. Every horizontally-oriented stack uses logical props... No `marginLeft`/`marginRight` — use `marginStart`/`marginEnd`"). The same rule extends to positioning.

---

### Finding 4 — [should-fix] `lockedItemName` declared in `WhyLockedSheetProps` but not consumed

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/(child)/_components/WhyLockedSheet.tsx` line 50

`lockedItemName?: string` is declared in the interface but the function only destructures `open, onClose, prerequisites`. The prop is accepted by both callers (Lessons tab line 279, Tree tab line 334) but silently dropped. The design spec (§1 Surface 5) says the sheet title should provide context ("Why is this locked?") — the item name could appear in the title or as a subtitle but it is not rendered at all. For now the title is generic `child.subjects.whyLocked.title` which the brief accepts for W11, but the prop interface is misleading. Either use it (show `lockedItemName` as a subtitle after the H3 title) or remove it from the interface to avoid confusion.

---

### Finding 5 — [should-fix] Raw hex in `SkillTreeNode` — disc background + glyph color

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/packages/ui/src/components/SkillTreeNode/index.tsx` lines 69-106

The `getDiscStyle` function returns hardcoded hex/rgba for disc backgrounds and `glyphColor`:

```
bg: 'rgba(239,68,68,0.55)'   // boss fallback — could be dangerSoft from tokens
bg: '#4F46E5'                // $primary
glyphColor: '#FFFFFF'        // could be $fg1 or just 'white'
shadowColor: 'rgba(99,102,241,0.6)' // close to shadowPrimaryGlowStrong now in tokens
bg: '#22C55E'                // $success
shadowColor: 'rgba(34,197,94,0.45)' // shadowSuccessGlow now in tokens
shadowColor: 'rgba(239,68,68,0.55)' // shadowDangerGlow now in tokens
```

The design spec §5.4 explicitly flagged these as design debt and noted "acceptable for W11 but logged as design debt" if the FE ships inline strings. Since the three glow shadow tokens (`shadowPrimaryGlowStrong`, `shadowSuccessGlow`, `shadowDangerGlow`) were added to `colors.ts` in this same wave, and `#4F46E5` = `$primary`, `#22C55E` = `$success`, the component should reference those tokens. However, because Tamagui's `backgroundColor` on `Stack` does not accept raw hex via the JSX prop without the `$` prefix token alias, and because the design spec explicitly accepted this as W11 debt, this is classified as **should-fix** rather than blocker. Fix in the next pass or alongside the shadow-token adoption.

---

### Finding 6 — [should-fix] `(child)/lessons/[lessonId].tsx` stub back arrow not RTL-aware

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/(child)/lessons/[lessonId].tsx` line 52

```
←
```

Hardcoded LTR `←` arrow. No RTL flip. The `useLocale` hook is imported (`direction`) but unused for the back chevron. The design spec §7 says back chevron should be `←` LTR, `→` RTL. The stub is a temporary placeholder (P2-05-FE replaces it in Wave 12) but it should still follow RTL conventions since it will be visible in AR-locale testing.

Fix: add `const backChevron = direction === 'rtl' ? '→' : '←'` and use `{backChevron}`.

---

### Finding 7 — [should-fix] Empty-state flash before `useMe` resolves

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/(child)/index.tsx` lines 86-93, 172

`meQuery.isLoading` is not included in the loading gate. When the app loads, `useMe` is still fetching, `grade` is null, `gradeKnown = false`, `subjectsQuery.enabled = false`, and `subjectsQuery.isLoading = false`. This means the empty state is shown briefly until `useMe` resolves. The brief explicitly calls this "graceful degradation" (§9 risk 1), but the UX is jarring: the student sees "Coming soon" for ~200ms before subjects appear.

Fix: gate the loading skeleton on `meQuery.isLoading || subjectsQuery.isLoading`. When `meQuery.isLoading` is true, show the 4-row shimmer skeleton regardless of `subjectsQuery.isLoading`.

---

### Finding 8 — [nit] `SubjectRow` mastery caption uses plain `%` not i18n key

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/packages/ui/src/components/SubjectRow/index.tsx` line 152

```tsx
{`${pct}%`}
```

The design spec §3.1 says the mastery caption should use `"{{percent}}% mastered"` / `"{{percent}}% إتقان"`. The component renders a plain `pct%` string instead of the full caption. The `masteryLabel` i18n key exists in resources (`child.subjects.masteryLabel`). However, since `SubjectRow` is a UI primitive in `@learnexia/ui` and not a screen component, passing a pre-localized caption string via a `masteryCaption` prop (caller provides the translated string) is the correct pattern for a primitive. The current implementation silently drops the "mastered" / "إتقان" suffix. The caller in `(child)/index.tsx` does not pass a caption — it only passes `masteryPercent` as undefined (no mastery data from the API at browse time). So in practice the caption block is hidden entirely (`hasMastery = false` since no `masteryPercent` prop passed). This is a nit — the component is correct for the current data, and `masteryPercent` is undefined in the subjects list (the API's `StudentSubjectDto` has no mastery field). However the prop contract could add a `masteryCaption?: string` for when a caller does have mastery data to render.

---

### Finding 9 — [nit] `WhyLockedSheet` close button uses physical `right: 0`/`left: 0` in Pressable style

**File:** `/home/ahmedelbaradeyahmedelbaradey/projects/learnexia/apps/student-app/app/(child)/_components/WhyLockedSheet.tsx` lines 86-90

```
...(isRtl ? { left: 0 } : { right: 0 }),
```

This is within a React Native `Pressable` `style` object (not Tamagui). React Native does not have logical `end`/`start` positioning in the same way as Tamagui. The conditional flip is functionally correct (right=0 in LTR, left=0 in RTL). For native React Native style objects, physical left/right with conditional flip is the accepted pattern — this is not a strict violation for the `Pressable` use case. Flag as a nit since the design spec says to use logical props where possible, but this is inside a RN Pressable where Tamagui logical props don't apply.

---

## Required fixes

Before the `committer` agent runs, the implementing agent must apply these three fixes:

**Fix 1:** Replace raw hex in `WhyLockedSheet.tsx` CTA button (lines 221-230). Use `<TamStack>` with `backgroundColor="$primary" pressStyle={{ backgroundColor: '$primaryPress' }}` instead of the `Pressable` with inline `#` colors. Or replace with `<Button variant="primary">` from `@learnexia/ui`.

**Fix 2:** Replace raw hex in `WhyLockedSheet.tsx` overlay/card backgrounds (lines 248-286, 290-336). The web Pressable overlay and the native Pressable overlay use `rgba(15,23,42,0.72)`. Replace with `colors.overlay` imported from `@learnexia/design-system/tokens/colors`. The inner modal/sheet containers use `'#1E293B'` — replace with `colors.card` (same import). Both values are in the exported `colors` object.

**Fix 3:** Replace physical `left`/`right` props in `LessonCard` absolute-positioned overlays (lines 262-263 lock badge, 277-278 boss badge) with logical `end` prop:

```tsx
// Lock badge
end={14}
// (remove left={...} and right={...})

// Boss badge
end={14}
// (remove left={...} and right={...})
```

Tamagui `Stack` supports `end` as a logical positioning prop that auto-flips with RTL direction.

---

## Non-blocking polish suggestions

1. **Fix 4 (should-fix):** Add `meQuery.isLoading` to the loading gate in `(child)/index.tsx`. Show shimmer while `meQuery` is still fetching to avoid the empty-state flash.

2. **Fix 5 (should-fix):** In `(child)/lessons/[lessonId].tsx` line 52, replace hardcoded `←` with `const backChevron = direction === 'rtl' ? '→' : '←'` for RTL parity.

3. **Fix 6 (should-fix):** Either use `lockedItemName` in `WhyLockedSheet` (e.g. as a subtitle: `color="$fg2" fontSize={14}`) or remove it from the prop interface to keep the contract clean.

4. **Design debt note (nit):** Track `SkillTreeNode` raw hex disc backgrounds and shadow colors as tech debt, to be resolved when the glow tokens are wired into the Tamagui config (they are currently in `colors.ts` but may not be in `tamagui.config.ts` tokens — verify if they need to be added to the Tamagui token config to be usable as `$shadowPrimaryGlowStrong` etc.).

---

## Security light check

- No student grade or `MissingPrerequisite` details rendered to unauthenticated views (auth guard routes students only after JWT validation).
- `WhyLockedSheet` renders only `prereqSkillName` + `currentAccuracy`/`requiredAccuracy` percentages. No `prereqSkillId`, `prereqNodeId` in UI text.
- Error states render localized i18n copy, not `ex.message` or stack traces.

Result: PASS

---

## Summary

The implementation is functionally complete and matches all acceptance criteria. All three typechecks and the backend build are green. Three blocker-level raw-color/logical-prop violations must be fixed before merge. Suggest the implementing agent apply fixes 1-3 (and optionally 4-6), then this wave can be committed.
