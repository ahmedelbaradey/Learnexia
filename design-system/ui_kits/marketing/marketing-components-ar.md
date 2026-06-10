# Design Spec — DS-01 Marketing site: 4 design-system components + Arabic/RTL

**Surface:** `apps/marketing-site` (Next.js 15 App Router · plain React + CSS Modules · `--lx-*` tokens · copy in `lib/copy.ts`). **No Tamagui.**
**Locale:** App-Router `app/[locale]/` segments (`/en`, `/ar`). Default product locale Arabic, but marketing default route is `/en` per lead (middleware redirects `/` → `/en`). Default theme dark.
**Source-of-truth pairs:** every component below cites its verified `design-system/preview/*.html` card. Values are transcribed from the card source, not eyeballed. Where a preview uses a raw hex with no `--lx-*` token in `globals.css`, the token decision is recorded in the **Token decisions table** (§7).

This spec is grounded in the existing `FeaturesSection.tsx`/`.module.css` and `PhoneMockup.module.css` shapes — the frontend agent mirrors those exactly (CSS Modules, logical properties, tokens only, copy from `getCopy(locale)`).

---

## 0. Lead-resolved decisions (design to these — do not re-raise)
- **Routing:** `app/[locale]/` (`en`/`ar`), middleware redirect `/`→`/en`, no i18n library. Copy in `COPY.en` / `COPY.ar` + `getCopy(locale)`.
- **Switcher:** top-nav EN/AR toggle matching `ar-web-nav.html`; the footer `العربية` stub (`copy.footer.links.arabic`) is removed.
- **Numerals:** Arabic-Indic (٠–٩) in `copy.ar` strings (match previews); Western in `copy.en`.
- **Export CSV:** decorative/inert — renders, no handler.
- **Mascot owl:** copied to `public/assets/mascot-owl.svg` from `design-system/assets/`.
- **Phone frame for Child Card:** NEW sibling component reusing `PhoneMockup.module.css` frame classes; live hero `PhoneMockup` untouched.

---

## 1. Component — BenefitsPanel
**Preview (LTR):** `design-system/preview/web-benefits-panel.html` · **RTL:** no AR-specific card — mirror via logical properties + `copy.ar`.
**Files:** `app/_components/BenefitsPanel.tsx` + `BenefitsPanel.module.css`. `data-testid="benefits-panel"`.

### 1.1 Anatomy (transcribed from card)
Full-width purple-gradient panel → big `🎮` glyph → bold heading → 3-row benefit list (each row = 34px rounded icon tile + text).

### 1.2 Exact values → CSS-Module rules
| Element | Preview literal | CSS-Module rule (tokens) |
|---|---|---|
| Panel bg | `linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)` | `background: var(--lx-grad-benefits)` — **new local var** (§7) |
| Panel padding | `32px` | `padding: var(--lx-space-8)` |
| Panel radius | (card had none — full-bleed) | `border-radius: var(--lx-radius-modal)` /* 24 */ — see §1.4 placement note |
| Panel text color | `#fff` | `color: var(--lx-fg1)` (`#f8fafc`, ≈white; acceptable) |
| Panel font | `var(--lx-font-display)` | `font-family: var(--lx-font-display)` |
| Inner gap (glyph→heading→list) | `gap:18px` | `display:flex; flex-direction:column; gap:var(--lx-space-5)` /* 20 ≈ 18 */ — or literal `18px`; prefer token |
| 🎮 glyph | `font-size:60px; line-height:1; filter:drop-shadow(0 0 20px rgba(250,204,21,0.5))` | `.glyph{font-size:60px;line-height:1;filter:drop-shadow(0 0 20px var(--lx-xp-glow))}` (`--lx-xp-glow`=`rgba(250,204,21,0.45)` ≈ `0.5`; reuse — flagged minor) |
| Heading | `font-weight:900; font-size:26px; line-height:1.15; letter-spacing:-0.02em` | `.heading{margin:0;font-weight:900;font-size:26px;line-height:1.15;letter-spacing:-0.02em;color:var(--lx-fg1)}` |
| List | `display:flex;flex-direction:column;gap:10px` | `.list{display:flex;flex-direction:column;gap:var(--lx-space-2)}` /* 8 ≈ 10; or literal 10px */ |
| Row | `display:flex;align-items:center;gap:12px;font-size:13px;color:rgba(255,255,255,0.92)` | `.row{display:flex;align-items:center;gap:var(--lx-space-3);font-size:13px;line-height:1.4;color:var(--lx-on-purple)}` — `--lx-on-purple` **new local** = `rgba(255,255,255,0.92)` (§7) |
| Icon tile | `width:34px;height:34px;border-radius:10px;background:rgba(255,255,255,0.1);font-size:16px;flex-shrink:0` (centered) | `.tile{inline-size:34px;block-size:34px;border-radius:10px;background:var(--lx-on-purple-tile);display:flex;align-items:center;justify-content:center;font-size:16px;flex-shrink:0}` — `--lx-on-purple-tile` **new local** = `rgba(255,255,255,0.1)` (§7) |

`10px` tile radius sits between `--lx-radius-sm` (8) and `--lx-radius-card` (20); the card uses 10 literally. Use a literal `10px` (small decorative inner radius, no token) — note it is **not** a 90° corner, so brand-law rule 4 is satisfied.

### 1.3 Copy (`copy.<locale>.benefits`)
| Key | EN | AR (Cairo heading / Tajawal body) |
|---|---|---|
| glyph | `🎮` | `🎮` |
| heading | `Set up once. Watch them learn forever.` | `أعِدّه مرة واحدة. وشاهدهم يتعلمون للأبد.` |
| items[0] = `✨` | `AI-powered explanations tailored to each child's grade` | `شروحات بالذكاء الاصطناعي مصمّمة لمستوى كل طفل` |
| items[1] = `📊` | `Weekly reports show exactly what they've mastered` | `تقارير أسبوعية تُظهر بالضبط ما أتقنوه` |
| items[2] = `🛡️` | `COPPA-compliant — no ads, no DMs, no data resold` | `متوافق مع COPPA — بلا إعلانات، بلا رسائل، بلا بيع للبيانات` |

`COPPA` stays Latin (technical/brand string) inside the AR line — render inline, no `dir` wrapper needed since it is a single token in flowing RTL; if it visually breaks, wrap in `<span dir="ltr">COPPA</span>`.

### 1.4 RTL deltas
- Rows already use `flex` + logical `gap`; with `dir="rtl"` the **icon tile leads (right side), text follows** automatically — no override.
- `text-align: start` on heading/body (do not set `left`).
- Heading uses Cairo, rows use Tajawal via the existing `[dir='rtl']` font swap. Heading weight 900 → Cairo only ships to 800 in `globals.css`; **use `font-weight:800` for the AR heading** (browser will not synthesize 900). Spec the CSS as `font-weight:900` for LTR; rely on the available Cairo 800 face — flagged as design gap §10.

### 1.5 Responsive (FeaturesSection pattern)
Panel is single-column at all widths; only padding/heading scale.
- `@1024` default: `padding: var(--lx-space-8)` (32), heading 26px.
- `@900` (`max-width:900px`): `padding: var(--lx-space-6)` (24).
- `@600`: heading `22px`, glyph `48px`, `padding: var(--lx-space-5)` (20).

---

## 2. Component — ActivityChart
**Preview (LTR):** `design-system/preview/web-activity-chart.html` · **RTL:** no AR card — mirror bar order + numerals via `copy.ar`.
**Files:** `app/_components/ActivityChart.tsx` + `ActivityChart.module.css`. `data-testid="activity-chart"`.
Static const in component: `CHART_VALUES = [45, 80, 30, 95, 50, 70, 110]` (Mon→Sun). Bar **pixel heights** (transcribed): `[60, 100, 40, 115, 65, 90, 140]` px. Sunday (index 6, value 110) is the highlighted bar.

### 2.1 Exact values → CSS-Module rules
| Element | Preview literal | CSS-Module rule (tokens) |
|---|---|---|
| Card | `background:#1E293B;border-radius:24px;padding:22px;border:1px solid rgba(255,255,255,0.06);box-shadow:0 4px 12px rgba(0,0,0,0.15)` | `.card{background:var(--lx-card);border-radius:var(--lx-radius-modal);padding:22px;border:1px solid var(--lx-border-soft);box-shadow:var(--lx-shadow-soft);font-family:var(--lx-font-display)}` (`--lx-border-soft`=`rgba(255,255,255,0.05)`≈`0.06`; reuse) |
| Max width | `max-width:920px;margin:0 auto` (body) | wrap section in `.inner{max-width:920px;margin:0 auto}` (mirror FeaturesSection `.inner`) |
| Header row | `display:flex;justify-content:space-between;align-items:flex-start;margin-bottom:18px` | `.header{display:flex;justify-content:space-between;align-items:flex-start;margin-block-end:18px}` |
| Title | `font-weight:800;font-size:16px;color:#F8FAFC` | `.title{font-weight:800;font-size:16px;color:var(--lx-fg1)}` |
| Subtitle | `font-size:12px;color:#94A3B8;margin-top:2px` | `.subtitle{font-size:12px;color:var(--lx-fg3);margin-block-start:2px}` |
| Export CSV btn | `background:transparent;border:1px solid rgba(255,255,255,0.12);color:#A5B4FC;padding:6px 12px;border-radius:9999px;font-weight:700;font-size:12px;cursor:pointer` | `.exportBtn{background:transparent;border:1px solid var(--lx-border-strong);color:var(--lx-indigo-200);padding:6px 12px;border-radius:var(--lx-radius-pill);font-weight:700;font-size:12px}` — `--lx-indigo-200` **new local** = `#a5b4fc` (§7). `border-strong`=`0.16`≈`0.12` (reuse). **Inert:** render as `<button type="button">` with no `onClick` (decorative). |
| Bars track | `display:flex;gap:12px;align-items:flex-end;height:180px` | `.bars{display:flex;gap:var(--lx-space-3);align-items:flex-end;height:180px}` |
| Bar column | `flex:1;display:flex;flex-direction:column;align-items:center;gap:6px` | `.col{flex:1;display:flex;flex-direction:column;align-items:center;gap:6px}` |
| Bar (default) | `width:100%;height:Npx;background:linear-gradient(180deg,#334155,#1E293B);border-radius:10px 10px 4px 4px;position:relative` | `.bar{inline-size:100%;background:var(--lx-grad-bar);border-radius:10px 10px 4px 4px;position:relative}` — `--lx-grad-bar` **new local** = `linear-gradient(180deg,#334155,#1e293b)` = `linear-gradient(180deg,var(--lx-card-soft),var(--lx-card))` (§7) |
| Bar (Sunday/highlight) | `background:linear-gradient(180deg,#A855F7,#4F46E5);box-shadow:0 6px 18px rgba(99,102,241,0.4)` | `.barHi{background:var(--lx-grad-levelup-180);box-shadow:0 6px 18px var(--lx-primary-glow)}` — `--lx-grad-levelup-180` **new local** = `linear-gradient(180deg,#a855f7,#4f46e5)` = `linear-gradient(180deg,var(--lx-purple),var(--lx-primary))` (§7). This is the **Level-Up gradient** (purple→indigo) — brand-law rule 3 permits it on a reward/highlight moment. `--lx-primary-glow`=`rgba(99,102,241,0.45)`≈`0.4` (reuse). |
| Value label | `position:absolute;top:-22px;left:50%;transform:translateX(-50%);font-size:11px;font-weight:800;color:#64748B;font-variant-numeric:tabular-nums` | `.value{position:absolute;inset-block-start:-22px;inset-inline-start:50%;transform:translateX(-50%);font-size:11px;font-weight:800;color:var(--lx-fg4);font-variant-numeric:tabular-nums}` (`--lx-fg4`=`#64748b`, exact). Highlight value color `#A5B4FC` → `color:var(--lx-indigo-200)` on `.value` inside `.barHi` column. |
| Day label (default) | `font-size:11px;font-weight:700;color:#94A3B8;text-transform:uppercase;letter-spacing:0.06em` | `.day{font-size:11px;font-weight:700;color:var(--lx-fg3);text-transform:uppercase;letter-spacing:0.06em}` |
| Day label (Sunday) | same but `color:#F8FAFC` | `.dayHi{color:var(--lx-fg1)}` |

Brand-law rule 7: value labels are numbers → weight 800 + `tabular-nums` already satisfied. Per brand-law, numbers/XP are normally weight 800 — the card uses 800, kept.

### 2.2 Copy (`copy.<locale>.activityChart`)
| Key | EN | AR |
|---|---|---|
| title | `Daily activity` | `النشاط اليومي` |
| subtitle | `XP earned per day` | `النقاط المكتسبة كل يوم` |
| exportBtn | `Export CSV` | `تصدير CSV` (`CSV` Latin) |
| days[0..6] | `Mon Tue Wed Thu Fri Sat Sun` | `الإثنين الثلاثاء الأربعاء الخميس الجمعة السبت الأحد` |
| values[0..6] | `45 80 30 95 50 70 110` (Western) | `٤٥ ٨٠ ٣٠ ٩٥ ٥٠ ٧٠ ١١٠` (Arabic-Indic strings) |

**Values:** EN renders computed/Western from `CHART_VALUES`; AR renders the **string** array from `copy.ar` (Arabic-Indic) so digits match the preview convention. Bar **heights** always come from the numeric `CHART_VALUES` (px array) regardless of locale — never from the display string.

### 2.3 RTL deltas (critical)
- **Bar order mirrors** — Sun must sit on the **left** in RTL (brief AC). Implement: `.bars` gets `[dir='rtl'] & { flex-direction: row-reverse }` (or a CSS module `:global([dir='rtl']) .bars`). Mon stays on the right, Sun on the left — reading L→R the bars still climb to the highlighted Sunday on the far left. (Per Arabic/RTL convention, bar charts may stay LTR; lead chose mirrored order here — honor the brief: mirror.)
- Value label uses `inset-inline-start:50%` + `translateX(-50%)` — centering is direction-agnostic, fine.
- Day labels: `letter-spacing` + `text-transform:uppercase` are no-ops on Arabic glyphs (Arabic has no uppercase); harmless, leave the rule.
- `tabular-nums` applies to Arabic-Indic too (kept).

### 2.4 Responsive
- Section `.inner` `max-width:920px`. Below 920 the card is fluid (bars `flex:1` already responsive).
- `@600`: reduce `.bars` `height:180px` → `140px`; `padding:22px` → `var(--lx-space-4)` (16); Export CSV button stays (wraps under title if needed via `flex-wrap:wrap` on `.header`).

---

## 3. Component — AITutorBubble
**Preview (LTR):** `design-system/preview/components-tutor.html` · **RTL:** `design-system/preview/ar-tutor.html` (the cards differ ONLY in the tail corner — verified).
**Files:** `app/_components/AITutorBubble.tsx` + `AITutorBubble.module.css`. `data-testid="ai-tutor-bubble"`. Needs `public/assets/mascot-owl.svg`.

> Brand-law rule 11: the mascot owl is a **placeholder** — flagged §10. Brand-law rule 5: glass/blur is permitted here because the tutor bubble is exactly the sanctioned "floating overlay" surface.

### 3.1 Exact values → CSS-Module rules
| Element | Preview literal | CSS-Module rule (tokens) |
|---|---|---|
| Stage | `display:flex;gap:14px;align-items:flex-end` (AR caps `max-width:600px;margin:0 auto`) | `.stage{display:flex;gap:14px;align-items:flex-end;max-width:600px;margin:0 auto}` |
| Avatar circle | `width:64px;height:64px;border-radius:50%;background:linear-gradient(135deg,#A78BFA,#6366F1);box-shadow:0 8px 20px rgba(99,102,241,0.4);flex-shrink:0` | `.avatar{inline-size:64px;block-size:64px;border-radius:50%;background:var(--lx-grad-avatar);box-shadow:0 8px 20px var(--lx-primary-glow);display:flex;align-items:center;justify-content:center;flex-shrink:0}` — `--lx-grad-avatar` **new local** = `linear-gradient(135deg,#a78bfa,#6366f1)` (§7). `--lx-primary-glow` `0.45`≈`0.4` (reuse). Do NOT mirror the avatar gradient in RTL (Arabic/RTL convention). |
| Avatar img | `width:54px;height:54px` | `.avatar img{inline-size:54px;block-size:54px}` |
| Bubble | `background:rgba(15,23,42,0.7);backdrop-filter:blur(20px);border:1px solid rgba(255,255,255,0.1);border-radius:22px;border-bottom-left-radius:4px;padding:14px 18px;max-width:480px;box-shadow:var(--lx-shadow-card)` | `.bubble{background:var(--lx-overlay-frost);-webkit-backdrop-filter:blur(20px);backdrop-filter:blur(20px);border:1px solid var(--lx-border-strong);border-radius:22px;border-end-start-radius:4px;padding:14px 18px;max-width:480px;box-shadow:var(--lx-shadow-soft)}` — `--lx-overlay-frost` **new local** = `rgba(15,23,42,0.7)` (§7). border `rgba(255,255,255,0.1)`≈`--lx-border-strong` (`0.16`) is close; **prefer a literal `rgba(255,255,255,0.1)` via new `--lx-border-frost`** (§7) for fidelity. `--lx-shadow-card` (preview token) → marketing's `--lx-shadow-soft`. **Bubble radius 22px** has no token (between card 20 and modal 24) — literal `22px`. |
| Tail corner | LTR `border-bottom-left-radius:4px`; RTL `border-bottom-right-radius:4px` | Use logical `border-end-start-radius:4px` on `.bubble` — auto-flips with `dir`. See §3.3. |
| Name | `font-family:var(--lx-font-display);font-weight:800;font-size:12px;color:var(--lx-primary);text-transform:uppercase;letter-spacing:0.08em;margin-bottom:4px` (AR uses `0.04em`) | `.name{font-family:var(--lx-font-display);font-weight:800;font-size:12px;color:var(--lx-primary);text-transform:uppercase;letter-spacing:0.08em;margin-block-end:4px}`. **AR override:** `:global([dir='rtl']) .name{letter-spacing:0.04em}` (ar-tutor uses `0.04em` + Cairo). |
| Message | `font-family:var(--lx-font-body);font-size:15px;line-height:1.5;color:var(--lx-fg1)` (AR `line-height:1.6`) | `.msg{font-family:var(--lx-font-body);font-size:15px;line-height:1.5;color:var(--lx-fg1)}`. **AR override:** `:global([dir='rtl']) .msg{line-height:1.6}` (Tajawal, per ar-tutor + brand-law generous AR line-height). |
| Highlighted word | `<b style="color:var(--lx-xp)">tens</b>` (AR `#FACC15`=xp) | `.highlight{color:var(--lx-xp)}` (`#facc15`, exact). |
| Chips wrap | `display:flex;gap:6px;margin-top:10px;flex-wrap:wrap` | `.chips{display:flex;gap:6px;margin-block-start:10px;flex-wrap:wrap}` |
| Chip | `font-size:12px;font-weight:600;background:rgba(79,70,229,0.18);color:#A5B4FC;padding:5px 10px;border-radius:9999px;border:1px solid rgba(99,102,241,0.3)` | `.chip{font-size:12px;font-weight:600;background:var(--lx-primary-soft);color:var(--lx-indigo-200);padding:5px 10px;border-radius:var(--lx-radius-pill);border:1px solid var(--lx-primary-chip-border)}` — `--lx-primary-soft`=`rgba(79,70,229,0.18)` exact (reuse). `--lx-primary-chip-border` **new local** = `rgba(99,102,241,0.3)` (§7). In AR the chips are `<button>` (decorative); in LTR they were `<div>`. Use `<button type="button">` both locales for a11y, no handler. |

### 3.2 Copy (`copy.<locale>.tutorBubble`)
| Key | EN | AR (name Cairo / msg+chips Tajawal/Cairo per preview) |
|---|---|---|
| name | `Lexi · AI Tutor` | `ليكسي · المعلم الذكي` |
| msgLead | `When we compare two numbers, the one with more ` | `عندما نقارن عددين، الأكبر هو العدد الذي يحتوي على ` |
| msgHighlight | `tens` | `عشرات` |
| msgRest | ` is bigger. Want me to show you with blocks?` | ` أكثر. هل تريد أن أوضح لك ذلك بالمكعّبات؟` |
| chips[0] | `Yes, show me` | `نعم، أرني` |
| chips[1] | `Give a hint` | `أعطني تلميحاً` |
| chips[2] | `Skip` | `تخطي` |

Message is split into lead / highlight / rest so the highlight `<span class={styles.highlight}>` renders in gold (mirrors the hero-headline split pattern already in `page.tsx`). Brand-law rule 8: AI tutor speaks first-person ("show you") — preserved both locales.

### 3.3 RTL deltas (critical)
- **Tail corner flips** — the ONLY structural difference between the two cards. Use the logical property `border-end-start-radius:4px` on `.bubble`: in LTR this resolves to bottom-left (matches `components-tutor`), in RTL to bottom-right (matches `ar-tutor`). The other three corners stay `22px` (set `border-radius:22px` first, then override the end-start corner). Frontend may also expose `data-dir` for the e2e tail-flip assertion.
- **Avatar/bubble order mirrors** automatically (flex + `dir`): avatar leads on the right in RTL, bubble follows left. No physical override.
- `letter-spacing` 0.08em→0.04em and `line-height` 1.5→1.6 per AR overrides above.
- Font weight 800 name → Cairo ships 800 (OK). Body Tajawal 400 (OK, registered).

### 3.4 Responsive
- `@600`: avatar `48px`/img `40px`; bubble `max-width:none` (fills column); chips already wrap. Stage stays row (avatar + bubble) — at very narrow widths the bubble `flex:1`.

---

## 4. Component — ChildCardPhone (Child Card inside a phone frame)
**Preview (LTR):** `design-system/preview/mobile-child-card.html` · **RTL:** `design-system/preview/ar-child-card.html`.
**Files:** `app/_components/ChildCardPhone.tsx` + `ChildCardPhone.module.css`. `data-testid="child-card-phone"`.

### 4.1 Phone-frame approach (Q7 — confirmed)
**New sibling component that reuses `PhoneMockup.module.css` frame classes. The live hero `PhoneMockup.tsx`/`.module.css` are NOT modified** (byte-for-byte unchanged — reviewer checks this).

- `ChildCardPhone.tsx` imports `frame from '../_components/PhoneMockup.module.css'` for the **device shell only** and `styles from './ChildCardPhone.module.css'` for the in-screen child-card content.
- **Reused frame classes (device shell):** `frame.stage`, `frame.phone`, `frame.screen`. These give the 320px stage, the `#1a1a1a` 8px bezel + `--lx-radius-device` (36) + tilt + indigo drop-shadow, and the rounded inner screen.
- **Override the screen background:** the hero `.screen` paints the purple→indigo gradient. The child-card screen is a flat dark app screen. Add a local `styles.screenChild` that the component applies **alongside** `frame.screen` to override `background` → `var(--lx-bg)` (`#0F172A`, app canvas), keep `frame.screen` padding/radius/overflow. Do this by composing `className={`${frame.screen} ${styles.screenChild}`}` — CSS-module cascade lets the later class win on `background` only; do NOT edit `PhoneMockup.module.css`.
  - If cascade order is unreliable, instead author `styles.screenChild` to redeclare only `background:var(--lx-bg)` and rely on source order (ChildCardPhone module imported after) — frontend confirms at build. Acceptable fallback: a thin local `.phone/.screen` copy in `ChildCardPhone.module.css` that references the same tokens (`--lx-radius-device`/`--lx-radius-screen`). Prefer reuse; this fallback is the documented escape hatch.
- **Not a new abstraction / pattern** — it is a sibling component reusing CSS classes, the existing FeaturesSection-style shape. No provider/compound-component pattern introduced (CLAUDE.md rule 8 respected).

### 4.2 In-screen child-card markup + exact values
The card content is transcribed from `mobile-child-card.html`. Note the preview card uses `#15161D` (a near-black tile) as its own background — but here it sits **inside** the phone screen, so the child card becomes the screen's single content tile.

| Element | Preview literal | CSS-Module rule (tokens) |
|---|---|---|
| Card | `background:#15161D;border-radius:20px;padding:16px;border:1px solid rgba(255,255,255,0.06);box-shadow:0 4px 12px rgba(0,0,0,0.15);display:flex;flex-direction:column;gap:14px` | `.card{background:var(--lx-bg-card-deep);border-radius:var(--lx-radius-card);padding:var(--lx-space-4);border:1px solid var(--lx-border-soft);box-shadow:var(--lx-shadow-soft);display:flex;flex-direction:column;gap:14px}` — `--lx-bg-card-deep` **new local** = `#15161d` (§7). Brand-law rule 1 says cards step lighter; this `#15161D` is a darker card from the mobile kit — flagged §10, but it is the verified preview value, so we ship it (preview is the pixel target) and document the deviation. |
| Row 1 | `display:flex;align-items:center;gap:12px` | `.r1{display:flex;align-items:center;gap:var(--lx-space-3)}` |
| Avatar monogram | `width:52px;height:52px;border-radius:50%;background:#FB923C;color:#fff;font-weight:900;font-size:22px;box-shadow:inset 0 -2px 4px rgba(0,0,0,0.18),0 4px 12px rgba(0,0,0,0.2);flex-shrink:0` | `.av{inline-size:52px;block-size:52px;border-radius:50%;background:var(--lx-streak);color:#fff;font-family:var(--lx-font-display);font-weight:900;font-size:22px;box-shadow:inset 0 -2px 4px rgba(0,0,0,0.18),var(--lx-shadow-soft);display:flex;align-items:center;justify-content:center;flex-shrink:0}` — `#FB923C` = `--lx-streak` (exact, orange). Inner-highlight inset shadow kept literal. Do NOT mirror the avatar in RTL. |
| Name+grade group | `display:flex;align-items:center;gap:8px;flex-wrap:wrap` | `.name{display:flex;align-items:center;gap:var(--lx-space-2);flex-wrap:wrap}` |
| Name | `font-family:var(--lx-font-display);font-weight:900;font-size:18px;color:#F8FAFC` | `.nm{font-family:var(--lx-font-display);font-weight:900;font-size:18px;color:var(--lx-fg1)}`. AR uses Cairo 900 → ships 800; use 800 in RTL (gap §10). |
| Grade pill | `padding:2px 8px;border-radius:9999px;background:rgba(79,70,229,0.18);color:#A5B4FC;font-weight:800;font-size:11px` | `.gb{padding:2px 8px;border-radius:var(--lx-radius-pill);background:var(--lx-primary-soft);color:var(--lx-indigo-200);font-weight:800;font-size:11px}` |
| Email | `font-size:12px;color:var(--lx-fg3);margin-top:4px` — **AR adds `font-family:var(--lx-font-mono);direction:ltr`** | `.em{font-family:var(--lx-font-mono);font-size:12px;color:var(--lx-fg3);margin-block-start:4px;direction:ltr;text-align:start}` — `--lx-font-mono` **new local** (§7). `direction:ltr` pins the email LTR in BOTH locales (email is a technical string). |
| Chevron | LTR `›`; RTL `‹`; `color:var(--lx-fg3);font-size:22px;margin-left:auto` | `.chev{color:var(--lx-fg3);font-size:22px;margin-inline-start:auto}` + glyph from copy (`›`/`‹`). `margin-inline-start:auto` pushes it to the trailing edge in both directions. |
| Stats row | `display:flex;gap:14px;align-items:center` | `.stats{display:flex;gap:14px;align-items:center}` |
| Stat | `display:flex;align-items:center;gap:4px;font-family:var(--lx-font-display);font-weight:800;font-size:13px;font-variant-numeric:tabular-nums` + per-stat color | `.st{display:flex;align-items:center;gap:var(--lx-space-1);font-family:var(--lx-font-display);font-weight:800;font-size:13px;font-variant-numeric:tabular-nums}` |
| Stat colors | `🧠 Lv` `#A855F7` · `⭐` `#FACC15` · `🔥` `#FB923C` | `.stLevel{color:var(--lx-purple)}` · `.stXp{color:var(--lx-xp)}` · `.stStreak{color:var(--lx-streak)}` — all exact token matches |
| Status dot | `margin-left:auto;font-size:11px;color:var(--lx-fg3)(EN)/#22C55E(AR);font-weight:600`; `::before` dot `8px;#22C55E;box-shadow:0 0 6px rgba(34,197,94,0.6)` | `.dot{margin-inline-start:auto;display:flex;align-items:center;gap:var(--lx-space-1);font-size:11px;color:var(--lx-fg3);font-weight:600}` + `.dot::before{content:'';inline-size:8px;block-size:8px;border-radius:50%;background:var(--lx-secondary);box-shadow:0 0 6px var(--lx-success-glow)}` — `--lx-success-glow` **new local** = `rgba(34,197,94,0.6)` (§7). EN dot text `--lx-fg3`; AR preview colors text green — **match per locale**: `:global([dir='rtl']) .dot{color:var(--lx-secondary)}`. |
| Footer | `display:flex;justify-content:space-between;padding-top:12px;border-top:1px solid rgba(255,255,255,0.05);font-size:12px;color:var(--lx-fg2)` | `.ft{display:flex;justify-content:space-between;padding-block-start:var(--lx-space-3);border-block-start:1px solid var(--lx-border-soft);font-size:12px;color:var(--lx-fg2)}` |
| Footer "Language:" label | `color:var(--lx-fg3)` + flag + lang name | `.ftLabel{color:var(--lx-fg3)}` |
| View progress link | `color:#A5B4FC;font-weight:700` + arrow | `.vp{color:var(--lx-indigo-200);font-weight:700}` |

### 4.3 Copy (`copy.<locale>.childCard`)
| Key | EN | AR |
|---|---|---|
| monogram | `S` | `س` |
| name | `Sami` | `سامي` |
| grade | `Grade 3` | `الصف ٣` |
| email | `sami@learnexia.com` | `sami@learnexia.com` (LTR-pinned both) |
| chevron | `›` | `‹` |
| statLevel | `🧠 Lv 12` | `🧠 المستوى ١٢` |
| statXp | `⭐ 1,240` | `⭐ ١٬٢٤٠` (Arabic-Indic + Arabic thousands separator `٬`) |
| statStreak | `🔥 7d` | `🔥 ٧ أيام` |
| statusActive | `Active today` | `نشط اليوم` |
| langLabel | `Language:` | `اللغة:` |
| langValue | `🇬🇧 English` | `🇸🇦 العربية` |
| viewProgress | `View progress →` | `عرض التقدم ←` |

### 4.4 RTL deltas (critical)
- **Chevron** `›` → `‹` (from copy string).
- **Footer arrow** `→` → `←` (from copy string — note it stays a *trailing* "next/forward" arrow per Arabic/RTL convention; the AR preview uses `←`).
- **Email pinned LTR** via `direction:ltr` (both locales) — never mirror an email.
- **Avatar monogram** does NOT mirror (it's a glyph shape); just swaps `S`→`س` via copy.
- **Status dot** floats to the trailing edge via `margin-inline-start:auto` (right in LTR, left in RTL). AR text colored green per preview override.
- **Brand name** in email domain stays Latin (technical).
- Stats row + footer flip side automatically via flex + `dir`.

### 4.5 Responsive
- Phone stage is `width:320px;max-width:100%` (from `frame.stage`). At `@600` the 320 phone scales down via `max-width:100%`; center it. The card content is fluid inside.
- Present `ChildCardPhone` and `AITutorBubble` together in one "see the product" section (§5) — two columns @1024, stacked @768/@390.

---

## 5. Page placement (within `app/[locale]/page.tsx` section flow)
Current flow: `nav → hero(PhoneMockup) → FeaturesSection → SubjectsBand → CTABanner → SiteFooter`.

**New flow (insert the 4 components after `SubjectsBand`, before `CTABanner`):**
```
nav (+ LanguageSwitcher)
hero (PhoneMockup, untouched)
FeaturesSection
SubjectsBand
── NEW ──────────────────────────────
"See it in action" product section:
  ├─ AITutorBubble        (full-width band, dark bg-deep, like FeaturesSection)
  ├─ ChildCardPhone       (paired with the bubble, OR its own band)
ActivityChart             (full-width band, max-width 920, like a parent-proof strip)
BenefitsPanel             (full-width purple gradient panel — visual "break" before CTA)
─────────────────────────────────────
CTABanner
SiteFooter
```
Rationale (SKILL.md marketing recipe + brand-law one-primary-action): keep the single primary CTA at the CTABanner. The product proof (tutor bubble + child card screenshot) and parent proof (activity chart + benefits) sit between SubjectsBand and the closing CTA, escalating "what you get" before the ask. BenefitsPanel's saturated purple panel is the deliberate color "break" immediately before the gradient CTA — do not place two saturated panels adjacent without the dark Activity strip between them.

**Concrete order to render:** `AITutorBubble` → `ChildCardPhone` → `ActivityChart` → `BenefitsPanel`. Frontend may wrap AITutorBubble + ChildCardPhone in a single two-column `<section>` (a "product preview" band) for layout economy; ActivityChart and BenefitsPanel are their own full-width bands. Each new band mirrors `FeaturesSection`'s `.section` (`background:var(--lx-bg-deep);border-top:1px solid var(--lx-border-soft)`) + `.inner` (`max-width`, centered, `padding:96px var(--lx-space-12)`) wrapper so vertical rhythm matches.

---

## 6. LanguageSwitcher (top-nav EN/AR toggle)
**Source:** `design-system/preview/ar-web-nav.html` (the nav layout; the toggle control itself is a designer addition matching the nav's button styling). **File:** `app/_components/LanguageSwitcher.tsx` + `.module.css`. `data-testid="lang-switcher"`.

### 6.1 Placement
Inside the existing `.navActions` cluster in `page.tsx`, **before** the Log in / Start free buttons (leading edge of the actions group, so the language toggle reads first). On RTL the whole `.navActions` cluster mirrors to the left side automatically. Remove the footer `العربية` link (`copy.footer.links.arabic`) — the switcher replaces it.

### 6.2 Visual (match nav button styling from ar-web-nav.html)
Two-segment pill toggle, styled like the nav's outline button (`height:36px;border-radius:var(--lx-radius-button)` /* the AR nav uses 12px radius on its buttons — note: ar-web-nav uses `border-radius:12px`, while the marketing nav uses `--lx-radius-button`=16. **Use `--lx-radius-button` (16) for consistency with the live nav buttons** — flagged as an intended 4px deviation from the preview's 12). Segments:
| State | Style |
|---|---|
| Container | `display:inline-flex;height:36px;border-radius:var(--lx-radius-button);border:1px solid var(--lx-border-strong);overflow:hidden;font-size:13px;font-weight:700` |
| Segment (inactive) | `padding:0 12px;display:flex;align-items:center;color:var(--lx-fg2);background:transparent` |
| Segment (active) | `background:var(--lx-primary-soft);color:var(--lx-indigo-200)` (active-pill convention) |
| Hover (inactive) | brighten: `color:var(--lx-fg1)` (never darken — brand-law rule 10) |
| Focus | `--lx-focus-ring` equivalent: `outline:2px solid var(--lx-primary);outline-offset:2px` + the existing focus convention |
| Labels | `EN` / `ع` (or `AR` — use `EN`/`ع` to mirror the bilingual toggle convention; the active locale's own script) |

Each segment is a Next.js `<Link>` to the locale-swapped path (`/en` ↔ `/ar`) preserving the rest of the path (single-page site → just `/en` or `/ar`). The active segment reflects `params.locale`. Touch target: container is 36px tall; pad segments so each tap target ≥44px wide (brand-law ≥48px target — bump container to `height:44px` on coarse-pointer if needed; flagged §10 as a minor a11y note since the nav buttons themselves are 36px in the preview).

### 6.3 Brand wordmark LTR pin (nav, both locales)
Per `ar-web-nav.html`: the `Learnexia` wordmark carries `dir="ltr"` even in RTL. The live nav uses the `/assets/logo.svg` image (already direction-agnostic) — no change needed, but if a text wordmark is ever used, pin `dir="ltr"`. The AR nav links order (`كيف يعمل / المواد / للمدارس / الأسعار`) maps 1:1 to the existing `nav.howItWorks / subjects / forSchools / pricing` keys.

---

## 7. Token decisions table (every non-tokenized value)
Add the **new local `--lx-*` vars** to `apps/marketing-site/app/globals.css` `:root`, following the existing "local:" comment convention (e.g. lines 31, 47–48, 63, 77–78). Reuses cite the existing token.

| # | Preview literal | Used by | Decision | Token / value |
|---|---|---|---|---|
| 1 | `linear-gradient(165deg,#1E1B4B 0%,#3B2C8F 50%,#5B21B6 100%)` | BenefitsPanel bg | **ADD local** | `--lx-grad-benefits: linear-gradient(165deg,#1e1b4b 0%,#3b2c8f 50%,#5b21b6 100%); /* local: benefits-panel gradient (web-benefits-panel) */` |
| 2 | `rgba(255,255,255,0.92)` | BenefitsPanel row text | **ADD local** | `--lx-on-purple: rgba(255,255,255,0.92); /* local: text on purple panel */` |
| 3 | `rgba(255,255,255,0.1)` | BenefitsPanel icon tile bg | **ADD local** | `--lx-on-purple-tile: rgba(255,255,255,0.1); /* local: tile on purple panel */` |
| 4 | `linear-gradient(180deg,#334155,#1E293B)` | ActivityChart default bar | **REUSE via new alias** | `--lx-grad-bar: linear-gradient(180deg,var(--lx-card-soft),var(--lx-card)); /* local: chart bar = card-soft→card */` |
| 5 | `linear-gradient(180deg,#A855F7,#4F46E5)` | ActivityChart Sunday bar | **ADD local (Level-Up, vertical)** | `--lx-grad-levelup-180: linear-gradient(180deg,var(--lx-purple),var(--lx-primary)); /* local: vertical level-up for highlight bar */` |
| 6 | `#A5B4FC` (indigo-200) | chart export/value, chips, grade pill, view-progress | **ADD local** | `--lx-indigo-200: #a5b4fc; /* local: indigo-200 link/label accent */` |
| 7 | `#64748B` | chart value label | **REUSE** | `--lx-fg4` (already `#64748b`) |
| 8 | `#94A3B8` | chart subtitle/day label, email | **REUSE** | `--lx-fg3` (already `#94a3b8`) |
| 9 | `linear-gradient(135deg,#A78BFA,#6366F1)` | tutor avatar | **ADD local** | `--lx-grad-avatar: linear-gradient(135deg,#a78bfa,#6366f1); /* local: tutor/avatar gradient */` |
| 10 | `rgba(15,23,42,0.7)` | tutor bubble frosted bg | **ADD local** | `--lx-overlay-frost: rgba(15,23,42,0.7); /* local: frosted overlay (tutor bubble) */` |
| 11 | `rgba(255,255,255,0.1)` | tutor bubble border | **ADD local** | `--lx-border-frost: rgba(255,255,255,0.1); /* local: frosted bubble hairline */` |
| 12 | `rgba(99,102,241,0.3)` | tutor chip border | **ADD local** | `--lx-primary-chip-border: rgba(99,102,241,0.3); /* local: tutor chip border */` |
| 13 | `rgba(79,70,229,0.18)` | tutor chip / grade pill bg | **REUSE** | `--lx-primary-soft` (exact) |
| 14 | `#FACC15` (xp gold) | tutor highlight, stat ⭐ | **REUSE** | `--lx-xp` (exact) |
| 15 | `#15161D` | child card bg | **ADD local** | `--lx-bg-card-deep: #15161d; /* local: mobile child-card surface (mobile-child-card) — darker kit value, see Design Gap */` |
| 16 | `#FB923C` | avatar monogram, 🔥 stat | **REUSE** | `--lx-streak` (exact) |
| 17 | `rgba(34,197,94,0.6)` | status dot glow | **ADD local** | `--lx-success-glow: rgba(34,197,94,0.6); /* local: success/active dot glow */` |
| 18 | `var(--lx-font-mono)` | child-card email | **ADD local** | `--lx-font-mono: ui-monospace, 'SF Mono', 'Cascadia Code', Menlo, Consolas, monospace; /* local: technical strings (email) */` |
| 19 | `0 6px 18px rgba(99,102,241,0.4)` | Sunday bar glow | **REUSE** | `box-shadow:0 6px 18px var(--lx-primary-glow)` (`0.45`≈`0.4`) |
| 20 | `#A855F7` `#22C55E` `#4F46E5` `#F8FAFC` `#CBD5E1` | various | **REUSE** | `--lx-purple` · `--lx-secondary` · `--lx-primary` · `--lx-fg1` · `--lx-fg2` (all exact) |

**Literals kept (no token, no 90° corner — brand-law rule 4 OK):** bar radius `10px 10px 4px 4px`; bubble radius `22px` + tail `4px`; icon-tile radius `10px`; inset highlight `inset 0 -2px 4px rgba(0,0,0,0.18)`; assorted `2px/4px/6px/14px/18px/22px` paddings that don't map to a space step (use literal px, matching the preview exactly — fidelity beats forcing a token step).

---

## 8. Motion (brand-law rule 12 — snappy, ≤800ms; honor `prefers-reduced-motion`)
These are presentational marketing screenshots, so motion is light. `globals.css` already gates all animation behind `prefers-reduced-motion`.
- **BenefitsPanel 🎮 glyph:** static drop-shadow glow (no animation needed). Optional: reuse `lxpulse` (already defined) only if a subtle idle is wanted — default **none**.
- **ActivityChart bars:** optional one-shot grow-in on scroll-into-view (`transform: scaleY(0)→1`, transform-origin bottom, 600ms `--lx-ease-out`) — matches XP-fill feel. Sunday bar may add an end glow flash. **If implemented, must respect reduced-motion.** Default acceptable: static.
- **AITutorBubble:** static. Optional chip hover = brighten + scale 1.02 (`--lx-ease-out`), press scale 0.95/80ms — but chips are inert here, so hover-brighten only.
- **ChildCardPhone:** static screenshot; the phone frame keeps the hero's `rotate(-4deg)` tilt (from `frame.phone`). Status dot may keep a subtle glow (static box-shadow, not animated).
- **LanguageSwitcher:** segment color transition `var(--lx-dur-fast)` on hover/active; no layout shift.
- **Page transitions** between `/en`↔`/ar`: standard Next navigation (full document re-render due to `dir` change) — no custom transition required.

---

## 9. RTL / i18n summary (all flips in one place)
| Component | Flips in RTL | Mechanism |
|---|---|---|
| BenefitsPanel | icon-tile leads / text follows; `text-align:start` | flex + `dir` (no override) |
| ActivityChart | **bar order mirrors** (Sun→left); day/value centering unaffected | `[dir='rtl'] .bars{flex-direction:row-reverse}` |
| AITutorBubble | **tail corner** bottom-left→bottom-right; avatar/bubble order; name `letter-spacing` 0.08→0.04em; msg `line-height` 1.5→1.6 | `border-end-start-radius` (logical) + `[dir='rtl']` overrides |
| ChildCardPhone | **chevron** `›`→`‹`; **footer arrow** `→`→`←`; stats/footer side; status-dot trailing edge; dot text→green | copy strings + `margin-inline-start:auto` + `[dir='rtl']` color |
| **Never flips** | avatar/monogram gradients & glyphs; **email** (`direction:ltr` both locales); brand `Learnexia` / `COPPA` / `CSV` (Latin technical) | `direction:ltr` pins / leave as-is |
| Numerals | Arabic-Indic in all `copy.ar` strings (chart values, Lv ١٢, ⭐ ١٬٢٤٠, ٧ أيام, الصف ٣); Western in `copy.en` | string-level, not computed |
| Fonts | headings → Cairo, body → Tajawal via existing `[dir='rtl']` font-var swap in `globals.css` | already wired |

Logical properties everywhere (`inset-inline-*`, `margin-inline-*`, `padding-inline/block-*`, `text-align:start/end`, `border-*-start/end-radius`) — matches `PhoneMockup.module.css`. No physical `left/right/margin-left` in new code.

---

## 10. Accessibility / kid-UX notes
- Decorative emoji = `role="img"` + `aria-label` (subject/stat meaning) or `aria-hidden` where purely decorative (mirror PhoneMockup's pattern). Brand-law rule 9: only the sanctioned semantic emoji set is used (🔥 streak · ⭐ XP · 🧠 level · 🏆 badge · ✨/📊/🛡️ benefits · 🎮/🌟 reward) — all present here are semantic.
- Tutor + child-card mascot/avatar images get meaningful `alt` (`alt=""` if decorative; the owl avatar is decorative → `alt=""`).
- Export CSV is a real `<button type="button">` (keyboard-focusable) but inert — give it `aria-disabled="true"` OR simply no handler; do NOT remove from tab order silently. Recommendation: `aria-disabled="true"` + `tabIndex={-1}` is overkill for marketing — leave focusable, no-op, since it visually reads as available. (Reviewer note: it must not navigate/crash.)
- Focus rings on switcher + chips + export per `--lx-focus-ring` convention (2px indigo + offset).
- LanguageSwitcher: each segment is a `<Link>` (real navigation, works without JS) — `aria-current="true"` on the active locale segment.
- Contrast: `--lx-fg3` (`#94a3b8`) on `--lx-card`/`#15161D` is the kit's chosen muted color (passes AA for the 11–12px secondary text per the kit). Keep.

---

## 11. Implementation handoff (per piece)
| Piece | Target |
|---|---|
| 5 new local `--lx-*` vars actually + reuses | `apps/marketing-site/app/globals.css` `:root` (and one `[dir='rtl']` is NOT needed — all flips are component-level) — see §7 table (add tokens #1,2,3,4,5,6,9,10,11,12,15,17,18) |
| BenefitsPanel | `apps/marketing-site/app/_components/BenefitsPanel.tsx` + `.module.css`; rendered in `app/[locale]/page.tsx` |
| ActivityChart | `apps/marketing-site/app/_components/ActivityChart.tsx` + `.module.css`; `CHART_VALUES`/heights const local; rendered in `page.tsx` |
| AITutorBubble | `apps/marketing-site/app/_components/AITutorBubble.tsx` + `.module.css`; `public/assets/mascot-owl.svg` copied from `design-system/assets/`; rendered in `page.tsx` |
| ChildCardPhone | `apps/marketing-site/app/_components/ChildCardPhone.tsx` + `.module.css`; imports `PhoneMockup.module.css` frame classes (read-only); rendered in `page.tsx` |
| LanguageSwitcher | `apps/marketing-site/app/_components/LanguageSwitcher.tsx` + `.module.css`; placed in `.navActions` in `page.tsx`; footer `العربية` removed |
| Copy | `apps/marketing-site/lib/copy.ts` → `COPY.en`/`COPY.ar` + `getCopy(locale)`; new slices `benefits`, `activityChart`, `tutorBubble`, `childCard` |
| Locale shell | `app/[locale]/layout.tsx` (`lang`/`dir` from `params.locale`), `middleware.ts` (`/`→`/en`) — per plan FE-B1 batch |

---

## 12. Design gaps / open questions (flag — do not silently fix)
1. **Mascot owl is a placeholder** (brand-law rule 11) — used in AITutorBubble. Acceptable for marketing screenshot; flag that the final owl art is pending. Ensure `public/assets/mascot-owl.svg` exists (copied per Q6/plan).
2. **Cairo weight 900:** previews specify Cairo `font-weight:900` (BenefitsPanel heading, child-card name) but `globals.css` only registers Cairo 400/600/700/800. In RTL these render at **800** (no 900 face) — visually slightly lighter than the LTR Poppins 900. Either accept (recommended — 800 Cairo is already chunky) or add a Cairo-Black 900 `@font-face`. Flagged for lead; spec ships with 800 in AR.
3. **`#15161D` child-card surface is darker than the dark canvas** (`#0F172A`) and steps *darker* than a card — technically against brand-law rule 1 ("cards step lighter"). It is the verified `mobile-child-card.html` value, and pixel-fidelity to the preview is the higher-priority rule, so we ship `#15161D` and document the deviation. Confirm the lead is OK with the kit's own value over the general rule (preview wins).
4. **LanguageSwitcher touch target:** nav buttons are 36px tall in the preview (< the 48px kid-UX target). Marketing nav is parent-facing desktop-first, so 36px matches the kit; flag that on coarse-pointer the switcher could bump to 44px. Designer recommends keeping 36px to match the live nav buttons.
5. **Switcher radius 16 vs preview 12:** the live marketing nav buttons use `--lx-radius-button` (16); `ar-web-nav.html` uses `12px`. Spec chooses **16** for internal consistency with the existing nav — intended 4px deviation, documented.
6. **No AR preview for BenefitsPanel & ActivityChart** — RTL is derived via logical properties + `copy.ar` (no pixel twin exists). The bar-order mirror is per the brief's explicit AC, overriding the general "charts stay LTR" convention.

---

Design spec ready for frontend.
