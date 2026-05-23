## Phase Reorder: Gamification = Phase 3, AI Tutor = Phase 4

### Summary
Reorders MVP phases to implement the **barrier-to-entry strategy**: Gamification (core loops: XP, levels, streaks, badges) now launches in **Phase 3 (Week 5)**, while AI Tutor (more complex adaptivity) moves to **Phase 4 (Weeks 6–7)**. This reduces student cognitive load at onboarding and lets us stabilize gamification mechanics before introducing AI complexity.

**Story IDs preserved (Option B):** Gamification stories remain `P4-01` through `P4-11`, AI Tutor remains `P3-01` through `P3-13`. Only folder paths and Sprint/Phase headers changed; `P4-01` backend code, tests, and ADR are untouched.

### Changes
- **user-stories/**: Renamed folders `Phase-4-Gamification/` → `Phase-3-Gamification/` and `Phase-3-AI-Tutor/` → `Phase-4-AI-Tutor/`
- **24 story files**: Updated `Sprint / Phase` headers in all gamification (P4-01…11) and AI Tutor (P3-01…13) stories
- **docs/BUSINESS_PLAN.md**: Reordered roadmap and success criteria by new phase/week assignment
- **docs/SRS.md**: Updated post-MVP phase ordering
- **docs/dev/PARALLELISM.md**: Adjusted phase dependency constraints
- **tasks/PROGRESS.md**: Reordered phase sections (Phase 1–2 unchanged), added missing P4-09/10/11 and P3-13 rows
- **tasks/README.md**: Added note on new phase order
- **user-stories/README.md**: Updated index/map with decision rationale
- **docs/briefs/barrier-to-entry-gap-analysis.md**: Marked gap 3b-1 (phase reorder) as resolved

### Timeline
- **Phase 1:** Weeks 1–2 (Identity, Core Models, Analytics)
- **Phase 2:** Weeks 3–4 (Assessment, Content, Practice)
- **Phase 3:** Week 5 (Gamification — XP, levels, streaks, badges, missions, leagues, notifications)
- **Phase 4:** Weeks 6–7 (AI Tutor — gateway, safety, prompt builder, explanations, hints, RAG, adaptivity, mastery)
- **Phase 5+:** Week 8+ (Advanced features, integrations)

### Rationale
Gamification is the **core engagement lever** and must stabilize early; it has fewer external dependencies (Redis/notifications) than AI Tutor (Claude API, RAG corpus, safety gates). By phase-shifting, students experience immediate progression feedback without waiting for expensive AI operations. P4-01 (domain events) was already built with a modular, event-driven design that supports both phases; no code churn.

### Review notes
- No backend code changes; `backend/` untouched.
- No API contract changes; all endpoints/handlers remain as-is.
- No database schema changes.
- Planning/docs are in-phase cleanup only.
- No dependencies broken; phase-shift is purely temporal.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
