# Pipeline Brief — P8 Localization Foundation (P8-01, P8-02, P8-03)

> Analyzer output. Read-only brief; the rest of the pipeline (planner → db-migration → backend-feature → api-tester → security-auditor → reviewer) executes against this. Covers the **three foundation stories** of Phase 8 — **P8-01** (set child learning language), **P8-02** (bilingual parallel-tree curriculum), **P8-03** (serve curriculum in the learning language). **Excludes P8-04** (parent-only change-learning-language + Math/Science reset — a follow-up that builds on this foundation).
>
> Sources of truth: the three story `.md` files in `user-stories/Phase-8-Localization/`, the BE task files in `tasks/Backend/Phase-8-Localization/`, the design of record [docs/architecture/localization-architecture.md](../architecture/localization-architecture.md), and the verified codebase state below. Conventions: [CLAUDE.md](../../CLAUDE.md), [CONVENTIONS.md](../dev/CONVENTIONS.md), [FEATURE_PLAYBOOK.md](../dev/FEATURE_PLAYBOOK.md), [ADR 0001](../dev/adr/0001-unit-of-work.md), [ADR 0002](../dev/adr/0002-domain-events-and-dispatch.md).

---

## Summary & traceability

- **Task (1 line):** Give each child a **learning language** (`ar`/`en`) set by the parent at onboarding and carried on the JWT; store curriculum as **parallel language trees keyed on `Subject`** (6 roots per grade); and make every curriculum read query serve the tree whose language matches a **pure per-subject resolver** (`ARABIC→ar`, `ENGLISH→en`, `MATH`/`SCIENCE→learning language`).
- **User stories:** P8-01 (3 SP, Identity), P8-02 (5 SP, Learning, technical enabler), P8-03 (5 SP, Learning). Epic: **Localization**, Phase 8.
- **Requirements:** **NFR-5** (localization), **FR-LR-1** (localized curriculum content). Product decision: *Arabic-first, bilingual; 4 subjects (Math/Science/Arabic/English); no Social Studies; parent-driven onboarding; LearningLanguage immutable by student.*
- **Module split:** P8-01 = **Identity** (+ Parent onboarding wiring + `Shared.Contracts` seam). P8-02 + P8-03 = **Learning**. No cross-module FK; Learning reads the learning language from the **JWT claim**, exactly as it already reads the student id (`"Id"` claim) — confirmed below.

### Mapping each story's acceptance criteria → concrete code touch-points

**P8-01 — set a child's learning language**
| Acceptance criterion | Code touch-point (verified path) |
|---|---|
| Child carries `LearningLanguage` (`ar`/`en`), **separate** from `PreferredLanguage` | New property on `User` — `backend/src/Modules/Identity/Learnexia.Modules.Identity.Domain/Entities/User.cs` (sits beside `PreferredLanguage`, line 9) |
| EF config + non-null column with default for existing rows | `UserEntityConfig.cs` (mirror the `PreferredLanguage` mapping at lines 31–35 — `HasDefaultValue`, `HasMaxLength`, comment) + Identity migration |
| Parent sets it at onboarding; **required** | `AddChildCommand.cs` (add `LearningLanguage`), `AddChildCommandValidator.cs` (mirror the existing `Language` ar/en rule at lines 35–38), `CreateChildRequest` record in `Shared.Contracts/Identity/IChildAccountService.cs`, `IdentityChildAccountService.CreateChildAsync` (set the new field, line ~41–52) |
| Immutable by student (no student-facing mutation) | **No new write path** beyond add-child. `UpdateChildRequest` / `UpdateChildAsync` may optionally carry it (parent-only) but per P8-01-BE notes the *change* flow is **P8-04** — do not add a student endpoint |
| JWT claim `learning_language`, re-issued on refresh | `AuthenticationIdentityService.GetClaims` (add one claim, lines 190–218). Refresh re-issues automatically: `GetRefreshToken` → `GenerateJwtToken` → `GetClaims` (the same builder) |
| `GET /Me` returns `learningLanguage` | `MeResponse.cs` (add field) + `GetMeQueryHandler` (populate from `user.LearningLanguage`, line ~64–84) |
| UI `PreferredLanguage` defaults to match chosen `LearningLanguage`, independently editable | `IdentityChildAccountService.CreateChildAsync` — keep `PreferredLanguage = NormalizeLanguage(req.Language)` aligned to the chosen learning language at creation; later edits stay independent via existing `EditUserPreferredLanguage` |

**P8-02 — bilingual parallel trees**
| Acceptance criterion | Code touch-point |
|---|---|
| `Subject` carries `SubjectCode` + `Language` | `Subject.cs` (add two enum props), new enums under `Learning.Domain/Enums/` |
| Language only on `Subject`; children inherit | **No** language column on `Unit`/`Lesson`/`Concept`/`Skill`/`QuizQuestion` — they inherit via `SubjectId`/`UnitId`/`ConceptId` chain |
| Migration + index on `(GradeId, SubjectCode, Language)` | `SubjectConfig.cs` (currently only `IX_Subjects_GradeId`, lines 26–27) + Learning migration |
| Seeder authors 6 roots per grade | `LearningSeeder.cs` — `EnsureSubjectAsync` (lines 587–600) and the 4 `Seed*Async` callers (lines 76–221) |
| Existing single-language seed migrated/replaced, no orphan trees | Seeder backfill / data step — see Open Question Q1 (the central decision) |

**P8-03 — serve in learning language**
| Acceptance criterion | Code touch-point |
|---|---|
| Read `learning_language` from JWT (not query param) | `ICurrentUserService.GetClaimValue("learning_language")` — already exists on the interface (`Shared.Kernel/Abstractions/ICurrentUserService.cs`) and is implemented in `Learning.Infrastructure/Service/CurrentUserService.cs` (line 36–37). **No new plumbing needed in Learning** beyond a typed accessor (P8-03-BE-1) |
| Pure resolver `effectiveLanguage(SubjectCode, learningLanguage)` | New pure service under `Learning.Domain/Services/` (mirrors `LearningPathEngine`/`SkillGraphValidator` static-engine pattern) + unit tests |
| Filter all curriculum reads by `Subject.Language` | `GetSubjectsForGradeQueryHandler`, `GetSubjectSkillTreeQueryHandler`, `GetSubjectLessonsQueryHandler`, `GetLessonQueryHandler`, `StartAttemptCommandHandler`, `GetDashboardQueryHandler` (paths in §"Affected modules") |
| Edge-case matrix (ar-medium vs en-medium) | Integration tests (`Learnexia.IntegrationTests`) |
| Missing-tree fallback + warning log | resolver/handler fallback via `ILoggerManager.LogWarn` (pattern already used in `GetSubjectLessonsQueryHandler` line 112) |

---

## Business context & value

- **Who benefits:** the **student** (curriculum matches their school's medium of instruction from day one — Math/Science in the right language; Arabic always Arabic, English always English) and the **parent** (sets the right track at onboarding). Indirectly the **platform** (clean parallel-tree model avoids per-row translation debt).
- **Value:** Phase 8 is the localization spine. P8-01 captures the medium of instruction; P8-02 makes bilingual content *possible* without per-row translation columns; P8-03 makes the read path *serve* it correctly. Together they satisfy NFR-5 / FR-LR-1 and unlock the bilingual market.
- **Success measurement:** an Arabic-medium and an English-medium student in the **same grade** both see all four subjects, with **only Math/Science differing in language** and Arabic/English identical — verified by the P8-03 integration matrix.
- **No-AI / deterministic:** resolution is a pure function of `(SubjectCode, learning language)`. No AI on this path.

---

## Verified codebase state (2026-06-04)

| Concern | State (verified) | Evidence |
|---|---|---|
| `User.PreferredLanguage` | Exists, `"ar-EG"` default, mapped non-null max-len 10 | `User.cs:9`, `UserEntityConfig.cs:31-35` |
| `User.LearningLanguage` | **Does NOT exist** — to be added by P8-01 | `User.cs` (absent) |
| JWT claim builder | Single private `GetClaims(user, roles)`; emits `Id`, name, email, jti, SessionId, roles, permission claims. Used by **both** initial issuance and refresh (`GenerateJwtToken`) | `AuthenticationIdentityService.cs:190-218`, `:171-188`, `:71-94` |
| Add-child path | Lives in the **Parent** module; `AddChildCommand` already has a `Language` (`ar`/`en`) field (UI language) validated ar/en; calls Identity via `IChildAccountService` seam (`CreateChildRequest`) | `Parent/.../AddChild/*`, `Shared.Contracts/Identity/IChildAccountService.cs:43-50` |
| `Language` → `PreferredLanguage` mapping | `IdentityChildAccountService.NormalizeLanguage` maps `en→en-US`, `ar→ar-EG` | `IdentityChildAccountService.cs:46,146-151` |
| `/Me` | `GetMeQueryHandler` returns `MeResponse` (Id, Roles, FullName, PreferredLanguage, IsFirstLogin, HasChildren, Phone, Country, AvatarUrl, Grade) | `GetMe/GetMeQueryHandler.cs`, `MeResponse.cs` |
| `Subject` entity | `Name`, `Country?`, `GradeId`, `Grade`, `Units`, `Concepts`. **No `SubjectCode`, no `Language`** | `Subject.cs` |
| `SubjectConfig` | `Name` required, `Country` nullable, single index `IX_Subjects_GradeId` | `SubjectConfig.cs` |
| `LearningSeeder` | Seeds **4 single-language** subjects/grade via `EnsureSubjectAsync(name, gradeId)` (natural key = `Name`+`GradeId`); then skill graph (Math edges only), demo content, boss-lesson marking | `LearningSeeder.cs:33-47, 76-221, 587-600` |
| Learning enums | `DifficultyLevel`, `AttemptStatus`, `GeneratedBy`, `QuestionType`, `NodeState`, `EdgeRelationshipType`, `KnowledgeNodeType` — all `enum` stored as int via `HasConversion<int>` convention. **No `SubjectCode`/`ContentLanguage`** | `Learning.Domain/Enums/*` |
| Learning reads student from JWT | `Learning.Infrastructure/Service/CurrentUserService` reads `"Id"` claim; `GetClaimValue(type)` already on the interface — **same mechanism the `learning_language` claim will use** | `CurrentUserService.cs:23,36-37` |
| Read-path handlers reaching Subject | subjects-for-grade filters by `GradeId`; skill-tree/lessons/dashboard load by `SubjectId`; lesson/start-attempt load by `LessonId`/`UnitId` (no Subject filter at all today) | handler files in §Affected |
| Pure-engine precedent | `LearningPathEngine.ComputeStates(...)` + `SkillGraphValidator.AssertAcyclic(...)` are static pure services in `Learning.Domain/Services/` | referenced in handlers |
| Seeder skill graph | Math prereq edges authored within-subject, cross-grade (G1→G6), validated acyclic. **Today only ONE Math tree exists per grade** — duplicating to ar+en doubles the candidate-edge resolution surface | `LearningSeeder.cs:361-508` |

**Bottom line:** the JWT/claims plumbing and the `GetClaimValue` read seam already exist — P8-01 adds one field + one claim line; P8-03 adds a pure resolver + filters. The heaviest real work is **P8-02's seeder rewrite** (4 single-language roots → 6 language-tagged roots/grade) and **migrating existing seeded data** without orphan trees (Q1).

---

## Acceptance criteria (reviewer gates)

Consolidated from the three stories. Reviewer gates the foundation on:

1. **`User.LearningLanguage` exists**, non-null, separate from `PreferredLanguage`; existing rows get a sensible default (Q3); migration applies cleanly. *(P8-01)*
2. **Add-child requires `learningLanguage`** (`ar`/`en`), persists it, and defaults `PreferredLanguage` to match at creation (independently editable later). No student-facing mutation path added. *(P8-01)*
3. **JWT carries `learning_language`** for the student; **refresh re-issues it** (proven by decoding a refreshed token). *(P8-01)*
4. **`GET /Me` returns `learningLanguage`.** *(P8-01)*
5. **`Subject` carries `SubjectCode` + `Language`**, language only on `Subject` (no child-entity language column); unique index/constraint on `(GradeId, SubjectCode, Language)`; migration + index applied. *(P8-02)*
6. **Seeder authors exactly 6 roots/grade**: `MATH/ar`, `MATH/en`, `SCIENCE/ar`, `SCIENCE/en`, `ARABIC/ar`, `ENGLISH/en` — idempotent; **no orphan/untagged trees remain** after migration+seed. *(P8-02)*
7. **Pure resolver** `effectiveLanguage(SubjectCode, learningLanguage)`: `ARABIC→ar`, `ENGLISH→en`, `MATH`/`SCIENCE→learning language`; unit-tested for all four codes × both media. *(P8-03)*
8. **Every curriculum read** (subjects-for-grade, skill-tree, lessons-in-unit, lesson, quiz/start-attempt, dashboard) returns the tree whose `Subject.Language` matches the resolved language; language sourced from the **JWT claim**, never a query param. *(P8-03)*
9. **Edge-case matrix** verified by integration test: ar-medium → Math(ar), Science(ar), Arabic(ar), English(en); en-medium → Math(en), Science(en), Arabic(ar), English(en); **both see all 4 subjects**; Arabic/English identical for both. *(P8-03)*
10. **Missing-tree fallback**: if the resolved tree is absent, serve the other language tree and `LogWarn` (should not occur once seeded). *(P8-03)*
11. **Conventions honored** — module isolation (no cross-module FK to `User.LearningLanguage`; Learning reads the claim), `BaseResponse<T>`/`Successed`, `ILoggerManager`, deferred-commit/UoW per ADR 0001, enums stored as int.

---

## Affected modules & data (new vs existing)

| Surface | New? | Notes |
|---|---|---|
| `User.LearningLanguage` (Identity) | **New field** | `ar`/`en`. Recommend storing as a short string (`"ar"`/`"en"`) to keep the JWT claim trivial and mirror the existing `Language` convention on the add-child path — see Q5. Non-null + default. |
| Identity migration | **New** | identity schema; add `LearningLanguage` column with default for existing rows. |
| `CreateChildRequest` (`Shared.Contracts`) | **Modify** | add `LearningLanguage` param (the seam Parent→Identity uses). `UpdateChildRequest` optionally too (parent-only; the *change* flow itself is P8-04). |
| `AddChildCommand` / validator (Parent) | **Modify** | add `LearningLanguage`, required, `ar`/`en` rule (mirror existing `Language` rule lines 35–38). |
| `IdentityChildAccountService` | **Modify** | set `User.LearningLanguage`; keep `PreferredLanguage` aligned at creation. |
| `AuthenticationIdentityService.GetClaims` | **Modify** | add `new Claim("learning_language", user.LearningLanguage)`. One line; covers issuance + refresh. |
| `MeResponse` + `GetMeQueryHandler` | **Modify** | surface `learningLanguage`. |
| `SubjectCode` enum (Learning) | **New** | `MATH`/`SCIENCE`/`ARABIC`/`ENGLISH`, `Learning.Domain/Enums/SubjectCode.cs`. |
| `ContentLanguage` enum (Learning) | **New** | `ar`/`en`, `Learning.Domain/Enums/ContentLanguage.cs`. |
| `Subject.SubjectCode` + `Subject.Language` | **New fields** | enums stored as int (`HasConversion<int>` convention). |
| `SubjectConfig` + Learning migration | **Modify/New** | configure the two columns; add unique index `(GradeId, SubjectCode, Language)`. |
| `LearningSeeder` | **Modify** | 6 roots/grade; idempotency key changes from `(Name, GradeId)` → `(GradeId, SubjectCode, Language)`; skill-graph edges authored **per-language tree** (Q4). |
| Language resolver | **New** | `Learning.Domain/Services/SubjectLanguageResolver` (pure static). |
| Read-path handlers | **Modify** | `Features/Subjects/Queries/GetSubjectsForGrade/GetSubjectsForGradeQueryHandler.cs`, `.../GetSubjectSkillTree/GetSubjectSkillTreeQueryHandler.cs`, `.../GetSubjectLessons/GetSubjectLessonsQueryHandler.cs`, `Features/Lessons/Queries/Get/GetLessonQueryHandler.cs`, `Features/Attempts/Commands/StartAttempt/StartAttemptCommandHandler.cs`, `Features/Dashboard/Queries/GetDashboard/GetDashboardQueryHandler.cs`. |
| `StudentSubjectDto` / subject DTOs | **Maybe modify** | consider exposing `SubjectCode` so the FE can drive ordering/iconography (architecture §7 flags this as a likely FE need) — confirm with planner. |
| api-client Swagger snapshot | **Regenerate** | add-child + `/Me` contracts changed (P8-01-BE-6). |

---

## Handoff → db-migration

**Two independent migrations, in two schemas (no cross-module coupling).**

**A. Identity migration (P8-01-BE-2):**
- Add `LearningLanguage` to the identity `Users` table: **non-null**, with a default for existing rows (Q3 — recommend `"ar"`, matching the Arabic-first product default and the existing `PreferredLanguage` `"ar-EG"` default).
- Mirror the `PreferredLanguage` mapping in `UserEntityConfig.cs:31-35`: `HasMaxLength` (small — `2` if storing `"ar"`/`"en"`, or `10` if reusing culture codes), `HasDefaultValue`, `HasComment`.
- Identity is the one module that **does** auto-migrate at startup (per CONVENTIONS §13) — confirm with planner whether this migration runs at startup or is applied manually.

**B. Learning migration (P8-02-BE-3):**
- Add `SubjectCode` (int) and `Language` (int) columns to the learning `Subjects` table (enums stored as int via `HasConversion<int>`).
- Add the index on `(GradeId, SubjectCode, Language)`. **Recommend UNIQUE** (it is the new natural key per AC6) — confirm in Q1, because making it unique forces a decision about existing rows before the constraint can be created.
- **Existing-row backfill is the load-bearing decision (Q1).** Today's seeded `Subjects` have `Name` only and no `SubjectCode`/`Language`. A non-null column needs a default or a data step. Two options: (i) re-seed from scratch in dev (drop/replace) — viable since seeding is Development-only and idempotent; (ii) data migration that maps `Name → SubjectCode` and assigns `Language` (Math/Science→`ar` for the legacy single tree, Arabic→`ar`, English→`en`), then the seeder adds the missing `/en` Math/Science + `/ar`… roots. **Do not guess — see Q1.**
- No cross-module FK from Learning to `User.LearningLanguage` (rule 1). The migration touches **only** the learning schema.
- CONVENTIONS §13: non-Identity modules do **not** auto-migrate — apply the Learning migration manually.

---

## Handoff → backend-feature

**Identity / Parent (P8-01):**
1. Add `User.LearningLanguage` (Q5 type decision) + EF config (mirror `PreferredLanguage`).
2. Add `LearningLanguage` to `CreateChildRequest` (and optionally `UpdateChildRequest`) in `Shared.Contracts/Identity/IChildAccountService.cs`; thread it through `AddChildCommand` + validator (required, `ar`/`en` — mirror lines 35–38) → `AddChildCommandHandler` → `IdentityChildAccountService.CreateChildAsync` (set the field; keep `PreferredLanguage = NormalizeLanguage(...)` aligned at creation).
3. Add **one claim** in `AuthenticationIdentityService.GetClaims`: `new Claim("learning_language", user.LearningLanguage)`. Verify refresh re-issues it (it does — `GetRefreshToken`→`GenerateJwtToken`→`GetClaims`). Use a named constant for the claim type (don't sprinkle the string).
4. Surface `learningLanguage` on `MeResponse` + populate in `GetMeQueryHandler`.
5. Regenerate the api-client Swagger snapshot (add-child + `/Me`).
6. **Do NOT** add a student-facing endpoint to change `LearningLanguage` (parent-only change is P8-04).

**Learning (P8-02 — schema + seed):**
7. Add `SubjectCode` + `ContentLanguage` enums (`Learning.Domain/Enums/`), enum-as-int convention.
8. Add the two props to `Subject`; configure in `SubjectConfig` + the `(GradeId, SubjectCode, Language)` index.
9. Rewrite `LearningSeeder` to author **6 roots/grade**. Change `EnsureSubjectAsync` to key on `(GradeId, SubjectCode, Language)` and set both new fields. Math/Science get `ar` **and** `en` roots; Arabic→`ar`; English→`en`. Keep idempotent. The Math skill-graph edges must be authored **within each language tree** (no cross-language edges) — duplicate the Math edge set per language root (Q4).
10. Backfill/replace existing single-language data per Q1 so no orphan trees remain.
11. Unit tests: exactly the expected 6 roots/grade; `(GradeId, SubjectCode, Language)` unique.

**Learning (P8-03 — read path):**
12. Add a typed accessor for the learning language from the claim (P8-03-BE-1) — read `_currentUser.GetClaimValue("learning_language")`, parse to `ContentLanguage`, default/fallback if absent (Q6).
13. Add the **pure resolver** `SubjectLanguageResolver.Resolve(SubjectCode, learningLanguage) → ContentLanguage` (`ARABIC→ar`, `ENGLISH→en`, `MATH`/`SCIENCE→learningLanguage`) under `Learning.Domain/Services/` + unit tests (mirror `LearningPathEngine` static-pure pattern).
14. Apply in **subjects-for-grade**: instead of returning all `GradeId` matches, return **one tree per `SubjectCode`** at the resolved language (filter `Subject.Language == resolved`). This handler currently `OrderBy(s => s.Name)` and returns all subjects (line 52–55) — it will now return exactly 4.
15. Apply in **skill-tree**, **lessons-in-unit**, **lesson**, **quiz/start-attempt**, **dashboard**: each must constrain to the resolved-language `Subject` tree. Note `GetLessonQueryHandler`/`StartAttemptCommandHandler` load by `LessonId` and **do not currently touch Subject** — they need a Subject-language guard (resolve the lesson's owning `Subject` and verify/redirect to the matching-language tree). `GetDashboardQueryHandler` hard-codes a `FallbackSubjectOrder` of subject **names** and loads Grade-1 subjects (lines 49–51, 98–101) — this must become language-aware (filter to the resolved-language tree per code).
16. Missing-tree fallback: serve the other language tree + `LogWarn` (pattern: `GetSubjectLessonsQueryHandler.cs:112`).

**Conventions:** `BaseResponse<T>`/`Successed`; `ILoggerManager`; module isolation (Learning never FKs `User`); deferred commit + UoW per ADR 0001; queries not auto-validated (validate in-handler). **No new design pattern** is expected — the resolver is a pure static service mirroring the existing `LearningPathEngine`/`SkillGraphValidator` shape, which is an established pattern in this module, **not** a new abstraction (so CLAUDE.md rule #8 does not trigger — see §Design-pattern note).

---

## Handoff → api-tester (P8-03-BE-7)

Validate against the running API with two seeded students in the **same grade**:
- **Arabic-medium student** (JWT `learning_language=ar`): subjects-for-grade returns Math(ar), Science(ar), Arabic(ar), English(en) — all four codes, correct language each.
- **English-medium student** (JWT `learning_language=en`): Math(en), Science(en), Arabic(ar), English(en).
- **Both** see all four subjects; **Arabic and English subjects are byte-identical** for both students; **only Math/Science** differ in language.
- Drill down: skill-tree, lessons-in-unit, a lesson, start-attempt, and the dashboard each return content from the **resolved-language** tree for each student.
- Negative: a token **without** `learning_language` (e.g. legacy token) — confirm the fallback default behavior (Q6) rather than a 500.
- Refresh: refresh an access token and confirm the new token still carries `learning_language` (P8-01 AC).

**Security note:** P8-01 touches **child data + an auth claim** → P8-01-BE flags `security-auditor` before the gate. Auditor focus: the learning language cannot be set/changed by the student (no student write path); the claim is server-issued only; add-child remains family-scoped (no IDOR via the new field).

---

## Handoff → frontend

No FE tasks are in scope for these three BE stories. For the planner's awareness (architecture §7): the FE will eventually (a) add the learning-language picker to the parent onboarding flow, and (b) may want `SubjectCode` on subject DTOs for ordering/iconography. Recommend the BE expose `SubjectCode` on `StudentSubjectDto` now (cheap, additive) so the later FE story isn't blocked — confirm with planner.

---

## Dependency order across the three stories

```
P8-01 (Identity: LearningLanguage field + add-child wiring + JWT claim + /Me)
   └─ produces the `learning_language` claim ───────────────┐
                                                            │
P8-02 (Learning: SubjectCode/Language enums + Subject       │
        columns + index + migration + 6-root seed)          │
   └─ produces the Subject.Language column + seeded trees ──┤
                                                            ▼
P8-03 (Learning: claim accessor + pure resolver +     consumes BOTH:
        read-path filters + fallback + integration tests)
        - resolver depends on SubjectCode enum (P8-02-BE-1)
        - filters depend on Subject.Language column + seed (P8-02)
        - claim accessor depends on the JWT claim (P8-01-BE-4)
```

- **P8-01 and P8-02 are largely independent** and can run in parallel (different modules, different schemas) — but both serialize on **shared files only if** they touch `Program.cs`/`.sln`/`Directory.Packages.props` (P8-01 likely does not; P8-02 may register the seeder rewrite — confirm). Per PARALLELISM.md, run them as independent siblings in their own worktrees.
- **P8-03 is the join point** and must run **after both** P8-01 (claim) and P8-02 (schema + seed) are merged. Within P8-03, the resolver + its unit tests can be built as soon as the `SubjectCode` enum (P8-02-BE-1) exists, ahead of the read-path filters.
- Recommended waves: **Wave 1** = P8-01 ∥ P8-02 (parallel). **Wave 2** = P8-03 (sequential, after Wave 1 merges). Each story is a security-relevant or data-shape change → reviewer gate per story; security-auditor on P8-01.

---

## Open questions for the lead (decision-shaping — flag, don't guess)

1. **(Highest) How are existing single-language seeded Subjects migrated?** Today's `Subjects` rows have `Name` only, no `SubjectCode`/`Language`. Options: **(a) re-seed from scratch** (drop/replace the dev data — viable because seeding is Development-only + idempotent, and there is no production data yet) vs **(b) data migration** that maps `Name → SubjectCode` + assigns a `Language` to the legacy tree, then seeds the missing roots. This also gates whether the `(GradeId, SubjectCode, Language)` index can be created **UNIQUE** immediately. **Recommend (a) re-seed** if no environment holds real student progress against the current Subject ids; otherwise (b). *Need explicit confirmation — it changes both the migration and the seeder.*
2. **Is `SubjectCode` derived from the existing `Name`, or newly assigned?** The seeder currently uses English names (`"Math"`, `"Science"`, `"Arabic"`, `"English"`). Recommend the seeder **assigns `SubjectCode` explicitly per root** (not parsed from `Name`), since localized/duplicate names will exist across the ar/en trees and `Name` is no longer a reliable key. Confirm.
3. **Default `LearningLanguage` for existing `User` rows?** Recommend `"ar"` (Arabic-first product default, consistent with `PreferredLanguage` default `"ar-EG"`). Confirm — this is the migration default for all pre-existing accounts.
4. **Must `KnowledgeNode`/`KnowledgeEdge` prerequisite edges be authored per-language tree?** The current seeder authors Math prereq edges within **one** Math tree per grade (cross-grade G1→G6). With two Math trees (ar+en) per grade, edges must be duplicated **within each language tree** (no cross-language edges) — doubling the candidate-edge set and the acyclicity validation surface. Confirm this is the intent (architecture §3 implies yes: "KnowledgeNode/KnowledgeEdge belong to a language-specific Subject tree"). The seeder rewrite must wire `SubjectId` on each `KnowledgeNode` to the correct language root.
5. **Storage type for `LearningLanguage`** — short code (`"ar"`/`"en"`, matches the add-child `Language` field and keeps the claim value trivial) vs. an enum-as-int vs. full culture code (`"ar-EG"`/`"en-US"`, matches `PreferredLanguage`). Recommend **short `"ar"`/`"en"` string** so the JWT claim is exactly `ar`/`en` and the Learning-side parse to `ContentLanguage` is direct. Confirm.
6. **Fallback when the claim is absent** (e.g. legacy tokens issued before P8-01, or non-student callers). Recommend defaulting to `ar` and `LogWarn`, never 500. Confirm the default.
7. **Expose `SubjectCode` on subject DTOs now?** Cheap additive change that unblocks the later FE ordering/iconography work (architecture §7). Recommend yes. Confirm with planner.
8. **(Minor) `UpdateChildRequest`/`UpdateChildAsync`** — should the parent's existing edit-child path accept `LearningLanguage` now, or is **all** learning-language change deferred to P8-04 (with the fresh-start reset)? P8-01-BE notes say the *change* flow is P8-04; recommend **not** wiring it into edit-child in this phase to avoid a change path without the Math/Science reset. Confirm.

---

## Design-pattern note (CLAUDE.md rule #8)

**No new design pattern is required.** The language resolver is a **pure static service** that mirrors the existing `LearningPathEngine.ComputeStates(...)` and `SkillGraphValidator.AssertAcyclic(...)` in `Learning.Domain/Services/` — an established shape in this module, not a new abstraction. All other work is field additions, a claim line, enum additions, a seeder rewrite, and `WHERE`-clause filters on existing query handlers. If, during implementation, an agent feels tempted to introduce a Strategy/Factory for per-subject resolution (rather than the plain pure function), **stop and ask the lead first** per rule #8 — but the brief's recommendation is the plain pure function, which needs no approval.

---

## Recommended pipeline order (first cut — planner finalizes)

```
0. LEAD/USER DECISION GATE (before code):
   - Q1 (re-seed vs data migration) — gates migration + seeder.
   - Q3 (existing-row default), Q5 (storage type), Q6 (claim-absent fallback).
   - Q2/Q4/Q7/Q8 (seeder code assignment, per-language edges, DTO expose, edit-child scope).

WAVE 1 (parallel, independent worktrees):
  P8-01:
    1a. db-migration  — Identity: User.LearningLanguage column + default.
    1b. backend-feature — field, seam+add-child wiring, JWT claim, /Me, swagger snapshot.
    1c. security-auditor — child data + auth claim.
    1d. api-tester — /Me carries learningLanguage; refresh re-issues claim.
    1e. reviewer → committer (feat/P8-01-…).
  P8-02:
    2a. db-migration  — Learning: SubjectCode + Language columns + (GradeId,SubjectCode,Language) index.
    2b. backend-feature — enums, Subject props, SubjectConfig, seeder rewrite (6 roots/grade),
                          per-language skill-graph edges, backfill per Q1, unit tests.
    2c. reviewer → committer (feat/P8-02-…).

WAVE 2 (sequential, after Wave 1 merges):
  P8-03:
    3a. backend-feature — claim accessor, pure resolver + unit tests, read-path filters across
                          all 6 handlers, missing-tree fallback.
    3b. api-tester — the ar-medium vs en-medium per-subject language matrix.
    3c. reviewer → committer (feat/P8-03-…).
```

**Clear to proceed?** Soft-blocked on the **decision gate**, not on missing information. The codebase is fully understood; the JWT/claim read seam and pure-engine pattern already exist. The one genuinely load-bearing decision is **Q1** (re-seed vs data migration), which shapes both the Learning migration and the seeder. With Q1 + Q3 + Q5 + Q6 answered, the pipeline is clear to plan.
