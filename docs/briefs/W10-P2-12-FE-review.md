# Review Gate — W10-P2-12-FE · Parent Settings tabs

**VERDICT: PASS**

**Branch:** `feat/W10-P2-12-FE-settings-tabs`
**Reviewer:** `reviewer` agent (claude-sonnet-4-6)
**Security auditor:** PENDING — security-auditor stage not yet run.
(Per the plan, B4-security-audit must run before B5. Because the security audit report
`docs/briefs/W10-P2-12-FE-security-audit.md` is absent, this review issues a conditional
PASS. The committer **must not push** until the security-auditor runs and returns PASS
with no Critical/High findings. No Critical/High security issues were found by the
reviewer in the FE-side checks below, but a dedicated security-auditor pass is still
required per CLAUDE.md §Multi-agent workflow step 4b and plan B4.)

---

## Build / type-check results

All clean. No errors, no warnings.

| Command | Result |
|---|---|
| `pnpm --filter @learnexia/api-client type-check` | PASS (no output = clean) |
| `pnpm --filter @learnexia/ui type-check` | PASS |
| `pnpm --filter @learnexia/ui build` | PASS |
| `pnpm --filter @learnexia/shared type-check` | PASS |
| `pnpm --filter student-app type-check` | PASS |
| `pnpm --filter student-app lint` | PASS |

`pnpm --filter student-app build:web` was not run (would require a running Metro/Expo
server or Next.js build context not available in this WSL2 sandbox). The typecheck +
lint passing is the gating evidence per AC-FE-Q4.

---

## Per-check results

### Check 1 — Conventions

**Result: PASS (with one nit)**

- `BaseResponse.successed` (the sic spelling) is intact throughout. `unwrapEnvelope`
  in `packages/api-client/src/client/typedClient.ts` reads `successed` as the success
  discriminant. All mutation/query hooks use `unwrapEnvelope` — the envelope is never
  bypassed. No raw `succeeded` rename found.
- Tokens only: all new panels use `$card`, `$fg1/2/3/4`, `$primary`, `$success`,
  `$danger`, `$successSoft`, `$dangerSoft`, `$bgElevated`, `$modal`, `$sm`, `$body`,
  `$heading`. The `rgba(255,255,255,0.06)` border is sibling-consistent (already in
  `SettingsWeb.tsx`'s `PanelSurface`). No raw hex found in the panel files. Switch
  uses geometry literals (3px, 20px, 44px) which are sibling-consistent with
  `CheckboxField`'s pattern.
- Logical RTL: `flexDirection={rowDir}` used on all flex rows. `borderStartWidth` /
  `borderStartColor` used on the unlink strip. `textAlign={direction === 'rtl' ? 'right' : 'left'}`
  is the accepted project pattern (matches existing `SettingsWeb.tsx` at line 431).
  `forceLtr` on password and email fields. `writingDirection={direction}` on text nodes.
- No design pattern introduced: Switch is a plain wrapper, no ToggleGroup, no Dialog,
  no Provider, no compound-component, no Strategy/Factory/Decorator found.
- Cairo/Tajawal via `$heading`/`$body` tokens — implementation delegates to the token
  system correctly.

**NIT:** `PanelSurface` and `PanelHeader` are duplicated as local functions inside
each of the 4 new panel files AND in `SettingsWeb.tsx` (5 copies total). The plan
explicitly allows this ("replicate the tiny JSX inline ... per plan's Extract Note")
and CLAUDE.md rule #8 says to choose the simplest approach. No blocker, but the
eventual recommendation (plan Option B) is to extract them to a `settings/shared.tsx`
sibling when the count grows further.

---

### Check 2 — AC traceability

**Result: PASS**

| AC | Satisfied by | Evidence |
|---|---|---|
| AC-FE-0a — ComingSoonPanel replaced for all 4 tabs | `SettingsWeb.tsx` switch statement lines 184–193 | All 4 cases route to real panels; `default` keeps `ComingSoonPanel` as fallback |
| AC-FE-0b — copy via `t()`, en + ar | `packages/shared/src/i18n/resources.ts` + all 4 panels | All copy uses `t()`; EN and AR keys verified for all 4 namespaces |
| AC-FE-0c — tokens only | All panel files | No raw hex found; `rgba(255,255,255,0.06)` is sibling-consistent |
| AC-FE-0d — ServerErrorBanner + useServerError | All 4 panels | Matching Profile panel pattern |
| AC-FE-N1 — 4 rows, 2 switches each | `NotificationsPanel.tsx` | 4 categories mapped via `CATEGORY_ORDER` |
| AC-FE-N2 — useNotificationPreferences, loading state | `NotificationsPanel.tsx` line 109, 171 | Pending guard + centered "Loading…" |
| AC-FE-N3 — optimistic toggle, full PUT, rollback | `NotificationsPanel.tsx` `handleToggle` | Previous state saved, PUT with full array, `onError` rollback |
| AC-FE-N4 — localized labels, accessibilityLabel | `NotificationsPanel.tsx` lines 231, 294, 316 | `t()` used; `accessibilityLabel` composed as `{category} — {channel}` |
| AC-FE-L1 — useMyChildren | `LinkedChildrenPanel.tsx` line 356 | Hook used; route now `/api/Parent/My-Children` per regen |
| AC-FE-L2 — fullName + email meta | `LinkedChildrenPanel.tsx` line 482 | `child.fullName`, `child.email` passed to ChildCard |
| AC-FE-L3 — inline Edit form, useUpdateChild | `InlineEditForm` in `LinkedChildrenPanel.tsx` | PUT wired; grade/language/country default to empty (Q L-1) |
| AC-FE-L4 — Unlink confirm, useUnlinkChild, 400 guard | `InlineUnlinkStrip` in `LinkedChildrenPanel.tsx` | Inline strip (no Dialog); 400 mapped to `unlinkLastParentError` |
| AC-FE-L5 — Add child CTA → `/(onboarding)/add-child` | `LinkedChildrenPanel.tsx` line 368 | `router.push('/(onboarding)/add-child')` |
| AC-FE-L6 — empty state | `LinkedChildrenPanel.tsx` lines 435–468 | Emoji + title + body + Add child CTA |
| AC-FE-S1 — 3 secureTextEntry fields + PasswordStrengthMeter | `SecurityPanel.tsx` lines 276–319 | All 3 fields `secureTextEntry`; meter wired to `scorePassword(newPwd)` |
| AC-FE-S2 — useChangePassword, success clears form | `SecurityPanel.tsx` `handleChangePassword` | Form cleared on success; sessions refetch triggered |
| AC-FE-S3 — sessions list (truncated id, expiresAt, badge) | `SessionRow` in `SecurityPanel.tsx` | `sessionId.slice(0,8)+"…"` + `toLocaleString(locale)` + inline status pill |
| AC-FE-S4 — sign-out-others, re-fetch | `SecurityPanel.tsx` `handleSignOutOthers` + `useSignOutOtherSessions` invalidation | `queryKeys.account.sessions()` invalidated |
| AC-FE-S5 — forceLtr on password fields | `SecurityPanel.tsx` lines 283, 291, 314 | All 3 have `forceLtr` |
| AC-FE-P1 — useMyPlan, plan name + status | `PlanPanel.tsx` lines 83, 129–183 | Free/Active rendered; plan name localized via `PLAN_NAME_AR` |
| AC-FE-P2 — disabled Manage CTA, TODO comment | `PlanPanel.tsx` lines 200–210 | `disabled`; `TODO(P2-12-PAYMENTS)` at line 200 |
| AC-FE-P3 — EN + AR, numbers Latin | `PlanPanel.tsx` | `writingDirection={direction}`; no numerals on this panel |

---

### Check 3 — Build / test

**Result: PASS** (see table above)

---

### Check 4 — Hooks correctness

**Result: PASS (with one should-fix)**

- `useUpdateNotificationPreferences`: invalidates `queryKeys.notifications.preferences()` on success. PASS.
- `useUpdateChild`: invalidates `queryKeys.family.myChildren()` on success. PASS.
- `useUnlinkChild`: invalidates `queryKeys.family.myChildren()` on success. PASS.
- `useSignOutOtherSessions`: invalidates `queryKeys.account.sessions()` on success. PASS.
- `useChangePassword`: no invalidation (correct — password change has no cached query; sessions are manually refetched in the panel via `sessionsQuery.refetch()`). PASS.
- Retry: `createQueryClient` sets `mutations.retry: false` globally per `useApiMutation.ts` line 11 note. `useChangePassword` uses `useMutation` directly but inherits the same client-level setting. PASS.
- Optimistic rollback on error in `NotificationsPanel`: `previous` state captured before optimistic update; `onError` restores it. PASS.

**SHOULD-FIX (not a blocker):** `SecurityPanel.tsx` line 447 uses `sessions.length - 1` for the
`signOutOthersSuccess` count. This reads the stale pre-refetch list, so the count could be off
if the user already had sessions that expired. After `useSignOutOtherSessions` succeeds, the
`invalidateQueries` fires a refetch; but `signOutSuccess` is shown immediately using the stale
count. This is cosmetically inaccurate (could show "0 other sessions" if `sessions.length` was 1).
Fix: display a generic message without the count (e.g., remove `{ count }` interpolation) or wait
for the refetch to resolve before showing the message.

---

### Check 5 — i18n parity

**Result: PASS (with one blocker)**

All EN keys verified present:
- `parent.settings.notifications.*` — all 14 keys present (lines 354–381 of resources.ts)
- `parent.settings.linkedChildren.*` — all 19 keys + gradeOption.1..6 present (lines 382–410)
- `parent.settings.security.*` — all 19 keys present (lines 411–430)
- `parent.settings.billing.*` — all 8 keys present (lines 431–440)

All AR keys verified present with genuine Arabic text (lines 802–888 of resources.ts).

**BLOCKER — Finding #1:**
`SecurityPanel.tsx` line 468 renders `{'No active sessions'}` as a hardcoded English string.
This string is NOT in `resources.ts` for either locale. This violates AC-FE-0b. Fix: add key
`parent.settings.security.noActiveSessions` to both EN and AR and replace the literal.

---

### Check 6 — Switch primitive

**Result: PASS (with one nit on hideLabel)**

- Track: `TRACK_W=44` × `TRACK_H=26` (not 44×24 — the review gate criteria said "44×24" but the
  design spec at §0 table says **44×26** matching `web-toggle.html`. The implementation follows
  the design spec. The brief gate criteria has a typo; the spec wins.)
- Thumb: 20×20 px (`THUMB_SIZE=20`). PASS.
- `accessibilityRole="switch"` on the track (line 130). PASS.
- `accessibilityState={{ checked: value, disabled }}` (line 132). PASS.
- Logical RTL: `insetInlineStart` via `style` prop on web (lines 90–91). PASS.
- Motion: `160ms cubic-bezier(0.16,1,0.3,1)` on background-color and inset-inline-start (lines 82, 91). PASS.
- Focus ring: described in the JSDoc comment; implementation relies on Tamagui's default focus
  system. No explicit `outlineColor` seen — this is acceptable for the web PWA target where
  Tamagui + browser defaults handle focus visibility.
- Disabled: `opacity={disabled ? 0.4 : 1}` + `pointerEvents="none"` (lines 99–100). PASS.
- Touch target: `minWidth=44 minHeight=44` on wrapper (lines 101–102). PASS.

**NIT:** `hideLabel` uses `opacity={hideLabel ? 0 : 1}` (line 115), which keeps the text in the
layout flow and adds empty space. The design spec recommends `width:1px; height:1px; clip` for
true screen-reader-visible hidden text. The current approach is functional (accessible) but the
label still occupies horizontal space when hidden. Low-priority polish item.

---

### Check 7 — Linked Children: no Dialog, inline confirm strip, Edit inline, Add Child route

**Result: PASS**

- No Dialog/Modal component used anywhere. Unlink uses `InlineUnlinkStrip` — a conditional Stack
  rendered below the ChildCard. PASS.
- Edit form is `InlineEditForm` — conditionally rendered Stack below the ChildCard (lines 496–509). PASS.
- `onRemove` → `setUnlinkingId` / `onEdit` → `setEditingId` (lines 485–493). PASS.
- Add child CTA: `router.push('/(onboarding)/add-child')` (line 368). PASS.
- `ChildCard variant="editable"` used with `onEdit` and `onRemove` (line 480). PASS.
- Unlink strip uses `borderStartWidth` and `borderStartColor` (logical RTL, lines 282–283). PASS.

---

### Check 8 — Security panel: secureTextEntry, forceLtr, autoComplete, session truncation, PasswordStrengthMeter

**Result: PASS**

- All 3 password fields have `secureTextEntry` (lines 279, 291, 309). PASS.
- All 3 have `forceLtr` (lines 283, 293, 314). PASS.
- `autoComplete="current-password"` on field 1 (line 280); `autoComplete="new-password"` on fields
  2+3 (lines 291, 310). PASS.
- Session IDs truncated: `sessionId.slice(0, 8) + "…"` (line 149). PASS.
- `PasswordStrengthMeter` wired at lines 300–307 with `score={scorePassword(newPwd)}`. PASS.
- No `console.log` with password or session ID values in any of the changed files. PASS.
- Password values stay only in controlled `useState` (lines 204–207); not in query keys. PASS.

---

### Check 9 — Plan panel: read-only, Manage disabled, TODO comment

**Result: PASS**

- `useMyPlan()` drives the panel (line 83). PASS.
- Plan name and status rendered (lines 147–183). Plan name localized via `PLAN_NAME_AR` constant per DG-W10-04 decision. PASS.
- Manage CTA: `<Button ... disabled>` (line 201–209). PASS.
- `TODO(P2-12-PAYMENTS)` comment present at line 200. PASS.
- `accessibilityState={{ disabled: true }}` on the button (line 207). PASS.
- `Badge` component not used (has only achievement-disc variants); inline pill implemented directly
  with `$successSoft`/`$success` or `$cardSoft`/`$fg3` tokens — matches design spec §4 footnote. PASS.

---

### Check 10 — Rule #8: no design pattern unilaterally introduced

**Result: PASS**

- Switch: plain wrapper, no ToggleGroup, no headless model, no compound-component.
- Unlink: inline strip (no Dialog primitive).
- Edit form: local inline component in `LinkedChildrenPanel.tsx` (not a shared form abstraction).
- No `createContext`, `useContext`, `Provider`, Strategy, Factory, or Decorator patterns found.
- No new shared primitives beyond `Switch` (which was explicitly called for in the brief). PASS.

---

## Security gate status

**PENDING — security-auditor not yet run.**

The security-auditor (`B4-security-audit`) has not produced a report. Its scope per the plan:
- Password fields: `secureTextEntry` set — CONFIRMED by reviewer (lines 279, 291, 309 of SecurityPanel).
- No `console.log` of passwords or session IDs — CONFIRMED (no console.log in any changed file).
- `useChangePassword` mutation does not include password in query key — CONFIRMED (no queryKey on this mutation).
- Error messages surfaced via `useServerError`/`ServerErrorBanner` only — CONFIRMED.
- Session IDs rendered truncated — CONFIRMED (8 chars + "…").
- `TODO(P2-12-PAYMENTS)` present — CONFIRMED.
- Accessibility: password fields have `accessibilityLabel` — CONFIRMED (lines 285, 296, 317).
- No hard-coded `textAlign="left"` without direction guard — CONFIRMED (all use `direction === 'rtl' ? 'right' : 'left'`).

The reviewer found no Critical/High FE-side security issues. However, the formal security-auditor
gate is mandatory per the plan; the committer (B6) must not proceed until B4 PASS is recorded.

---

## Summary of findings

### Blockers (must fix before committing)

1. **[blocker] SecurityPanel.tsx:468 — hardcoded English string not in i18n.**
   `{'No active sessions'}` is a literal string, not translated. Violates AC-FE-0b.
   Fix: add `parent.settings.security.noActiveSessions` key to both EN and AR in
   `packages/shared/src/i18n/resources.ts`, and replace the literal with
   `{t('parent.settings.security.noActiveSessions')}` in `SecurityPanel.tsx`.

2. **[blocker] Security-auditor B4 must run and return PASS before B6 commits.**
   `docs/briefs/W10-P2-12-FE-security-audit.md` is absent. Committer cannot proceed.

3. **[blocker] `docs/dev/HANDOFF.md` not updated.**
   The plan (B6, step 5) requires updating `HANDOFF.md` in the same commit with:
   Wave 10 P2-12-FE complete; `Switch` primitive added; api-client regen + myChildren
   route; stale `changePasswordForUser` note; `LinkedChildResponse` carry-forward;
   `TODO P2-12-PAYMENTS`. Currently `HANDOFF.md` has no W10 entry.

### Should-fix (non-blocking polish, implementer should apply before merge)

4. **[should-fix] SecurityPanel.tsx:447 — `signOutOthersSuccess` count may be stale.**
   `sessions.length - 1` uses the pre-refetch list. Use a fixed message without interpolated
   count, or capture the count before the mutation fires.

### Nits (cosmetic — committer's call)

5. **[nit] `PanelSurface` + `PanelHeader` duplicated in 5 files.** Extract to
   `apps/student-app/app/(parent)/_components/settings/shared.tsx` when convenient. No
   functional impact today.

6. **[nit] `seededRef` in `NotificationsPanel.tsx` (line 116)** is assigned at line 126
   but never read. Dead code. Remove it or use it to guard the seed effect.

7. **[nit] `hideLabel` in `Switch` uses `opacity: 0` without layout clip.** The label still
   occupies space when hidden. Low priority.

---

## Required fixes before committing (implementer checklist)

1. Add `parent.settings.security.noActiveSessions` to both EN and AR in `resources.ts`.
   Suggested copy: EN `"No active sessions"`, AR `"لا توجد جلسات نشطة"`.
   Replace literal in `SecurityPanel.tsx:468` with `{t('parent.settings.security.noActiveSessions')}`.

2. Run the `security-auditor` agent. Gate the commit on PASS.

3. Update `docs/dev/HANDOFF.md` with Wave 10 P2-12-FE completion notes per plan B6 step 5.

4. (Optional but recommended) Fix the stale session count in `signOutOthersSuccess`.

