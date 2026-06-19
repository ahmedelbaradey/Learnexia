# P7-02 — Lessons & Content Blocks admin — Frontend (web E2E) reference

> Lighter reference for the frontend admin lead. Admin-only surface; the lesson content-block editor is the richest
> editing screen in the area.

| ID | Title | Type | Pri | Preconditions | Steps | Expected |
|----|-------|------|-----|---------------|-------|----------|
| FE-TC-01 | Create/edit lesson persists metadata (title/difficulty/est-time/order/lock) | functional | P0 | admin; a unit | Create lesson with all fields; reload | Lesson shows persisted metadata |
| FE-TC-02 | Lesson language shown for context but not editable | i18n | P1 | a lesson under MATH/Ar | Open lesson editor | Language label = Ar, read-only (no language field) |
| FE-TC-03 | Add content blocks of each type (text/image/video/callout) | functional | P0 | a lesson | Add one of each type | Blocks render with type-appropriate editors |
| FE-TC-04 | Reorder + remove blocks persists | state | P1 | lesson with ≥2 blocks | Drag-reorder, delete one; reload | New order + removal persisted |
| FE-TC-05 | Reorder lessons within a unit persists | state | P1 | unit with ≥2 lessons | Drag-reorder; reload | New order persisted |
| FE-TC-06 | Delete lesson removes it from student-served view, handles blocks atomically | functional | P1 | lesson with blocks | Delete lesson | Lesson + its blocks gone; no orphan block errors |
| FE-TC-07 | Per-type payload validation errors are friendly (i18n) | error-state/i18n | P1 | block editor | Submit a callout with bad variant | Localized validation message; no raw 422 keys |
| FE-TC-08 | Image/video block upload routes via shared storage | functional | P2 | block editor | Upload an image | Media saved; URL renders |
| FE-TC-09 | Non-admin blocked / redirected | auth-routing | P0 | non-admin / signed out | Open lesson editor URL | Redirect / 403 screen |
| FE-TC-10 | RTL (ar) vs LTR (en) layout for the content editor | RTL-i18n | P2 | locale=ar then en | Open editor | Mirrored RTL for ar; LTR for en |
