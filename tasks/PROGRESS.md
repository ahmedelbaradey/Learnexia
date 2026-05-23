# Learnexia — Build Progress Tracker

> Single source of truth for **what's done vs. not** across the whole backlog.
> Maintained automatically: the **`committer` agent updates this file on every commit** (flips the row for the story it just committed). The lead may also reconcile it after merges.
>
> Status reflects **merged to `main`** unless a row says otherwise.

## Legend
- ✅ **Done** — pipeline complete, reviewer PASS, committed, merged to `main`
- 🟡 **In progress** — pipeline running (branch exists, not yet merged)
- 🔲 **Not started**
- `—` — no work in this stack for this story (single-stack story)

## Recently completed (newest first)
- **Wave 5:** P2-01-BE (model curriculum hierarchy, 6 entities, CQRS vertical slices, 30 endpoints) — committed
- **Wave 4:** P1-09 (auth & onboarding screens, Expo + NSwag client + shared Tamagui UI primitives + Me endpoint), P1-10 (admin dashboard sign-in on shared Tamagui UI) — PR open
- **Wave 3:** P1-03-BE (parent onboarding & add children), P1-05-BE (role-based access control) — merged
- **Wave 2:** P1-02-BE (token refresh & sign-out), P1-04-BE (parent↔child link + family-scope authz) — merged (PR #1)
- **Wave 1:** P1-01-BE (register parent), P1-07-BE (Docker/CI + health + jobs) — merged
- **Wave 0:** P1-06-BE (Postgres+pgvector+Redis), P1-08-FE (design system) — merged
- **Foundation:** PKG-FOUNDATION-FE (Turborepo monorepo, shared + api-client) — merged
- **(earlier)** P4-01 (domain-events backbone) — merged

---

## Phase 1 — Foundation
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| — | Monorepo, api-client & shared (foundation) | — | ✅ |
| P1-01 | Register as a parent | ✅ | 🔲 |
| P1-02 | Stay signed in (token refresh & sign-out) | ✅ | 🔲 |
| P1-03 | Parent onboarding & add children | ✅ | 🔲 |
| P1-04 | Link a parent to a child account | ✅ | 🔲 |
| P1-05 | Role-based access control | ✅ | — |
| P1-06 | PostgreSQL + pgvector + Redis | ✅ | — |
| P1-07 | Dockerized environment & CI/CD | ✅ | — |
| P1-08 | Design system & components (RTL) | — | ✅ |
| P1-09 | Auth & onboarding screens | ✅ | ✅ |
| P1-10 | Sign in to the admin dashboard | ✅ | ✅ |

## Phase 2 — Learning Core
| Story | Title | Backend | Frontend |
|---|---|:--:|:--:|
| P2-01 | Model the curriculum hierarchy | ✅ | — |
| P2-02 | Browse subjects and lessons | 🔲 | 🔲 |
| P2-03 | Navigate the skill tree | 🔲 | 🔲 |
| P2-04 | Unlock lessons by prerequisite/mastery | 🔲 | — |
| P2-05 | Open and complete a lesson | 🔲 | 🔲 |
| P2-06 | Take a quiz (4 question types) | 🔲 | 🔲 |
| P2-07 | Get instant answer feedback | 🔲 | 🔲 |
| P2-08 | Record granular per-question answers | 🔲 | — |
| P2-09 | See the home dashboard | 🔲 | 🔲 |
| P2-10 | Seed demo subjects & skill trees | 🔲 | — |

## Phase 3 — Gamification *(story IDs `P4-xx`)*
| Story | Title | Status |
|---|---|:--:|
| P4-01 | Emit learning domain events | ✅ |
| P4-02 | Earn XP and level up | 🔲 |
| P4-03 | Maintain a daily streak | 🔲 |
| P4-04 | Lose hearts and enter Practice Mode | 🔲 |
| P4-05 | Earn badges | 🔲 |
| P4-06 | Complete daily/weekly missions | 🔲 |
| P4-07 | Compete in weekly leagues | 🔲 |
| P4-08 | Gamification screens & motion | 🔲 |
| P4-09 | Re-engagement notifications | 🔲 |
| P4-10 | Redis realtime gamification state | 🔲 |
| P4-11 | Streak freeze, timed events & weekly challenges | 🔲 |

## Phase 4 — AI Tutor *(story IDs `P3-xx`)*
| Story | Title | Status |
|---|---|:--:|
| P3-01 | Route AI requests through an AI Gateway | 🔲 |
| P3-02 | Filter AI output through a Safety Layer | 🔲 |
| P3-03 | Build personalized tutor prompts | 🔲 |
| P3-04 | Explain a concept on demand | 🔲 |
| P3-05 | Progressive hints & simpler re-explanations | 🔲 |
| P3-06 | Generate curriculum-grounded questions (RAG) | 🔲 |
| P3-07 | Retrieve curriculum context via vector search | 🔲 |
| P3-08 | Adjust difficulty adaptively | 🔲 |
| P3-09 | Track per-skill mastery | 🔲 |
| P3-10 | Schedule spaced-repetition practice | 🔲 |
| P3-11 | Serve adaptive quizzes | 🔲 |
| P3-12 | Interact with the AI tutor UI | 🔲 |
| P3-13 | Build the adaptive student profile | 🔲 |

## Phase 5 — Parent + Analytics
| Story | Title | Status |
|---|---|:--:|
| P5-01 | Generate a weekly student report | 🔲 |
| P5-02 | Detect and rank weak areas | 🔲 |
| P5-03 | Capture product analytics events | 🔲 |
| P5-04 | Deliver reports via notifications | 🔲 |
| P5-05 | View the parent dashboard | 🔲 |
| P5-06 | Transition a child to a new grade | 🔲 |

## Phase 6 — Stabilization
| Story | Title | Status |
|---|---|:--:|
| P6-01 | Meet API & AI performance targets | 🔲 |
| P6-02 | Validate AI safety with an eval set | 🔲 |
| P6-03 | Pass localization & RTL review | 🔲 |
| P6-04 | Regression, prompt-tuning & bug triage | 🔲 |
| P6-05 | Observability: logging, tracing, dashboards | 🔲 |

## Backlog (Phase 2+) — Curriculum Intelligence
| Story | Title | Status |
|---|---|:--:|
| BL-01 | Upload curriculum documents with metadata | 🔲 |
| BL-02 | Parse curriculum files (Multimodal Parsing) | 🔲 |
| BL-03 | Build & query the knowledge graph | 🔲 |
| BL-04 | Curriculum, KG & vector schema | 🔲 |
| BL-05 | Ingest parsed content into hierarchy | 🔲 |

---

## Deferred / follow-up debt (not blocking; track for a hardening pass)
- Anti-automation (rate-limit/CAPTCHA) on anonymous registration — P1-01
- `RoleHelper` legacy lowercase-constant cleanup — Identity
- Remove `DEMO_PgvectorProof` migration when the real embedding table lands — P1-06
- Container non-root image, CI action SHA-pinning, staging TLS cert — P1-07
- Tokenize inline glow/alpha shades in components — P1-08
- **Open decision:** staging deploy provider (Azure / Railway / Render) — see `docs/deploy/staging-decision.md`
