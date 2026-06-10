# Pipeline Brief — Marketing site: 4 design-system components + Arabic/RTL

## Summary & traceability

**1-line task:** Add four static/presentational design-system components to the marketing site (`apps/marketing-site`) and add full Arabic locale + RTL support, grounded 1:1 in named `design-system/preview/*.html` source files.

- **User story:** none — this is an ad-hoc marketing enhancement. **THIS BRIEF + the task spec are the source of truth.** There is no `user-stories/<phase>/*.md` file and no `tasks/Frontend/...` file for this work; downstream agents must treat this brief as the spec rather than looking for a story ID.
- **Loosely related prior work:** the marketing site itself was built under **P1-11-FE-12** (the landing page); see `apps/marketing-site/app/page.tsx` header comment. This task extends that surface. No FR-ID / BRD goal mapping applies (marketing collateral, not a product feature).
- **Stack:** Next.js 15 App Router, **plain React + CSS Modules (NOT Tamagui)**, `--lx-*` CSS custom properties, copy centralized in `lib/copy.ts`. Confirmed in `apps/marketing-site/next.config.ts` (no `withTamagui`) and `CLAUDE.md`.
- **Product-decision alignment:** 4 subjects only (Math/Science/Arabic/English — already reflected in `lib/copy.ts`); no teacher role; parent-facing marketing. No conflicts.

## Business context & value

The marketing site is the top-of-funnel parent-acquisition surface (CTAs deep-link to the Expo student-app parent register/login per `lib/config.ts`). This task:
- **Richer parent-facing proof** — a Benefits Panel and an Activity Chart give parents concrete "what you get" visuals, and an AI-Tutor bubble + in-phone Child Card show the actual product experience as screenshots.
- **Arabic reach** — Learnexia targets Arabic-speaking students/parents; the marketing site is currently English-only while the entire app and design-system are Arabic-first. Adding `ar` + RTL closes the single biggest gap between the marketing surface and the product.

Success = the four components render pixel-faithful to their named preview files in both locales, the site is fully navigable in Arabic with correct RTL mirroring, and a language switcher lets a visitor toggle locales. All four components are **static/presentational** — dummy data, no API, no auth.

## Acceptance criteria (testable)

### Item 1 — Web · Benefits Panel (`app/_components/BenefitsPanel.tsx` + `.module.css`)
Source: `design-system/preview/web-benefits-panel.html` (verified exists).
- [ ] Full-width section rendered in `app/page.tsx` (placement among the below-the-fold sections, per designer).
- [ ] Purple gradient panel (`linear-gradient(165deg,#1E1B4B → #3B2C8F → #5B21B6)`) expressed via `--lx-*` tokens where one exists, otherwise a documented local CSS var (see "tokens" note).
- [ ] Large 🎮 glyph, a bold heading, and a 3-item benefit list, each row = a 34px rounded icon tile (✨ / 📊 / 🛡️) + text.
- [ ] Copy ("Set up once. Watch them learn forever." + the 3 benefit lines) added to `lib/copy.ts` in **both** `en` + `ar`.
- [ ] Responsive (stacks gracefully at mobile widths) and RTL-correct (icon tile leads, text follows; `inset-inline` not `left/right`).

### Item 2 — Web · Activity Chart (`app/_components/ActivityChart.tsx` + `.module.css`)
Source: `design-system/preview/web-activity-chart.html` (verified exists).
- [ ] Card (`#1E293B`, radius 24, hairline border) with header (title "Daily activity" + subtitle "XP earned per day") and an "Export CSV" ghost button (decorative — no handler / no download; **see open question Q5**).
- [ ] 7 vertical bars (Mon–Sun) with **static dummy values** `[45, 80, 30, 95, 50, 70, 110]`, each bar showing its value label above and the day label below; Sunday is the highlighted bar (purple→indigo gradient + glow).
- [ ] No API call, no auth, no data fetching — values are a hardcoded const in the component (or in `lib/copy.ts` if day labels are localized).
- [ ] Day labels + header copy added to `lib/copy.ts` en + ar; numeric values use `tabular-nums`. **Open question Q4:** Arabic numerals (Arabic-Indic ١٢٣ vs Western 123) — the `ar-child-card` preview uses Arabic-Indic; decide per designer.
- [ ] Bars read bottom-aligned in RTL with the order mirrored (Sun on the left in RTL), responsive within `max-width:920px`.

### Item 3 — AI Tutor Bubble (`app/_components/AITutorBubble.tsx` + `.module.css`)
Sources: `design-system/preview/components-tutor.html` (LTR, verified) + `design-system/preview/ar-tutor.html` (RTL, verified).
- [ ] Standalone component: owl mascot avatar (64px gradient circle, `mascot-owl.svg`) + a frosted glass bubble (`rgba(15,23,42,0.7)` + `backdrop-filter: blur(20px)`).
- [ ] Bubble has the asymmetric tail: **bottom-left** radius=4 in LTR, **bottom-right** radius=4 in RTL (the previews differ exactly here — must flip with `dir`).
- [ ] "Lexi · AI Tutor" name label (uppercase, primary color), the tutor message with an inline highlighted word (`--lx-xp` gold on "tens"/"عشرات"), and 3 suggestion chips ("Yes, show me / Give a hint / Skip").
- [ ] All strings (name, message, 3 chips) in `lib/copy.ts` en + ar. Mascot asset path resolves in the marketing site (verify `mascot-owl.svg` is reachable — **open question Q6**: the previews reference `../assets/mascot-owl.svg`; the marketing site serves from `/public/assets`, confirm the owl SVG is present there or add it).
- [ ] Responsive; chips wrap; RTL mirrors avatar/bubble order.

### Item 4 — Mobile · Child Card inside a phone frame (`app/_components/...` new component)
Sources: `design-system/preview/mobile-child-card.html` (LTR, verified) + `design-system/preview/ar-child-card.html` (RTL, verified).
- [ ] The child card is recreated **as an in-app screen inside one phone-mock device frame**, reusing the **existing `PhoneMockup` pattern + `PhoneMockup.module.css`** (device bezel, screen, tilt/shadow) so it reads as a screenshot, **NOT a flat web card**. Designer decides whether to extend `PhoneMockup` (e.g. a `variant`/`children` prop) or add a sibling phone-frame component — **flag as Q7** (do not invent an abstraction without lead approval per CLAUDE.md rule 8).
- [ ] Inside the frame: avatar monogram circle (orange, "S"/"س"), name + grade pill, email (mono, LTR even in RTL — note the AR preview pins `direction:ltr` on the email), a stats row (🧠 Lv 12 · ⭐ 1,240 · 🔥 7d) + "Active today" status dot, and a footer (Language: 🇬🇧 English / 🇸🇦 العربية + "View progress →"/"عرض التقدم ←").
- [ ] **Static dummy data** only. Chevron flips `›` (LTR) → `‹` (RTL); footer arrow flips `→` → `←`.
- [ ] All strings in `lib/copy.ts` en + ar; rendered somewhere in `page.tsx` (placement per designer).

### Item 5 — Arabic site + locale routing + language switcher
Sources of truth: `design-system/preview/_base-ar.css`, `ar-web-nav.html`, and the `ar-web-*` kit (`ar-web-features.html` etc., all verified). Plus the existing `[dir='rtl']` block already in `app/globals.css`.
- [ ] An `ar` locale exists end-to-end: visiting the Arabic site sets `<html lang="ar" dir="rtl">` and the English site `<html lang="en" dir="ltr">`. Today `layout.tsx` hardcodes `lang="en" dir="ltr"` (line 22) — this must become per-locale.
- [ ] `lib/copy.ts` is restructured to carry **both** `en` and `ar` for **all** existing copy (nav, hero, phone, chips, features, subjects, cta, footer) **and** all new component copy. The Arabic translations for the existing sections already exist in the design-system AR kit (`ar-web-nav.html`, `ar-web-features.html`, `ar-web-cta.html`, etc.) — use them as the source of Arabic wording rather than machine-translating.
- [ ] A **language switcher** matching `ar-web-nav.html` (the nav shows brand pinned LTR via `dir="ltr"` on the wordmark; Arabic nav links: كيف يعمل / المواد / للمدارس / الأسعار; buttons تسجيل الدخول / ابدأ مجاناً). Switcher placement per designer (the existing footer already has an `العربية` link in `copy.footer.links.arabic` — that is the current stub entry point).
- [ ] RTL correctness across **all** sections (existing + new): use logical properties (`inset-inline`, `margin-inline`, `padding-inline`, `text-align: start/end`) — the codebase already uses `inset-inline-*` in `PhoneMockup.module.css`, follow that. Arrows/chevrons that imply direction flip; email/brand stay LTR.
- [ ] Fonts: Arabic uses Cairo (display) + Tajawal (body) — already wired via `[dir='rtl']` font-var swap in `globals.css` and the `@font-face` declarations; verify the weights used by the new components are among the self-hosted weights.
- [ ] The locale strategy (App-Router routing vs. a context/cookie toggle, and whether an i18n library is introduced) is **the biggest open design decision — see Q1/Q2/Q3.** Do NOT introduce an i18n library or a new routing pattern without lead approval (CLAUDE.md rule 8).

## Affected modules & data

- **Module:** `apps/marketing-site` only. No backend, no DB, no entities, no migrations, no API. This is a presentational front-end change.
- **New files (expected):** `app/_components/BenefitsPanel.tsx` (+`.module.css`), `app/_components/ActivityChart.tsx` (+`.module.css`), `app/_components/AITutorBubble.tsx` (+`.module.css`), the phone-framed Child Card component (+`.module.css`), a language-switcher component (+`.module.css`), and — depending on the chosen locale strategy — possibly `app/[locale]/...` route segments and/or a small locale helper module.
- **Modified files:** `app/page.tsx` (render the new web components), `app/layout.tsx` (per-locale `lang`/`dir`), `lib/copy.ts` (restructure to en+ar, add all new copy). Possibly `next.config.ts` (only if the chosen strategy needs it — flag, don't assume), `public/assets/` (mascot owl SVG if absent).
- **No new entities/fields/relationships.**

## Handoff → db-migration
**Not applicable.** No database work. Skip this agent entirely.

## Handoff → backend-feature
**Not applicable.** No commands/queries/endpoints/DTOs/validation. All four components are static; the "Export CSV" button is decorative (no endpoint). Skip this agent.

## Handoff → designer (REQUIRED — runs first)
Produce a Design Spec at `design-system/ui_kits/marketing/marketing-components-ar.md` covering:
1. **Per-component visual spec** mapped to the exact preview file (paths below), translating each preview's inline styles into CSS-Module rules using `--lx-*` tokens. Call out every color/gradient/radius/shadow that has **no** existing `--lx-*` token (e.g. the benefits-panel gradient stops `#1E1B4B/#3B2C8F/#5B21B6`, the chart bar gradient `#334155→#1E293B`, slate label colors `#64748B/#94A3B8`) and decide: reuse an existing token, or add a documented local `--lx-*` var to `globals.css` (the file already documents several "local" vars — follow that convention).
2. **RTL deltas** per component — exactly what flips (bubble tail corner, chevron/arrow glyphs, bar order, email/brand LTR pinning) referencing the `ar-*` previews.
3. **Phone-frame decision** for the Child Card (Q7): recommend extend-`PhoneMockup` vs. new sibling, with rationale; if it implies a reusable pattern, name it for the lead.
4. **Locale-strategy recommendation** (Q1–Q3): pick one of the options below and justify it; specify language-switcher placement + visual (match `ar-web-nav.html`).
5. **Placement** of the new web components within `page.tsx`'s section flow.
6. Responsive breakpoints (follow the existing `FeaturesSection` pattern: 3-col @1024, 2-col @768, 1-col @390).

**Verified design-system source files (each confirmed to exist):**
| Component | LTR source | RTL source |
|---|---|---|
| Benefits Panel | `design-system/preview/web-benefits-panel.html` | (no AR-specific file — mirror via logical props + ar copy) |
| Activity Chart | `design-system/preview/web-activity-chart.html` | (no AR-specific file — mirror via logical props + ar copy) |
| AI Tutor Bubble | `design-system/preview/components-tutor.html` | `design-system/preview/ar-tutor.html` |
| Child Card (phone) | `design-system/preview/mobile-child-card.html` | `design-system/preview/ar-child-card.html` |
| Arabic site / nav | `design-system/preview/_base-ar.css` + `ar-web-nav.html` | + `ar-web-*` kit (features/cta/etc.) |

## Handoff → frontend (implementers)
- Build in **plain React + CSS Modules**, tokens only, copy from `lib/copy.ts`. Mirror the existing section-component shape (`FeaturesSection.tsx` + `.module.css` + copy slice) exactly — see `apps/marketing-site/app/_components/FeaturesSection.tsx` as the reference.
- Reuse `PhoneMockup.module.css` for Item 4's device frame per the designer's Q7 decision.
- Restructure `lib/copy.ts`: the current shape is a flat `LANDING_COPY` object (English only). Move to a locale-keyed shape (e.g. `COPY.en` / `COPY.ar`) or a per-locale lookup; **the exact shape depends on the locale strategy (Q1)** — do not start `lib/copy.ts` before the strategy is locked.
- `app/layout.tsx` line 22 currently hardcodes `<html lang="en" dir="ltr">` — make it locale-driven.
- Use logical CSS properties throughout (the codebase already does, e.g. `PhoneMockup.module.css` `inset-inline-start/end`).
- **No new design pattern, no i18n library, no new routing pattern without lead approval** (CLAUDE.md rule 8) — if the locked strategy needs any of these, that approval must come via the open questions below before this batch starts.
- Run `pnpm --filter @learnexia/marketing-site type-check` and `lint` before handing to the e2e tester.

## Handoff → frontend-e2e-tester
- The marketing site runs on **Next.js port 3002** (`next dev -p 3002`), **NOT** the student-app Expo `:8081` harness. The existing `tests/e2e` Playwright harness targets `localhost:8081` (student app) — confirm whether it can be pointed at `:3002` or whether a marketing-site-specific spec/baseURL is needed (**open question Q8**).
- Suggested checks: both locales render `<html lang dir>` correctly; the 4 components render with correct text per locale; the language switcher toggles locale and persists (per the chosen strategy — URL segment vs cookie); RTL mirroring is visually correct (bubble tail, chevrons, bar order); responsive layout at mobile width; no console errors; "Export CSV" is inert (no navigation/crash).
- Selectors: prefer `data-testid` / roles — Arabic copy makes text selectors brittle. Frontend should add `data-testid`s to the new components and the switcher.

## Open questions / assumptions / risks (for the lead)

**Q1 — Locale-routing strategy (BIGGEST decision; needs a lead call before frontend starts).** Three concrete options:
- **(A) App-Router `[locale]` path segments** — `app/[locale]/page.tsx` with `en`/`ar` (+ optional middleware redirect from `/`). SEO-friendly (distinct URLs, `hreflang`), idiomatic Next 15, no new dependency. Cost: restructures the route tree (move `page.tsx` under `[locale]`), each component reads `params.locale`. **Recommended** if SEO/shareable Arabic URLs matter (likely for marketing).
- **(B) Cookie/context toggle, single route** — a client `LocaleProvider` (React context) + a cookie; switcher flips it, `layout.tsx` reads it to set `lang/dir`. Minimal file churn, no route restructure. Cost: single URL for both locales (weaker SEO), and App-Router server components make reading a client toggle awkward (you'd lean on a cookie read in the layout).
- **(C) Next.js built-in i18n** — note the App Router **dropped** the legacy `i18n` config from `next.config` (that was Pages-Router only); the official App-Router answer is essentially option A. So "built-in routing" ≈ option A.
- **Recommendation: Option A** (path segments), no library. It is the idiomatic App-Router approach, gives real Arabic URLs, and avoids a dependency.

**Q2 — i18n library vs. keep `lib/copy.ts`?** The site has ~8 small copy sections; the student app uses `react-i18next` but that's a separate app. **Recommendation: keep the typed `lib/copy.ts` approach** (just locale-keyed) — it matches the existing marketing pattern and the admin app's `strings.ts` pattern, adds no dependency, and the copy volume is tiny. Introducing `next-intl`/`react-i18next` here would be a new dependency + pattern → **CLAUDE.md rule 8 requires lead approval; flagging rather than deciding.**

**Q3 — Language-switcher placement & shape.** `ar-web-nav.html` shows the nav layout but not an explicit EN/AR toggle control; the current footer has an `العربية` link stub (`copy.footer.links.arabic`). Options: a compact toggle in the top nav (`navActions`, next to Log in/Start free) vs. keep it in the footer. **Recommendation: top-nav toggle** (discoverable), with the footer link removed or repurposed — designer to finalize the visual against `ar-web-nav.html`.

**Q4 — Arabic numerals in the Activity Chart & Child Card.** `ar-child-card.html` uses Arabic-Indic numerals (المستوى ١٢ / ١٬٢٤٠ / ٧). Decide whether the AR Activity Chart bar values and child-card stats render Arabic-Indic (matches the preview) or Western digits. Assumption: **match the preview (Arabic-Indic in `ar`)** unless the lead says otherwise — this means the numbers live in `copy.ar` as strings, not computed.

**Q5 — "Export CSV" button behaviour.** The chart preview shows an Export CSV button. Task says no API/auth. Assumption: **render it as a decorative/disabled affordance** (no handler, or a no-op). Confirm the lead doesn't want it hidden entirely.

**Q6 — Mascot owl asset.** The tutor previews reference `../assets/mascot-owl.svg` (design-system relative). The marketing site serves static assets from `public/` (logo at `/assets/logo.svg`). Verify `public/assets/mascot-owl.svg` exists in the marketing site; if absent, the frontend agent must copy it from `design-system/assets/` (flag — do not assume it's already there).

**Q7 — Phone-frame reuse for the Child Card.** Reuse `PhoneMockup.module.css` per the task. The existing `PhoneMockup.tsx` is a fixed-content decorative component (hardcoded subjects grid). To render a *different* in-phone screen (the child card), either (a) generalize `PhoneMockup` to accept `children`/a `variant`, or (b) add a sibling component that imports the same `.module.css` frame classes. Option (b) avoids changing the live hero. **This is a potential "pattern" decision → needs designer recommendation + lead awareness per CLAUDE.md rule 8.** Recommendation: (b) a thin sibling that reuses the frame CSS, keeping the hero `PhoneMockup` untouched.

**Q8 — E2E harness target.** The Playwright harness (`tests/e2e`) is wired to the student-app Expo `:8081`. The marketing site is Next.js `:3002`. Confirm whether the e2e stage should (a) extend the existing harness with a marketing project/baseURL, or (b) run a lighter check. Flag for the lead.

**Risks:**
- **Route restructure churn (if Option A):** moving `page.tsx` under `app/[locale]/` touches imports and the e2e/build; low risk but should be a single clean batch.
- **RTL regressions in existing sections:** adding `ar` exercises RTL across components that were built English-only; some may use physical `left/right` (audit needed). `globals.css` already has the `[dir='rtl']` font swap, which de-risks fonts.
- **Copy-shape refactor:** every existing component reads `LANDING_COPY as C`; restructuring to locale-keyed copy touches all current `_components` imports. Mechanical but broad — keep it in one batch.
- **Pixel fidelity:** previews use raw hex inline; mapping to tokens may shift a shade. Designer must reconcile token-vs-literal explicitly.

## Recommended pipeline order (first cut — `planner` finalizes)

1. **Lead resolves Q1–Q3 (and ideally Q4–Q8)** — locale strategy + library + switcher placement gate everything. Frontend cannot start `lib/copy.ts`/`layout.tsx` until Q1 is locked.
2. **designer** — Design Spec at `design-system/ui_kits/marketing/marketing-components-ar.md` (all 4 components + locale strategy + switcher + phone-frame decision).
3. **frontend (Batch 1 — locale foundation):** restructure `lib/copy.ts` to en+ar (port existing AR copy from the design-system AR kit), wire per-locale `layout.tsx` (`lang`/`dir`), implement the chosen routing strategy, add the language switcher. Translate existing copy.
4. **frontend (Batch 2 — the 4 components):** BenefitsPanel, ActivityChart, AITutorBubble, Child-Card-in-phone — independent of each other, can be built in parallel; all depend on Batch 1's copy shape. Render web items in `page.tsx`.
   - (Batches 1 and 2 are sequential because Batch 2 consumes the Batch-1 copy/locale shape; the 4 components within Batch 2 are mutually parallel.)
5. **frontend-e2e-tester** — both locales, RTL mirroring, switcher, responsive, inert Export button (pending Q8 on harness target).
6. **reviewer** — gate against this brief's acceptance criteria + CONVENTIONS. No `security-auditor` needed (no auth/data/upload/AI/secrets/payments).
7. **committer** — after reviewer PASS, on `feat/marketing-components-ar`, PR (do not merge).

No `db-migration`, `backend-feature`, or `api-tester` stages — there is no backend surface.
