# Design Spec — Pixel-Alignment Pass: Landing Page (marketing-site)

**Capture target:** `design-system/screenshots/web/01-landing.png`
**Surface:** `apps/marketing-site/` — Next.js 15, EN/LTR primary
**Scope:** delta table only — every element where current implementation diverges from the capture and/or the canonical preview cards. No new screens, no new components.
**HANDOFF note:** marketing-site is English-only for now. AR/RTL appendix is at the bottom.

---

## Delta Table

Order: Blocker > Major > Minor. Each row cites the proving source (capture section + preview card).

| # | Element | Current | Target (token + card) | Severity | Fix |
|---|---|---|---|---|---|
| **B-1** | **Nav — backdrop / background** | `background: var(--lx-bg)` = solid `#0F172A`, no blur | `rgba(15,23,42,0.85)` + `backdrop-filter: blur(20px)` — the frosted glass nav visible in capture and specified in `web-nav.html` line 4 | **Blocker** | Change `.nav` background to `rgba(15,23,42,0.85)` and add `backdrop-filter:blur(20px);-webkit-backdrop-filter:blur(20px)`. Also align border to `rgba(255,255,255,0.05)` (capture shows near-invisible line; current is `var(--lx-border)` = `rgba(255,255,255,0.08)` — tolerable but `web-nav.html` uses 0.05). |
| **B-2** | **Nav — "Log in" button shape** | Rendered as plain text link (no border, no background box), styled with `border: none; background: transparent` and just a color transition | Capture shows "Log in" inside a visible outlined pill/rect — `web-nav.html` uses `border-radius:12px; border:1px solid rgba(255,255,255,0.12)` ghost button; `components-buttons.html` `.ghost` = `border:1px solid rgba(255,255,255,0.16)`, `border-radius:16px` | **Blocker** | Add `border: 1px solid var(--lx-border-strong)` to `.btnOutline`. Radius should be `var(--lx-radius-button)` = 16px (token `--lx-radius-button`). Height: match "Start free" = 36px in the nav (see `web-nav.html`: `height:36px`). |
| **B-3** | **Hero headline — accent treatment** | `color: var(--lx-accent)` = flat `#F59E0B` on "adventure game" | `PagesPublic.jsx` line 56 + capture shows warm yellow-to-orange **gradient text**: `background: linear-gradient(90deg,#FACC15,#FB923C); -webkit-background-clip:text; -webkit-text-fill-color:transparent` | **Blocker** | Replace `.headlineAccent { color: var(--lx-accent) }` with gradient text: `background: linear-gradient(90deg, var(--lx-xp), var(--lx-streak)); -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text`. Tokens: `--lx-xp` (#FACC15) and `--lx-streak` (#FB923C). |
| **B-4** | **Hero — radial glow color/origin** | `.heroGlow` is a mix of `var(--lx-primary-soft)` (indigo) and `rgba(168,85,247,0.06)` positioned top-left | Capture shows a strong **purple** radial glow behind the headline area. `PagesPublic.jsx` line 40: `radial-gradient(circle, rgba(168,85,247,0.25) 0%, transparent 65%)` positioned `top:-80, left:-80`. Current glow is too dim (0.06 for purple) and origin offset is off (`inset-inline-start:-80px` is correct but paired with wrong inner color) | **Blocker** | Change `.heroGlow` inner stop to `rgba(168,85,247,0.25)` (remove the `var(--lx-primary-soft)` stop entirely). Final value: `radial-gradient(circle, rgba(168,85,247,0.25) 0%, transparent 65%)`. Dimensions: 480×480px. Position: `top:-80px; left:-80px` (not `inset-inline-start` — this is LTR only). |
| **B-5** | **Phone mock — gradient / screen fill** | Screen: `linear-gradient(160deg, #6d28d9 0%, #5b21b6 40%, #4c1d95 100%)` — dark purple/grape | Capture + `web-hero-phonemock.html` + `PagesPublic.jsx` use `linear-gradient(165deg, #A855F7 0%, #4F46E5 50%, #1E293B 100%)` — starts at bright `#A855F7` (token `--lx-purple`), transitions to primary indigo, then to card dark. Current is too dark and grape-toned, not the bright gamified purple | **Blocker** | Change `.screen` background to `linear-gradient(165deg, var(--lx-purple) 0%, var(--lx-primary) 50%, var(--lx-card) 100%)`. Tokens: `--lx-purple` = `#A855F7`, `--lx-primary` = `#4F46E5`, `--lx-card` = `#1E293B`. |
| **B-6** | **Phone mock — device frame border + rotation** | `border: 2px solid var(--lx-border-strong)` (white 16%); no rotation on `.phone` | Capture + `PagesPublic.jsx` show `border: 8px solid #1a1a1a` (near-black, not white tint) and `transform: rotate(-4deg)` on the phone. Current has no tilt and a white-ish border | **Blocker** | Change `.phone` border to `8px solid #1a1a1a`. Add `transform: rotate(-4deg)`. Keep the indigo box-shadow glow: `0 40px 100px rgba(99,102,241,0.5), 0 0 0 1px rgba(255,255,255,0.05)`. Current `box-shadow` includes the right glow but spread/blur differs — match `PagesPublic.jsx` exactly. |
| **B-7** | **XP chip — background color** | `.chipXp { background: var(--lx-secondary) }` = green `#22C55E`; text `color: var(--lx-fg-inverse)` = `#0F172A` | Capture shows the "+50 XP ⭐" chip in green — this part is correct. But the chip is MISSING the emoji star in the copy. Current copy.ts: `chips.xp = '+50 XP'` (no star). Capture + `web-hero-phonemock.html` + SKILL.md rule: chip must show real reward `+50 XP ⭐` | **Blocker** | Update `lib/copy.ts`: `xp: '+50 XP ⭐'`. The "⭐" is semantic (XP icon per README rule 8). Also verify chip rotation: `PagesPublic.jsx` applies `transform: rotate(8deg)` — current CSS has no rotation on `.chipXp`. Add `transform: rotate(8deg)`. |
| **B-8** | **Below-fold: Features, Subjects, CTA, Footer** | Implemented as plain `.section` stubs — a centered `h2` + `p` + optional CTA button with only a border-top divider. No feature cards, no subject band, no gradient CTA banner, no proper footer | Capture (scrollable) + `PagesPublic.jsx` (lines 127–219) require: (a) **3-column feature grid** (`web-feature-card.html`), (b) **4-column subjects band** (`web-subject-band.html` — must show Math/Science/Arabic/English, not Social Studies), (c) **gradient CTA banner** (`web-cta-banner.html` — `linear-gradient(135deg, #4F46E5, #A855F7)`, radius 28px, 56px padding, white CTA button), (d) **footer** (`web-footer.html` — two-column with logo-mark + copyright left, Privacy/Terms/Support/العربية links right) | **Blocker** | Implement all four sections. See Implementation Handoff below for per-section detail. |

| # | Element | Current | Target (token + card) | Severity | Fix |
|---|---|---|---|---|---|
| **M-1** | **Nav — nav-link color** | `color: var(--lx-fg1)` = `#F8FAFC` (full white) | `web-nav.html` uses `color:#CBD5E1` = `var(--lx-fg2)` for nav links. Capture shows slightly muted link text, not pure white | **Major** | Change `.navLinks` color to `var(--lx-fg2)`. Keep hover as `var(--lx-fg3)` (already correct direction but goes too dim — hover to `var(--lx-fg1)` instead, brightening on hover matches the README interaction rule). |
| **M-2** | **Nav — nav-link font size** | `font-size: 15px` | `web-nav.html` uses `font-size:13px`. `PagesPublic.jsx` uses `font-size:14px`. Capture reads as ~13–14px — the 15px current value makes the nav feel heavy | **Major** | Change `.navLinks` to `font-size: 14px` (split the difference, matches `PagesPublic.jsx`). |
| **M-3** | **Nav — button height** | No explicit height on `.btnOutline` / `.btnPrimary` in nav; padding `10px 20px` produces ~38–40px height | `web-nav.html` specifies `height:36px` for both buttons. Capture shows compact nav buttons | **Major** | Set explicit `height: 36px; padding: 0 14px` on both `.btnOutline` and `.btnPrimary` within the nav context. (Hero CTA buttons remain `padding: 18px 24px`.) Use separate selectors: `.navActions .btnOutline` and `.navActions .btnPrimary`. |
| **M-4** | **Pill badge — background and border** | `.pill { background: var(--lx-primary-soft); border: 1px solid var(--lx-border-strong); color: var(--lx-primary-hover) }` — indigo-tinted | `PagesPublic.jsx` line 46 and `web-hero-phonemock.html` analogue: `background: rgba(168,85,247,0.15); border: 1px solid rgba(168,85,247,0.3); color: #A855F7` — purple-tinted. The capture pill reads as purple | **Major** | Change `.pill` to `background: rgba(168,85,247,0.15); border: 1px solid rgba(168,85,247,0.3); color: var(--lx-purple)`. Token: `--lx-purple-soft` = `rgba(168,85,247,0.18)` (close enough, use it). |
| **M-5** | **Pill badge — spark character** | Renders `✦` (four-pointed star) | `PagesPublic.jsx` uses `✨` (sparkles emoji). Capture shows a glowing sparkle icon, closer to ✨ than ✦. README emoji rules allow ✨ for AI/achievement context | **Major** | Change `C.hero.pill` to `'✨ POWERED BY AI'` and remove the separate `.pillSpark` span (embed inline). Or keep the span but swap to `✨`. |
| **M-6** | **Hero headline — font size** | `clamp(40px, 5.2vw, 68px)` — max 68px | `PagesPublic.jsx` uses `fontSize: 64` (fixed). Capture headline at 1280px reads at approximately 64–68px, filling the left column. At 1024px `clamp` would produce ~53px which may under-deliver | **Major** | Keep clamp for responsiveness but raise the floor: `clamp(48px, 5.5vw, 68px)`. At 1280px: `5.5vw` = 70px → capped at 68px. At 1024px: `5.5vw` = 56px. At 768px: 48px floor. Matches capture better. Letter-spacing: already `-0.02em` (correct, token `--lx-tracking-tight`). |
| **M-7** | **Hero paragraph — font size** | `font-size: 19px` | `PagesPublic.jsx` uses `fontSize: 18`. Capture body at ~18px. 19px is close but slightly off token | **Major** | Change `.paragraph` to `font-size: 18px`. Line-height stays `1.55` (matches reference). |
| **M-8** | **CTA primary button — background** | `background: var(--lx-primary)` = flat `#4F46E5` | Capture shows the "Create parent account" CTA as a solid deep indigo. This matches the token correctly. However `PagesPublic.jsx` applies a **Level-Up gradient** on the CTA section button (white on gradient). For the hero CTA button, solid indigo is correct per the capture. Flag: the hero CTA should stay solid indigo, NOT gradient. Already correct — but confirm no gradient was accidentally added | **Major** | Verify `.ctaPrimary` stays `background: var(--lx-primary)` (solid). Add hover: `background: var(--lx-primary-hover)` (already present). Add `active` scale: `transform: scale(0.95)` on `:active` (80ms, per `components-buttons.html` rule). |
| **M-9** | **Phone mock — chip: "New badge!" text and rotation** | `.chipBadge` content: `🏆 New badge!` positioned `bottom:96px; inset-inline-start:-56px`. No rotation | `PagesPublic.jsx` applies `transform: rotate(-4deg)`. `web-hero-phonemock.html` has same tilt. Also position: `bottom:70px; left:-40px` in reference (current offset slightly different). Capture shows badge chip bottom-left of phone, slightly tilted left | **Major** | Add `transform: rotate(-4deg)` to `.chipBadge`. Adjust position: `bottom: 70px; inset-inline-start: -40px`. |
| **M-10** | **Phone mock — progress bar gradient** | `.progressFill { background: var(--lx-grad-xp) }` = `#22C55E → #4F46E5` | `web-hero-phonemock.html` uses `linear-gradient(90deg,#22C55E,#FACC15)` — green to yellow (not green to indigo). Capture bar shows warm green-to-yellow fill | **Major** | Change `.progressFill` background to `linear-gradient(90deg, var(--lx-secondary), var(--lx-xp))`. Tokens: `--lx-secondary` (#22C55E) + `--lx-xp` (#FACC15). |
| **M-11** | **Phone mock — subject grid 4th tile shows "GB" text** | `phone.subjectEnglish = 'GB'` as text in a `.subjectText` span | `web-hero-phonemock.html` uses `🇬🇧` flag emoji in all four tiles. Capture shows emoji in all 4 grid cells. "GB" is a fallback that degrades to plain text when the flag emoji does not render, but it should be the emoji | **Major** | Change `phone.subjectEnglish` copy to `'🇬🇧'`. Render it as a `<span className={styles.subjectIcon}>` (emoji span) identical to the other three tiles, not `.subjectText`. |
| **M-12** | **Phone mock — star element** | `.star` renders `✦` at 24px with xp-glow shadow | `PagesPublic.jsx` + `web-hero-phonemock.html` use `🌟` at 32px with `animation: lxpulse 2s ease-in-out infinite`. Capture shows a glowing animated star, not the thin ✦ glyph | **Major** | Change `.star` content to `🌟`, `font-size: 32px`. Add CSS animation: `@keyframes lxpulse { 0%,100%{transform:scale(1);opacity:1} 50%{transform:scale(1.08);opacity:0.85} }` and apply `animation: lxpulse 2s ease-in-out infinite`. |
| **M-13** | **Hero grid — column ratio** | `grid-template-columns: 1.05fr 0.95fr` — nearly equal | `PagesPublic.jsx` uses `1.1fr 1fr`. Capture shows copy column slightly wider than phone column | **Major** | Change to `grid-template-columns: 1.1fr 1fr`. |
| **M-14** | **Hero section — vertical padding** | `padding: var(--lx-space-16) var(--lx-space-6)` = 64px top/bottom | `PagesPublic.jsx`: `padding: '72px 48px 96px'`. Capture shows more breathing room below the CTA row before the next section. Bottom 96px is correct for the scroll-reveal effect | **Major** | Change `.hero` padding to `72px var(--lx-space-12) 96px`. Keep horizontal at `var(--lx-space-12)` = 48px to match reference. |
| **M-15** | **Trust row — gap** | `gap: var(--lx-space-6)` = 24px | `PagesPublic.jsx` uses `gap: 28`. Capture shows three items with comfortable spacing (~28px). 24px is fine but slightly tight | **Major** | Change `.trustRow` gap to `28px`. |

| # | Element | Current | Target (token + card) | Severity | Fix |
|---|---|---|---|---|---|
| **m-1** | **Nav — `max-width` / horizontal padding** | `.navInner` max-width `1180px`, padding `16px 24px` | `PagesPublic.jsx` nav: `padding: '18px 48px'`, no explicit max-width (full-width nav). Capture nav spans full width. `web-nav.html`: `padding:18px 28px`. Split difference: use `padding:18px 48px` (matches `PagesPublic.jsx`) and remove the max-width constrain on the inner nav so logo is left-edge, actions are right-edge | **Minor** | Change `.navInner` to `padding: 18px var(--lx-space-12)` (48px horizontal). Set `max-width: none` or remove the constraint. Keep `justify-content: space-between`. |
| **m-2** | **Phone mock — frame border-radius** | `.phone` `border-radius: 44px` | `PagesPublic.jsx` uses `borderRadius: 44` — matches. But inner `.screen` uses `border-radius: 32px`. `web-hero-phonemock.html` phone outer `border-radius: 32px`, no explicit inner screen radius (relies on overflow:hidden of the outer 32px shell). Capture phone has the same outer 32px look. Token `radii.html`: pill/modal are the closest buckets; 32px (inner screen) is between modal (24) and pill. No token gap — use `28px` or `32px` as a literal, flag it | **Minor** | Tighten `.phone border-radius` to `36px` (close to capture which shows slightly less curvature than 44px). Keep `.screen border-radius: 28px`. Flag: no `--lx-radius-*` token covers 28/36px — design gap (see Design Gaps). |
| **m-3** | **CTA secondary button — hover state** | `border-color: var(--lx-fg3)` on hover (goes to muted grey) | README interaction rule: "Brighten on hover, never darken". Hover should brighten the border to `var(--lx-border-strong)` → `var(--lx-fg2)` (slate-300) | **Minor** | Change `.ctaSecondary:hover` to `border-color: var(--lx-fg2); background: var(--lx-card-soft)` (slight surface brighten). |
| **m-4** | **Trust row — icon size** | `.trustIcon font-size: 15px` | `PagesPublic.jsx` inline emoji runs at inherited 13px. Capture trust row is visually compact | **Minor** | Remove explicit `.trustIcon { font-size: 15px }` and let it inherit the `14px` row size. |
| **m-5** | **Footer — copy** | `C.footer.rights = '© Learnexia. Learning, leveled up.'` with no logo-mark, no links | `web-footer.html` + `PagesPublic.jsx`: two-column footer with logo-mark (28px, 0.7 opacity) + "© 2026 Learnexia · Made for curious kids" left; Privacy / Terms / Support / العربية links right. Capture is consistent | **Minor** | Update footer markup and copy. (Covered by B-8 — implement the full footer component.) |
| **m-6** | **Play icon in Watch demo button** | `▶` character + separate `.playIcon` span at 12px | `PagesPublic.jsx` renders `▶` + space at inherited size with `marginRight:8`. No separate sizing needed | **Minor** | Remove `.playIcon { font-size: 12px }` override — let it inherit 17px from the button. |
| **m-7** | **Nav sticky backdrop — z-index** | `z-index: 10` | `PagesPublic.jsx` uses 10. Adequate — no delta. Cite: `web-nav.html` has no explicit z-index (inline nav has natural stacking). No change needed | **Minor** | No action. |
| **m-8** | **Max-width of hero `max-width`** | `1180px` | `PagesPublic.jsx` uses `1280px` as the outer frame (browser chrome) but inner section is `padding: '72px 48px'` with no max-width container (relies on padding only). For the marketing Next.js app, `max-width: 1280px; margin: 0 auto` on `.hero` would be more accurate | **Minor** | Change `.hero max-width` from `1180px` to `1280px`. Keep `margin: 0 auto`. |
| **m-9** | **CTA primary button — active state** | No `:active` transform | `components-buttons.html`: `.btn:active { transform: scale(0.95) }`. 80ms per README | **Minor** | Add `.ctaPrimary:active { transform: scale(0.95); transition-duration: 80ms }`. |
| **m-10** | **Headline `headlineRest` word-break on mobile** | `{C.hero.headlineRest}` = " your kids will love — that teaches." — single string. At 390px this may orphan "teaches." on its own line incorrectly | Capture (390px not shown but inferred): the em-dash is a natural break. Add `word-break: keep-all` to prevent awkward mid-word breaks on narrow | **Minor** | Add `word-break: keep-all` to `.headline`. |

---

## New Components Needed

None. All pieces exist in `web-feature-card.html`, `web-subject-band.html`, `web-cta-banner.html`, `web-footer.html`. The delta is implementation gap — section stubs need to be fleshed out using the existing preview cards as exact templates.

---

## Implementation Handoff — Per Section

### Nav (`.nav`, `.navInner`, `.btnOutline`, `.btnPrimary`)
- **File:** `apps/marketing-site/app/page.module.css`
- **Proving card:** `design-system/preview/web-nav.html`
- **Token refs:** `--lx-bg` (nav bg base), `--lx-border` (0.05 alpha version), `--lx-radius-button` (16px), `--lx-primary`, `--lx-primary-glow`
- Fixes: B-1, B-2, M-1, M-2, M-3, m-1

### Hero glow + pill (`.heroGlow`, `.pill`, `.pillSpark`)
- **File:** `apps/marketing-site/app/page.module.css`
- **Proving card:** `design-system/preview/web-hero-phonemock.html`; `PagesPublic.jsx` lines 39–51
- **Token refs:** `--lx-purple`, `--lx-purple-soft`, `--lx-purple-glow` (not in globals.css — see Design Gaps)
- Fixes: B-4, M-4, M-5

### Hero headline (`.headline`, `.headlineAccent`)
- **File:** `apps/marketing-site/app/page.module.css`
- **Proving card:** `design-system/preview/type-display.html`; `PagesPublic.jsx` line 55
- **Token refs:** `--lx-xp` (#FACC15), `--lx-streak` (#FB923C), `--lx-tracking-tight`
- Fixes: B-3, M-6, m-10

### Hero paragraph + CTA row + trust row
- **File:** `apps/marketing-site/app/page.module.css`
- **Token refs:** `--lx-fg2`, `--lx-fg1`, `--lx-radius-button`, `--lx-shadow-primary-glow`
- Fixes: M-7, M-8, M-9, M-13, M-14, M-15, m-3, m-4, m-6, m-9

### Phone mock (`.phone`, `.screen`, `.progressFill`, `.chip*`, `.star`)
- **File:** `apps/marketing-site/app/_components/PhoneMockup.module.css`
- **Copy file:** `apps/marketing-site/lib/copy.ts` (chips.xp, phone.subjectEnglish)
- **Proving card:** `design-system/preview/web-hero-phonemock.html`; `PagesPublic.jsx` lines 81–124
- **Token refs:** `--lx-purple`, `--lx-primary`, `--lx-card`, `--lx-secondary`, `--lx-xp`, `--lx-streak`, `--lx-xp-glow`
- Fixes: B-5, B-6, B-7, M-9 (chip rotation), M-10, M-11, M-12, m-2

### Features section (new — replaces `#how-it-works` stub)
- **File:** `apps/marketing-site/app/page.tsx` — replace stub section with 3-column feature grid
- **New component file:** `apps/marketing-site/app/_components/FeaturesSection.tsx` + `.module.css`
- **Proving card:** `design-system/preview/web-feature-card.html`; `PagesPublic.jsx` lines 127–141
- **Token refs:** `--lx-card` (bg), `--lx-radius-modal` (24px card radius), `--lx-shadow-soft`, `--lx-border` (0.06 alpha), `--lx-purple`, `--lx-streak`, `--lx-secondary`
- Feature cards — 3 columns at 1024+, 2 at 768, 1 at 390:
  - Card 1: icon 🤖 `rgba(168,85,247,0.15)` purple, "AI tutor that explains"
  - Card 2: icon 🎮 `rgba(251,146,60,0.15)` orange, "Gamified, not gimmicky"
  - Card 3: icon 📊 `rgba(34,197,94,0.15)` green, "Parents stay in the loop"
  - Cards 4–6 from `PagesPublic.jsx` lines 137–139 (Arabic+English, Safe, 5min)
- Section heading: `font-weight:900; font-size:44px` (PagesPublic style); section eyebrow: `color:#A855F7; font-size:12px; font-weight:800; letter-spacing:0.12em; text-transform:uppercase` — "Why Learnexia"

### Subjects band (new — replaces `#subjects` stub)
- **File:** `apps/marketing-site/app/page.tsx`; new `_components/SubjectsBand.tsx`
- **Proving card:** `design-system/preview/web-subject-band.html`; `PagesPublic.jsx` lines 143–172
- 4-column grid: Math (🧮 `#4F46E5`), Science (🧪 `#22C55E`), Arabic (📖 `#FB923C`), English (🇬🇧 `#A855F7`)
- **No Social Studies.** Product override confirmed.
- Card: `background:var(--lx-card); border-radius:var(--lx-radius-card) [20px]; padding:20px; border:1px solid var(--lx-border)`
- Icon wrapper: `width:48px; height:48px; border-radius:14px; background:{color}22` (13% tint)
- Subject name: `font-weight:900; font-size:18px; color:var(--lx-fg1)`
- Topic subtitle: `font-size:12px; color:var(--lx-fg3)`
- "Grade 1–6 →": `color:{subject-color}; font-weight:700; font-size:12px`
- Section title: "Four subjects. One adventure." — `font-weight:900; font-size:36px`

### CTA banner (new — replaces `#for-schools` and `#pricing` stubs)
- **File:** `apps/marketing-site/app/page.tsx`; new `_components/CTABanner.tsx`
- **Proving card:** `design-system/preview/web-cta-banner.html`; `PagesPublic.jsx` lines 175–199
- Container: `background: linear-gradient(135deg, var(--lx-primary) 0%, var(--lx-purple) 100%); border-radius: 28px; padding: 56px 48px`
- Box shadow: `0 24px 60px rgba(99,102,241,0.45), inset 0 1px 0 rgba(255,255,255,0.2)`
- Decorative element: 🌟 at 280px, `opacity:0.15`, absolute `right:40px bottom:-40px`
- Headline: "Ready to start the adventure?" — `font-weight:900; font-size:36px; color:#fff; letter-spacing:-0.02em`
- Sub: "Free for your first child · No credit card required" — `font-size:16px; color:rgba(255,255,255,0.9)`
- CTA button: `height:60px; padding:0 32px; border-radius:var(--lx-radius-button) [16px]; background:#fff; color:var(--lx-primary); font-weight:900; font-size:17px`
- Link: `href={REGISTER_URL}` (from `lib/config`)

### Footer (new — replaces current minimal text footer)
- **File:** `apps/marketing-site/app/page.tsx` (inline) or new `_components/SiteFooter.tsx`
- **Proving card:** `design-system/preview/web-footer.html`; `PagesPublic.jsx` lines 202–219
- Layout: `display:flex; align-items:center; justify-content:space-between; padding:40px 48px`
- Border: `border-top: 1px solid rgba(255,255,255,0.05)` (NOT `var(--lx-border)` = 0.08 — use 0.05)
- Left: `<img src="/assets/logo-mark.svg" width=28 height=28 style="opacity:0.7">` + "© 2026 Learnexia · Made for curious kids"
- Right: links `color:var(--lx-fg3)` — Privacy, Terms, Support, العربية — `font-weight:600; gap:22px`
- Text color: `#64748B` (token `--lx-fg3` is `#94A3B8`; `web-footer.html` uses `#64748B` = slate-500 — this is a **design gap**, see below)
- Copy change: update `C.footer.rights` to `'© 2026 Learnexia · Made for curious kids'` and expose footer links in `copy.ts`

### Responsive behavior
| Breakpoint | Nav | Hero | Features | Subjects | CTA |
|---|---|---|---|---|---|
| 1024px | Full nav visible | `1.1fr 1fr` grid, 1024 constrains columns | 3 cols | 4 cols | Full banner |
| 768px | Hamburger or collapse nav links (current: `display:none` at 900px — tolerable) | `1fr` single col, phone art first (order:-1) | 2 cols | 2 cols | Stack vertically |
| 390px | Logo + "Start free" only | Single col, reduced font-size | 1 col | 2 cols | Stack, button full-width |

Current `@media (max-width:900px)` breakpoint is reasonable (covers 768px). Add a `@media (max-width:600px)` for 390px refinements.

---

## Design Gaps

1. **Phone frame radius (36px / 28px inner):** no `--lx-radius-*` token covers these values. `--lx-radius-modal` = 24px and `--lx-radius-pill` = 9999px are the nearest. Recommend adding `--lx-radius-device: 36px` and `--lx-radius-screen: 28px` to `design-system/colors_and_type.css` (and mirroring in `globals.css`). Until then use raw `36px` / `28px` literals with a comment.

2. **Footer text color `#64748B`:** `web-footer.html` uses `#64748B` (Tailwind slate-500), which is dimmer than `--lx-fg3` = `#94A3B8` (slate-400). No token maps to `#64748B`. Recommend adding `--lx-fg4: #64748B` for footer/caption contexts, or using `--lx-fg3` and accepting the slight lightness difference.

3. **Purple glow variant for marketing hero:** the current `globals.css` has `--lx-primary-glow` (indigo) and `--lx-xp-glow` / `--lx-streak-glow` etc., but no `--lx-purple-glow` token despite `colors_and_type.css` having none either (`--lx-purple-soft` = `rgba(168,85,247,0.18)` but no glow at 0.45). Add `--lx-purple-glow: rgba(168,85,247,0.45)` to `globals.css` (already exists in `colors_and_type.css` via the spec pattern — confirm and replicate).

4. **Chip rotation transforms not in CSS module:** `.chipXp` and `.chipBadge` have no `transform` property. This is a gap in `PhoneMockup.module.css` vs the reference implementation.

5. **`lxpulse` keyframe animation:** defined in `PagesPublic.jsx` as inline style string (`animation: lxpulse 2s ease-in-out infinite`) but the `@keyframes lxpulse` is not included in `globals.css` or any marketing-site CSS file. The star animation will silently fail. Add `@keyframes lxpulse` to `globals.css`.

6. **Feature section and subjects band background:** `PagesPublic.jsx` features section uses `background: '#0B1020'` (a bespoke near-black slightly bluer than `--lx-bg` = `#0F172A`). This value has no token. Either use `--lx-bg` (minor visual difference) or define `--lx-bg-deep: #0B1020` if the differentiation matters.

---

## FUTURE-AR Appendix (do not implement now — English-only per HANDOFF)

Captures compared: `design-system/screenshots/web-ar/01-landing.png` vs `web/01-landing.png`.

Observed deltas for when AR is enabled:

| Element | EN (current target) | AR capture delta |
|---|---|---|
| `dir` attribute | `ltr` on `<html>` | `rtl` |
| Font — nav links | Poppins 600 | Tajawal 600 (per `ar-web-nav.html`) |
| Font — headline | Poppins 800 | Cairo 900 — `'لعبة تعليمية سيحبها أطفالك — تُعلّمهم حقاً.'` |
| Font — body | Poppins 400 | Tajawal 400 |
| Nav layout | Logo left, links center, actions right | Logo right, links center, actions left (RTL flip) |
| "Log in" / "Start free" | EN | `تسجيل الدخول` / `ابدأ مجاناً` — per `ar-web-nav.html` |
| Pill label | "✨ POWERED BY AI" | "✨ مدعوم بالذكاء الاصطناعي" |
| CTA primary | "Create parent account →" | "أنشئ حساب ولي الأمر ←" (arrow flipped) |
| CTA secondary | "Watch demo (2 min)" | "شاهد العرض (دقيقتان)" |
| Trust row | "4.9 in App Store" etc. | "٤٫٩ في متجر التطبيقات" etc. (Eastern-Arabic numerals inline) |
| Phone mock position | Right column | Left column (RTL) |
| Chip XP | Right of phone | Left of phone (positions flip with `inset-inline-*` — already uses logical properties in current CSS, which is correct) |
| Chip Badge | Left of phone | Right of phone |
| Footer links order | Privacy · Terms · Support · العربية | Reversed in RTL: العربية · Support · Terms · Privacy (or use flex-direction: row-reverse) |
| Numbers (XP, ratings) | Latin `4.9` | Latin kept in `dir="ltr"` wrapper per SKILL.md rule: keep Latin for technical strings |
| Feature/CTA copy | EN | Translated — `ar-web-features.html` + `ar-web-cta.html` have exact AR copy |

Implementation when AR is scoped in: add `[dir="rtl"]` overrides to the CSS modules, swap font via the `--lx-font-display` / `--lx-font-body` CSS variable already wired in `globals.css`, and update `copy.ts` to export a parallel `LANDING_COPY_AR` object. The logical-property usage (`inset-inline-start`, `inset-inline-end`) already in `PhoneMockup.module.css` is correct and will auto-flip.

---

Design spec ready for frontend.
