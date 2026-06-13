# Design Spec — A5 · Attempt history, BOTH surfaces (P2-09 / G5, reopened as CO-FE-7)

> Carryover plan `docs/plans/p1-p2-p3-carryover.md` batch **2d** (security-audited: IDOR on `studentId`).
> Plan **L2**: ships on BOTH surfaces — ① a child "My activity" screen in `app/(child)/` and ② a parent
> per-child attempt review **panel inside the Reports page** (NOT a new sidebar item — plan §3 forbids
> `(parent)/_layout.tsx`/nav edits; the kit capture's "Activity" nav item is deferred). One spec, two renders
> of the same row component. EN + AR/RTL, child-safe voice. No app code here.

## 0. Source-of-truth pairs

No dedicated capture exists for attempt history (post-kit feature — **flagged as a derived design**). It is
composed 100% from existing sanctioned pieces:

| Piece | Grounding |
|---|---|
| Row chrome | `preview/components-missions.html` row anatomy (icon tile + body + trailing stat) — the house list-row |
| Score/accuracy/time stat treatment | `packages/ui` `AttemptSummaryCard` (28/800 stats, `$success`/`$primary` accents, tabular-nums) + `preview/mobile-reward-stats.html` |
| Child screen shell | W12/W13 patterns (`design-system/ui_kits/student-mobile/W13-home-dashboard.md`) |
| Parent panel chrome | `preview/web-skills-mastery.html` panel (bg `$card`, radius 24, padding 22, title 16/800 + sub 12) on `screenshots/web/06-reports.png` composition |

## 1. Shared data & row model

- **Hook (2d builds, 2c reuses):** `useStudentAttempts(studentId)` in `packages/api-client` over
  `GET /api/Learning/Students/{studentId}/Attempts` → `AttemptListItemDto[]`
  `{ id, lessonId, status, accuracyPercentage, durationSeconds, hintsUsedCount, startedAt, completedAt }`.
  Child surface passes its own id (`useMe().data.id`); parent surface passes the active child's id
  (`activeChildStore`). **Authz is server-side** (owning student / linked parent only) — the FE must surface the
  403/404 failure as the generic error state, never retry-loop (security-auditor checks; e2e asserts the
  cross-student denial).
- **Metadata note:** the per-answer `attemptOrder`/`timeMs` fields live inside the Matching answer payload
  (C1 contract) and are NOT in this list DTO — the row shows attempt-level `accuracyPercentage`,
  `durationSeconds`, `startedAt`. Do not invent per-answer drill-down this wave.
- **Lesson name gap (G-1):** the DTO carries `lessonId` only. Render the localized fallback
  "Lesson {n}" / "الدرس {n}" — do NOT fan out N lesson GETs per row. Flag to backend: add `lessonName`
  to `AttemptListItemDto` (carry-forward; trivial join).

### AttemptRow (new in `packages/ui`, composes existing primitives — not a new pattern)

Anatomy (LTR; logical order, mirrors in RTL):

```
[status disc 40×40]  [Lesson 12          ]   [ 84% ]  [ 3m 20s ]
                     [Today · 14:05      ]
```

| Element | Spec |
|---|---|
| Row container | bg `$card`, border 1px `$borderSubtle`, radius `$card` 20, padding 14 16, gap 14, min-height 64 (≥48 target); list gap 10 |
| Status disc | 40×40 `$pill`. Completed → bg `$successSoft`, glyph ✓ `$success` 16/900. Abandoned/InProgress → bg `$cardSoft`, glyph ⏸-free: "…" `$fg4` (never red — non-shaming) |
| Title | lesson name/fallback, 15/700 `$fg1`, `$heading` family |
| Sub line | 12 `$fg3`: relative date + time — "Today · 14:05" / "اليوم · ١٤:٠٥", "Yesterday"/"أمس", else short date "12 Jun" / "١٢ يونيو" (Eastern-Arabic numerals in AR; time can stay numeric AR digits). From `startedAt`. |
| Score stat | trailing: `{accuracy}%` 16/800 tabular-nums, color by band: ≥80 `$success` · 50–79 `$xp` · <50 `$fg3` (NOT `$danger` — child-safe; the same row renders for the parent, keep identical) |
| Time stat | 13/700 `$fg3` tabular-nums `dir="ltr"`: "3m 20s" / "٣ د ٢٠ ث" from `durationSeconds` |
| Hints chip (optional, parent surface only) | when `hintsUsedCount > 0`: `$pill` chip bg `$warningSoft` fg `$warning` 11/700 "💡 2 hints" / "٢ تلميح" — hidden on the child surface (kids don't need hint-shame) |
| Press (child surface) | row is **non-interactive this wave** (no attempt-detail screen scoped). No chevron. |
| Hover (parent web) | brighten to `$cardSoft`, no scale (informational row) |

States per row-list: **loading** = 4 skeleton rows (`$cardSoft` 64px, opacity 0.6); **error** = W13 error strip
(`$dangerSoft` + ⚠️ + Retry ghost); **empty** = §2.3/§3.3 per surface.

## 2. Surface ① — Child "My activity" screen (`app/(child)/attempts`)

Push screen (NOT a tab — B0-nav §1; `href:null`). Entry: "My activity" link-row on the Home dashboard
(wired by batch **3c**, since only B-int edits `(child)/index.tsx`): a ghost section header
"My activity" / "نشاطي" with "See all →" / "عرض الكل ←".

Layout 390 (24px padding; ≥768 centered maxWidth 720, W13 precedent):
1. Header: back affordance (W12 `ScreenHeader` pattern) + H1 **"My activity"** / **"نشاطي"** 24/800 `$fg1`.
2. Encouragement sub-line 14 `$fg2`: **"Every try makes you stronger 💪"**-style is OFF-set (💪 not in the emoji
   set) → use **"Look how much you've done!"** / **"انظر كم أنجزت!"** (genuine-win exclamation allowed).
3. **Summary strip** (derived, optional but cheap): `$card` bar radius 16 padding 12, 3 cells per
   `preview/mobile-badge-stats-strip.html` chrome — value 20/900 tabular-nums + label 10/700 uppercase `$fg3`:
   Attempts (`$primaryLight`) · Completed (`$success`) · Best score (`$xp`). Computed from the fetched list.
4. **Attempt list** — `AttemptRow`s, newest first (sort by `startedAt` desc). Group headers optional:
   "This week" / "هذا الأسبوع", "Earlier" / "سابقًا" — 12/700 uppercase tracking 0.04em `$fg3`.
5. Pagination: render all (the endpoint is unpaged today); if >50, soft "Show more" ghost button (client slice).

Empty state: 📭-free — centered ⭐ 48px at 40% opacity + 16/700 `$fg1`
**"No adventures yet — start your first lesson!"** / **"لا مغامرات بعد — ابدأ أول درس!"** + primary Button
"Start learning" / "ابدأ التعلم" → router to Home. (One primary action.)
TabBar: hidden? No — visible (it's a child surface in the tab flow pushed over Home; keep the bar, content
bottom-padded per B0-nav §3).

## 3. Surface ② — Parent "Recent attempts" panel (inside `(parent)/reports.tsx`)

Rendered as the LAST section of the Reports page (A4 spec §1 composition). Batch 2d owns this panel file
(`(parent)/_components/RecentAttemptsPanel.tsx`); 2c places the slot.

1. Panel chrome = `web-skills-mastery.html` literal: bg `$card`, radius 24, padding 22, border `$borderSubtle`,
   shadow `$shadowSoft`.
2. Title 16/800 `$fg1` **"Recent attempts"** / **"المحاولات الأخيرة"**; sub 12 `$fg3`
   **"{Child}'s latest quizzes"** / **"آخر اختبارات {الاسم}"**.
3. Rows: `AttemptRow` (same component; `hintsUsedCount` chip ENABLED here), filtered by the page's date-range
   select (A4 §2.1) — the two sections share the one `useStudentAttempts` result. Show max 8 + ghost
   "Show all" expander.
4. Empty (in range): 13 `$fg3` centered "No attempts in this period" / "لا توجد محاولات في هذه الفترة".
   First-week page-level empty state (A4 §2.5) hides this panel entirely.
5. Wide ≥1024: panel is full-width under the 2-col row. Narrow: stacks like everything else.

## 4. RTL / i18n / a11y / security-UX

- RTL: rows are `rowDir`-driven; date prose uses Eastern-Arabic numerals; durations + percentages follow §1
  (duration `dir="ltr"`); arrows "→"↔"←"; Cairo/Tajawal per family rules.
- i18n namespace **`attempts.*`** (2d registers; hands keys to the Batch-2 merge owner per plan §3):
  `childTitle`, `childSub`, `summary.{attempts,completed,best}`, `row.{lessonFallback,today,yesterday,completed,inProgress,hints}`,
  `group.{thisWeek,earlier}`, `childEmpty.{title,cta}`, `parent.{title,sub,empty,showAll}`.
- A11y: each row `accessibilityRole="text"` with a composed label
  ("Lesson 12, completed today, 84 percent, 3 minutes 20 seconds"); summary strip labelled as a group; list
  announces count. Touch targets ≥48.
- Security-UX (audit support): the screen never embeds `studentId` from route params on the child surface
  (always `useMe()`); the parent surface only offers children from `useMyChildren()` via the shell switcher —
  no free-form id input anywhere. 403/404 → generic error state (no "this child exists but isn't yours" leak).

## 5. Design gaps / open questions

| # | Gap | Action |
|---|---|---|
| G-1 | `AttemptListItemDto` lacks `lessonName` | fallback "Lesson {n}"; backend carry-forward request |
| G-2 | No capture/preview card for attempt rows | derived from sanctioned cards (§0); add a preview card to the kit later |
| G-3 | Endpoint is unpaged | client-side "Show more"; flag if lists grow (Phase 5 paging) |
| G-4 | Child row tap → attempt detail | out of scope; confirm nobody expects drill-down this wave |
| G-5 | Placement decision | parent surface INSIDE Reports (no nav edit) — lead confirm at Gate-1 spec review; if a standalone Activity page is preferred later, the panel promotes 1:1 |

## 6. Implementation handoff

| Piece | Target |
|---|---|
| `useStudentAttempts(studentId)` | `packages/api-client/src/hooks/` (2d; endpoint already typed — no regen needed) |
| `AttemptRow` | `packages/ui/src/components/AttemptRow/` (composes existing primitives) |
| Child screen | `apps/student-app/app/(child)/attempts.tsx` (NEW file; does NOT touch `_layout.tsx` — 3c registers `href:null` + Home entry link) |
| Parent panel | `apps/student-app/app/(parent)/_components/RecentAttemptsPanel.tsx`, slotted by A4 |
| i18n `attempts.*` | `packages/shared/src/i18n/resources.ts` via Batch-2 merge owner |
| e2e | cross-student denial + RTL render (Wave D, plan 4a) |

Design spec ready for frontend.
