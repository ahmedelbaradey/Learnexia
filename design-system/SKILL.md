---
name: learnexia-design
description: Use this skill to generate well-branded interfaces and assets for Learnexia, a gamified AI educational platform for children (ages 6–14). Covers production code, prototypes, marketing assets, slides, and throwaway mocks — in English (LTR) or Arabic (RTL). Contains the full token system, fonts, brand assets, UI kits, and 80+ atomic preview components ready to lift wholesale.
user-invocable: true
---

# Learnexia Design Skill

> **Learnexia** should always feel like an **Educational Game World**, not an LMS / dashboard / generic AI chatbot.

When invoked, read `README.md` first for the full design philosophy, then drill into the file(s) matching what you need to build. Below are common task playbooks.

---

## Quick reference — what's on disk

| Path | Use it for |
|---|---|
| `README.md` | Full brand voice, visual foundations, iconography rules |
| `colors_and_type.css` | Drop-in CSS variables for every token (colors, type, spacing, radii, shadows, motion). Self-hosts Poppins + Tajawal; loads Cairo from Google Fonts. |
| `fonts/` | Poppins (full family) + Tajawal weights |
| `assets/` | Logo, mascot owl placeholder, icon SVGs, background patterns |
| `preview/` | 80+ self-contained atomic component cards. Each is one HTML file you can clone into a new project (it only needs `colors_and_type.css`). English (unprefixed), Arabic (`ar-*`), mobile (`mobile-*`), web (`web-*`). |
| `ui_kits/student-mobile/index.html` | Full English mobile click-through (18 screens) |
| `ui_kits/student-mobile/index-ar.html` | Full Arabic RTL mobile click-through (18 screens) |
| `ui_kits/parent-dashboard/index.html` | Full English web app (7 pages) |
| `ui_kits/parent-dashboard/index-ar.html` | Full Arabic RTL web app (7 pages) |
| `screenshots/` | PNG captures of every screen and page, en + ar |

---

## Core design rules — never break these

1. **Dark canvas by default.** App bg `#0F172A`. Cards step *lighter*, not darker (`#1E293B` → `#334155`).
2. **Five named primaries:** Primary `#4F46E5` · Secondary `#22C55E` · Accent `#F59E0B` · Danger `#EF4444` · Purple `#A855F7`.
3. **Three named gradients:** XP `#22C55E → #4F46E5` · Reward `#F59E0B → #EF4444` · Level-up `#A855F7 → #6366F1`.
4. **Radii are bucketed:** 8 (chips/inputs), **16 (all buttons)**, 20 (cards), 24 (modals/popups), pill (HUD chips, badges). Never use raw 90° corners.
5. **Shadows are soft:** `0 4px 12px rgba(0,0,0,0.15)` resting, `0 8px 24px rgba(0,0,0,0.25)` floating. Reserve glow (yellow, indigo, purple) for reward/CTA states only.
6. **Type:** Poppins (Latin) + Cairo (Arabic display) + Tajawal (Arabic body). Numbers always weight 800 with `font-variant-numeric: tabular-nums`.
7. **Voice:** Short, second-person, encouraging. *"Nice work! +50 XP"* — not *"You have successfully completed this learning activity."*
8. **Emoji** are semantic only: 🔥 streak · ❤️ life · ⭐ XP · 🏆 trophy · 💎 gem · 🎯 mission. Never decorative.
9. **Press feedback:** Scale `0.95` for 80ms. Brighten on hover, never darken (the bg is already dark).
10. **One primary action per screen.** Kids' attention is finite.

---

## Skill 1 — Build a new screen for the Learnexia mobile app

1. Open `ui_kits/student-mobile/index.html` and find the screen closest to what you're building. Copy its structure as a starting point.
2. Bring in reusable JSX from `Components.jsx` (`HudBar`, `XPBar`, `PrimaryButton`, `LessonCard`, `MissionRow`, `AnswerButton`, `TabBar`, `MascotAvatar`, `TutorBubble`). Don't reinvent.
3. Wrap content in `<ScreenShell>` (handles padding for status bar + tab bar). Add `padTop={56}` or `{70}` depending on whether you keep the HUD.
4. Buttons should always be 16px radius and use `<PrimaryButton variant="primary|success|danger|secondary|purple|ghost">`.
5. If the screen sits in the bottom-tab flow, include `<TabBar>` and update the `showsTabBar` allowlist in `index.html`.
6. Add the screen to the crumb `groups` config in `index.html` so it's navigable.
7. For Arabic, also add the equivalent to `index-ar.html` — `dir="rtl"`, swap fonts to Cairo/Tajawal, translate copy, flip arrows (`→` becomes `←`), and use Eastern-Arabic numerals (٠١٢٣٤٥٦٧٨٩) for natural reading.

## Skill 2 — Build a new page for the parent web dashboard

1. Open `ui_kits/parent-dashboard/index.html`. Inline app pages live there; landing/login/register are in `PagesPublic.jsx`; in-app pages in `PagesApp.jsx`.
2. App pages must use `<AppShell active="..." onNav={...}>` so the sidebar + browser chrome stay consistent.
3. Top of every app page: `<PDHeader title="..." sub="..." />`.
4. Use `<PDPanel title sub action>` for any card. KPI tiles use `<PDStatCard>`. Charts use `<PDActivityChart>` / `<PDWeakAreas>`.
5. Page width is fixed at 1280px; the frame wraps in a browser chrome with a fake URL — update the `urlFor()` map.
6. Add the page to the `pages` map + crumb `groups` so it's reachable.

## Skill 3 — Build a marketing or external-facing page

1. Use the landing page in `PagesPublic.jsx` as the reference for tone and layout: sticky nav → hero with phone mock + floating reward chips → 3-column feature grid → subjects band → gradient CTA banner → footer.
2. Marketing copy is bolder and more declarative than in-app copy, but still short. Headlines ≤ 8 words.
3. Hero gradient is always the Level-Up gradient (`#A855F7 → #6366F1`) or radial purple glow.
4. Floating chips around the phone mock should always show real rewards (`+50 XP ⭐`, `🏆 New badge!`) — never lorem.

## Skill 4 — Make any existing screen Arabic / RTL

1. Set `dir="rtl"` on the wrapper.
2. Swap font: headings → `'Cairo'`, body → `'Tajawal'`.
3. Translate copy (see existing Arabic kits for vocabulary — *درس مكتمل*, *مهمة اليوم*, *النقاط*, *سلسلة*, *الشارات*).
4. Replace arrows: `→` becomes `←` (still pointing the "next" direction in RTL).
5. Use Eastern-Arabic numerals for in-line text: *المستوى ١٢ · ٧ أيام*. **Exception:** keep Latin numerals (and `dir="ltr"`) for technical strings like `820 / 1000 XP`, email addresses, brand name `Learnexia`, and currency.
6. Bar charts and progress bars stay LTR (wrap in `direction: ltr`) — progress reads left-to-right universally.
7. Avatar gradients and icon shapes stay the same — don't mirror them.

## Skill 5 — Pick the right atomic component

For any small piece you need, **first** look in `preview/`. Naming:

- Tokens (no prefix): `colors-*`, `type-*`, `radii.html`, `elevation.html`, `gradients.html`, `spacing-scale.html`, `borders-focus.html`
- Brand (no prefix): `logo.html`, `logo-mark.html`, `mascot.html`
- Shared components (no prefix): `components-buttons.html`, `components-hud.html`, `components-xp-bar.html`, `components-badges.html`, `components-hearts-streak.html`, `components-lesson-card.html`, `components-quiz.html`, `components-tutor.html`, `components-missions.html`, `components-reward.html`, `components-input.html`, `components-skill-node.html`
- Mobile-specific: `mobile-*` (29 files — auth, onboarding, home, gamification, profile, hearts, badges)
- Web-specific: `web-*` (25 files — nav, hero, sidebar, KPIs, charts, settings, recommendations)
- Arabic equivalents: `ar-*` (27 files covering the most-used atoms in RTL)

Lift the HTML directly. Each file imports `_base.css` (or `_base-ar.css`) which imports `colors_and_type.css` — to use in a new project, copy the markup and include `colors_and_type.css`.

## Skill 6 — Generate slides / decks / marketing assets

- Brand colors and gradients are already defined — use them via CSS variables (`var(--lx-primary)`, `var(--lx-grad-levelup)`).
- For slide backgrounds, use the deep navy `#0F172A` or the radial purple glow seen in the splash screen.
- Logo files: `assets/logo.svg` (wordmark + mark) or `assets/logo-mark.svg` (just the spark glyph).
- Mascot is a placeholder — flag this in deliverables.
- Numbers and stats should pop: weight 800, tabular nums, glow shadow if it's a reward number.

## Skill 7 — Add a new tweak / feature flag

If you're building a prototype the user wants to experiment with, use the standard `tweaks_panel.jsx` starter (see project-level patterns). Common Learnexia tweaks: button radius (16 vs pill), gradient palette swaps, dark/light surface, RTL toggle.

---

## Common copywriting cheat sheet

| Context | English | Arabic |
|---|---|---|
| Greeting | "Welcome back" | "أهلاً بعودتك" |
| Continue lesson | "Continue learning →" | "واصل التعلم ←" |
| Start mission | "Start Mission" | "ابدأ المهمة" |
| Correct | "Nice work! +50 XP" | "أحسنت! +٥٠ نقطة" |
| Wrong | "Hmm, not quite — try again" | "ليس بالضبط — حاول مرة أخرى" |
| Streak | "🔥 7-day streak" | "🔥 سلسلة ٧ أيام" |
| Level up | "Level Up!" | "ارتقيت في المستوى!" |
| Daily mission | "Today's Mission" | "مهمة اليوم" |
| XP earned | "+50 XP" | "+٥٠ نقطة" |
| Locked | "Locked" | "مقفل" |
| New badge | "New Badge Unlocked!" | "شارة جديدة!" |

---

## Known caveats (flag these in any deliverable)

1. **Mascot owl** (`assets/mascot-owl.svg`) is a placeholder — needs real Learnexia character art.
2. **Cairo font** loads from Google Fonts CDN; not self-hosted yet (Poppins + Tajawal are local).
3. **Icons** are emoji-based throughout. If a custom icon set exists, drop SVGs into `assets/icons/` and replace.
4. **Light theme** is not implemented — only the `--lx-bg-light: #F8FAFC` token exists.

---

## When the user invokes this skill cold

Ask:
1. **What surface?** Mobile app (student) / Web app (parent) / Marketing / Slides / Other
2. **Which screen or component?** (Or "design from scratch")
3. **English, Arabic, or both?**
4. **High-fidelity recreation or new exploration?**

Then act as an expert designer who outputs HTML artifacts or production code, using the rules above as the floor — not the ceiling.
