# P7-03 — Skills & Knowledge Graph admin — Frontend (web E2E) reference

> Lighter reference for the frontend admin lead. The visual graph editor design pattern is still an open lead-gated
> decision (per the task note) — these cases assume a basic node/edge editor.

| ID | Title | Type | Pri | Preconditions | Steps | Expected |
|----|-------|------|-----|---------------|-------|----------|
| FE-TC-01 | Create/edit skill persists name/threshold/est-time/owner | functional | P0 | admin; a concept/lesson | Create skill; reload | Skill shows persisted fields |
| FE-TC-02 | Add prerequisite edge between two skills | functional | P0 | two skills, same language tree | Draw edge; reload | Edge (prerequisite/related + strength) persisted |
| FE-TC-03 | Cycle edge rejected with friendly message, not persisted | error-state | P0 | A→B exists | Try B→A | "would create a cycle" shown; graph unchanged |
| FE-TC-04 | Cross-language edge rejected with friendly message | error-state/i18n | P0 | an ar skill + an en skill | Try to connect them | "edge must stay within one language tree" shown; not persisted |
| FE-TC-05 | View prerequisites-of / unlocked-by for a skill | functional | P1 | a skill with edges | Open skill detail | Both lists render correctly |
| FE-TC-06 | Remove edge re-renders graph | state | P1 | an edge | Delete edge | Edge gone; graph re-rendered |
| FE-TC-07 | Per-language graph view shows one tree at a time | functional/i18n | P1 | ar + en trees | Switch language filter | Only the selected language's nodes/edges shown |
| FE-TC-08 | Non-admin blocked / redirected | auth-routing | P0 | non-admin / signed out | Open graph editor URL | Redirect / 403 screen |
| FE-TC-09 | RTL (ar) vs LTR (en) for graph editor chrome | RTL-i18n | P2 | locale ar then en | Open editor | Mirrored RTL for ar |
