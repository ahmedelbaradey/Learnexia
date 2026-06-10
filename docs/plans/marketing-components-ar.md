# Execution Plan — marketing-components-ar
## Marketing site: 4 design-system components + Arabic/RTL

---

## Source

| Artifact | Path |
|---|---|
| Pipeline Brief | `docs/briefs/marketing-components-ar.md` |
| No story file | Ad-hoc enhancement — brief is the spec |
| No task file | Ad-hoc enhancement — brief is the spec |
| Reference component | `apps/marketing-site/app/_components/FeaturesSection.tsx` |
| Locale foundation target | `apps/marketing-site/app/layout.tsx`, `lib/copy.ts` |
| Phone frame source | `apps/marketing-site/app/_components/PhoneMockup.module.css` |
| E2E harness | `tests/e2e/playwright.config.ts` (currently targets `:8081`) |
| Design-system sources | `design-system/preview/web-benefits-panel.html`, `web-activity-chart.html`, `components-tutor.html`, `ar-tutor.html`, `mobile-child-card.html`, `ar-child-card.html`, `ar-web-nav.html`, `_base-ar.css`, `ar-web-*` |

**Lead-resolved decisions baked into this plan:**
- Q1: App-Router `app/[locale]/` path segments; middleware redirect `/` → default locale. No i18n library.
- Q2: Keep `lib/copy.ts`; restructure to `COPY.en` / `COPY.ar` shape.
- Q3: Top-nav EN/AR toggle matching `ar-web-nav.html`; footer `العربية` stub removed/repurposed.
- Q4: Arabic-Indic numerals (١٢٣) in `copy.ar` for Activity Chart values and Child Card stats.
- Q5: "Export CSV" button is decorative/inert — no handler.
- Q6: `mascot-owl.svg` is absent from `public/assets/`; frontend agent copies it from `design-system/assets/mascot-owl.svg`.
- Q7: New sibling phone-frame component that reuses `PhoneMockup.module.css` frame classes; live hero `PhoneMockup` untouched.
- Q8: Extend the Playwright harness with a marketing-site project/baseURL at `http://localhost:3002` (separate from `:8081`).

---

## Task inventory

| ID | Stack | Summary | Rough est | Depends on |
|---|---|---|---|---|
| DS-01 | designer | Design Spec: per-component visual spec, token mappings, RTL deltas, phone-frame decision, locale-strategy, switcher placement, section flow, breakpoints | 1–2 h | — |
| FE-B1-01 | frontend | Restructure route tree: create `app/[locale]/` directory; move `page.tsx`, `page.module.css`, `layout.tsx` logic under the locale segment; update all existing component imports | 1.5 h | DS-01 |
| FE-B1-02 | frontend | Create Next.js middleware (`middleware.ts` at project root) that redirects bare `/` to `/en` (default) and validates the `[locale]` param | 0.5 h | DS-01 |
| FE-B1-03 | frontend | Restructure `lib/copy.ts`: rename `LANDING_COPY` → `COPY.en`; add `COPY.ar` with Arabic translations for all existing copy sections (nav, hero, phone, chips, features, subjects, cta, footer) ported from the design-system AR kit (`ar-web-nav.html`, `ar-web-features.html`, `ar-web-cta.html`, etc.); export a `getCopy(locale)` helper | 2 h | DS-01 |
| FE-B1-04 | frontend | Update per-locale `app/[locale]/layout.tsx`: read `params.locale`; set `<html lang={locale} dir={locale === 'ar' ? 'rtl' : 'ltr'}>`. Update `metadata` to be locale-aware. Remove hardcoded `lang="en" dir="ltr"` from current `layout.tsx` | 0.5 h | FE-B1-01, FE-B1-03 |
| FE-B1-05 | frontend | Implement top-nav language switcher component (`app/_components/LanguageSwitcher.tsx` + `.module.css`) matching `ar-web-nav.html`; remove/repurpose the footer `العربية` stub from `copy.footer.links.arabic`; add `data-testid="lang-switcher"` | 1 h | FE-B1-03, FE-B1-04 |
| FE-B1-06 | frontend | Update all existing section components (`page.tsx`, `FeaturesSection`, `SubjectsBand`, `CTABanner`, `SiteFooter`, `PhoneMockup`) to consume `getCopy(locale)` instead of the old `LANDING_COPY as C` import; audit each for physical `left/right` usages and convert to logical properties | 1.5 h | FE-B1-03, FE-B1-04 |
| FE-B2-01 | frontend | Build `BenefitsPanel.tsx` + `.module.css` per Design Spec: purple-gradient panel, 🎮 glyph, heading, 3-item benefit list, icon tiles; copy from `COPY[locale].benefits`; RTL-correct via logical props; add `data-testid="benefits-panel"`; render in `page.tsx` | 2 h | FE-B1-03, FE-B1-04 |
| FE-B2-02 | frontend | Build `ActivityChart.tsx` + `.module.css` per Design Spec: dark card, header + decorative "Export CSV" button (inert), 7 vertical bars with static values `[45,80,30,95,50,70,110]`, Sunday highlighted, day labels from `COPY[locale].activityChart`; Arabic-Indic values in `copy.ar`; RTL bar order via CSS logical/flex-direction; `data-testid="activity-chart"`; render in `page.tsx` | 2.5 h | FE-B1-03, FE-B1-04 |
| FE-B2-03 | frontend | Copy `design-system/assets/mascot-owl.svg` → `public/assets/mascot-owl.svg`. Build `AITutorBubble.tsx` + `.module.css` per Design Spec: owl avatar, frosted-glass bubble, asymmetric tail (bottom-left LTR / bottom-right RTL), name label, message with inline highlighted word, 3 suggestion chips; copy from `COPY[locale].tutorBubble`; `data-testid="ai-tutor-bubble"`; render in `page.tsx` | 2 h | FE-B1-03, FE-B1-04 |
| FE-B2-04 | frontend | Build `ChildCardPhone.tsx` + `.module.css` per Design Spec (sibling component reusing `PhoneMockup.module.css` frame classes, live `PhoneMockup` untouched): phone frame wrapping a child-card screen; avatar monogram, name + grade pill, email (LTR-pinned in RTL), stats row, status dot, footer with language flags + `View progress →` / `‹ ←` flips; copy from `COPY[locale].childCard`; `data-testid="child-card-phone"`; render in `page.tsx` | 2.5 h | FE-B1-03, FE-B1-04 |
| E2E-01 | frontend-e2e-tester | Extend `tests/e2e/playwright.config.ts` with a `marketing` Playwright project pointing to `baseURL: http://localhost:3002`; add `webServer` entry for `pnpm --filter @learnexia/marketing-site dev` on port 3002 | 0.5 h | FE-B2-01–04 |
| E2E-02 | frontend-e2e-tester | Write `tests/e2e/specs/marketing-components-ar.spec.ts`: locale routing, `<html lang/dir>`, switcher toggle, BenefitsPanel, ActivityChart (inert CSV button), AITutorBubble (tail flip, chips), ChildCardPhone (email LTR-pin, chevron flip), RTL layout at mobile width, no console errors | 3 h | E2E-01 |
| REV-01 | reviewer | Gate all batches against brief acceptance criteria + CONVENTIONS.md; verify logical-props audit completeness, mascot-owl copy, no physical left/right, type-check + lint pass | 1 h | E2E-02 |
| COM-01 | committer | Stage + commit all changes on `feat/marketing-components-ar`; push branch; open PR with full description | 0.25 h | REV-01 PASS |

**No db-migration, backend-feature, api-tester, or security-auditor stages.** No backend surface, no auth/data/upload/AI/secrets/payments.

---

## Dependency order

```
DS-01
  └─ FE-B1-01  (route restructure)
  └─ FE-B1-02  (middleware)
  └─ FE-B1-03  (copy.ts en+ar)
       └─ FE-B1-04  (locale layout)
            └─ FE-B1-05  (lang switcher)   ─┐
            └─ FE-B1-06  (existing comps)  ─┘  (can run in parallel after FE-B1-04)
  ── FE-B1-01 + FE-B1-03 + FE-B1-04 all complete ──
       └─ FE-B2-01  (BenefitsPanel)        ─┐
       └─ FE-B2-02  (ActivityChart)         │  mutually parallel
       └─ FE-B2-03  (AITutorBubble + owl)   │
       └─ FE-B2-04  (ChildCardPhone)       ─┘
  ── all FE-B2 complete ──
       └─ E2E-01  (harness extension)
            └─ E2E-02  (marketing spec)
                 └─ REV-01  (reviewer gate)
                      └─ COM-01  (committer)
```

Notes on intra-batch ordering:
- FE-B1-01, FE-B1-02, FE-B1-03 are mutually independent and can run in parallel; DS-01 is the only prerequisite for all three.
- FE-B1-04 depends on FE-B1-01 (route structure exists) and FE-B1-03 (copy shape known).
- FE-B1-05 and FE-B1-06 can run in parallel after FE-B1-04 completes; both depend on FE-B1-03 + FE-B1-04.
- All four FE-B2 tasks depend only on FE-B1-03 (copy shape) + FE-B1-04 (locale layout) being in place; they are mutually independent.

---

## Execution batches

### Stage 0 — Designer (sequential, prerequisite for everything)

**Agent:** `designer`
**Output:** `design-system/ui_kits/marketing/marketing-components-ar.md`

Tasks: DS-01

The Design Spec must cover:
1. Per-component visual spec mapped to preview files with CSS-Module rules and `--lx-*` token reconciliation. Document every color/gradient/radius/shadow that has no existing token (benefits-panel gradient stops `#1E1B4B/#3B2C8F/#5B21B6`; chart card gradient `#334155→#1E293B`; frosted-glass bubble `rgba(15,23,42,0.7) + backdrop-filter`; slate label colors `#64748B/#94A3B8`) and decide: map to existing token or add a documented local `--lx-*` var to `globals.css`.
2. RTL deltas per component (bubble tail corner flip, chevron/arrow glyph direction, chart bar order, email/brand LTR pinning, wordmark `dir="ltr"` in AR nav).
3. Phone-frame decision for the Child Card (Q7): confirmed new sibling component reusing `PhoneMockup.module.css` frame classes; describe the exact CSS classes to reuse.
4. Locale strategy confirmation (Q1–Q3): `app/[locale]/` routing; `COPY.en`/`COPY.ar`; top-nav EN/AR toggle visual specification against `ar-web-nav.html`; middleware behavior.
5. Placement of the 4 new components within `page.tsx`'s existing section flow (after `SubjectsBand`? before `CTABanner`? specify the order).
6. Responsive breakpoints: follow the `FeaturesSection` pattern (3-col @1024, 2-col @768, 1-col @390).

**Gate:** lead reviews Design Spec before dispatching Batch 1.

---

### Batch 1 — Locale foundation (mixed: B1-01/B1-02/B1-03 in parallel, then B1-04, then B1-05/B1-06 in parallel)

**Agent:** `frontend`
**Branch:** `feat/marketing-components-ar`

#### Batch 1a — parallel (no interdependencies within this group)

| Task | What |
|---|---|
| FE-B1-01 | Create `app/[locale]/` directory structure under `apps/marketing-site`; move `page.tsx` + `page.module.css` under `app/[locale]/page.tsx`; split the existing `app/layout.tsx` into a root layout (`app/layout.tsx`, fonts/globals only) and a per-locale layout (`app/[locale]/layout.tsx`, `lang`/`dir` attributes); update all relative imports in the moved files |
| FE-B1-02 | Create `middleware.ts` at `apps/marketing-site/` project root: redirect bare `/` to `/en`; allow `/en/*` and `/ar/*` through; return 404 for unknown locale segments |
| FE-B1-03 | Restructure `lib/copy.ts`: rename `LANDING_COPY` → `COPY.en`; add full `COPY.ar` block porting Arabic wording from design-system AR kit files (do not machine-translate); add a `getCopy(locale: 'en' \| 'ar')` export that returns the right block; maintain `as const` for type safety |

#### Batch 1b — sequential after 1a (requires route shape + copy shape)

| Task | What |
|---|---|
| FE-B1-04 | Update `app/[locale]/layout.tsx` to read `params.locale` and set `<html lang={locale} dir={locale === 'ar' ? 'rtl' : 'ltr'}>`. Update `metadata` export to be locale-keyed. Confirm the existing `[dir='rtl']` font-var swap in `globals.css` now activates correctly. |

#### Batch 1c — parallel after 1b

| Task | What |
|---|---|
| FE-B1-05 | Add `LanguageSwitcher.tsx` + `.module.css` in `app/_components/`; top-nav placement in `page.tsx` (within `navActions` or adjacent, per Design Spec); switcher generates locale-segment URLs (`/en` ↔ `/ar`) matching `ar-web-nav.html` visual; remove/repurpose footer `العربية` entry; add `data-testid="lang-switcher"` |
| FE-B1-06 | Audit and update all existing `_components/`: replace `LANDING_COPY as C` import with `getCopy(locale)` call (locale comes from the page `params` prop); convert any physical `left/right` CSS to logical properties; verify `PhoneMockup.module.css` already uses `inset-inline-*` (it does — confirm no regressions) |

**After Batch 1 completes:** run `pnpm --filter @learnexia/marketing-site type-check` and `pnpm --filter @learnexia/marketing-site lint`. Must pass before Batch 2.

**Review gate (mini):** `reviewer` spot-checks that locale routing works end-to-end (both `/en` and `/ar` render with correct `lang`/`dir`) and `lib/copy.ts` type-checks cleanly before Batch 2 begins.

---

### Batch 2 — The 4 components (mutually parallel)

**Agent:** `frontend` (all four tasks can run in parallel — independent files, no shared state)
**Prerequisite:** Batch 1 fully merged into the working branch.

| Task | New files | Key implementation notes |
|---|---|---|
| FE-B2-01 BenefitsPanel | `app/_components/BenefitsPanel.tsx` + `.module.css` | Purple-gradient background; document local CSS vars for gradient stops if no `--lx-*` token exists. 3-row benefit list, 34px rounded icon tiles. Copy from `getCopy(locale).benefits`. Logical props throughout. Responsive per Design Spec breakpoints. |
| FE-B2-02 ActivityChart | `app/_components/ActivityChart.tsx` + `.module.css` | Dark card (`--lx-card` = `#1E293B`, `--lx-radius-modal` = 24px, `--lx-border`). Static const `CHART_VALUES = [45,80,30,95,50,70,110]`; day labels + header copy from `getCopy(locale).activityChart`. Arabic-Indic value strings in `COPY.ar`. "Export CSV" button: renders but `onClick` is absent or a no-op. Sunday bar index highlighted (indigo gradient + glow). `tabular-nums` on value labels. RTL: bar order mirrors via `flex-direction: row-reverse` when `dir='rtl'` (or a CSS `[dir='rtl']` override). |
| FE-B2-03 AITutorBubble | `public/assets/mascot-owl.svg` (copied from `design-system/assets/`), `app/_components/AITutorBubble.tsx` + `.module.css` | Frosted-glass bubble: `background: rgba(15,23,42,0.7); backdrop-filter: blur(20px)` — document as local CSS vars if no token. Asymmetric tail: `.tail` has `border-bottom-left-radius: 4px` in LTR, `.tail` in `[dir='rtl']` block has `border-bottom-left-radius: var(--lx-radius-sm); border-bottom-right-radius: 4px`. Inline highlighted word via `<span class={styles.highlight}>` using `--lx-xp` gold. 3 suggestion chips wrap on narrow viewports. `data-testid="ai-tutor-bubble"`. |
| FE-B2-04 ChildCardPhone | `app/_components/ChildCardPhone.tsx` + `.module.css` | Import `PhoneMockup.module.css` frame classes (`styles.phone`, `styles.screen` — or a subset of them) without modifying that file. Inside the frame: avatar monogram circle (orange, "S"/"س"), name + grade pill, email pinned LTR even in RTL (`direction: ltr` on the email `<span>`), stats row, "Active today" dot, footer with language flags + directional arrow copy from `getCopy(locale).childCard`. Chevron in footer: `›` LTR / `‹` RTL; arrow: `→` / `←`. `data-testid="child-card-phone"`. All static dummy data. |

All four components are rendered in `app/[locale]/page.tsx` at the placement specified in the Design Spec (expected: after `SubjectsBand`, before `CTABanner`, in an order the designer specifies).

**After Batch 2:** run full type-check + lint again.

---

### Batch 3 — E2E testing

**Agent:** `frontend-e2e-tester`
**Prerequisite:** Batch 2 complete; marketing site running on `:3002`.

| Task | What |
|---|---|
| E2E-01 | Extend `tests/e2e/playwright.config.ts`: add a `marketing` Playwright project with `baseURL: http://localhost:3002`; add a second `webServer` entry pointing to `pnpm --filter @learnexia/marketing-site dev` on port 3002; existing student-app project and webServer must be unaffected |
| E2E-02 | Write `tests/e2e/specs/marketing-components-ar.spec.ts` with the following test groups: |

**E2E-02 test coverage (minimum):**

| Group | Checks |
|---|---|
| A. Locale routing | `/en` → `<html lang="en" dir="ltr">`; `/ar` → `<html lang="ar" dir="rtl">`; bare `/` redirects to `/en`; unknown locale path 404s |
| B. Language switcher | Switcher visible in top nav (`data-testid="lang-switcher"`); clicking AR link navigates to `/ar`; clicking EN link navigates to `/en`; no footer `العربية` stub present (removed) |
| C. BenefitsPanel | `data-testid="benefits-panel"` renders in both locales; EN heading text present; AR heading text present; not empty |
| D. ActivityChart | `data-testid="activity-chart"` renders; 7 bar elements present; "Export CSV" button present but click produces no navigation/crash; AR locale shows Arabic-Indic numeral strings in bar labels |
| E. AITutorBubble | `data-testid="ai-tutor-bubble"` renders; mascot image loads (no broken img); EN chip text present; AR chip text present; bubble tail class/style differs between LTR and RTL (inspect computed style or a `data-dir` attribute) |
| F. ChildCardPhone | `data-testid="child-card-phone"` renders; email element has `direction: ltr` in both locales; footer arrow is `›`/`→` in EN, `‹`/`←` in AR |
| G. RTL layout | At mobile viewport (390px): both locales render without overflow; components stack gracefully |
| H. No console errors | Zero console errors on `/en` and `/ar` page load |

Selectors: prefer `data-testid` over copy text (Arabic copy makes text selectors brittle). Frontend agent must have added `data-testid` attributes to the new components and the switcher per the task notes above.

---

### Batch 4 — Reviewer gate

**Agent:** `reviewer`
**Prerequisite:** E2E-02 passes (all non-skip tests green).

Review against:
- Brief acceptance criteria (all 5 items, each sub-bullet tested or explicitly documented as design-only).
- CONVENTIONS.md — logical CSS properties, no physical `left/right` in new code, token-first.
- No `LANDING_COPY` remnants (all old imports replaced by `getCopy(locale)`).
- `mascot-owl.svg` present at `apps/marketing-site/public/assets/mascot-owl.svg`.
- Live hero `PhoneMockup.tsx` and `PhoneMockup.module.css` are unchanged (Q7 constraint).
- No i18n library added (Q2 constraint) — `package.json` diff check.
- `next.config.ts` unchanged (no new plugin needed for `[locale]` routing in Next.js 15 App Router).
- Type-check and lint pass (`pnpm --filter @learnexia/marketing-site type-check` + lint).
- Font weight audit: components that use Cairo/Tajawal only use weights already registered in `globals.css` (400, 600, 700, 800 for Cairo; 400, 500, 700 for Tajawal).

Critical/High findings block the gate; Medium/Low go into a follow-up note.

---

### Batch 5 — Committer

**Agent:** `committer`
**Prerequisite:** Reviewer PASS.

- Branch: `feat/marketing-components-ar`
- Commit with conventional message (scope: `marketing`).
- Push branch; open a Pull Request with full description referencing the brief.
- Do NOT merge the PR.

---

## Review gates summary

| Gate | Trigger | Agent | Blocks |
|---|---|---|---|
| Design Spec review | DS-01 complete | lead (informal) | Batch 1 start |
| Batch 1 mini-check | FE-B1-01–06 complete, type-check+lint pass | reviewer | Batch 2 start |
| Batch 2 full gate | FE-B2-01–04 complete, type-check+lint pass | reviewer | Batch 3 start |
| E2E gate | E2E-01–02 pass | reviewer (REV-01) | Committer |

---

## Blockers / prerequisites

| # | Blocker | Status | Action |
|---|---|---|---|
| 1 | Design Spec not yet produced | Unblocked — DS-01 is Batch 0 | Dispatch `designer` first |
| 2 | `mascot-owl.svg` absent from `public/assets/` (only `logo.svg` and `logo-mark.svg` present) | Known, resolved via Q6 | Frontend agent copies from `design-system/assets/mascot-owl.svg` in FE-B2-03 |
| 3 | All existing components import `LANDING_COPY as C` (6 component files + `layout.tsx`) | Known scope | FE-B1-06 covers the update sweep |
| 4 | `tests/e2e/playwright.config.ts` is wired to `:8081` only | Known, resolved via Q8 | E2E-01 adds a second Playwright project for `:3002`; student-app config untouched |
| 5 | Arabic font weight coverage | Already satisfied — `globals.css` registers Cairo 400/600/700/800 and Tajawal 400/500/700; reviewer verifies no new weight is needed |
| 6 | `next.config.ts` stability | No changes needed — Next.js 15 App Router supports `[locale]` dynamic segments natively without config changes; confirm during FE-B1-01 |

No backend blockers. No DB provisioning. No prior stories depended upon.

---

## Definition of done

### Batch 0 (Design Spec)
- [ ] `design-system/ui_kits/marketing/marketing-components-ar.md` exists and covers all 6 spec items listed in Stage 0.

### Batch 1 (Locale foundation)
- [ ] `app/[locale]/page.tsx` and `app/[locale]/layout.tsx` exist; old top-level `app/page.tsx` is replaced or restructured.
- [ ] `middleware.ts` redirects `/` → `/en`; both `/en` and `/ar` resolve without 404.
- [ ] `lib/copy.ts` exports `COPY.en`, `COPY.ar`, and `getCopy(locale)` with full coverage of all existing copy sections. No `LANDING_COPY` export remains (or it is an alias for backward compat if needed during migration).
- [ ] `<html lang="en" dir="ltr">` on `/en`; `<html lang="ar" dir="rtl">` on `/ar`.
- [ ] `LanguageSwitcher` visible in top nav on both locales; footer `العربية` stub removed.
- [ ] All existing `_components` consume `getCopy(locale)`; no physical `left`/`right` in new edits.
- [ ] `pnpm --filter @learnexia/marketing-site type-check` and `lint` pass.

### Batch 2 (4 components)
- [ ] All 4 components render without errors in both `/en` and `/ar`.
- [ ] BenefitsPanel: purple gradient, 3-row benefit list, correct copy both locales, RTL-correct via logical props. (AC Item 1)
- [ ] ActivityChart: 7 bars, static values, Sunday highlighted, "Export CSV" inert, Arabic-Indic values in AR, day labels from copy. (AC Item 2)
- [ ] AITutorBubble: owl avatar loaded from `/assets/mascot-owl.svg`, frosted-glass bubble, tail flips with `dir`, highlighted word in gold, 3 chips. (AC Item 3)
- [ ] ChildCardPhone: phone frame from `PhoneMockup.module.css`, email LTR-pinned in RTL, chevron + arrow flip, static dummy data. (AC Item 4)
- [ ] Live `PhoneMockup.tsx` and `PhoneMockup.module.css` are byte-for-byte unchanged.
- [ ] `pnpm --filter @learnexia/marketing-site type-check` and `lint` pass.

### Batch 3 (E2E)
- [ ] `tests/e2e/playwright.config.ts` has a `marketing` project targeting `:3002`; existing student-app project untouched.
- [ ] `marketing-components-ar.spec.ts` exists; all non-skipped tests pass in at least Chromium.
- [ ] Groups A–H from the E2E coverage table above all have passing tests.

### Batch 4 (Reviewer PASS)
- [ ] Reviewer confirms all 5 acceptance-criteria items from the brief are satisfied.
- [ ] No Critical/High findings outstanding.
- [ ] Token audit: no undocumented raw hex in component CSS Modules.

### Batch 5 (Committer)
- [ ] Branch `feat/marketing-components-ar` pushed; PR open with full description; branch not merged.

### Overall
All Batch 0–5 DoD items checked. The four components render pixel-faithful to their named preview files in both locales. The marketing site is fully navigable in Arabic with correct RTL mirroring. The language switcher lets a visitor toggle locales via URL segment. No i18n library added. No backend surface modified.
