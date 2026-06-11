# Pipeline Brief — Marketing Landing Re-skin (apps/marketing-site → new design-system previews)

> **No formal user story.** There is no file in `user-stories/` for this work. **This task spec (the lead's re-skin request) IS the source of truth.** No FR-ID / BRD goal / phase applies — this is a visual re-skin of the public marketing surface, not a product feature. Where this brief and the spec conflict, the spec wins; where a `--lx-*` token and a preview's raw hex conflict, the **token** wins (CLAUDE.md: tokens only, never raw hex).

## Summary & traceability
- **Task (1 line):** Re-skin the entire `apps/marketing-site` landing (nav, hero phone-mock, feature cards, subject band, CTA, footer, and the four parent-value components) in **both /en and /ar** to match the current `design-system/preview/*.html` files, using `--lx-*` tokens and `getCopy(locale)` only — no new deps, no Tamagui, no new patterns, no architecture regression.
- **Source of truth:** this spec + the named `design-system/preview/*.html` files + `design-system/SKILL.md` + `design-system/colors_and_type.css`.
- **Surface:** public marketing site only (`apps/marketing-site`). No backend, no DB, no student/admin app.

> ## ⚠️ Critical finding — verify the premise before building (open question #1)
> The spec's stated premise is *"the marketing app still reflects the OLD design; `design-system/preview/*.html` holds the NEW design; re-skin the app to match."* **A file-level comparison on this branch does not bear that out.** Every current `apps/marketing-site` component already matches its named preview faithfully — including the AR variants. The prior "P1-11 pixel-alignment v2" pass (HANDOFF.md line 1066) explicitly re-aligned the Landing to these **same** `design-system/preview/*.html` cards in EN **and** AR, and PR #112's four components (HANDOFF line ~1010) were authored directly against these previews. So on this branch the app and the previews are **in lockstep**, not divergent.
>
> **What this means for the pipeline:** either (a) the design-system update the spec refers to (the "PR #113 Claude Design handoff bundle") **did not actually change these specific preview files** relative to what PR #112 consumed — in which case there is almost nothing to re-skin and the work collapses to the handful of genuine deltas listed below; or (b) the intended "new" previews live somewhere this branch does not yet have (a newer `colors_and_type.css` token set, updated preview HTML, or screenshots) and the branch needs to be rebased / the bundle re-applied **before** any frontend work starts. **The lead must confirm which world we are in before the planner schedules a frontend batch.** A `git diff origin/main -- design-system/` (and inspecting the PR #113 merge) will settle it definitively — the analyzer is read-only and cannot run git, so this is flagged, not resolved.
>
> The rest of this brief is written to be correct under **both** interpretations: it (1) enumerates the *genuine* preview-vs-app deltas that exist right now (the floor of work), and (2) gives per-surface acceptance criteria + handoffs that hold whatever the "new" previews turn out to contain.

---

## Business context & value
- **Audience:** parents (the buyers — product is parent-driven onboarding; students do not self-register). The landing must read as a kid-friendly *educational game world*, not an LMS/dashboard (SKILL.md rule).
- **Value:** a polished, on-brand, fully-bilingual (EN/AR, LTR/RTL) landing page increases parent trust and registration conversion. Re-skinning to the latest design keeps the public face consistent with the in-app design system.
- **Success measure (qualitative for a re-skin):** every listed surface visually matches its preview in both locales; voice/tone rules honored (playful, ≤6-word headlines, numbers always rendered, Title Case buttons); no PR #112 architecture regression; build + type-check + lint + the existing `marketing`/`marketing-mobile` Playwright projects stay green.

---

## Voice / tone / brand rules to enforce (from SKILL.md + spec)
- Playful, kid-friendly, second-person, encouraging. Marketing copy bolder but still short.
- **Headlines ≤ 6 words** (spec) — note SKILL.md Skill 3 says ≤ 8; **the spec's ≤6 is tighter and wins.** ⚠️ Current EN hero headline "An adventure game your kids will love — that teaches." is **8 words** → flag for copy review (open question #5).
- **Numbers always rendered** (never spelled), weight 800, `tabular-nums`. **Arabic-Indic numerals (٠–٩) in AR**, Latin digits kept for technical strings (email, `App Store`, brand `Learnexia`, `COPPA`/`CSV`).
- **Buttons in Title Case.** ⚠️ Current buttons use sentence case ("Start free", "Create parent account", "Log in", "Export CSV") — Title Case would be "Start Free", "Create Parent Account", "Log In". **This is a spec rule the current build does not satisfy** → flag for copy decision (open question #5); applies to both COPY.en and the visible button strings.
- Brand law: dark canvas; cards step lighter; 16px button radius (**but several previews use 12–14px on nav/CTA buttons — see token note**); soft shadows; glow reserved for reward/CTA; press scale 0.95/80ms; one primary action per screen.
- Emoji are semantic only (🔥 streak · ⭐ XP · 🏆 trophy · 🌟 etc.) — already respected.

---

## Affected surface (no new entities — pure presentation)
All work is in `apps/marketing-site`. **Read-only files** for this task per spec constraints: `app/[locale]/layout.tsx`, `middleware.ts`, `app/layout.tsx`, `next.config.ts`, the `prebuild` script — do **not** touch routing/middleware/static-params behavior.

| Surface | Component file(s) | Style file | Preview source(s) |
|---|---|---|---|
| Nav chrome | `app/[locale]/page.tsx` (inline `<header>`) + `LanguageSwitcher.tsx` | `app/page.module.css`, `LanguageSwitcher.module.css` | `web-nav.html`, `ar-web-nav.html` |
| Hero phone-mock | `_components/PhoneMockup.tsx` | `PhoneMockup.module.css` | `web-hero-phonemock.html` |
| Feature cards | `_components/FeaturesSection.tsx` | `FeaturesSection.module.css` | `web-feature-card.html` (+ `ar-web-features.html`) |
| Subject band | `_components/SubjectsBand.tsx` | `SubjectsBand.module.css` | `web-subject-band.html` |
| CTA banner | `_components/CTABanner.tsx` | `CTABanner.module.css` | `web-cta-banner.html`, `ar-web-cta.html` |
| Footer | `_components/SiteFooter.tsx` | `SiteFooter.module.css` | `web-footer.html` |
| BenefitsPanel | `_components/BenefitsPanel.tsx` | `BenefitsPanel.module.css` | `web-benefits-panel.html` |
| ActivityChart | `_components/ActivityChart.tsx` | `ActivityChart.module.css` | `web-activity-chart.html` |
| AITutorBubble | `_components/AITutorBubble.tsx` | `AITutorBubble.module.css` | `components-tutor.html`, `ar-tutor.html` |
| ChildCardPhone | `_components/ChildCardPhone.tsx` | `ChildCardPhone.module.css` (reuses `PhoneMockup.module.css` frame) | `mobile-child-card.html`, `ar-child-card.html` |
| Copy (all surfaces) | — | — | `lib/copy.ts` (`COPY.en` / `COPY.ar`) |
| Local tokens | — | — | `app/globals.css` (`:root` `--lx-*`) |

**All 14 named preview files were verified to exist** in `design-system/preview/`: `web-nav`, `ar-web-nav`, `web-hero-phonemock`, `web-feature-card`, `web-subject-band`, `web-cta-banner`, `ar-web-cta`, `web-footer`, `web-benefits-panel`, `web-activity-chart`, `components-tutor`, `ar-tutor`, `mobile-child-card`, `ar-child-card` (plus the referenced `ar-web-features`). ✅

---

## Per-surface acceptance criteria + concrete delta vs current build

> Deltas below are the result of a line-by-line read of each preview against its current component. Where I write **"matches — no change found,"** the current build already equals the preview on this branch; any *intended* "new design" change there is not present in the preview file and must come from open-question #1 (a rebased/updated preview), not from this brief.

### 1. Nav (`web-nav.html` / `ar-web-nav.html`)
**Genuine delta found:**
- **Brand lockup differs.** Preview = `logo-mark.svg` (32×32) **+ a text wordmark** `Learnexia` (weight 900, 18px; AR wordmark uses Cairo, pinned `dir="ltr"`). Current `page.tsx` = single full `logo.svg` image at 170×45, **no separate mark+text**. → Re-skin the brand block to mark + wordmark span; wordmark color `--lx-fg1`.
- **Nav links font-size:** EN preview 13px, **AR preview 14px**; current `.navLinks` is 14px for both. Minor — align EN to 13px (or confirm acceptable).
**AC:**
- [ ] Brand = `logo-mark.svg` 32px + "Learnexia" wordmark (900/18px), wordmark pinned LTR in AR.
- [ ] Links: How it works / Subjects / For schools / Pricing in `--lx-fg2`, hover `--lx-fg1`.
- [ ] Actions: `LanguageSwitcher` pill + outline "Log in" + filled "Start free" (`--lx-primary`, glow). Frosted sticky nav `rgba(15,23,42,0.85)` blur(20) + hairline `--lx-border-soft`. Unchanged — matches.
- [ ] AR: full RTL mirror, Tajawal links / Cairo buttons, `ar-web-nav` copy.

### 2. Hero phone-mock (`web-hero-phonemock.html`) — **PhoneMockup component**
**Genuine deltas found (preview vs `PhoneMockup.module.css`):**
| Aspect | Preview | Current | Action |
|---|---|---|---|
| Device frame radius | **32px** | `--lx-radius-device` = 36px | reconcile: set frame radius to 32 (or update `--lx-radius-device`) |
| Frame size | 220×440 (fixed) | 320 wide, `aspect-ratio 320/640` | current is a scaled-up but proportional render; confirm target size |
| Frame border | `6px solid #1a1a1a` | `8px solid #1a1a1a` | align bezel to 6px |
| Frame shadow | `0 32px 80px rgba(99,102,241,0.5)` | `0 40px 100px …0.5` | align to preview |
| Continue card bg | `rgba(0,0,0,0.3)`, radius **14px** | `rgba(0,0,0,0.22)`, radius `--lx-radius-card` (20) | align bg + radius (14 = an inner-card radius) |
| Subject tiles bg | `rgba(0,0,0,0.3)`, radius **12px** | `rgba(255,255,255,0.08)`, radius `--lx-radius-card` (20) | **notable** — tiles go dark-on-dark, smaller radius |
| Continue label color | `#FACC15` (XP yellow) | `--lx-accent` (#F59E0B) | align to `--lx-xp` |
| Progress fill | `linear-gradient(90deg,#22C55E,#FACC15)` | `--lx-secondary → --lx-xp` (same intent) | matches |
| Streak chip | `rgba(251,146,60,0.2)` bg, 10px text | `rgba(0,0,0,0.28)` bg, 13px | align bg tint + size |

**AC:**
- [ ] Phone frame, screen gradient (`165deg #A855F7→#4F46E5→card`), header (name + 🔥 streak chip), continue card, 2×2 subject grid (🧮🧪📖🇬🇧), bottom 🌟, and floating `+50 XP ⭐` (green) + `🏆 New badge!` (frosted) chips all match the preview's exact tints, radii, and sizes listed above.
- [ ] Remains decorative (`aria-hidden`); numbers rendered (streak `7`/`٧` from copy); AR streak digit Arabic-Indic.
- [ ] **`PhoneMockup.module.css` frame classes (`.stage/.phone/.screen`) stay reusable by `ChildCardPhone`** — do not break that contract.

### 3. Feature cards (`web-feature-card.html`)
**Delta:** none found — current `FeaturesSection` matches (card bg `--lx-card`, radius `--lx-radius-modal`=24, 48px icon tile radius 14, tone tints purple/orange/green at 15%, title 18/900, body 13/`--lx-fg2`). Grid 3→2→1 responsive.
**AC:**
- [ ] 6 cards, eyebrow "Why Learnexia" (`--lx-purple`), title, per-card tone tint matches preview; AR from `ar-web-features` + COPY.ar.

### 4. Subject band (`web-subject-band.html`)
**Delta:** none found — current `SubjectsBand` matches (4-up grid, card radius `--lx-radius-card`=20, icon tile radius 14 at 13% tone tint, name 18/900, topics 12/`--lx-fg3`, grade link in tone color). **4 subjects only (Math/Science/Arabic/English) — no Social Studies** ✅.
**AC:**
- [ ] 4 tiles, tones indigo/green/orange/purple, "Grade 1–6 →" / "الصف ١–٦ ←" in tone color. AR numerals Arabic-Indic.

### 5. CTA banner (`web-cta-banner.html` / `ar-web-cta.html`)
**Genuine deltas found:**
- Banner radius: preview **28px** (current uses `--lx-radius-screen`=28 ✅ matches).
- CTA button radius: preview **14px**, current `--lx-radius-button` (16). Minor reconcile (or keep 16 per brand-law "all buttons 16").
- Button height: preview 52px, current 60px. Align to 52.
- Title size: preview 28px, current 36px. **Notable** — align to 28px.
- Decorative 🌟: preview 200px opacity .15; current 280px. Align to 200px.
**AC:**
- [ ] Indigo→purple gradient banner, white button (`--lx-primary` text), 🌟 behind content, title/subtitle/button sizes per preview. AR mirrored via `ar-web-cta`.

### 6. Footer (`web-footer.html`)
**Delta:** none found — current matches (logo-mark 24–28px @ .7 opacity, `© 2026 Learnexia · Made for curious kids`, Privacy/Terms/Support links `--lx-fg3`). Note preview footer also shows an `العربية` link; current intentionally **dropped it** (lang switching now in nav — HANDOFF) — keep dropped.
**AC:**
- [ ] Two-column footer; copyright with AR year `٢٠٢٦`; links from copy; no separate language link (switcher owns that).

### 7. BenefitsPanel (`web-benefits-panel.html`)
**Delta:** none found — current matches (purple gradient `165deg #1E1B4B→#3B2C8F→#5B21B6` via `--lx-grad-benefits`, 🎮 60px glyph w/ XP-glow drop-shadow, 26px/900 heading, 3 rows w/ 34×34 tiles at `rgba(255,255,255,0.1)`, text `--lx-on-purple`).
**AC:**
- [ ] Gradient panel, glyph, heading ≤6 words?(`Set up once. Watch them learn forever.` = 7 — flag), 3 benefit rows; AR translation w/ same structure.

### 8. ActivityChart (`web-activity-chart.html`)
**Delta:** none found — current matches (dark card radius `--lx-radius-modal`=24, header title 16/800 + subtitle, inert "Export CSV" pill `--lx-indigo-200`, 7 bars Mon–Sun, default bar `card-soft→card`, Sunday highlighted `levelup` gradient w/ glow + indigo-200 value, values above bars `tabular-nums`). RTL reverses bar order.
**AC:**
- [ ] 7 bars w/ numeric values **always rendered** (AR Arabic-Indic ٤٥…١١٠), Sunday highlighted; bars track stays LTR-readable but column order mirrors in RTL; Export CSV inert/non-interactive.

### 9. AITutorBubble (`components-tutor.html` / `ar-tutor.html`)
**Delta:** none found — current matches (owl avatar 64px circle `135deg #A78BFA→#6366F1`, frosted bubble `rgba(15,23,42,0.7)` blur(20) radius 22 + tail corner, "LEXI · AI TUTOR" label `--lx-primary` uppercase, message w/ gold-highlighted word `tens`/`عشرات`, 3 reply chips `--lx-primary-soft`/`--lx-indigo-200`). RTL flips the tail corner via `border-end-start-radius`.
**AC:**
- [ ] Avatar (mascot-owl placeholder — **flag: placeholder art**), name label, message + 1 highlighted word, 3 decorative chips; AR uses Cairo/Tajawal + `ar-tutor` copy; tail corner flips in RTL.

### 10. ChildCardPhone (`mobile-child-card.html` / `ar-child-card.html`)
**Delta:** none found — current matches (reuses `PhoneMockup` frame; screen bg overridden to flat `--lx-bg`; inner card `--lx-bg-card-deep` #15161D radius 20; orange monogram avatar; name 18/900 + grade pill `--lx-primary-soft`/`--lx-indigo-200`; mono LTR-pinned email; 3 stats 🧠/⭐/🔥 w/ purple/xp/streak colors + `tabular-nums`; glowing green "Active today" dot; footer language + "View progress →"). AR: monogram `س`, grade `الصف ٣`, status text turns green, chevron flips, numerals Arabic-Indic.
**AC:**
- [ ] In-phone child card matches preview; numbers rendered (Lv 12 / 1,240 / 7d → ١٢ / ١٬٢٤٠ / ٧); email stays LTR mono in both locales; AR fully mirrored.

> **Note for the planner/reviewer:** because §§3,4,6,7,8,9,10 currently show **no delta**, if open-question #1 resolves to "the previews on this branch ARE the new design and didn't change," those surfaces need only a **verification pass** (pixel re-check EN+AR), not a rewrite. The real edit surface is concentrated in **§1 nav (brand lockup)**, **§2 phone-mock (multiple tint/radius/size deltas)**, **§5 CTA (sizes)**, plus the **voice/tone copy rules (Title Case buttons, ≤6-word headlines)** which apply across copy regardless of preview state.

---

## Token-reconciliation note (new `colors_and_type.css` vs PR #112 local `--lx-*` in `globals.css`)
The marketing `globals.css` `:root` **mirrors** the design-system token surface and adds **documented local vars** for things the canonical token set doesn't name. Comparison of the current `design-system/colors_and_type.css` against `apps/marketing-site/app/globals.css`:

**Canonical tokens — present & identical in both** (no action): all primaries, secondary/accent/danger/purple + softs, `--lx-xp/-glow`, `--lx-streak`, `--lx-gold`, the 3 named gradients, surfaces (`--lx-bg/-elevated/-card/-card-soft`), text `--lx-fg1/2/3`, borders, radii (`sm/button/card/modal/pill`), shadows (`soft/float/primary-glow`), full spacing scale, motion (`ease-out/spring/dur-fast/base`), font families.

**Local-only tokens added by PR #112** (not in `colors_and_type.css` — keep, they're documented; re-confirm none has since been promoted into the canonical token set on the new bundle):
- Surfaces/text: `--lx-bg-deep` (#0b1020 feature/subject bg), `--lx-fg4` (#64748b footer/caption), `--lx-bg-card-deep` (#15161d child-card).
- Borders: `--lx-border-soft` (rgba .05 nav/footer hairline).
- Purple/glow: `--lx-purple-soft`, `--lx-purple-glow` (hero radial), `--lx-success-glow`.
- Radii: `--lx-radius-device` (36) / `--lx-radius-screen` (28) phone frame — ⚠️ **note §2: the phone preview uses 32px, not 36; reconcile `--lx-radius-device` or the component.**
- Gradients/accents: `--lx-grad-benefits`, `--lx-grad-bar`, `--lx-grad-levelup-180`, `--lx-grad-avatar`, `--lx-indigo-200` (#a5b4fc), `--lx-on-purple`, `--lx-on-purple-tile`, `--lx-overlay-frost`, `--lx-border-frost`, `--lx-primary-chip-border`, `--lx-font-mono`.

**Reconciliation rule for the frontend agent (per spec):** when a preview shows a color/gradient with no canonical token, **first** check `design-system/colors_and_type.css` for a token name; only if absent, add a **documented local `--lx-*` var** to `globals.css` (matching the existing `/* local: … */` comment convention). **Do not introduce raw hex in component CSS.**

⚠️ **Open token risk (#1-dependent):** if the PR #113 bundle introduced **new token names or changed hex values** in `colors_and_type.css`, those changes are **not yet reflected** in `globals.css`. The frontend agent must diff the (post-rebase) `colors_and_type.css` against `globals.css` and pull any new/changed canonical tokens **before** restyling. On *this* branch the two are consistent, which is itself evidence for open-question #1.

---

## Handoff → db-migration
**None.** Pure front-end presentation re-skin. No entities, fields, schema, or relationships. Skip this stage entirely.

## Handoff → backend-feature
**None.** No commands/queries/endpoints/DTOs/validation. The marketing site is static/SSR-only; CTAs link to the student app via `NEXT_PUBLIC_APP_URL` (`REGISTER_URL`/`LOGIN_URL` in `lib/config.ts`) — unchanged. **No `api-tester` / `security-auditor` stage** is warranted (no auth, no user/child data, no uploads, no AI prompts, no secrets handled here).

## Handoff → designer (Design Spec — REQUIRED before frontend)
This is a UI surface → a **Design Spec** under `design-system/ui_kits/marketing/marketing-landing-reskin.md` should be produced first. The designer should:
- **Resolve open-question #1 inputs:** state, per surface, the exact pixel target by citing the preview file (and the `screenshots/web*`/`web-ar*` capture if one exists) as the canonical target; call out where the current build already matches vs where it diverges (use the delta tables above as the starting inventory and verify against the actual previews on the then-current branch).
- Produce per-surface EN **and** AR delta specs (mirror the prior "align-*.md" pattern referenced in HANDOFF) covering: exact tints/radii/sizes for nav brand lockup, phone-mock (the multi-row delta table in §2), CTA sizes, and a verification checklist for the no-delta surfaces.
- Encode the **voice/tone decisions** (Title Case buttons, ≤6-word headlines) once they're settled (open question #5) so the frontend has exact copy strings.
- Honor brand law: tokens only, 16px buttons (note the 12–14px nav/CTA exceptions and decide whether to standardize), press 0.95/80ms, dark canvas, glow only on CTA/reward.

## Handoff → frontend
- **Stack constraints (hard):** plain React + CSS Modules; `--lx-*` tokens only (no raw hex in components); copy via `getCopy(locale)` from `lib/copy.ts`; **no Tamagui, no new dependency, no new abstraction/pattern** (CLAUDE.md rule 8 — if any surface seems to need one, STOP and ask the lead).
- **Do NOT touch:** `app/[locale]/layout.tsx`, `middleware.ts`, `app/layout.tsx`, `next.config.ts`, the `prebuild` `.next` clean script, or add `generateStaticParams` (crashes with `headers()` → keep routes fully dynamic). Keep the EN/AR `LanguageSwitcher` (plain `<a>` full-document nav) and per-locale `<html lang dir>` intact.
- **Phone-mock = `PhoneMockup` component** — update it to `web-hero-phonemock.html`; keep its `.stage/.phone/.screen` frame classes intact because `ChildCardPhone` reuses them.
- **Copy:** any copy change updates **both** `COPY.en` and `COPY.ar`; AR strings come from the AR previews; **Arabic-Indic numerals** in `ar`; keep Latin for technical strings (email, `App Store`, `COPPA`, `CSV`, brand).
- **Tokens:** reconcile new colors/gradients against `colors_and_type.css` first; else add a documented local `--lx-*` var to `globals.css` (existing comment convention). Never raw hex.
- **Scope by surface:** the genuine edits are nav brand lockup (§1), phone-mock tints/radii/sizes (§2), CTA sizes (§5), and copy voice/tone (§§ across). The other six surfaces are verification passes unless open-question #1 surfaces new previews.

## Handoff → frontend-e2e-tester
- The student-app harness rule about "web PWA" applies here to the **marketing** projects: `tests/e2e` already has `marketing` + `marketing-mobile` Playwright projects at `:3002` (HANDOFF). After the frontend batch, run them.
- Cover: both `/en` (LTR) and `/ar` (RTL); `<html lang dir>` correct per locale; `LanguageSwitcher` round-trips and middleware re-runs; all 10 surfaces render; numbers rendered (and Arabic-Indic in AR); CTA links point at `REGISTER_URL`/`LOGIN_URL`; inert controls (Export CSV, tutor chips) are non-navigating; reduced-motion respected. Keep student-app `:8081` untouched.

## Handoff → reviewer → committer
- **Reviewer** gates against the per-surface AC above + CONVENTIONS + the spec constraints (tokens-only, no new deps/patterns, routing untouched), plus the e2e results. Confirm EN **and** AR for each surface and that no `generateStaticParams`/middleware regression slipped in.
- **Committer** (only after PASS): per-story branch (suggest `feat/marketing-landing-reskin`), conventional message, push + open PR; **update `docs/dev/HANDOFF.md`** in the same PR (note what re-skinned, any token reconciliation, the resolution of open-question #1). Never on `main`.

---

## Open questions / assumptions / risks (flag, don't guess)
1. **🚩 BLOCKER — the premise itself.** On this branch the app already matches every listed preview (incl. AR); the prior alignment pass (HANDOFF 1066) already targeted these same previews. **Did the "PR #113 Claude Design handoff bundle" actually change these specific `design-system/preview/*.html` files and/or `colors_and_type.css`, and is that change present on this branch?** If yes → where (a `git diff origin/main -- design-system/` will show it)? If the bundle didn't touch these files, the re-skin collapses to the small deltas in §§1/2/5 + copy rules. **The lead must confirm before the planner schedules the frontend batch.** (Analyzer is read-only — cannot run git to settle this.)
2. **Phone-mock frame radius:** preview = 32px, current `--lx-radius-device` = 36px. Standardize the token to 32, or override per-component? (Affects only the phone frame.)
3. **Button radius brand exception:** nav buttons (12px) and CTA button (14px) in the previews contradict brand-law "all buttons 16px." Standardize to 16 (brand) or honor the previews' 12/14? Need a design call (designer/lead).
4. **Nav links font-size EN vs AR:** EN preview 13px, AR 14px, current 14px both. Match per-locale, or keep 14 uniform?
5. **Voice/tone vs current copy:** spec mandates **Title Case buttons** and **≤6-word headlines**, but the current EN copy uses sentence-case buttons ("Start free", "Export CSV", "Create parent account") and an 8-word hero headline (and a 7-word BenefitsPanel heading). Do we rewrite copy to satisfy the rules (and re-translate AR to match), or treat the current strings as approved exceptions? This changes both `COPY.en` and `COPY.ar`.
6. **Footer `العربية` link:** preview still shows it; current build dropped it (switcher in nav). Assumption: keep dropped. Confirm.
7. **Mascot owl is a placeholder** (SKILL.md caveat) — the AITutorBubble avatar uses `mascot-owl.svg`. Re-skin keeps the placeholder; real character art is out of scope. Confirm acceptable for the public landing.
8. **Scope boundary:** assume only the 10 named surfaces + copy + local tokens are in scope; `lib/config.ts` URLs, routing, build scripts, and the student/admin apps are out of scope.

**Assumptions (proceeding unless told otherwise):** (A) tokens-only, no raw hex, no new deps/patterns; (B) routing/middleware/static-dynamic behavior frozen; (C) both locales must reach parity for every surface; (D) the e2e `marketing`/`marketing-mobile` projects are the regression gate.

---

## Recommended pipeline order (first cut — the `planner` finalizes)
0. **Lead resolves open-question #1** (git-diff the design-system bundle vs origin/main; confirm whether/where previews changed) — **gate before any build.**
1. **designer** → Design Spec at `design-system/ui_kits/marketing/marketing-landing-reskin.md` (per-surface EN+AR deltas + verification checklist + settled voice/tone copy). Resolve OQ #2–#7 here with the lead.
2. **frontend** (single batch — all surfaces share `globals.css`/`copy.ts`, so serialize within one agent to avoid shared-file clobber): apply token reconciliation in `globals.css` first, then the per-surface edits (nav brand lockup, phone-mock, CTA, copy), then verify the no-delta surfaces.
3. **frontend-e2e-tester** → run `marketing` + `marketing-mobile` Playwright projects (EN+AR).
4. **reviewer** → gate against AC + constraints + e2e.
5. **committer** → `feat/marketing-landing-reskin`, PR + HANDOFF update.

*No db-migration, backend-feature, api-tester, or security-auditor stages — pure presentation re-skin with no backend/auth/data surface.*
