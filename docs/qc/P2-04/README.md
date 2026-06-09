# QC Test Plan & Coverage Report — P2-04 (Learning Path Unlock Engine)

**Story:** `user-stories/Phase-2-Learning-Core/P2-04-unlock-rules-learning-path-engine.md` — "Unlock lessons by prerequisite/mastery rules"
**Brief:** `docs/briefs/P2-04.md` · **Plan:** `docs/plans/P2-04.md` · **Task:** `tasks/Backend/Phase-2-Learning-Core/P2-04-BE.md`
**Pass scope:** **Backend-only.** No `frontend-test-cases.md` (P2-04 has no UI surface — task file: "Backend/infra only").
**Designed:** 2026-06-08 · **Designer:** qc-test-designer (Opus)

---

## 1. Summary

P2-04 is a deterministic **Learning Path Engine** that derives per-student lesson/skill lock state from the P2-11 prerequisite graph (`KnowledgeNode`/`KnowledgeEdge`) and the P2-08 mastery signal. The engine itself is **pure domain logic** (`LearningPathEngine`, static, no HTTP). Its **observable API surface** is the two authenticated read endpoints that now carry engine-derived `NodeState` + missing-prereq explanations:

- `GET /api/learning/Subjects/{id}/SkillTree` → per-skill `state` + `missingPrerequisites`
- `GET /api/learning/Subjects/{id}/Lessons` → per-lesson `state` + `missingPrerequisites`

**Scope of this pass:** the unlock state surfaced in those two endpoints (root-unlocked / dependent-locked / mastery-flips / missing-prereq explanation / determinism / auth / IDOR / error mapping), plus the **cross-language guard (P8)** that shares the same lesson-start surface, plus a **documented-gap** case for the start-a-locked-lesson path.

**Counts:**
- **Total cases: 22** (all backend) + **5 deferred-to-unit** notes.
- By priority: **P0 = 9** · **P1 = 10** · **P2 = 3**.
- By group: Auth/authz 3 · Fresh-student lock derivation 4 · Mastery-flip & rule edges 3 · Missing-prereq explanation 4 · Completed-separation & gap 2 · Cross-language guard 2 · Determinism & error-mapping 4.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

| Acceptance criterion (story / brief) | Covered by | Status |
|---|---|---|
| **AC1** — Engine determines lock/unlock from prereq + mastery rules (deterministic, not AI) | BE-TC-03, BE-TC-04, BE-TC-05, BE-TC-06, BE-TC-09, BE-TC-10, BE-TC-18, BE-TC-19 + unit U-1/U-2/U-4 | **Covered** |
| **AC2** — Complete + meet mastery threshold → dependent lessons unlock | BE-TC-08 (positive flip); BE-TC-13, BE-TC-15 (negative: below-threshold / completion-without-mastery stay locked) | **Covered** |
| **AC3** — Unmet prereqs → stays locked + explains which prerequisite is missing | BE-TC-05, BE-TC-07, BE-TC-11, BE-TC-12, BE-TC-13, BE-TC-14 | **Covered** |
| **AC4** — Unlock decisions reproducible for same inputs | BE-TC-19 (HTTP-level) + unit U-4 (authoritative pure-determinism) | **Covered** |
| Root/entry nodes unlocked for a fresh student (story: "always have a clear next step") | BE-TC-04, BE-TC-10 | **Covered** |
| Partial progress (attempted, below mastery) → still locked | BE-TC-13, BE-TC-15 | **Covered** |
| Cross-language guard (P8) on same surface | BE-TC-17 (403 on wrong-lang start), BE-TC-18 (skill-tree redirect) | **Covered** |
| Auth tightening (Q7 — endpoints now `[Authorize]`) | BE-TC-01, BE-TC-02 | **Covered** |
| IDOR / cross-student isolation (studentId from JWT only) | BE-TC-03 | **Covered** |
| Status-code mapping (404 not 500; empty 200; envelope) | BE-TC-20, BE-TC-21, BE-TC-22 | **Covered** |
| Engine rule completeness — null-skill lesson / no-prereq skill | BE-TC-09, BE-TC-10 | **Covered** |

**Gap flagged (not an AC):** the task hypothesized a guard that *rejects starting a locked lesson* with 403/424. **No such guard exists in code.** This is captured as **BE-TC-16** (documents current 200 behavior) and as Open Question OQ-1 below. No P2-04 acceptance criterion is left uncovered.

---

## 3. Risk notes (where the weight went and why)

1. **The "start a locked lesson is rejected" surface does not exist.** Highest-signal finding. `QuizzesController.StartAttempt` enforces only auth, lesson-exists (404), and the **language** guard (403) — **never** the unlock engine. The engine is purely advisory (read-side state); nothing on the write path consumes it. P0/P1 weight therefore sits on the **read endpoints** (the real surface) and BE-TC-16 exists only to *document* the gap, not to validate a non-existent feature. If product intends locked lessons to be unstartable, that is new backend work (a guard in `StartAttemptCommandHandler`), not a test failure.
2. **Mastery-flip is the core differentiator (AC2)** and the riskiest stateful path — it requires seeding real attempts through the write pipeline and re-reading. Weighted P0 (BE-TC-08) with negative counterparts (BE-TC-13 below-threshold, BE-TC-15 completion-without-mastery) to pin the exact boundary between "completed" and "mastered."
3. **IDOR / per-student determinism** — engine derives `studentId` from JWT only. BE-TC-03 (two JWTs, divergent state) is the load-bearing isolation check; cheap to get wrong if a future refactor accepts a client-supplied id.
4. **Immediate-vs-transitive explanation (Q9)** — easy to over-report. BE-TC-07 specifically asserts a two-hop locked skill lists only its *immediate* prereq, not the transitive ancestor.
5. **Cross-language guard (P8) confounds unlock tests** — a wrong-language start returns 403 for *language* reasons, which could be mistaken for an unlock rejection. BE-TC-16 explicitly requires a **language-matched** locked lesson so the 403 (if any) cannot be attributed to language. BE-TC-17/18 isolate the language behavior itself.
6. **Cycle defense and exact-threshold boundary are unreachable over HTTP** (seed-time validation blocks cycles; HTTP accuracy is a coarse integer fraction). Correctly deferred to unit tests (U-1, U-2) — not faked over the API.

---

## 4. Open questions / assumptions (need lead decision before/with implementation)

- **OQ-1 (decision needed):** Should starting a **locked** lesson/quiz be rejected (403/424)? **Current code does not reject it** (BE-TC-16 documents 200). The task brief assumed a guard; the P2-04 plan/brief never specified one (no AC requires it). **Recommendation:** confirm with product. If "yes," it's a new `StartAttemptCommandHandler` guard (run the engine for the lesson's subject, reject if the lesson resolves to `Locked`) — out of scope for this QC pass, would add an integration case. If "no," BE-TC-16 stands as documentation of intended behavior.
- **OQ-2 (assumption):** Mastery threshold semantics = `AccuracyPercentage >= Skill.MasteryThreshold AND TotalAnswers >= 1` (Q2, lead-resolved). Cases BE-TC-08/13/15 assume seeded thresholds (70/75) and the `>=` boundary. Confirmed against `LearningSeeder` + plan.
- **OQ-3 (assumption):** Skill names are stable lookup keys (HANDOFF: "do not rename skill name strings"). Cases resolve `subjectId`/`skillId`/`lessonId` by **name lookup at runtime**, not hardcoded ints. If the seeder renames skills, the fixture-resolution helper breaks — flag to lead.
- **OQ-4 (fixture availability):** BE-TC-21 (empty-subject) assumes a subject with no concepts exists or can be seeded. If every seeded subject has concepts, mark BE-TC-21 **blocked — no fixture** rather than forcing it (noted inline in the case).
- **OQ-5 (assumption):** A wrong-language Subject for the same `SubjectCode`+Grade exists in the seed for BE-TC-17/18. Validated by P8-03 design; if absent for the chosen student's language, mark those cases **blocked — no cross-language fixture**.

---

## 5. Handoff

| File | Consumer | Action |
|---|---|---|
| `docs/qc/P2-04/backend-test-cases.md` | **`api-tester`** | Implement BE-TC-01..22 as integration tests (mirror `P2_11_KnowledgeGraph_Tests.cs` / `P2_09_HomeDashboard_Tests.cs`; Testcontainers Postgres + Student JWT). Treat **BE-TC-16** as a behavior-documenting case (assert actual status, flag the gap). Note U-1..U-5 as already covered by `LearningPathEngineTests` — do not re-implement over HTTP. |
| `docs/qc/P2-04/execution-report.md` | **`api-tester`** | Fill in pass/fail per case + defects after running. This QC pass scaffolds the empty template only — qc-test-designer never fills results. |
| `frontend-test-cases.md` | — | **Not produced.** No UI surface in P2-04. |

**Note:** P2-04 is already marked ✅ Done in the task file (audited 2026-06-07). This pass provides a deliberate, traceable regression catalog over the merged engine + a documented gap on the start-guard. No feature edits are made by QC.
