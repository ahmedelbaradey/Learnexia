# Wave 6 — Phase 2 Learning Core (backend): P2-12, P2-10, P2-06

Bundles three Phase-2 backend stories. Each ran the full pipeline (analyzer → planner → implementers → security-auditor/api-tester → reviewer → committer) and merged into `feat/wave-6` with `--no-ff`. **Full integration suite: 417/417 green; all module unit tests green; `dotnet build` clean.**

## P2-12 — Parent account settings (3-module refactor)
Lead-directed restructure of the original Identity-only plan into clean module boundaries:
- **New `Parent` module** (schema `parent`) now owns all parent↔child family code — `AddChild`, `LinkChild`, `UpdateChild`, `ListMyChildren`, and the new `UnlinkChild` (with a **last-parent guard** so a child is never orphaned; TOCTOU-safe via a transaction-scoped advisory lock). Routes moved `/api/Users/Parent/*` → `/api/Parent/*`.
- **`IChildAccountService`** (Shared.Contracts, implemented in Identity.Infrastructure) is the cross-module seam for child-account create/read/update (mirrors `IUserLookup`); reverse **`IParentChildQuery`** preserves Identity `GetMe.HasChildren`. No Parent↔Identity project references.
- **Notifications module** gained `NotificationPreference` (schema `notifications`) + `GET/PUT /api/Notifications/Preferences` (categories WeeklyReport/StreakAtRisk/ProductAnnouncement/Achievement × Email/Push; defaults on first read).
- **Identity** kept account security: `ChangePassword` (now invalidates other sessions + revokes refresh token, rate-limited), `GET /Sessions`, `SignOutOthers`, `GET /Plan` (Free stub).
- Migrations: `InitialParent`, `AddNotificationPreferences`, `DropParentStudent` (identity). Dev `ParentStudents` rows are disposable; **production data-copy is a tracked follow-up**.
- Legacy P1-03/04/12/09/05 integration tests retargeted to the new `/api/Parent/*` routes.
- Security: 2 High findings (ChangePassword error leak; empty validator) remediated; TOCTOU, mass-assignment, duplicate-logger, and per-endpoint rate-limit also fixed.

## P2-10 — Seed demo subjects & skill trees
- Dev-only idempotent `LearningSeeder` (gated on `IsDevelopment()` in `LearningModule.InitializeAsync`): **6 grades × 4 subjects** (Math, Science, Arabic, English), **Math deepest** (5 units/15 lessons vs 2/4). Stable Skill names are the P2-11 prerequisite-edge seam. System user id `0`. 5/5 unit tests.

## P2-06 — Take a quiz (folded into Learning)
- **Lead decision: quiz lives in the Learning module, not a separate Assessment module** (a scaffolded Assessment module was removed). Entities `QuizQuestion`/`Attempt`/`StudentAnswer` (schema `learning`, migration `AddQuizTables`); 4 question types (MCQ/TrueFalse/Matching/FillInBlank) with a per-type content validator (28 unit tests).
- `POST /api/Learning/Quizzes/{lessonId}/Attempt` `[Authorize(Roles="Student")]`: creates/**resumes** an InProgress attempt, validates lesson existence (404), returns questions **without `CorrectAnswer`** (explicit, test-asserted). StudentId from JWT only. 17/17 integration tests.

## Config / security hygiene
- New optional `appsettings.{Environment}.local.json` loader in `Program.cs`; the remote dev DB connection lives only in the **gitignored** `appsettings.Development.local.json` (never committed). Tracked `appsettings.Development.json` keeps the localhost default.
- **Remote DB** (`learnexia`): all 5 module schemas migrated; **not yet seeded** (seeding via a Host run was permission-blocked — run the Host in Development to seed, or grant a `dotnet run` allow-rule).

## Deferred to P6-06 (pre-existing, not introduced here)
JWT `CHANGE_ME` default secret (env-drive + startup guard), `RequireHttpsMetadata=false` (Development-only), DbContext audit stamp `DateTime.Now`→`UtcNow`, MinIO default creds, and the MSB3277 EF 10.0.0/10.0.8 version conflict in `Directory.Packages.props`.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
