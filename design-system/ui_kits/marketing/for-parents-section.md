# Design Spec — Marketing "For Parents — value showcase" section (EN + AR)

> **Premise correction (authoritative):** PR #113 did **not** restyle the atomic preview
> components — they are visually unchanged (#113 only added manifest comments + a token
> cleanup). The real new design is **this composed section** added to the canonical landing
> page. This spec — not `docs/briefs/marketing-landing-reskin.md` — is the source of truth
> for this work.

## Screens in scope (with capture + canonical-source citations, EN + AR)
| Surface | Ground-truth visual | EN source | AR source |
|---|---|---|---|
| Landing → "For Parents — value showcase" section | `design-system/screenshots/web/01b-landing-for-parents.png` | `design-system/ui_kits/parent-dashboard/PagesPublic.jsx` **lines 304–426** (heading "See exactly what your child gets out of it.") | `design-system/ui_kits/parent-dashboard/index-ar.html` **lines 135–195** (heading "شاهد بالضبط ما يستفيده طفلك.") |

**Do NOT use** the alternate "Parent value section" at `PagesPublic.jsx` **174–302** (heading
"See exactly what they're learning.") — that is a superseded variant. The canonical section is
**304–426**, which matches the screenshot.

Tokens already present in `apps/marketing-site/app/globals.css` (from PR #112) are reused
verbatim; new tokens are listed in §New tokens. The design-system tokens import is
`design-system/colors_and_type.css` (mirrored 1:1 into globals.css).

---

## 1. Section layout + per-element visual spec (source-line → token)

### Section wrapper (304–305 / AR 136)
- `padding: 32px 48px 96px` → `var(--lx-space-8) var(--lx-space-12) 96px`. (96px = `--lx-space-12 × 2`; no token for 96 — use literal `96px`, consistent with CTA/Subjects sections.)
- Background: inherits page `--lx-bg` (`#0F172A`). No own background.
- Full content max-width: align to the existing landing inner width (`max-width: 1180px`, centered) used by `FeaturesSection.module.css` line 9. Wrap the grid in a centered `1180px` container so the section lines up with Features/Subjects above it.

### Centered header (306–309 / AR 137)
| Element | Value (EN source) | Token |
|---|---|---|
| Container | `text-align:center; margin-bottom:56px` | margin `56px` (no token; literal) |
| Eyebrow "For Parents" | `font-weight:800; font-size:12px; color:#22C55E; letter-spacing:0.12em; text-transform:uppercase` | color `--lx-secondary`; weight 800; size 12; tracking `0.12em` |
| Heading | `margin:8px 0 0; font-weight:900; font-size:44px; letter-spacing:-0.02em`; color inherits `#F8FAFC` | color `--lx-fg1`; weight 900; size 44; tracking `-0.02em` |

Font: heading = Poppins (EN) / Cairo (AR, `.h-display`); eyebrow = Poppins 800 (EN) / Cairo
800 (AR).

### Grid (310 / AR 138)
- `display:grid; grid-template-columns:1fr 1.2fr; gap:32px; align-items:start`
- gap → `var(--lx-space-8)` (32). Columns literal `1fr 1.2fr`.
- LEFT = Benefits panel; RIGHT = "Live demos" stacked column.

---

### LEFT — Benefits panel (312–337 / AR 140–149)
| Element | Value | Token |
|---|---|---|
| Panel bg | `linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)` | **`--lx-grad-benefits`** |
| Radius | `28px` | **`--lx-radius-screen`** (=28; reuse) |
| Padding | `40px` | `var(--lx-space-10)` |
| Layout | `flex column; gap:24px` | gap `var(--lx-space-6)` |
| Shadow | `0 24px 60px rgba(91,33,182,0.35), inset 0 1px 0 rgba(255,255,255,0.15)` | **new token** `--lx-shadow-benefits` (see §New tokens) |
| Icon 🎮 | `font-size:64px; line-height:1; filter:drop-shadow(0 0 20px rgba(250,204,21,0.5))` | size 64; glow color `--lx-xp-glow`-adjacent (0.5 alpha — see §New) |
| Heading "Set up once…" | `font-weight:900; font-size:30px; line-height:1.15; letter-spacing:-0.02em`; color `#F8FAFC` (white) | color `--lx-fg1`; weight 900; size 30; lh 1.15; tracking `-0.02em` |
| Bullet list | `flex column; gap:14px` | gap `14px` (literal) |
| Bullet row | `flex; align-items:center; gap:14px; font-size:15px; color:rgba(255,255,255,0.92)` | text `--lx-on-purple`; size 15; gap 14 |
| Bullet icon tile | `width/height:40px; border-radius:12px; background:rgba(255,255,255,0.1); font-size:20px; flex-shrink:0; centered` | bg `--lx-on-purple-tile`; radius `12px` (literal — between sm/button); emoji 20 |

Four bullets (emoji + text) — see §3 copy table. Emoji order: ✨ 📊 🎯 🛡️.

---

### RIGHT — "Live demos" column (340 / AR 152)
`flex column; gap:20px` → gap `var(--lx-space-5)`. Three stacked cards:

#### (1) "Your weekly report" activity chart (342–365 / AR 154–165)
| Element | Value | Token |
|---|---|---|
| Card bg | `#1E293B` | `--lx-card` |
| Radius | `24px` | `--lx-radius-modal` |
| Padding | `24px` | `var(--lx-space-6)` |
| Border | `1px solid rgba(255,255,255,0.06)` | **new** `--lx-border-hair` (0.06; see §New) |
| Shadow | `0 4px 12px rgba(0,0,0,0.15)` | `--lx-shadow-soft` |
| Header row | `flex; justify-content:space-between; align-items:baseline; margin-bottom:16px` | margin `--lx-space-4` |
| Title "Your weekly report" | `font-weight:800; font-size:16px` (color `--lx-fg1`) | weight 800; size 16 |
| Delta "+28% vs last week" | `font-size:12px; color:#22C55E; font-weight:800` | color `--lx-secondary`; size 12; weight 800 |
| Bars container | `flex; gap:10px; align-items:flex-end; height:120px` | gap `10px`; height `120px` |
| Bar (each) | `flex:1; column; align-items:center; gap:6px`; bar `width:100%; height:(xp/110)*95 px; border-radius:8px 8px 3px 3px` | radius top `--lx-radius-sm` (8), bottom 3 literal |
| Normal bar fill | `linear-gradient(180deg,#334155,#1E293B)` | **`--lx-grad-bar`** |
| Sunday (i=6) fill | `linear-gradient(180deg,#A855F7,#4F46E5)` + `box-shadow:0 6px 18px rgba(99,102,241,0.4)` | **`--lx-grad-levelup-180`** + **new** `--lx-shadow-bar-highlight` (§New) |
| Day label | `font-size:10px; font-weight:700; text-transform:uppercase`; normal `#64748B`, Sunday `#F8FAFC` | normal `--lx-fg4`; Sunday `--lx-fg1` |

XP heights array (px after `(xp/110)*95`): `[45,80,30,95,50,70,110]` → `[38.9, 69.1, 25.9, 82.0, 43.2, 60.5, 95.0]`. Labels EN: Mon Tue Wed Thu Fri Sat Sun. **This chart is "Your weekly report" with NO Export CSV button** (the standalone `ActivityChart` has one — omit here).

#### (2) AI tutor bubble (368–387 / AR 168–174)
| Element | Value | Token |
|---|---|---|
| Row | `flex; gap:12px; align-items:flex-end` | gap `--lx-space-3` |
| Avatar | `56×56; border-radius:50%; background:linear-gradient(135deg,#A78BFA,#6366F1); box-shadow:0 8px 20px rgba(99,102,241,0.4)` | bg `--lx-grad-avatar`; shadow ≈ `--lx-shadow-primary-glow` (8 24 vs 8 20 — see note) |
| Owl img | `mascot-owl.svg` 46×46 | **Mascot owl = placeholder — flag (§Design gaps)** |
| Bubble | `background:rgba(15,23,42,0.75); backdrop-filter:blur(20px); border:1px solid rgba(255,255,255,0.1); border-radius:22px; border-bottom-left-radius:4px; padding:16px 20px; flex:1` | bg `--lx-overlay-frost` (0.7) vs source 0.75 — see note; border `--lx-border-frost`; radius 22 + corner 4; padding `16px 20px` |
| Label "Lexi · AI Tutor" | `font-weight:800; font-size:11px; color:#A5B4FC; text-transform:uppercase; letter-spacing:0.08em; margin-bottom:4px` | color `--lx-indigo-200`; size 11; tracking `0.08em` |
| Message | `font-size:15px; line-height:1.55; color:#F8FAFC`; **gold** `<b>tens</b>` `color:#FACC15` | text `--lx-fg1`; lh 1.55; highlight `--lx-xp` |

Notes: source uses `rgba(15,23,42,0.75)` and avatar shadow `0 8px 20px rgba(99,102,241,0.4)`.
The nearest existing tokens are `--lx-overlay-frost` = `rgba(15,23,42,0.7)` and
`--lx-shadow-primary-glow` = `0 8px 24px var(--lx-primary-glow)`. **Intended decision:** use
the exact source literals here (0.75 frost, `0 8px 20px rgba(99,102,241,0.4)`) to match the
capture pixel-for-pixel — frontend should add `--lx-overlay-frost-75` and
`--lx-shadow-avatar` rather than re-use the 0.7/24px values (§New tokens). **This bubble has
NO suggestion chips** (the standalone `AITutorBubble` may render chips — omit them here).

#### (3) Sami child card (390–423 / AR 177–192)
| Element | Value | Token |
|---|---|---|
| Card bg | `#15161D` | **`--lx-bg-card-deep`** |
| Radius | `20px` | `--lx-radius-card` |
| Padding | `18px` | `18px` (literal — between space-4/5) |
| Border | `1px solid rgba(255,255,255,0.06)` | `--lx-border-hair` (§New) |
| Shadow | `0 4px 12px rgba(0,0,0,0.15)` | `--lx-shadow-soft` |
| Layout | `flex column; gap:14px` | gap `14px` |
| **Row 1** | `flex; align-items:center; gap:12px` | gap `--lx-space-3` |
| Monogram "S" | `52×52; border-radius:50%; background:#FB923C; color:#fff; font-weight:900; font-size:22px; box-shadow:inset 0 -2px 4px rgba(0,0,0,0.18)` | bg `--lx-streak` (#FB923C); weight 900; size 22; inset shadow literal |
| Name "Sami" | `font-weight:900; font-size:18px` (color `--lx-fg1`) | weight 900; size 18 |
| Grade pill "Grade 3" | `padding:2px 8px; border-radius:9999px; background:rgba(79,70,229,0.18); color:#A5B4FC; font-weight:800; font-size:11px` | bg `--lx-primary-soft`; color `--lx-indigo-200`; radius `--lx-radius-pill` |
| Email | `font-size:12px; color:#94A3B8; margin-top:4px` | color `--lx-fg3`; **AR pins `dir="ltr"` + mono font** (`--lx-font-mono`) |
| Active dot+label | label `font-size:11px; color:#22C55E; font-weight:700`; dot `8×8; border-radius:50%; background:#22C55E; box-shadow:0 0 6px rgba(34,197,94,0.6)` | color `--lx-secondary`; dot glow `--lx-success-glow` |
| **Row 2** stats | `flex; align-items:center; gap:16px` | gap `--lx-space-4` |
| 🧠 Lv 12 | `font-weight:800; font-size:13px; color:#A855F7` | color `--lx-purple` |
| ⭐ 1,240 | `font-weight:800; font-size:13px; color:#FACC15` | color `--lx-xp` |
| 🔥 7d | `font-weight:800; font-size:13px; color:#FB923C` | color `--lx-streak` |
| "View progress →" | `margin-left:auto; color:#A5B4FC; font-weight:700; font-size:12px` | color `--lx-indigo-200`; pushed to trailing edge via `margin-inline-start:auto` |

Number rules: ⭐ `1,240` keeps Latin numerals + grouping in EN; AR renders `١٬٢٤٠` (Arabic-Indic,
Arabic thousands separator `٬`). XP/Lv/streak counters are gamification numbers → weight 800 +
`font-variant-numeric: tabular-nums` per brand law.

---

## 2. KEY IMPLEMENTATION DECISION (this gates the frontend)

**Recommendation: (a) build ONE new `ParentValueSection` component that renders the composed
layout inline**, using token-driven styles (CSS module mirroring the existing
`*.module.css` pattern). **Do not** add "compact/showcase" variant props to the four existing
components.

**Justification:**
- The four standalone components (`BenefitsPanel`, `ActivityChart`, `AITutorBubble`,
  `ChildCardPhone`) are built as **full-width bands** with their own copy, paddings, and extras
  (Export CSV on the chart, suggestion chips on the tutor, a **phone frame** around the child
  card). This section is a **materially different composition**: compact, framed inside a
  `1fr 1.2fr` grid, chart has no Export CSV, tutor has no chips, child card is **flat (no phone
  frame)**. Reuse value across the two is low — the shared part is just token values, which the
  new component already reads from `globals.css`.
- **CLAUDE.md rule 8 (ask-first on patterns):** adding `variant`/`compact` props to four
  components introduces a variant/configuration pattern (branching layout by prop) — that needs
  lead approval before implementation. The inline composed section needs **no new pattern**: it's
  a single presentational component matching the existing `FeaturesSection`/`SubjectsBand`
  shape. This keeps us inside "mirror existing shapes" and avoids a rule-8 stop.
- Single component = single source of truth for this exact pixel target; no risk of a variant
  prop drifting the band layouts.

> **Flag for lead:** option (b) (variant props) is the rule-8 path — if the lead later wants the
> bands and the showcase to share one component, that is a deliberate pattern decision to raise
> first. Default ships (a).

**Fate of the four existing standalone components:** **Remove all four from the page** (see §5).
After removal, if nothing else in the app imports `BenefitsPanel`, `ActivityChart`,
`AITutorBubble`, `ChildCardPhone`, their files are **deletable** (recommend deleting them and
their `*.module.css` to avoid dead code). Frontend should grep for other importers first;
`PhoneMockup` stays (still used by the hero). Note: their copy keys in `lib/copy.ts` should be
re-pointed to the new section's keys or removed if now unused.

---

## 3. EN + AR copy table
AR ported from `index-ar.html` 135–192. Arabic-Indic numerals in AR; email + brand stay LTR.
Each string needs an i18n key in `apps/marketing-site/app/lib/copy.ts` (suggested
namespace `parentValue.*`).

| Key | EN | AR |
|---|---|---|
| `eyebrow` | For Parents | لأولياء الأمور |
| `heading` | See exactly what your child gets out of it. | شاهد بالضبط ما يستفيده طفلك. |
| `panel.heading` | Set up once. Watch them learn forever. | جهّز الحساب مرة. شاهدهم يتعلمون للأبد. |
| `panel.b1` (✨) | AI-powered explanations tailored to each child's grade | شرح بالذكاء الاصطناعي مكيّف لصف كل طفل |
| `panel.b2` (📊) | Weekly reports show exactly what they've mastered | تقارير أسبوعية تعرض ما أتقنوه بدقة |
| `panel.b3` (🎯) | Daily missions keep them coming back without nagging | مهام يومية تعيدهم للتطبيق دون إلحاح |
| `panel.b4` (🛡️) | COPPA-compliant — no ads, no DMs, no data resold | متوافق مع COPPA — بلا إعلانات أو بيع بيانات |
| `chart.title` | Your weekly report | تقريرك الأسبوعي |
| `chart.delta` | +28% vs last week | ‎+٢٨٪ عن الأسبوع الماضي |
| `chart.days` | Mon Tue Wed Thu Fri Sat Sun | Mon Tue Wed Thu Fri Sat Sun (day labels stay Latin/LTR — see §4) |
| `tutor.label` | Lexi · AI Tutor | ليكسي · المعلم الذكي |
| `tutor.message` | When we compare two numbers, the one with more **tens** is bigger. Want me to show you with blocks? | عندما نقارن عددين، الأكبر هو الذي يحتوي على **عشرات** أكثر. هل تريد أن أوضح لك بالمكعّبات؟ |
| `card.name` | Sami | سامي |
| `card.monogram` | S | س |
| `card.grade` | Grade 3 | الصف ٣ |
| `card.email` | sami@learnexia.com | sami@learnexia.com (LTR, mono) |
| `card.active` | Active today | نشط اليوم |
| `card.lv` | 🧠 Lv 12 | 🧠 المستوى ١٢ |
| `card.xp` | ⭐ 1,240 | ⭐ ١٬٢٤٠ |
| `card.streak` | 🔥 7d | 🔥 ٧ أيام |
| `card.cta` | View progress → | عرض التقدم ← |

Bold word in `tutor.message` is the gold-highlighted token (`tens` / `عشرات`) — render as a
`<b>` colored `--lx-xp`. Copy law: headlines ≤6 words; no exclamation marks here (no win moment);
second-person, friendly; emoji are semantic (🧠 level, ⭐ XP, 🔥 streak, 🎮/🛡️/🎯/📊/✨ benefit icons).

---

## 4. RTL deltas (AR build)
Apply on `dir="rtl"` with Cairo (display/headings, `.h-display`) + Tajawal (body).

1. **Grid mirrors:** `1fr 1.2fr` → in RTL the Benefits panel sits on the **right**, Live-demos
   column on the **left**. Use logical layout (grid auto-flips under `dir="rtl"`); do not hard-pin
   `left/right`.
2. **Chart bars stay LTR:** AR source wraps the bars row in `direction:ltr` (index-ar.html 156).
   Bar **order does NOT reverse** — Mon→Sun reads left→right, Sunday highlighted bar stays on the
   right, progress reads L→R universally. Day labels stay **Latin** (`Mon…Sun`) per the AR source
   (kit keeps them Latin; do not translate or convert to Arabic-Indic).
3. **Tutor bubble corner flips:** EN cuts `border-bottom-left-radius:4px`; AR cuts
   `border-bottom-right-radius:4px` (index-ar.html 170) so the "tail" stays on the avatar side.
   Avatar is leading (right in RTL). The owl glyph and avatar gradient are **NOT mirrored**.
4. **"View progress →" arrow flips to ←** (`عرض التقدم ←`). Use `margin-inline-start:auto` so it
   stays on the **trailing** edge in both directions. The stats row (🧠⭐🔥) reorders RTL
   automatically; emoji glyphs are not mirrored.
5. **Email stays LTR + mono:** wrap `sami@learnexia.com` in `dir="ltr"` with `--lx-font-mono`
   (AR source 182). Brand name `Learnexia` and the email never localize.
6. **Numerals:** convert in-reading numbers to Arabic-Indic — `+٢٨٪`, `المستوى ١٢`, `الصف ٣`,
   `٧ أيام`, XP `١٬٢٤٠` (Arabic thousands sep `٬`). The `٪` percent and `٬` separator follow
   Arabic typography. Monogram becomes `س` (first letter of سامي), not `S`.
7. **Eyebrow/heading tracking:** `-0.02em` negative tracking is a Latin nicety; keep the same
   values — Cairo tolerates it at these sizes (matches AR source which keeps `letter-spacing:-0.02em`).

---

## 5. Placement
The new `ParentValueSection` **replaces the four current standalone bands** in
`apps/marketing-site/app/[locale]/page.tsx`. Remove lines **128–134** (the
`AITutorBubble` / `ChildCardPhone` / `ActivityChart` / `BenefitsPanel` block plus its comment
banner) and their imports (lines 12–15), and insert:

```
<SubjectsBand locale={locale} />
<ParentValueSection locale={locale} />   ← new, sits HERE
<CTABanner locale={locale} />
```

Order: **between `SubjectsBand` and `CTABanner`** (matches the screenshot, which shows this
section directly above the indigo→purple CTA banner). Confirm in the matching kit
(`PagesPublic.jsx` 304–426 sits between the subjects band ~174 region and the CTA at 428).

---

## 6. Responsive (mirror the FeaturesSection breakpoints)
`FeaturesSection.module.css` breaks at **`max-width:900px`** (3→2 col) and **`max-width:600px`**
(2→1 col). For this 2-column section use the **`900px`** breakpoint to collapse, plus polish at
the 390/768/1024 targets:

| Width | Layout |
|---|---|
| **≥1024** (and up to 1180 inner) | 2-col grid `1fr 1.2fr`, gap 32, header 44px, panel padding 40 |
| **768** (≤900px bp) | **Stack to 1 column** (`grid-template-columns:1fr`), Benefits panel first then Live-demos column; reduce section side padding `48px→32px`; heading may step to ~36px to avoid wrapping awkwardly (intended deviation — flag). Chart/tutor/card go full container width. |
| **390** (≤600px bp) | Single column; section padding `32px 20px 64px`; heading ~28–30px; panel padding `40→24`; bullet font 15 stays; chart bars keep `gap:10`/`height:120` (or step height to ~100 if cramped — minor); stats row may wrap — keep `gap:16` and allow "View progress →" to drop below stats if needed. Touch targets: "View progress →" link ≥44px tap height. |

One primary visual focus per section (the purple panel); the "View progress →" is a soft link,
not a competing CTA (the section's real CTA is the CTABanner immediately below).

---

## New tokens to add to `apps/marketing-site/app/globals.css`
Reuse existing where exact. Add these (literals taken straight from source):

| New token | Value | Used by |
|---|---|---|
| `--lx-shadow-benefits` | `0 24px 60px rgba(91,33,182,0.35), inset 0 1px 0 rgba(255,255,255,0.15)` | Benefits panel (also reused by CTA banner if desired) |
| `--lx-border-hair` | `rgba(255,255,255,0.06)` | chart card + child-card border (distinct from `--lx-border` 0.08) |
| `--lx-shadow-bar-highlight` | `0 6px 18px rgba(99,102,241,0.4)` | Sunday highlighted bar glow |
| `--lx-overlay-frost-75` | `rgba(15,23,42,0.75)` | tutor bubble bg (exact-match vs existing 0.7) |
| `--lx-shadow-avatar` | `0 8px 20px rgba(99,102,241,0.4)` | tutor avatar (exact-match vs existing `--lx-shadow-primary-glow` = 0 8 24) |

Reused as-is (no new token): `--lx-grad-benefits`, `--lx-on-purple`, `--lx-on-purple-tile`,
`--lx-grad-bar`, `--lx-grad-levelup-180`, `--lx-indigo-200`, `--lx-grad-avatar`,
`--lx-border-frost`, `--lx-bg-card-deep`, `--lx-success-glow`, `--lx-font-mono`, `--lx-card`,
`--lx-radius-modal/card/sm/pill/screen`, `--lx-secondary`, `--lx-xp`, `--lx-purple`,
`--lx-streak`, `--lx-primary-soft`, `--lx-fg1/3/4`, `--lx-shadow-soft`, `--lx-space-*`.

Drop-shadow glow on 🎮 (`rgba(250,204,21,0.5)`) is a one-off filter — inline literal is fine
(`--lx-xp-glow` is 0.45; do not substitute — keep 0.5 to match capture).

---

## Motion (brand law; ≤800ms, kid-snappy)
- **Section reveal on scroll:** slide-up 16px + fade, 250–300ms, `var(--lx-ease-out)`. Stagger the
  three right-column cards ~60ms each for a light cascade. Respect `prefers-reduced-motion` (no
  transform, instant).
- **Activity chart bars:** on first in-view, grow from 0 height to target over 600–800ms ease-out;
  Sunday bar ends with a brief glow flash (`--lx-shadow-bar-highlight` fading in). Decorative —
  gate behind reduced-motion.
- **Active dot:** optional soft pulse (1→1.04→1, ~2s loop) — streak/active affordance.
- **Hover (web):** the whole "View progress →" link brightens ~8% + arrow nudges 2px toward its
  trailing edge on hover; no card hover-lift needed (these are presentational demo cards, not
  interactive). Do **not** darken anything on interaction.
- No press/scale states — there are no buttons in this section (the link is the only interactive
  affordance and it's a soft text link).

---

## Accessibility / kid-UX
- Emoji are decorative-with-meaning: give each benefit-tile emoji `role="img"` + an `aria-label`
  (or `aria-hidden` with the adjacent text carrying meaning). The 🧠/⭐/🔥 stat glyphs pair with
  visible numbers — mark `aria-hidden` and keep the text.
- Chart is decorative demo content → wrap in `aria-hidden="true"` or provide a concise
  `aria-label` ("Sample weekly activity, Sunday highest"). It is not real user data.
- Contrast: white text on the purple gradient (`--lx-on-purple` 0.92) and `--lx-fg3` email on
  `#15161D` both clear AA at these sizes; day labels `--lx-fg4` on `--lx-card` are decorative.
- "View progress →" link ≥44px tap height on mobile; visible focus ring `--lx-focus-ring`
  (2px indigo + 4px glow) on keyboard nav.
- Heading is the section's `<h2>`; panel/chart/card sub-heads are `<h3>`. One logical heading order.

---

## Implementation handoff
| Piece | Target |
|---|---|
| New tokens (`--lx-shadow-benefits`, `--lx-border-hair`, `--lx-shadow-bar-highlight`, `--lx-overlay-frost-75`, `--lx-shadow-avatar`) | `apps/marketing-site/app/globals.css` |
| New component `ParentValueSection` + `ParentValueSection.module.css` | `apps/marketing-site/app/_components/` |
| Copy keys `parentValue.*` (EN + AR) | `apps/marketing-site/app/lib/copy.ts` |
| Page composition (remove 4 bands + imports, insert new section between Subjects and CTA) | `apps/marketing-site/app/[locale]/page.tsx` |
| Delete (if no other importers) `BenefitsPanel`, `ActivityChart`, `AITutorBubble`, `ChildCardPhone` + their `.module.css` | `apps/marketing-site/app/_components/` |

This is a marketing surface only — **no `packages/ui` or `packages/design-system` change**
(those serve the student-app/Tamagui layer; the marketing app owns its own `--lx-*` mirror).

---

## Design gaps / open questions
1. **Mascot owl is a placeholder** (`assets/mascot-owl.svg`) — the tutor avatar uses it; flag for
   final art replacement, same as elsewhere in the kit.
2. **Frost/avatar token drift:** existing `--lx-overlay-frost` (0.7) and `--lx-shadow-primary-glow`
   (0 8 24) differ slightly from this section's source (0.75 / 0 8 20). Spec chooses exact-match
   new tokens for pixel parity. If the lead prefers token reuse over pixel-exactness, reuse the
   0.7/24px tokens and treat the delta as an accepted ~minor deviation — confirm preference.
3. **Day labels stay Latin in AR** per the kit source (`Mon…Sun`). If product wants Arabic day
   abbreviations, that's a copy decision beyond this capture — flag, don't invent.
4. **`lib/copy.ts` key cleanup:** removing the four bands may orphan their copy keys — frontend
   should re-home or delete them in the same PR to avoid dead i18n entries.
5. **96px section padding-bottom** has no spacing token — used as a literal consistent with the
   adjacent CTA/Subjects sections; not introducing a token for a single value.

Design spec ready for frontend.
