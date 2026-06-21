# Pipeline Brief — P4-12 Timed-event participation progress (FRONTEND)

## Summary & traceability
- **Task (1 line):** Extend the existing P4-11 timed-event surface in the student app so each child sees *their own* progress toward a live timed event's goal, a join-by-playing empty state before they contribute, and a completion celebration when they finish — consuming the new JWT-scoped participation read endpoint.
- **User story:** `user-stories/Phase-3-Gamification/P4-12-timed-event-participation.md` (Track my progress in a limited-time event). FE slice only — BE/eligibility/lifecycle events are out of scope here.
- **FE task file:** `tasks/Frontend/student-app/Phase-3-Gamification/P4-12-FE.md` (FE-1..FE-4).
- **FR / requirement:** FR-GM-9 (timed events — SRS §4.6). **Extends P4-11.** Pairs with P9-12 (nudges — backend/Notifications, not this story).
- **BRD goal:** G3 (engagement / habit-forming gamification — story labels `gamification`, `habit`).
- **Epic / phase:** Gamification Module · Phase 3 — Gamification (Week 5). **Story Points: 2 (FE slice).** SMALL — extends, does not build a new screen.
- **Backend dependency:** P4-12-BE-8 (#201) — shipped. Endpoint **verified against source of truth** (controller + query handler + service + Shared.Contracts DTO).

## Business context & value
- **Who benefits:** the **student (child)** primarily — a timed event becomes a *completable challenge* with visible progress and a payoff, not a passive background XP multiplier. Secondary: the platform's engagement loop (G3) and the future P9-12 nudge recipient model (backend already emits the lifecycle events; FE just renders the read state).
- **Value:** turns P4-11's platform-wide multiplier banner into a per-child goal with a clear "I'm in / I'm close / I finished" arc, reinforcing the daily-return habit.
- **Success measure (from story):** a child can see who's in (join state), how close they are (progress vs target), and that they finished (completion + reward celebration). FE owns the kid-facing surface; BE owns participation/eligibility/events.

## Acceptance criteria (FE, testable)
Mapped to FE-1..FE-4 per the task file's coverage table.
- **AC-1 (FE-1):** A TanStack Query v5 hook exposes the current student's active timed-event participation snapshots from `GET /api/Gamification/TimedEventParticipations/Me`, returning `TimedEventParticipationSnapshot[]` (empty array when not yet participating). [api-client]
- **AC-2 (FE-2):** The timed-event surface shows, per active event, a **progress bar + numeric progress vs target** and a **completion state** (joined/in-progress → completed), rendered alongside the existing P4-11 countdown. Three visible states: **(a) not-yet-joined** (no participation row → "join by playing"), **(b) in-progress** (progress/target bar), **(c) completed** (full bar + completed treatment).
- **AC-3 (FE-2):** Progress is joined to the event by `Code` so the localized event name/multiplier (from the dashboard's `ActiveTimedEventDto`) and the participation progress (from the new endpoint) render on the **same** card — no duplicate, no orphaned progress with no name.
- **AC-4 (FE-3):** On a participation transitioning to **Completed** (observed via query refresh diff), a **completion celebration** fires, reusing the P4-08 `RewardPopup` motion system; honors reduced motion (the popup degrades gracefully, consistent with badge/level-up).
- **AC-5 (FE-4):** ar + en copy with correct **RTL** layout for all new labels (progress, remaining-to-target, completed); a clear **not-yet-participating empty/join state**; counts use Eastern-Arabic numerals in ar / Latin in en per the screen's existing `formatNumber`, while the XP multiplier stays Latin+LTR (XP-counter exception already established on this screen).
- **AC-6 (lifecycle hygiene):** Progress never renders for an event whose window has closed (the screen already filters `endUtc <= now`); a completed participation stays completed and does not regress.

## Affected modules & data
- **Module:** Gamification (FE student-app surface) + `@learnexia/api-client` (new hook) + `@learnexia/shared` i18n.
- **No new backend entities.** The participation entity (`TimedEventParticipation`), the read endpoint, and the cross-module seam already shipped in P4-12-BE. This story consumes them.
- **New FE artifacts:**
  - `packages/api-client/src/gamification/timed-events.ts` — the participation hook (new file per the task; the api-client has **no** `gamification/` dir yet).
  - Regenerated/added generated types for `TimedEventParticipationSnapshot` + `TimedEventParticipationStatusDto` (see Open Question O-1 — they are currently **absent** from the api-client's `swagger.json` and `generated/nswag-client.ts`).
  - New i18n keys under `events.timed.*` in `packages/shared/src/i18n/resources.ts` (ar + en).
- **Existing FE reused (do NOT replace):**
  - `apps/student-app/app/(child)/events.tsx` — **this is where the P4-11 timed-event UI actually lives**, as the inline `TimedEventBanner` component (gradient banner + minute-tick countdown), sourced from `useDashboard().activeTimedEvents` (`ActiveTimedEventDto`). **There is no `components/gamification/TimedEventCard.tsx` file** — the task's file path is a planning placeholder; the real extension point is `TimedEventBanner` / the `events-timed` section of `events.tsx`.
  - `apps/student-app/app/(child)/index.tsx` — Home shows the first active event as a banner that pushes to `/(child)/events`; the progress detail belongs on the events screen, not Home (Home banner can stay as-is). See Open Question O-3.
  - `@learnexia/ui` `RewardPopup` (variants `xp | badge-unlock | level-up`) + `useDashboardDiff` celebration-queue pattern from `index.tsx` — reuse for the completion celebration (P4-08 motion).

## Verified backend contract (source-of-truth read)
- **Route:** `GET /api/Gamification/TimedEventParticipations/Me` — `[Authorize]`, student resolved from JWT only (no id param; IDOR-proof by construction).
  - Controller: `backend/src/Modules/Gamification/Learnexia.Modules.Gamification.Api/Controllers/TimedEventParticipationsController.cs`
  - Handler: `.../Application/Features/TimedEvents/Queries/GetMyTimedEventParticipations/GetMyTimedEventParticipationsQueryHandler.cs`
- **Response envelope:** `BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>` (success flag `Successed`; unwrap to the array as `useMyMissions` does via `unwrapEnvelope`).
- **DTO** (`backend/src/Shared/Learnexia.Shared.Contracts/Gamification/IStudentTimedEventParticipationQuery.cs`):
  - `TimedEventId: int` (opaque FK)
  - `Code: string` (stable event code, e.g. `"DOUBLE_XP_WEEKEND"`) — **the join key to `ActiveTimedEventDto.code`**
  - `Progress: int` (in-window qualifying-action count)
  - `Target: int` (denormalized at participation creation)
  - `Status: TimedEventParticipationStatusDto` — `NotStarted=0 | InProgress=1 | Completed=2`
  - `JoinedUtc: DateTime`, `CompletedUtc: DateTime?`, `EventEndUtc: DateTime`
- **Not-yet-participating representation:** participation is created **lazily on first qualifying contribution**, so for an active event the child hasn't contributed to **there is NO row** — the endpoint returns an **empty array** (handler/seam doc: "empty array for students who have not yet joined"). The FE derives the **join-by-playing** state by: event present in `useDashboard().activeTimedEvents` but **no matching `Code`** in the participation list. (Note: `Status = NotStarted` can also exist transiently per the domain enum, but the common pre-contribution case is simply *no row*.)
- **No name, no reward in the snapshot:** the DTO carries only `Code` — **no localized name and no reward amount.** Localized name/multiplier come from `ActiveTimedEventDto` (dashboard). The reward is granted server-side through the XP/badge engine (no parallel path); the FE celebration is a **visual reaction to the Completed transition**, not a reward fetch.
- **Client-generation gap:** `packages/api-client/swagger.json` and `src/generated/nswag-client.ts` **do not contain** this endpoint or `TimedEventParticipationSnapshot` yet (verified: 0 matches). The api-client must be **regenerated against the live backend** (which exposes the route) before/as part of FE-1, or the types hand-added. `ActiveTimedEventDto` IS already generated (`{code, nameEn, nameAr, multiplier, endUtc}`).

## Handoff → db-migration
- **None.** No schema work in this story — the participation table + read path shipped in P4-12-BE. Skip this agent.

## Handoff → backend-feature
- **None expected.** Endpoint, handler, service, and Shared.Contracts seam are present and verified. *Only* re-engage backend if live verification (Open Question O-1) shows the route missing from the running build or the swagger can't be regenerated — flag to lead, don't patch BE silently.

## Handoff → frontend
- **FE-1 (api-client hook):** New file `packages/api-client/src/gamification/timed-events.ts`.
  - Mirror `useMyMissions.ts`: `useTypedClient()` + `unwrapEnvelope(client.<op>())`, TanStack Query v5, a new `queryKeys.gamification.timedEventParticipations()` key.
  - Returns `TimedEventParticipationSnapshot[]`. Export from `packages/api-client/src/hooks/index.ts` (and barrel) following existing export style.
  - **Pre-req:** regenerate the api-client against live backend so `client.timedEventParticipationsMe()` (or the pinned `/Me` operation id) + the DTO types exist. Confirm the generated operation id with the Host NSwag `CustomOperationIds` `/Me` convention (HANDOFF "Durable NSwag /Me fix").
- **FE-2 (progress + completion on the card):** Extend the `events-timed` section of `apps/student-app/app/(child)/events.tsx` (the `TimedEventBanner`).
  - Call the new hook in `EventsScreen`; build a `Map<code, snapshot>`.
  - For each `visibleEvents` banner, look up its snapshot by `event.code`:
    - **no snapshot** → render the join-by-playing sub-state (encouraging "play to join" copy; no bar, or a zeroed/ghost bar).
    - **snapshot InProgress** → progress bar (reuse the existing `WeeklyChallengeCard` bar geometry: LTR-locked `flexDirection="row"`, `gradLevelup` fill, `CHALLENGE_BAR_HEIGHT`) + "progress of target" label.
    - **snapshot Completed** → solid `$success` full bar + completed treatment (mirror `WeeklyChallengeCard` completed chrome).
  - Keep the existing minute-tick countdown intact; progress is additive to the banner, not a replacement.
- **FE-3 (completion celebration):** Reuse `RewardPopup` from `@learnexia/ui` and the `useDashboardDiff`-style queue pattern from `index.tsx`.
  - Detect a participation `Status` transition to `Completed` across query refreshes (diff previous vs current snapshot list by `Code`). Fire once per completion (de-dupe like the dashboard celebration queue).
  - Use `variant="xp"` (reward is XP-engine-granted; the snapshot has no reward amount — if no amount is available, present a non-numeric "event complete" celebration; see Open Question O-2). Honor reduced motion via the popup's built-in degradation.
- **FE-4 (i18n + RTL + empty state):** Add `events.timed.*` keys (progress label, remaining-to-target, completed, join-by-playing/empty, completion-celebration title/subtitle/a11y) to `packages/shared/src/i18n/resources.ts` for **both** ar and en.
  - Follow the screen's existing numeral rules: `formatNumber` (Eastern-Arabic in ar) for progress/target prose; multiplier stays `formatXp`/Latin+LTR. Set `writingDirection={direction}` on text and use `rowDir` for rows, consistent with the existing banner.
- **API shapes the FE consumes:**
  - `GET /api/Gamification/TimedEventParticipations/Me` → `TimedEventParticipationSnapshot[]` (fields above).
  - `useDashboard().activeTimedEvents` → `ActiveTimedEventDto[] {code, nameEn, nameAr, multiplier, endUtc}` (existing; the name/multiplier source).

## Designer stage — RECOMMENDATION: SKIP
- This story **extends an existing card** rather than introducing a new screen/surface, and every visual primitive it needs already exists in the screen and the kit:
  - Progress bar + completed treatment: copy the `WeeklyChallengeCard` bar (same screen, P4-08 Design Spec §8.3 chrome).
  - Empty/join state: the screen's existing `EmptyCard` pattern (P4-08 §8 states).
  - Completion celebration: `RewardPopup` (P4-08 Design Spec §4.8) — already the canonical celebration.
- A standalone Design Spec adds little beyond pointing at P4-08 §8.2/§8.3/§4.8. **Recommend skipping `designer`** and instead noting in the plan: "reuse P4-08 Design Spec §8.2 (timed banner), §8.3 (progress-bar/completed chrome), §4.8 (RewardPopup)."
- Re-engage `designer` only if the lead wants a bespoke join-by-playing visual distinct from the existing `EmptyCard`, or a timed-event-specific celebration distinct from `RewardPopup`.

## Open questions / assumptions / risks
- **O-1 (verify before FE-1):** I confirmed the endpoint from source (controller/handler/DTO) but **could not curl the running backend** (no shell in this analysis; WebFetch is public-URL only). The api-tester / frontend agent should hit `GET http://localhost:5080/api/Gamification/TimedEventParticipations/Me` with a student JWT (register parent+child via API) to confirm the live route, envelope, and the **empty-array** not-yet-participating response, and to drive the api-client regeneration. **Risk:** the api-client `swagger.json`/generated client currently lack this endpoint+DTO (verified 0 matches) — regeneration against live backend is a hard pre-req for FE-1.
- **O-2 (reward amount for the celebration):** The participation snapshot has **no reward field**, and `ActiveTimedEventDto` carries `multiplier` but no fixed reward XP. So the completion celebration **cannot show a precise XP number** from these contracts. **Question for lead:** is a non-numeric "event complete!" celebration acceptable, or should BE expose the granted reward on the snapshot (BE change) / should the FE read the XP delta from the dashboard diff at completion time? **Assumption pending answer:** non-numeric celebration via `RewardPopup variant="xp"` with a generic title.
- **O-3 (Home surface scope):** Home (`(child)/index.tsx`) shows a timed-event banner that deep-links to `/(child)/events`. The task scopes progress to the card on the events screen. **Assumption:** Home banner stays as-is (no progress on Home); progress/completion live only on the events screen. Confirm if the lead wants a progress hint on the Home banner too.
- **O-4 ("qualifying action" / target source):** The story Notes flag confirming what counts as a qualifying action and the per-event target source — but this is **resolved on the backend** (`Target` is denormalized into the snapshot; `Progress` is accrued in `XpService`). No FE action needed; the FE just renders the values. Noting for traceability.
- **O-5 (completion-detection timing):** The Completed transition is detected via query refetch diff, so the celebration fires on the next dashboard/participation refresh after the contributing action, not instantly at the moment of contribution. **Assumption:** acceptable (mirrors how the dashboard celebration queue already works). Flag if the lead expects an immediate in-session celebration tied to the action that completed it.

## Recommended pipeline order (first cut — `planner` finalizes)
1. **(Verify)** api-tester or a quick live check confirms the running endpoint + drives api-client regeneration (O-1). Gate FE-1 on this.
2. **FE-1** (api-client hook) — depends on regenerated client/types. Touches `packages/api-client` (shared file — serialize per PARALLELISM if other stories touch it).
3. **FE-2** (progress + completion states on the events-screen banner) — depends on FE-1. The bulk of the work.
4. **FE-3** (completion celebration via `RewardPopup` diff) and **FE-4** (i18n + RTL + empty state) — both depend on FE-2; **can run in parallel** (FE-3 = motion wiring, FE-4 = copy/RTL/empty-state), they touch largely disjoint concerns (celebration logic vs i18n keys/labels).
5. **frontend-e2e-tester** — student-app UI surface: drive the events screen in ar+en/RTL, assert join-by-playing → in-progress → completed states, the completion celebration, and the empty state. (Seeding an active timed event + simulating a contribution may need a backend/admin seam — flag if the running env can't produce a participation row.)
6. **reviewer** gate against the AC above + CONVENTIONS.
7. **No `db-migration`, no `backend-feature`** unless O-1 surfaces a missing route. **`designer` skipped** (see recommendation). `security-auditor` not required (read-only, JWT-scoped, no PII, no new write path) — defer to lead.
