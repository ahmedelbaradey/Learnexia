# Learnexia Design System

> 🎮 **Learnexia** — a Gamified AI Educational Platform for children.
> The product should feel like an **Educational Game World**, not an LMS, dashboard, or generic AI chatbot.

This design system was built from a written specification (Arabic + English) provided by the team. **No codebase or Figma file was attached**, so every visual decision is derived from the spec, gamified-learning conventions (Duolingo, Khan Academy Kids, Prodigy), and the explicit token list below.

---

## Sources

- Written design brief (pasted into the chat) describing Figma file structure, color palette, type scale, spacing, radii, components, screens, gamification mechanics, AI flows, parent dashboard, motion, and UX rules.
- No GitHub repo, Figma URL, or production codebase provided.

> **If you have a real codebase or Figma file**, attach it via the Import menu and ask for a re-pass — the system can then be tightened against the source of truth instead of the spec.

---

## At a glance

| | |
|---|---|
| **Audience** | Children (roughly 6–14), with a parallel parent surface |
| **Platforms** | Mobile-first (390×844) student app + web Parent Dashboard |
| **Languages** | English + Arabic (RTL); fonts cover both |
| **Theme** | Dark by default (deep navy canvas, bright candy accents) |
| **Mood** | Playful, rewarding, energetic, friendly — *not* corporate |

---

## Index — what's in this folder

```
README.md                  ← you are here
SKILL.md                   ← machine-readable skill descriptor for Claude/Agents
colors_and_type.css        ← all CSS variables (tokens + semantic styles)
assets/
  logo.svg                 ← Learnexia wordmark + spark glyph
  logo-mark.svg            ← standalone glyph
  mascot-owl.svg           ← placeholder mascot (REPLACE with real illustration)
  patterns/                ← soft repeating SVG textures for backgrounds
fonts/
  Poppins/                 ← Latin display + UI
  Cairo/                   ← Arabic display
  Tajawal/                 ← Arabic UI
preview/                   ← cards rendered into the Design System tab
ui_kits/
  student-mobile/          ← iOS-frame click-thru: Home, Lesson, Quiz, Reward
  parent-dashboard/        ← web-frame Parent surface
```

There are no sample slides — none were supplied.

---

## Content fundamentals

### Voice & tone
Learnexia speaks **to a kid, like a friendly older sibling cheering them on**. The voice is warm, energetic, and short. It is *never* lecturing, never corporate, and never dryly informational.

- **Person:** Second-person ("you", "your streak", "let's go!"). The AI tutor uses first-person ("I'll show you", "let me explain").
- **Tense:** Present and imperative — *"Tap to start", "Keep going!", "You earned 50 XP"*.
- **Length:** Microcopy. One idea per line. Buttons are 1–3 words. Headlines are ≤6 words.
- **Casing:** Title Case for buttons and headings (`Start Lesson`, `Daily Mission`). Sentence case for body and explanations.
- **Punctuation:** Exclamation marks are allowed *sparingly* — reserved for genuine wins (`Level Up!`, `Perfect Streak!`). Never on neutral UI labels.
- **Numbers:** Always rendered, never spelled out — `+10 XP`, `3-day streak`, `Level 7`. The number is the reward; let it shine.
- **Arabic:** Fully RTL. Direct, encouraging, child-friendly Modern Standard Arabic. Avoid heavy classical phrasing.

### Examples

| ✅ Good | ❌ Avoid |
|---|---|
| `Nice work! +10 XP` | `You have successfully completed this learning activity.` |
| `Keep your streak alive 🔥` | `It is recommended to maintain daily engagement.` |
| `Hmm, not quite — try again` | `Incorrect answer. Please review and resubmit.` |
| `Pick the bigger number` | `Identify the numerical value of greater magnitude` |
| `Math · Numbers · Lesson 3` | `Mathematics Module 1.3: Numerical Foundations` |

### Emoji
Used **deliberately, not decoratively**. A small fixed set acts as semantic icons inside the brand:

- 🔥 streak / fire
- ❤️ life / heart
- ⭐ XP / star
- 🏆 trophy / league win
- 💎 gem / premium currency
- 🎯 mission / goal

Never sprinkle emoji into body copy for flavor. They are functional badges.

### Vibe
> Imagine the AI is a friendly cartoon owl coaching you through a Saturday-morning game show. That's the energy.

---

## Visual foundations

### Color
A **dark, saturated** palette — the canvas is deep navy (`#0F172A`), letting candy-bright accents pop like neon arcade signage.

**Five named primaries** (per spec):
- **Primary** indigo `#4F46E5` — main actions, CTAs, focus
- **Secondary** green `#22C55E` — success states
- **Accent** amber `#F59E0B` — rewards & highlights
- **Danger** red `#EF4444` — errors, heart loss
- **Purple** `#A855F7` — badges & achievements

**Three named gradients:**
- **XP gradient** — `#22C55E → #4F46E5` (green → indigo). Used on XP bars and mission progress.
- **Reward gradient** — `#F59E0B → #EF4444` (orange → red). Used on reward heroes.
- **Level-Up gradient** — `#A855F7 → #6366F1` (purple → indigo). Used on level-up moments and hero CTAs.

**Surfaces** — bg `#0F172A` (slate-900), card `#1E293B` (slate-800), soft card `#334155` (slate-700), light surface `#F8FAFC` (for parent print/light surfaces). Cards step *lighter*, not darker — opposite of most enterprise dark themes.

**Text** — high contrast white `#F8FAFC` for headings, `#CBD5E1` for body, `#94A3B8` for muted.

**Gamification accent palette** (added on top of the spec's 5 named primaries, since the spec referenced them by emoji only):
- XP / star: `#FACC15` (warm yellow)
- Streak / fire: `#FB923C` (orange)
- Hearts: `#FB7185` (rose)
- League gold: `#FBBF24`
- Gem: `#38BDF8`
- **Helper Energy: `#2DD4BF` (teal)** + deep `#14B8A6` — the AI-help meter. Deliberately a different hue from hearts (rose) and gem (sky) so the two depleting meters can never be confused. See _Helper Energy_ below.

Imagery and illustrations should read **warm + saturated** — never washed-out, never grayscale.

### Typography
- **Display + UI (Latin):** Poppins (400/500/600/700/800). Friendly, rounded, geometric — reads as fun and modern.
- **Display + UI (Arabic):** Cairo for headings, Tajawal for body. Both pair well metrically with Poppins.
- **Scale:** H1 32 / H2 24 / H3 18 / Body 14–16 / Small 12. Headings use weight 700–800; body 400–500; numbers/XP counters use 800 to feel **chunky and rewarding**.
- Line-height generous — 1.4–1.6 for body — so kids can read easily.

### Spacing
4 / 8 / 16 / 24 / 32 / 48. Cards breathe — never tight. Touch targets ≥ 48px (kid-finger friendly).

### Radii
Spec-defined: **8px** small elements (chips, inputs) · **16px** buttons · **20px** cards · **24px** modals/popups · pill (`9999px`) for HUD status pills and badges. **No sharp 90° corners** anywhere in the UI.

### Backgrounds
- The main canvas is solid deep navy, occasionally tinted with a **soft radial glow** (10–15% indigo or violet) behind hero areas.
- Reward screens use **bright full-bleed gradients** (indigo → violet → pink) — but ONLY on reward / level-up moments, never on standard UI.
- Subtle dotted/grid patterns may appear on the skill-tree screen.
- Never use stock photography. Imagery is **flat illustration only** (mascot, badge graphics, lesson visuals).

### Shadows & elevation
The dark theme means we layer with **light, not shadow**. Spec defines two:

- **Soft shadow** (default cards): `0 4px 12px rgba(0,0,0,0.15)`
- **Floating shadow** (hover, popups): `0 8px 24px rgba(0,0,0,0.25)`
- **Glow effects** are reserved for XP gain, rewards, badges, level-up animations — used like neon (yellow XP glow, indigo CTA glow, purple level-up glow).
- **Focus rings**: 2px indigo + outer 4px indigo-glow at 30% opacity.

### Borders & transparency
- 1px borders at low alpha (`rgba(255,255,255,0.06–0.10)`) define cards.
- Glass / blur is used **only** for floating overlays (reward popups, AI tutor bubble) — `backdrop-filter: blur(20px)` on a `rgba(15,23,42,0.7)` surface.
- Frosted/glass should not be used on regular cards — it makes everything feel uncertain.

### Animation
Animations are **CRITICAL** for kids and should feel snappy and rewarding.

- **Easing:** Spring-like overshoot (`cubic-bezier(0.34, 1.56, 0.64, 1)`) for pops. Standard ease-out for transitions.
- **XP bar fill:** 600–800ms ease-out with a brief glow flash at the end.
- **Badge earned:** Scale 0 → 1.15 → 1 (pop-in), then a slow rotating shimmer.
- **Correct answer:** Green pulse + confetti burst (4–8 small particles).
- **Wrong answer:** 60ms horizontal shake (±6px), red border flash, soft haptic tone (not loud).
- **Streak flame:** Continuous gentle scale 1 → 1.04 → 1 (1.5s loop) + hue flicker.
- **Page transitions:** Slide + fade, 250–300ms.
- Never use long (>800ms) blocking animations — kids lose attention fast.

### Interaction states
- **Hover (web/parent dash):** Brighten by ~8% (lighten the surface, don't darken), slight scale 1.02.
- **Press (mobile primary):** Scale **0.95**, 80ms. Never flash a darker color — feels broken on dark UI. Instead, **brighten** or compress.
- **Disabled:** Reduce opacity to 0.4, remove glow, do not change layout.
- **Focus:** 2px indigo outline + outer glow. Visible on keyboard nav for accessibility.

### Layout rules
- One **primary action per screen**, always.
- Bottom tab bar is fixed on mobile (Home / Skills / Missions / League / Profile).
- Top bar shows persistent gamification HUD: streak, hearts, XP.
- Generous bottom padding to clear the home indicator and thumb zone.
- Avoid dense screens — if there's too much, split into two.

---

## Iconography

- **Primary set:** [Lucide](https://lucide.dev) icons — clean line style, 2px stroke, rounded caps. Loaded via CDN (`lucide.min.js`) since no in-house icon set was provided. **This is a substitution** — flag to the team if they have a bespoke set.
- **Decorative game icons:** custom flat-illustrated badges, mascot, trophies, hearts — these live in `assets/` as SVG. The mascot owl included is a **placeholder** that should be replaced with a properly illustrated character.
- **Emoji:** used semantically only (see Content Fundamentals). Apple's emoji set is fine on iOS; on Android/web use [Twemoji](https://twemoji.maxcdn.com) for visual consistency.
- **Unicode chars:** not used as icons — always prefer a Lucide icon or an SVG asset.

### Substitutions flagged
1. **Lucide** in place of an in-house icon set.
2. **Twemoji** for cross-platform emoji rendering.
3. **Placeholder mascot SVG** — looks like a stylised owl, but is generic. Needs the real Learnexia character.

---

## UI Kits

| Product | Path | What it covers |
|---|---|---|
| **Student Mobile** | `ui_kits/student-mobile/` | iPhone 402×874. 18-screen click-thru: Splash · Login · Register · Role/Grade/Subject onboarding · My Children · Home · Skill Tree · Lesson · Quiz · Reward · Mission Completed · Daily Mission · League · Badges · Hearts · Profile. Working **Add Child** bottom sheet (photo upload, grade tiles, language flags). Arabic RTL twin at `index-ar.html`. |
| **Parent Web** | `ui_kits/parent-dashboard/` | Web 1280px, 7 pages: Landing · Login · Register · My Children · Dashboard · Reports · Settings. Working **Add Child** modal with photo upload. Arabic RTL twin at `index-ar.html`. |
| **Helper Energy** | `ui_kits/helper-energy/` | The ⚡ AI-help credit system. One showcase (`index.html`) with an EN/AR × mobile/web toggle: an interactive lesson demo where spending a helper drains energy live, plus indicator states, cost-confirm, out-of-energy, nudges, and the parent top-up + plan compare. See _Helper Energy_ below. |

Each kit has its own `README.md` and exposes small reusable JSX components
(`MobileComponents.jsx` / `DashboardComponents.jsx`, `Screens*.jsx`, `Pages*.jsx`, `AddChildModal.jsx`).

### Bilingual parity
Every screen exists in **English (LTR)** and **Arabic (RTL)** with identical layout + content — Cairo headings, Tajawal body, Eastern-Arabic numerals in prose, Latin for technical strings (`820 / 1000 XP`, emails, brand name). Screenshots for both in `screenshots/{mobile,web,mobile-ar,web-ar}/`.

### Add Child form — conventions
- **Photo upload** with live circular-avatar preview; falls back to a colored initial.
- **Grade** picker = 6 plant-emoji **tiles** (🌱→🌴), not a dropdown.
- **Language** = two **flag tiles**: 🇪🇬 **AR** and 🇺🇸 **EN**.
- **Centered modal** on web, **bottom sheet** on mobile.

### Dashboard layout
"Areas to focus on" + "Recommendations from Lexi" sit **side by side** in a 2-column row (EN + AR); recommendation cards stack vertically within their column.

### Scrollbars
Both web kits ship a **brand-styled scrollbar** (indigo gradient thumb, pill shape, subtle track, lighter hover) for both axes. Reuse this in any new web page rather than the default OS scrollbar.

Each kit has its own `README.md` and exposes small reusable JSX components.

---

## Helper Energy (⚡ طاقة المساعد)

The credit system that meters **AI-helper usage only** — a resource entirely separate from **hearts** (lives/mistakes in Practice Mode). Children see and spend energy but never see prices or buy; parents purchase top-up packs.

**Economy (numbers the UI surfaces):**
- **Free** — 300 credits/month, 20/day cap.
- **Premium** — 3000 credits/month, 150/day soft cap.
- **Top-up pack** — 500 credits.
- **Per-action cost:** Hint = ⚡1 · Explain Mistake = ⚡3 · Deep Explanation = ⚡5 · Practice Generation = ⚡5.

**Surfaces designed (each with all states):**
1. Persistent indicator — full / low / **daily-cap reached** (balance fine, resets at midnight) vs **monthly-empty** (ask a parent).
2. Per-action cost preview + confirm, showing the cost and the **remaining balance after**.
3. Out-of-energy — kid-friendly, non-punitive, with a clear path (wait for daily reset vs. ask a parent to top up).
4. Parent top-up flow (500-credit pack) + Free-vs-Premium comparison.
5. Low/empty nudges and daily/monthly-reset messaging.

**Hearts vs Energy — the hard distinction.** An 8-year-old must never confuse the two depleting meters. They differ on **every** channel:

| Channel | ❤️ Hearts | ⚡ Energy |
|---|---|---|
| Means | Lives / mistakes | AI-helper fuel |
| Color | Rose `#FB7185` | Teal `#2DD4BF` |
| Icon / shape | Heart glyphs | Battery + lightning bolt |
| Motion | Shatter + shake on loss | Smooth drain + bolt pulse |
| Position | Top-**left** of HUD | Top-**right** of HUD |
| Refills | Slowly over time (1 / 30 min) | Daily reset / parent top-up |

**Tokens:** `--lx-energy` `#2DD4BF`, `--lx-energy-deep` `#14B8A6`, `--lx-energy-soft`, `--lx-energy-glow` (in `colors_and_type.css`).

**Where it lives:**
- Interactive showcase: `ui_kits/helper-energy/index.html` (toggle EN/AR × mobile/web).
- Design System tab cards: `preview/colors-energy.html`, `components-energy-vs-hearts.html`, `components-energy-indicator.html`, `components-energy-cost.html`, `components-energy-empty.html`, `web-energy-topup.html`, plus Arabic `ar-energy-vs-hearts.html` and `ar-energy-indicator.html`.

---

## Substitutions flagged for the team

These are choices made because the spec didn't pin them down. Confirm or override:

1. **Cairo** is still loaded from Google Fonts — no brand font files were supplied for that family. (Poppins + Tajawal are self-hosted from `/fonts/` ✅)
2. **Icon set** — system emoji used throughout (🔥 ❤️ ⭐ 🏆 💎 🎯 🥉🥈🥇👑). The spec calls for emoji as emotional reinforcement, so this matches; flag if you have a custom icon set.
3. **Mascot owl** in `assets/mascot-owl.svg` is a hand-drawn placeholder. Replace with the real Learnexia character art.
4. **Gamification accent palette** (XP yellow `#FACC15`, streak orange `#FB923C`, hearts rose `#FB7185`, league gold `#FBBF24`, gem cyan `#38BDF8`) were added on top of the spec's 5 named primaries since the spec referenced these by emoji only.
5. **Light theme** — spec lists a `#F8FAFC` light surface token but no full light-theme system. Parent Dashboard could benefit from a full light theme — flag if you want one designed.

## Next steps for the team

1. Share the **real mascot character** and any other custom illustrations.
2. Share a **Figma file or codebase** so this system can be aligned with shipped UI.
3. Confirm the gamification accent palette above.
4. Decide whether a **light theme** is needed for parent surfaces (the dashboard might benefit from one).

