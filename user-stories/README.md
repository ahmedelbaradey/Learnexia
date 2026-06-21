# Learnexia — User Stories Backlog

Generated from [BRD.md](../docs/BRD.md), [BUSINESS_PLAN.md](../docs/BUSINESS_PLAN.md), [SRS.md](../docs/SRS.md), and [TASK_BREAKDOWN.md](../docs/TASK_BREAKDOWN.md), reconciled into one coherent backlog.

- **Project:** Learnexia
- **Format:** one user story per `.md` file, Jira-ready (Summary / Issue type / Description / Acceptance Criteria / Story Points / Labels / Notes).
- **Sprints:** one sprint per phase, following the 9-week, 6-phase MVP plan. Each phase folder = one Jira sprint.
- **Phase 2+ (post-MVP):** Curriculum Intelligence work lives in `Backlog-Phase-2-Plus/` (modeled now, built later).

## Product decisions that override the source docs

These intentional decisions diverge from the source docs; each affected story records the override in its Notes.

- **Parent-driven onboarding:** the parent (not the student) registers, adds one or more children, and completes each child's onboarding (grade/language/country). The parent assigns each child a login email; the child then logs in to their own account. Students do **not** self-register. *(Overrides SRS FR-ID-2's implied student onboarding — see P1-01, P1-03, P1-04, P1-09.)*
- **4 subjects, not 5:** Math, Science, Arabic, English. **Social Studies removed.** *(Overrides BRD §4 — see P2-02, P2-10, P3-03.)*
- **Grade transition:** the parent dashboard has a per-child grade-transition control that **re-scopes the skill tree to the new grade while preserving history** (XP/badges/streaks/mastery retained). *(New — see P5-05, P5-06.)*
- **Phase order: Gamification before AI Tutor.** **Phase 3 = Gamification** (Week 5), **Phase 4 = AI Tutor** (Weeks 6–7) — building the habit loop before the AI layer, per the barrier-to-entry strategy ([docs/briefs/barrier-to-entry-gap-analysis.md](../docs/briefs/barrier-to-entry-gap-analysis.md)). **Story IDs were kept stable** when the phases were resequenced, so the prefix no longer equals the phase number: Gamification stories are `P4-xx` (in `Phase-3-Gamification/`) and AI-Tutor stories are `P3-xx` (in `Phase-4-AI-Tutor/`). This avoids renaming the already-built, merged `P4-01`.

## Sprint → Phase mapping

| Sprint (folder) | Phase | Weeks | Theme | Done when… |
|---|---|---|---|---|
| `Phase-1-Foundation` | P1 | 1–2 | Auth, users, DB, design system, DevOps | A user can register/login; design system + auth screens live; DB provisioned |
| `Phase-2-Learning-Core` | P2 | 3–4 | Subjects, lessons, skill tree, quiz | A student can browse subjects, open a lesson, navigate a skill tree, take a quiz |
| `Phase-3-Gamification` | P3 | 5 | XP, streaks, hearts, badges, missions, leagues *(story IDs `P4-xx`)* | Gamification fires on learning events and is visible in UI |
| `Phase-4-AI-Tutor` | P4 | 6–7 | Prompt builder, RAG, hints, adaptivity *(story IDs `P3-xx`)* | Tutor explains/hints/generates questions, grounded + behind safety layer |
| `Phase-5-Parent-Analytics` | P5 | 8 | Weekly reports, weak areas, KPIs, parent dashboard | A parent sees a weekly report with weak areas; KPI events captured |
| `Phase-6-Stabilization` | P6 | 9 | Testing, perf, prompt tuning, observability | NFR-1 perf met, prompts tuned, critical bugs cleared → launch-ready |
| `Phase-7-Admin-Console` | P7 | post-MVP | Admin console: curriculum mgmt, user/account mgmt, moderation, analytics/AI oversight | Admins can manage curriculum, users, moderate content, and view platform + AI-safety dashboards |
| `Phase-8-Localization` | P8 | post-MVP | Learning language (medium of instruction) vs UI language; bilingual curriculum (parallel ar/en trees) | A student gets Math/Science in their learning language; Arabic/English subjects pinned by subject |
| `Phase-9-Notifications` | P9 | post-MVP | Push end-to-end (Expo FE) + full habit-forming notification catalog + arbitration, on the merged P4-09 engine | A child's device registers for push, nudges deep-link to the right screen, an in-app inbox exists, and many notification types are arbitrated to few daily sends |
| `Phase-10-Payments-Billing` | P10 | post-MVP | AI credit economy (energy), subscriptions (Free/Premium), payment provider, parent billing | A parent can subscribe + buy energy packs; a child spends "⚡ طاقة المساعد" on AI help, charged on delivery |
| `Backlog-Phase-2-Plus` | post-MVP | — | Curriculum Intelligence (ingestion, KG, RAG at scale) | Deferred; data model designed in MVP |

## How to load into Jira

1. Create a **Scrum** project with key **`LEX`** (or reuse an existing Learnexia project).
2. Create one sprint per phase folder above (Sprint name = phase name).
3. For each `.md` file, create a **Story** (or Epic / Technical Enabler per the file's *Issue type*):
   - Copy the file body into the Description field.
   - Set Story Points, Labels, and Sprint as stated in the file.
   - Link dependencies using the *Notes* ("blocked by") section.
4. Group stories under the phase **Epic** named in each file if you use an Epic layer.

> No live Jira sync was performed — these are local artifacts to copy or bulk-import.

## Story index

### Phase 1 — Foundation
- P1-01 Register as a parent (children provisioned by parent)
- P1-02 Stay signed in (token refresh & sign-out)
- P1-03 Parent completes onboarding & adds children
- P1-04 Link a parent to a child account
- P1-05 Enforce role-based access control
- P1-06 Provision PostgreSQL + pgvector + Redis (Npgsql migration)
- P1-07 Dockerized environment & CI/CD pipeline
- P1-08 Design system & core component library (RTL/Arabic)
- P1-09 Auth & onboarding screens
- P1-10 Sign in to the admin dashboard
- P1-11 Parent web app — all pages, pixel-perfect from design-system screenshots *(epic: landing, login, register, my-children, dashboard, reports, settings; fonts, language switch, dark-mode)*
- P1-12 Web account backend — profile, avatar, OAuth, password reset *(**Batch 2**, deferred; from the Phase-1 design gap analysis)*
- P1-13a Notifications email delivery *(enabler split from P1-13; **built first**; unblocks P1-12d & P5-04)*
- P1-13 Backend hardening — lockout, sign-in safety, admin seed & CAPTCHA *(post-Batch-2; from the Phase-1 **backend** gap analysis)*
- P1-13b Backend hardening pass — auth rate-limiting, forgot-password timing-oracle, email localization & secrets *(bundles the non-blocking security-audit follow-ups)*

### Phase 2 — Learning Core
- P2-01 Model the curriculum hierarchy
- P2-02 Browse subjects and lessons
- P2-03 Navigate the skill tree
- P2-04 Unlock lessons by prerequisite/mastery rules
- P2-05 Open and complete a lesson
- P2-06 Take a quiz (4 question types)
- P2-07 Get instant answer feedback
- P2-08 Record granular per-question answers
- P2-09 See the home dashboard
- P2-10 Seed demo subjects & skill trees
- P2-11 Author the skill dependency graph (relational, hand-authored) *(barrier-to-entry: BE1; MVP launch-bridge — full OCR-driven Curriculum Intelligence pipeline `BL-01..05` stays post-MVP per the strategic deferral)*
- P2-12 Parent account settings — notifications, linked children, security, plan & billing *(back + front; carved out of P1-11 Settings)*

### Phase 3 — Gamification *(story IDs `P4-xx`)*
- P4-01 Emit learning domain events
- P4-02 Earn XP and level up
- P4-03 Maintain a daily streak
- P4-04 Lose hearts and enter Practice Mode
- P4-05 Earn badges
- P4-06 Complete daily/weekly missions
- P4-07 Compete in weekly leagues
- P4-08 Gamification screens & motion
- P4-09 Bring the student back tomorrow (re-engagement notifications) *(barrier-to-entry: BE4)*
- P4-10 Serve realtime gamification state from Redis *(barrier-to-entry: BE3)*
- P4-11 Streak freeze, timed events & weekly challenges *(barrier-to-entry: BE4)*
- P4-12 Timed-event participation *(✅ BUILT 2026-06-20 — per-child progress/completion + eligibility queries on P4-11 timed events; unblocks P9-12 timed-event nudges)*

### Phase 4 — AI Tutor *(story IDs `P3-xx`)*
- P3-01 Route AI requests through an AI Gateway
- P3-02 Filter AI output through a Safety Layer
- P3-03 Build personalized tutor prompts
- P3-04 Explain a concept on demand
- P3-05 Get progressive hints & simpler re-explanations
- P3-06 Generate curriculum-grounded questions (RAG)
- P3-07 Retrieve curriculum context via vector search
- P3-08 Adjust difficulty adaptively
- P3-09 Track per-skill mastery
- P3-10 Schedule spaced-repetition practice
- P3-11 Serve adaptive quizzes
- P3-12 Interact with the AI tutor UI
- P3-13 Build the adaptive student profile (behavioral modeling) *(barrier-to-entry: BE2)*
- P3-14 Lexi recommendation narration *(kid-style AI voice over P5-09 recommendations; new HelperIntent, energy-costed — added 2026-06-18)*
- P3-14a Level- and profile-aware Lexi framing *(enrichment: gamification level → motivational framing + profile encouragement style — added 2026-06-18)*
- P3-13a Behavioral profile depth (grit/time-of-day derivations) *(**BACKLOG** — deferred; extends P3-13 — added 2026-06-18)*

### Phase 5 — Parent + Analytics
- P5-01 Generate a weekly student report
- P5-02 Detect and rank weak areas
- P5-03 Capture product analytics events
- P5-04 Deliver reports via notifications
- P5-05 View the parent dashboard
- P5-06 Transition a child to a new grade
- P5-07 Feed learning data back into the system (calibration loop) *(barrier-to-entry: BE7)*
- P5-08 Parent-scoped per-child read API *(unblocks the faked parent dashboard; backend fan-out over Gamification/Learning/Billing/Ai behind parent-owns-child authz — added 2026-06-18)*
- P5-09 Per-child recommendation engine *(deterministic "Areas to focus" → next actions, free, daily job; narrated by P3-14 Lexi — added 2026-06-18)*
- P5-09a Profile-aware recommendation selection *(enrichment: P3-13 profile dims → action-type/quantity/difficulty-nudge/ordering — added 2026-06-18)*

### Phase 6 — Stabilization
- P6-01 Meet API & AI performance targets
- P6-02 Validate AI safety with an eval set *(✅ BUILT 2026-06-20 — offline CI eval harness, 62 cases; closed the last P7-11 facet; live ar+en run = launch Gate B)*
- P6-03 Pass localization & RTL review
- P6-04 Regression, prompt-tuning & bug triage
- P6-05 Observability: logging, tracing, dashboards
- P6-06 Backend security hardening *(auth timing-oracle, email localization, secrets, Redis rate-limit store; relocated from the P1-13b pass)*
- P6-07 Access-token revocation *(audit finding G2; per-request SessionId validation via `OnTokenValidated` — split out of P6-06)*

### Phase 7 — Admin Console *(post-MVP / ongoing)*
*The admin feature set behind the P1-10 dashboard shell — curriculum management, user/account management, content moderation, and analytics/AI oversight. Admin-only per SRS §3; no teacher role.*
- P7-01 Manage subjects & units
- P7-02 Manage lessons & lesson content
- P7-03 Author skills & the skill dependency graph *(admin UI over P2-11)*
- P7-04 Manage quizzes & questions
- P7-05 Publish, version & preview curriculum content
- P7-06 Search & inspect users (parents + children)
- P7-07 Suspend, reactivate & delete accounts
- P7-08 Manage child profiles & grade overrides
- P7-09 Content moderation queue & review actions
- P7-10 Platform analytics & KPI dashboard
- P7-11 AI-safety & quality monitoring dashboard
- P7-11b Capture AI tutor usage for streamed (SSE) responses *(follow-up to the P7-11 tutor-cost slice; closes the StreamAsync usage-capture gap)*
- P7-12 Admin action audit log
- P7-13 Gamification admin overrides — league tier override, badge/mission catalog editors, timed-event write endpoints, streak-freeze grants *(new; the Phase-3 gamification admin overrides deferred to P7)*

### Phase 8 — Localization
*Learning language (medium of instruction) is a per-student attribute, separate from UI language. Math/Science follow the learning language; the Arabic and English subjects are pinned to their own language. See [../docs/architecture/localization-architecture.md](../docs/architecture/localization-architecture.md).*
- P8-01 Set a child's learning language *(parent-driven, immutable by student; JWT claim)*
- P8-02 Author bilingual curriculum *(SubjectCode + Language on Subject; parallel ar/en trees) — Technical Enabler*
- P8-03 Serve curriculum in the student's learning language *(read-path resolution + Arabic/English edge case)*
- P8-04 Change a child's learning language *(parent-only, fresh-start reset of Math/Science progress)*

### Phase 9 — Notifications
*Make push notifications work end-to-end and build the full habit-forming notification catalog on top of the already-merged P4-09 nudge engine (ExpoPushSender, DevicesController, NudgeDispatcher, per-child preferences). The P4-09 backend handlers + Arabic templates for streak-danger, comeback and badge/achievement already exist — Phase 9 closes the **client side** (Gap A) and adds the missing categories + cross-category arbitration. See [../docs/business-gap-analysis-by-fable.md](../docs/business-gap-analysis-by-fable.md) (Gaps A, L).*
- P9-01 Turn on push notifications *(Expo permission priming + device-token registration; FE)*
- P9-02 Notification tap routing + foreground + web fallback *(per-category deep links; FE)*
- P9-03 In-app notification inbox *(consumes existing InboxController; FE)*
- P9-04 Parent per-child notification controls *(per-child toggles, quiet hours, daily cap; FE)*
- P9-05 Light up the gamification events we already emit *(level-up, league, freeze, timed-event handlers; BE)*
- P9-06 New habit-loop notification categories *(✅ weekly-recap #194 + weekly-challenge ending-soon 2026-06-20; streak-milestone DROPPED as redundant; BE)*
- P9-07 Many types, few sends *(cross-category arbitration + global daily push budget + cooldowns; BE)*
- P9-08 Comeback escalation ladder *(day 2/5/14 tiered win-back; extends P4-09 LapseWinBack; BE)*
- P9-09 Spaced-repetition review reminder *(✅ BUILT 2026-06-20 — consumes P3-10 `ReviewDueIntegrationEvent`; inbox-only v1; BE)*
- P9-10 Get every notification in my selected language *(🟡 v1 BUILT 2026-06-20 — welcome inbox+email localized at send time; reset-email → P6-06; read-time inbox re-localization → P9-03 FE; BE)*
- P9-11 Notification analytics sink *(✅ BUILT 2026-06-20 — send/suppress/open → Analytics `ActivityEvent` + admin aggregate endpoint; BE)*
- P9-12 Timed-event nudges *(✅ BUILT 2026-06-20 — join fan-out + progress/ending-soon/completion over P4-12; inbox-only v1; BE)*

### Phase 10 — Payment, Billing & Credits *(post-MVP)*
*The AI credit economy + monetization. **Parent-driven: all purchasing/billing/payment happens in the parent app/account** — the child only spends "⚡ طاقة المساعد" (energy) and sees a read-only meter. Credits = two pools: monthly **granted** (expire) vs **purchased** packs (never expire). Charge-on-delivery, cache-hits charged the same, no charge on refuse/error. Wires into the AI Gateway (P3-01) and supersedes the AI-Helper MVP daily-cap guardrail.*
- P10-01 Credit (energy) account & ledger *(Technical Enabler — dual pool, append-only ledger)*
- P10-02 Grant monthly energy per plan *(scheduled; granted credits expire at cycle rollover)*
- P10-03 Spend energy on AI help *(charge-on-delivery, wired into the gateway)*
- P10-04 Daily soft cap & low-energy warning *(bounded by the monthly pool)*
- P10-05 Manage subscription plan *(Free vs Premium — 199 EGP/month or 1990 EGP/year — parent)*
- P10-06 Pay for a subscription *(payment provider — Paymob/Fawry DECISION; recurring; security-sensitive)*
- P10-07 Buy an energy pack *(1000 credits / $5, never expire — parent, assigned to a child)*
- P10-08 Billing history & receipts *(parent)*
- P10-09 Failed payments & refunds *(dunning + clawback)*
- P10-10 Kid-facing energy UI *(⚡ طاقة المساعد — read-only, the only student-app billing surface; distinct from hearts)*
- P10-11 Admin: configure plans, grants & action costs *(admin console; config-driven economy)*
- P10-12 Runtime-configurable AI economy via Global Settings *(Technical Enabler — `IGlobalSettingsProvider`, DB-backed, Redis-cached, audited; values tunable without a deploy)*
- P10-13 Family energy wallet & per-child allocation *(parent/family-owned `FamilyEnergyAccount`; two non-convertible buckets — subscription vs purchased; equal-split allocation; child-first → shared-purchased-fallback spend; supersedes the per-child `CreditAccount` model + data migration)*
- P10-14 Child seats & seat-reserved add-child *(included + extra paid seats; `PlanEnergyPerSeat × ActivePaidSeats`; provider-webhook extra-seat purchase; add-child reserves a seat first via a new `Shared.Contracts/Billing` seam)*
- P10-15 Seat enforcement, grace period & NoSeat/Locked child lifecycle *(grace on downgrade/seat-cancel/payment-failure; enforcement never deletes children; over-limit children become NoSeat/Locked and keep progress/XP/history; parent chooses who keeps seats)*
- P10-16 Family energy redistribution & intra-family transfers *(parent moves unspent allocated allowance sibling→sibling; family-only, zero-sum; already-spent energy never reclaimable; paired immutable ledger entries)*
- P10-17 Refund reconciliation (unused purchased energy) *(refunds apply to purchased only, never subscription grants; refundable = purchased − consumed-purchased; ledger-reconciled; settled via verified provider webhook)*
- P10-18 Pause / unpause a child's access *(immediate parent-control toggle — blocks AI features without affecting seat, billing, or energy; entirely separate from the billing-driven NoSeat/Locked state; `ParentPauseState` stored on the Billing child seat record; AI spend gate checks BOTH SeatState and ParentPauseState independently; security-auditor mandatory — IDOR risk)*

> **Family energy model (FINAL mid-cycle seat model, lead-approved 2026-06-17) — P10-13..18 wave:** energy is **parent/family-owned** (`FamilyEnergyAccount`) with **two non-convertible buckets** — (A) subscription/entitlement (temporary/monthly = `PlanEnergyPerSeat × ActivePaidSeats`, allocated per active-seat child, resets each cycle) and (B) purchased packs (permanent, shared family reserve). Per-child spend hits the child's **own allocation first**, then the shared purchased row as fallback. Seats define entitlement (only paid active seats generate energy). **Mid-cycle seat ADD/REACTIVATE: prorate MONEY only — NO energy is minted mid-cycle; the child's mid-cycle energy comes ONLY from the parent allocating family-wallet credit (P10-16); at renewal the seat is a normal full-cycle seat.** **Mid-cycle seat REMOVE/CANCEL (voluntary): effective at CYCLE END — seat stays Active until next renewal; child keeps remaining allowance; NO prorated refund; NO energy reclaim, forfeit, or conversion; at renewal the seat is removed and over-seat children → NoSeat/Locked (never deleted, keep all progress/XP/history).** **7-day grace = payment-failure retry window at the renewal boundary ONLY — NOT triggered by voluntary cancels or downgrades.** Transfers are sibling-only; refunds apply to purchased energy only. **This supersedes P10-01's per-child `CreditAccount` ownership model** — a data migration is required (`credits.premium_monthly`/`credits.free_monthly` become `PlanEnergyPerSeat`). **Dependency order:** P10-13 (core) → P10-14 (seats) → P10-15 (lifecycle); P10-16 (redistribution, also the mid-cycle energy mechanism) + P10-17 (refunds) build on P10-13; P10-18 (pause/unpause) builds on P10-14 + P10-15.

> **Business model (launch):** Monthly **199 EGP** + Annual **1990 EGP** (≈ 2 months saved). **Payments: web checkout primary — no native mobile IAP** (deliberate; needs App/Play Store policy review before native launch). **AI economy:** credits configurable (Global Settings), charge value delivered, cache aggressively. **Success metrics (track these, NOT AI cost):** Free→Paid conversion, CAC, retention, average subscription months — instrumented via P5-03 analytics. **Voice (TTS), when added:** generate-once / cache-always via `AiResponseCache` — *needs its own story (not yet specified).*

### Backlog (Phase 2+) — Curriculum Intelligence
*Three-stage pipeline: Multimodal Parsing (BL-02) → Curriculum Ingestion (BL-05) → Knowledge Graph (BL-03).*
**Status: deferred post-MVP.** P2-11 ships an MVP launch-bridge — a hand-authored relational knowledge graph (`KnowledgeNode`/`KnowledgeEdge`) modeled as a forward-compatible superset of `BL-04`, with **no OCR / Azure Document Intelligence dependency**. When BL-01..05 is built, the BL-04 schema extends the P2-11 tables rather than replacing them.
- BL-01 Upload curriculum documents with metadata
- BL-02 Parse curriculum files into structured content (Multimodal Parsing)
- BL-03 Build & query the knowledge graph
- BL-04 Curriculum, knowledge-graph & vector schema *(P2-11's `KnowledgeNode`/`KnowledgeEdge` are the MVP slice of this)*
- BL-05 Ingest parsed content into the curriculum hierarchy (Curriculum Ingestion)
