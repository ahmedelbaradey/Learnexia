# Design Spec — Batch A · Auth flow redesign (Splash → Role Select → Login)

**Source of truth:** `design-system/ui_kits/parent-mobile/PMScreens.jsx` (native) and
`design-system/ui_kits/parent-dashboard/PagesPublic.jsx` (web); `design-system/design_handoff_parent_app_and_auth/colors_and_type.css` (tokens); `design-system/design_handoff_parent_app_and_auth/README.md` (Update 2).
All CSS `--lx-*` names below refer to that CSS file; all Tamagui `$name` tokens refer to `packages/design-system/src/tokens/`.

---

## 0. Locked lead decisions encoded here

| Decision | Spec |
|---|---|
| `role` transport | expo-router route param: `router.push({pathname:'/(auth)/login', params:{role}})` / `useLocalSearchParams` |
| No-role default | `parent` — matches today's `useState(LOGIN_PERSONAS.Parent)` and the kit's `role='parent'` default |
| `RoleCard` + `RoleBadge` home | `apps/student-app/app/(auth)/_components/` for Batch A; promote to `packages/ui` when Batch B/C reuse (flag Q4 open) |
| `signingInAs` i18n shape | Two literal keys — `auth.login.signingInAsParent` / `auth.login.signingInAsStudent` — to sidestep interpolated-noun gender issues in Arabic RTL |
| PersonaToggle retirement | Delete `PersonaToggle.tsx` + `persona`/`setPersona` state from `LoginForm.tsx`; retire orphaned i18n keys `auth.login.personaParent` / `auth.login.personaStudent` / `auth.login.personaToggleLabel` in the same PR |
| RTL rule | Native `dir`/`writingDirection` + natural source order; `flexDirection:'row-reverse'` BANNED; LTR islands only for email fields and brand wordmark |

---

## 1. Screens in scope

| Screen | Route | Native JSX ref | Web JSX ref |
|---|---|---|---|
| Splash | `app/index.tsx` | `PMSplashScreen` | `SplashWebPage` |
| Role Select | `(auth)/role-select` | `PMRoleSelectScreen` | `RoleSelectWebPage` |
| Login (restyled) | `(auth)/login` | `PMLoginScreen` | `LoginWebPage` |

Register is out of scope for Batch A (no visual change).

---

## 2. Screen 1 — Splash (`app/index.tsx`)

### 2.1 Status — carry-over, no visual restyle

The current `app/index.tsx` already matches `PMSplashScreen` / `SplashWebPage` in the kit. **No visual change is needed.** The only functional change in this batch is in `useAuthRoute.ts`: the signed-out redirect target changes from `/(auth)/login` to `/(auth)/role-select`. The splash screen itself is untouched.

**Delta to call out for frontend:**

| Element | Current | Target | Fix |
|---|---|---|---|
| `useAuthRoute.ts` signed-out redirect | `/(auth)/login` | `/(auth)/role-select` | Change string in the guard effect |
| Splash subtitle copy (EN) | `common.splash.subtitle` — "AI Learning Adventure Begins" | Keep as-is | No change |
| Splash subtitle copy (AR) | `common.splash.subtitle` AR twin | Keep as-is | No change |

### 2.2 Reference values for completeness

| Token | Value | Source |
|---|---|---|
| Background | `radial-gradient(circle at 50% 45%, #4F3FB0 0%, #3B2C8F 40%, #241B6A 100%)` | `PMSplashScreen` L14 |
| Brand mark emoji | 🌟 at `fontSize: 88` (native) / 104 (web) | `PMSplashScreen` L21 / `SplashWebPage` L832 |
| Star glow | `drop-shadow(0 0 20px rgba(250,204,21,0.6))` = `--lx-xp-glow` at 60% | kit L21 |
| Star halo | `radial-gradient(circle, rgba(250,204,21,0.35) 0%, rgba(168,85,247,0) 65%)` | kit L20 |
| Star container size | 132px circle (native) / 160px (web) | kit |
| Wordmark "Learnexia" | `fontSize: 36` (native) / 48 (web), `fontWeight: 900`, `color: #F8FAFC` (`--lx-fg1`), `letterSpacing: -0.02em` (`--lx-tracking-tight`), **always `dir="ltr"`** | kit L24 / L835 |
| Subtitle native (EN) | "Parent Companion", `fontSize: 14`, `color: rgba(255,255,255,0.7)` (`--lx-fg2Alpha`), `fontWeight: 500` | kit L25 |
| Subtitle native (AR) | "رفيق ولي الأمر" | `index-ar.html` L56 |
| Loading bar | `width: 220px` (native) / 260px (web), `height: 6px`, `borderRadius: 9999` (`$pill`), bg `rgba(0,0,0,0.35)`, fill `linear-gradient(90deg,#C4B5FD,#818CF8)` at 55% width | kit L32-33 |
| Loading text | "Loading… ⚡" / "جارٍ التحميل… ⚡", `fontSize: 15`, `color: rgba(255,255,255,0.7)` | kit L35 / AR kit L60 |
| Dot loader | 3 dots: purple `#A855F7`, indigo `#6366F1`, ghost `rgba(255,255,255,0.3)`, each 8px circle | kit L30 |
| Bottom eyebrow | "POWERED BY AI", `fontSize: 11`, `fontWeight: 700`, `color: rgba(255,255,255,0.45)`, `letterSpacing: 0.18em`, `textTransform: uppercase` | kit L38 |
| Bottom tagline | "✦ Family Learning ✦", `fontSize: 13`, `color: rgba(255,255,255,0.65)`, accents in `#A78BFA` | kit L39 |

---

## 3. Screen 2 — Role Select (`(auth)/role-select.tsx`) — NEW

### 3.1 Layout

**Native (single-column, full-screen):**
- Background: `#0F172A` (`$bg`, `--lx-bg`)
- Scroll container with `paddingTop: 70` (safe-area aware), `padding: 0 16 24`
- No tab bar (`tabBar={false}`)
- Outer flex column, `gap: 24` (`$6`, `--lx-space-6`)

**Web (centered column over dark canvas):**
- Background: `#0F172A` (`$bg`), full-width, min-height screen
- Purple radial top glow: `position: absolute; top: -160; left: 50%; transform: translateX(-50%); width: 620px; height: 420px; borderRadius: 50%; background: radial-gradient(circle, rgba(168,85,247,0.28) 0%, rgba(168,85,247,0) 65%); pointerEvents: none` (NOT glass/blur — it is a background decal)
- Centered column: `width: 460px`, `padding: 24px`, `gap: 26px`
- `display: flex; alignItems: center; justifyContent: center`

**Touch targets:** every interactive `RoleCard` button is at minimum 56px tall (inner content) + 18px padding top/bottom = total 92px (native) / 108px (web) — far exceeds the 44px minimum.

### 3.2 Component composition

```
RoleSelectScreen
  └── header block (🎮 emoji + H title + subtitle)
  └── role cards column (gap 10 native / 12 web)
       ├── RoleCard (id='parent', emoji='👨‍👩‍👦', ...)
       └── RoleCard (id='student', emoji='🎓', ...)
  └── footnote Text
```

### 3.3 Header block — exact values

| Element | EN copy | AR copy | Token |
|---|---|---|---|
| Header emoji | 🎮, `fontSize: 40`, `marginBottom: 4` | 🎮 (same) | — |
| Title | "Welcome to Learnexia" (native `fontSize: 26`, web `fontSize: 30`) `fontWeight: 900`, `color: #F8FAFC` | "مرحباً بك في Learnexia" (note: "Learnexia" stays Latin/LTR in the middle of the Arabic string) | `$fg1` / `--lx-fg1` |
| Title font | Poppins (EN) / Cairo (AR display) | — | `$heading` |
| Title letter-spacing | `-0.02em` (`--lx-tracking-tight`) | — | — |
| Subtitle | "Who's signing in?" native `fontSize: 14`, web 15, `color: #94A3B8`, `marginTop: 6` | "من الذي يسجّل الدخول؟" | `$fg3` / `--lx-fg3` |
| Subtitle font | Poppins (EN) / Tajawal (AR body) | — | `$body` |
| `textAlign` | center | center | — |

### 3.4 RoleCard — reusable component spec

**File:** `apps/student-app/app/(auth)/_components/RoleCard.tsx`

**Props:**
```typescript
interface RoleCardProps {
  id: 'parent' | 'student';
  emoji: string;           // '👨‍👩‍👦' | '🎓'
  label: string;           // localized
  sub: string;             // localized subtitle
  onPress: () => void;
  direction?: 'ltr' | 'rtl';
  testID?: string;
}
```

**Visual anatomy (LTR):**
```
[ icon tile (56×56) ][ flex-1 text block (label / sub) ][ chevron › ]
```

**RTL (AR):** reverse the source order to `[chevron ‹][text block][icon tile]` using natural `dir="rtl"` — the chevron becomes `‹` (U+2039) flipped. Do NOT use `flexDirection:'row-reverse'`.

**Exact values:**

| Property | Native | Web | Token |
|---|---|---|---|
| Outer padding | `18px` all sides | `22px` all sides | `$5` / `$6` |
| Border radius | `20px` | `22px` | `$card` / `--lx-radius-card` |
| Background (default) | `#1E293B` | `#1E293B` | `$card` / `--lx-card` |
| Border (default) | `2px solid rgba(255,255,255,0.06)` | same | `$borderSubtle` |
| Shadow | `0 4px 12px rgba(0,0,0,0.15)` | same | `--lx-shadow-soft` |
| Gap between icon / text / chevron | `16px` (native) / `18px` (web) | — | `$4` / `$5` |
| Icon tile size | `56×56px` | `64×64px` | — |
| Icon tile radius | `16px` | `18px` | `$button` / `--lx-radius-button` |
| Icon tile background | `rgba(79,70,229,0.18)` | same | `$primarySoft` / `--lx-primary-soft` |
| Emoji font size | `28px` | `32px` | — |
| Label font-size | `17px` (native) / `19px` (web) | — | between `$body` and `$h3` — **design gap: no exact token step; use raw px** |
| Label font-weight | `800` | `800` | `$black` / `--lx-weight-black` |
| Label color | `#F8FAFC` | `#F8FAFC` | `$fg1` |
| Label font | Poppins (EN) / Cairo (AR) | same | `$heading` |
| Sub font-size | `12px` | `13px` | `$small` / `--lx-size-small` |
| Sub color | `#94A3B8` | `#94A3B8` | `$fg3` |
| Sub font | Poppins (EN) / Tajawal (AR) | same | `$body` |
| Sub `marginTop` | `2px` | `3px` | — |
| Chevron character | `›` (U+203A) | same | — |
| Chevron color | `#A5B4FC` | `#A5B4FC` | `$primaryLight` |
| Chevron font-size | `22px` | `24px` | — |
| RTL chevron | `‹` (U+2039) | same | — |

**States:**

| State | Visual delta |
|---|---|
| Default | Background `$card`, border `$borderSubtle` (rgba 0.06) |
| Hover (web) | Background `#243349` (~8% brighter than `#1E293B`), border `#4F46E5` (`$primary`); `transform: scale(1.02)` — **no darkening** |
| Press | `transform: scale(0.95)` for 80ms (`--lx-dur-base` = 240ms but press feedback is snappy 80ms) |
| Focus (keyboard) | `box-shadow: 0 0 0 2px #4F46E5, 0 0 0 6px rgba(99,102,241,0.45)` = `--lx-focus-ring` |
| Disabled | `opacity: 0.4`, `pointerEvents: none` |

**Motion:** the spring `cubic-bezier(0.34,1.56,0.64,1)` applies to hover scale-up; press is a straight `0.95` ease-out, 80ms.

**Accessibility:**
- `accessibilityRole="button"` on each card
- `accessibilityLabel` from prop: `t('auth.roleSelect.parentA11y')` / `t('auth.roleSelect.studentA11y')`
- `testID="role-card-parent"` / `"role-card-student"`

### 3.5 Footnote text

| Property | EN | AR | Token |
|---|---|---|---|
| Copy | "Children log in with the email a parent assigned — they never create their own account." | "يدخل الأطفال بالبريد الذي يحدده ولي الأمر — لا ينشئون حساباتهم بأنفسهم." | `auth.roleSelect.footnote` |
| Font-size | `12px` | `12px` | `$small` |
| Color | `#64748B` | `#64748B` | `$fg4` |
| Line-height | `1.5` | `1.5` | `--lx-lh-normal` |
| Text-align | center | center | — |
| Margin top | `6px` (native) / inherits gap 26 (web) | — | — |
| Font | Poppins (EN) / Tajawal (AR) | — | `$body` |

### 3.6 RTL — Role Select

- `dir="rtl"` on the screen container
- Card source order: icon tile → text → chevron (natural RTL flow renders them right-to-left: chevron shows at the START = right edge; icon at the END = left edge)
- Chevron character changes to `‹` (U+2039, single left-pointing angle quotation mark) in AR
- Brand wordmark "Learnexia" within the title stays Latin: wrap in a `<Text dir="ltr">Learnexia</Text>` island inside the AR title string
- No `flexDirection:'row-reverse'` anywhere

### 3.7 Full i18n keys for Role Select

```
auth.roleSelect.title         EN: "Welcome to Learnexia"         AR: "مرحباً بك في Learnexia"
auth.roleSelect.subtitle      EN: "Who's signing in?"             AR: "من الذي يسجّل الدخول؟"
auth.roleSelect.parentLabel   EN: "Parent"                        AR: "ولي الأمر"
auth.roleSelect.parentSub     EN: "Track your child's progress"   AR: "تابع تقدّم طفلك"
auth.roleSelect.studentLabel  EN: "Student"                       AR: "طالب"
auth.roleSelect.studentSub    EN: "Learn, play, level up"         AR: "تعلّم والعب وتقدّم"
auth.roleSelect.footnote      EN: "Children log in with the email a parent assigned — they never create their own account."
                              AR: "يدخل الأطفال بالبريد الذي يحدده ولي الأمر — لا ينشئون حساباتهم بأنفسهم."
auth.roleSelect.parentA11y    EN: "Sign in as Parent"             AR: "تسجيل الدخول كولي أمر"
auth.roleSelect.studentA11y   EN: "Sign in as Student"            AR: "تسجيل الدخول كطالب"
```

---

## 4. Screen 3 — Login restyled (`(auth)/login.tsx`)

### 4.1 Layout

**Native (single-column, scrollable):**
- Background: `#0F172A` full-height + `overflow: auto`
- Purple top radial glow (behind the header, absolute-positioned, pointerEvents:none):
  `position: absolute; top: -180px; left: 50%; transform: translateX(-50%); width: 460px; height: 360px; borderRadius: 50%; background: radial-gradient(circle, rgba(168,85,247,0.35) 0%, rgba(168,85,247,0) 65%); pointerEvents: none`
- Content padding: `64px 24px 32px`
- Flex column, `gap: 20` (`$5`)
- Back button (absolute, `top: 64, left: 24` LTR / `right: 24` RTL): `width: 40, height: 40, borderRadius: 12, background: #1E293B, border: 1px solid rgba(255,255,255,0.08), color: #CBD5E1, fontSize: 18` — this is the native `onBack` chevron (`‹` LTR, `›` RTL), not part of `RoleBadge`

**Web (split-panel, unchanged from P1-11):**
- Left column: `LoginBrandPanel` on `background: linear-gradient(165deg,#4F3FB0 0%,#3B2C8F 50%,#1E1B4B 100%)`, with twinkling particles, logo, 🌟 star, tagline
- Right column: `padding: clamp(22px,5vw,56px)`, `maxWidth: 520px`, centered
- Both columns: `minHeight: 100%` / `overflow: auto`, `flexShrink: 0` on each panel child where collapse risk exists
- The brand panel is still controlled by `FormScaffold variant="split" brandSide={...}` (existing component — do not redesign the scaffold)
- On phones (≤768px): brand panel hidden; stacked single column with logo mark + header

The web right-column now adds the `RoleBadge` between the eyebrow block and the `LoginForm` — replacing the old `PersonaToggle` position.

### 4.2 Login header block

| Element | Value | Token |
|---|---|---|
| Brand tile size | 72×72px | — |
| Brand tile radius | 22px (native) / not specified web — use `$modal` = 24px | `--lx-radius-modal` |
| Brand tile bg | `linear-gradient(135deg,#A855F7,#6366F1)` | `--lx-grad-levelup` |
| Brand tile shadow | `0 12px 32px rgba(168,85,247,0.4), inset 0 2px 0 rgba(255,255,255,0.18)` | purple glow variant of `--lx-shadow-float` + inner highlight |
| Brand tile emoji | 🌟, `fontSize: 34` | — |
| Emoji glow | `drop-shadow(0 0 8px rgba(250,204,21,0.6))` | `--lx-xp-glow` at 60% |
| Title "Welcome back" | `fontSize: 27` (native) / 32 (web via existing `login.tsx`), `fontWeight: 900`, `color: #F8FAFC` | `$fg1`; existing `t('auth.login.title')` — no change |
| Title letter-spacing | `-0.02em` | `--lx-tracking-tight` |
| Subtitle (parent, EN) | "Sign in to follow your children's progress" | `auth.login.subtitleParent` (NEW) |
| Subtitle (student, EN) | "Log in to keep your streak alive 🔥" | `auth.login.subtitle` (existing key, repurposed to student) |
| Subtitle font-size | `14px`, `color: #94A3B8` | `$fg3`, `--lx-size-body-sm` |
| Subtitle font | Poppins (EN) / Tajawal (AR body) | `$body` |
| Margin brand-tile → title | `marginBottom: 12` (tile), then title is direct sibling | — |

### 4.3 RoleBadge — reusable component spec

**File:** `apps/student-app/app/(auth)/_components/RoleBadge.tsx`

Renders a centered pill below the login header, above the form fields. It replaces `PersonaToggle` entirely — there is no toggle, only a read-only badge with a "Change" tap target.

**Props:**
```typescript
interface RoleBadgeProps {
  role: 'parent' | 'student';
  onChangePress: () => void;
  direction?: 'ltr' | 'rtl';
  disabled?: boolean;
  testID?: string;
}
```

**Visual anatomy (LTR):**
```
[ role emoji ][ "Signing in as Parent" label ][ "Change" link ]
```

All three items on a single row, centered in the container, `gap: 8px`.

**Exact values:**

| Property | Value | Token |
|---|---|---|
| Container background | `#15161D` (deepest surface, slightly darker than `$bg`) | no exact token — see gap note |
| Container border-radius | `12px` | `$nav` (12) |
| Container border | `1px solid rgba(255,255,255,0.06)` | `$borderSubtle` |
| Container padding | `8px 14px` | `$2` / `$3` |
| Container alignment | `alignItems: center; justifyContent: center` (centered row) | — |
| Role emoji font-size | `16px` | — |
| Role emoji for parent | 👨‍👩‍👦 | — |
| Role emoji for student | 🎓 | — |
| Label text (parent, EN) | "Signing in as Parent" | `auth.login.signingInAsParent` (NEW) |
| Label text (student, EN) | "Signing in as Student" | `auth.login.signingInAsStudent` (NEW) |
| Label text (parent, AR) | "تسجيل الدخول كولي أمر" | `auth.login.signingInAsParent` AR |
| Label text (student, AR) | "تسجيل الدخول كطالب" | `auth.login.signingInAsStudent` AR |
| Label font-size | `13px` | between `$small` and `$body-sm` — raw px |
| Label font-weight | `700` | `$bold` |
| Label color | `#CBD5E1` | `$fg2` |
| Label font | Poppins (EN) / Cairo (AR, badge is display-like) | `$heading` |
| "Change" link (EN) | "Change" | `auth.login.change` (NEW) |
| "Change" link (AR) | "تغيير" | `auth.login.change` AR |
| "Change" font-size | `12px` | `$small` |
| "Change" font-weight | `700` | `$bold` |
| "Change" color | `#A5B4FC` | `$primaryLight` |
| "Change" margin-start | `4px` from label | `$1` |
| "Change" touch target | wraps in a `Pressable`/`TouchableOpacity` with min 44px touch area | — |
| Focus ring on "Change" | `--lx-focus-ring` (2px indigo + 6px indigo-glow) | `$focusRing` |

**States:**

| State | "Change" link delta |
|---|---|
| Default | `color: $primaryLight` (#A5B4FC) |
| Hover (web) | `color: #6366F1` ($primaryHover), `textDecoration: underline` |
| Press | `opacity: 0.7` for 80ms (text links don't scale) |
| Focus | Focus ring on the link target only |
| `disabled=true` | badge container `opacity: 0.4`, "Change" `pointerEvents: none` |

**RTL:** In AR the source order is the same (emoji — label — Change), but `dir="rtl"` causes them to visually render right-to-left. The emoji does NOT mirror (brand rule). Label and "Change" read naturally RTL.

**Accessibility:**
- `accessibilityRole="button"` on the "Change" pressable
- `accessibilityLabel={t('auth.login.change')}` + `accessibilityHint="Tap to go back to role selection"`
- The badge container itself: `accessibilityRole="text"`, `accessibilityLabel={t(role==='parent' ? 'auth.login.signingInAsParent' : 'auth.login.signingInAsStudent')}`
- `testID="role-badge"`, `testID="role-badge-change"`

### 4.4 Email / Password fields (unchanged from P1-11, carried over)

These already match the spec. Record the exact values for the frontend's reference:

| Element | Value | Token |
|---|---|---|
| Input height | `48px` (web) / `52px` (native) | `$12` |
| Input background | `#1E293B` (`$card`) / `#15161D` (native — deeper, `$bgElevated`-adjacent) | `$card` |
| Input border | `1px solid rgba(255,255,255,0.08)` (resting) | `$border` |
| Input border (focus) | `1px solid #4F46E5` + `--lx-focus-ring` | `$borderFocus` |
| Input border-radius | `14px` | `$cardInner` |
| Input font-size | `15px` | raw (between `$body-sm` 14 and `$body` 16) |
| Input font-weight | `500` | `$medium` |
| Input color | `#F8FAFC` | `$fg1` |
| Label font-size | `12px` | `$small` |
| Label font-weight | `700` | `$bold` |
| Label color | `#CBD5E1` | `$fg2` |
| Email field | `direction: ltr` island (LTR even in RTL locale) | — |
| Email placeholder (parent, EN) | "parent@email.com" | — |
| Email placeholder (student, EN) | "sami@learnexia.com" | — |
| Password placeholder | "••••••••" | — |

### 4.5 Remember-me / Forgot-password row (unchanged from P1-11)

Carried over as-is. In RTL the row uses natural source order (`dir="rtl"`):
- Source order: [Remember-me label] [Forgot password link]
- Rendered in RTL: "Forgot password?" appears at the start (right), "Remember me" at the end (left)

### 4.6 Login CTA button (unchanged from P1-11)

| Property | Value | Token |
|---|---|---|
| Height | `52px` | `$12` |
| Border-radius | `16px` | `$button` / `--lx-radius-button` |
| Background (active) | `#4F46E5` | `$primary` |
| Background (disabled) | `#2A2D3E` (between `$card` and `$cardSoft`) — **design gap: no exact token** | raw |
| Shadow (active) | `0 4px 12px rgba(99,102,241,0.4), inset 0 1px 0 rgba(255,255,255,0.2)` | `--lx-shadow-primary-glow` variant + inner highlight |
| Font-size | `16px` | `$body` |
| Font-weight | `700` | `$bold` |
| EN copy | "Log in →" | `auth.login.submitButton` (existing) |
| AR copy | "تسجيل الدخول ←" | existing AR twin |
| Arrow direction (AR) | `←` (arrow points visually left = reading-end = forward in RTL) | existing |
| Press feedback | `scale: 0.95` for 80ms | brand law |
| Hover (web) | brightness 8% up + `scale: 1.02` | brand law |
| Disabled | `opacity: 0.4`, no glow | brand law |

### 4.7 OR divider (unchanged from P1-11)

| Property | Value | Token |
|---|---|---|
| Line | `height: 1px`, `background: rgba(255,255,255,0.08)` | `$border` |
| Label (phone EN) | "Or" | `auth.login.orDivider` |
| Label (tablet+ EN) | "Or continue with" | `auth.login.orContinueWith` |
| Label (AR) | "أو" / "أو تابع مع" | AR twins |
| Label font-size | `12px`, `fontWeight: 600`, `color: #64748B` | `$fg4` |

### 4.8 Social buttons (unchanged from P1-11)

Google/Apple/Microsoft row carried from P1-11/P1-12. No change needed. Labels stay LTR on native (`labelLtr` prop already set). The `SocialButton` label for Google is `auth.login.socialGoogle` = "Google" (EN) / "جوجل" (AR). Note: the brand names "Google", "Apple", "Microsoft" may stay transliterated in AR (as the existing AR HTML does: "جوجل" / "آبل").

### 4.9 Role-conditional footer — CRITICAL CHANGE

This is the most important visual difference from today.

**Parent role (`role === 'parent'`):**

| Element | Value | Token |
|---|---|---|
| Copy (EN) | "New to Learnexia?" + "Create parent account" (link) | `auth.login.newParent` + `auth.login.createAccount` (both existing) |
| Copy (AR) | "جديد هنا؟" + "أنشئ حساباً" | existing AR twins |
| Container alignment | centered row, `gap: $1` | — |
| "New to Learnexia?" color | `#94A3B8` | `$fg3` |
| "Create parent account" color | `#A5B4FC` | `$primaryLight` |
| "Create parent account" font-weight | `800` | `$black` |
| "Create parent account" font-size | `14px` | `$body-sm` |
| On press | `router.push('/(auth)/register')` | — |
| Accessibility | `accessibilityRole="link"` | existing |

**Student role (`role === 'student'`):**

There is NO register link. Instead render the amber notice:

| Element | Value | Token |
|---|---|---|
| Container background | `rgba(245,158,11,0.06)` (native) / `rgba(245,158,11,0.08)` (web) | near `$warningSoft` at half alpha — see gap note |
| Container border | `1px solid rgba(245,158,11,0.18)` (native) / `rgba(245,158,11,0.25)` (web) | warning border variant |
| Container border-radius | `12px` | `$nav` (12) |
| Container padding | `12px 14px` | `$3` |
| Container margin-top | `4px` | `$1` |
| "Need an account?" (EN) | bold prefix, `color: #F59E0B`, `fontWeight: 700` | `$accent` / `--lx-accent` |
| " Ask a parent to add you." (EN) | `color: #64748B` (native) / `#CBD5E1` (web body) | `$fg4` / `$fg2` |
| Full copy (EN) | "Need an account? Ask a parent to add you." | `auth.login.studentNoAccountTitle` + `auth.login.studentNoAccountBody` |
| Full copy (AR) | "هل تحتاج إلى حساب؟ اطلب من ولي أمرك إضافتك." | AR twins |
| Font-size | `13px` (native) / `13px` (web) | raw |
| Text-align | center | — |
| Accessibility | `accessibilityRole="text"`, `accessibilityLabel={...}` | — |

**Caution:** The current `login.tsx` renders the register link unconditionally for ALL users. The register link block MUST be gated: `role === 'parent'` renders the link; `role === 'student'` renders the amber notice; no fallback renders both.

### 4.10 Role-conditional subtitle

The subtitle (below "Welcome back") is now role-driven. Current `auth.login.subtitle` = "Log in to keep your streak alive 🔥" — repurpose this as the **student** subtitle, and add a new **parent** subtitle key:

```
auth.login.subtitleParent    EN: "Sign in to follow your children's progress"
                             AR: "سجّل الدخول لمتابعة تقدّم أطفالك"
auth.login.subtitle          (existing, now = student)
                             EN: "Log in to keep your streak alive 🔥"
                             AR: "سجّل الدخول للحفاظ على سلسلتك 🔥"
```

### 4.11 Back behavior

The native back-button (`‹` at `top: 64, left: 24` LTR; `right: 24` RTL) and the `RoleBadge` "Change" link BOTH call `router.replace('/(auth)/role-select')` — use `replace`, not `push`, so the back stack does not accumulate.

`onBack` is always non-null on the login screen (it is always reached from role-select). The back button should always be rendered.

### 4.12 Locale/theme controls

`LocaleThemeControls` sits at the top-right (LTR) / top-left (RTL) of the form column, as today. No change to this component. In native it is rendered in `login.tsx` (existing, keep). In web it is in the right panel.

### 4.13 Flash message banner (unchanged)

The `flashKey` / `ServerErrorBanner` block is carried over unchanged.

### 4.14 Full i18n keys — new keys for Login

```
auth.login.signingInAsParent   EN: "Signing in as Parent"           AR: "تسجيل الدخول كولي أمر"
auth.login.signingInAsStudent  EN: "Signing in as Student"          AR: "تسجيل الدخول كطالب"
auth.login.change              EN: "Change"                          AR: "تغيير"
auth.login.subtitleParent      EN: "Sign in to follow your children's progress"
                               AR: "سجّل الدخول لمتابعة تقدّم أطفالك"
auth.login.studentNoAccountTitle  EN: "Need an account?"            AR: "هل تحتاج إلى حساب؟"
auth.login.studentNoAccountBody   EN: "Ask a parent to add you."    AR: "اطلب من ولي أمرك إضافتك."
```

**Keys to RETIRE (with PersonaToggle deletion):**
```
auth.login.personaParent       (retire)
auth.login.personaStudent      (retire)
auth.login.personaToggleLabel  (retire)
```

---

## 5. Token map — CSS → Tamagui

| CSS `--lx-*` | Tamagui `$` | Value | Usage in auth screens |
|---|---|---|---|
| `--lx-bg` | `$bg` | `#0F172A` | Screen background |
| `--lx-card` | `$card` | `#1E293B` | RoleCard bg, input bg (web) |
| `--lx-primary` | `$primary` | `#4F46E5` | CTA button bg, focus border |
| `--lx-primary-hover` | `$primaryHover` | `#6366F1` | Button hover; `LoginBrandPanel` logo tile bg |
| `--lx-primary-press` | `$primaryPress` | `#4338CA` | CTA press state |
| `--lx-primary-soft` | `$primarySoft` | `rgba(79,70,229,0.18)` | Icon tile bg in `RoleCard` |
| `--lx-primary-glow` | `$primaryGlow` | `rgba(99,102,241,0.45)` | Button shadow, `--lx-focus-ring` outer |
| `--lx-purple` | `$purple` | `#A855F7` | Brand tile gradient start, splash bg |
| `--lx-accent` | `$accent` | `#F59E0B` | Student amber notice accent text |
| `--lx-fg1` | `$fg1` | `#F8FAFC` | Headings, card labels |
| `--lx-fg2` | `$fg2` | `#CBD5E1` | Role badge label, field labels |
| `--lx-fg3` | `$fg3` | `#94A3B8` | Subtitles, "New to Learnexia?" |
| `--lx-fg4` | `$fg4` | `#64748B` | Footnote, OR divider, student notice body (native) |
| `--lx-fg2Alpha` | `$fg2Alpha` | `rgba(255,255,255,0.70)` | Splash subtitle |
| `--lx-border` | `$border` | `rgba(255,255,255,0.08)` | Default input border, divider lines |
| `--lx-border-strong` | `$borderStrong` | `rgba(255,255,255,0.16)` | Back button border |
| `--lx-border-focus` | `$borderFocus` | `#4F46E5` | Input focus border |
| `$borderSubtle` | `$borderSubtle` | `rgba(255,255,255,0.06)` | RoleCard default border |
| `$primaryLight` | `$primaryLight` | `#A5B4FC` | Chevron, "Change" link, "Create parent account" |
| `--lx-radius-sm` | `$sm` | `8px` | Chips (not used here) |
| `$nav` | `$nav` | `12px` | Back button radius, RoleBadge radius, amber notice radius |
| `$cardInner` | `$cardInner` | `14px` | Input radius |
| `--lx-radius-button` | `$button` | `16px` | CTA button radius, icon tile radius (native) |
| `--lx-radius-card` | `$card` radius | `20px` | RoleCard radius (native) |
| `--lx-radius-modal` | `$modal` | `24px` | Brand tile radius (web) |
| `--lx-space-1` | `$1` | `4px` | Tiny gaps |
| `--lx-space-2` | `$2` | `8px` | Badge padding vertical |
| `--lx-space-3` | `$3` | `12px` | Badge padding horizontal, amber notice padding |
| `--lx-space-4` | `$4` | `16px` | Card gap (native) |
| `--lx-space-5` | `$5` | `20px` | Login form gap |
| `--lx-space-6` | `$6` | `24px` | Role select outer gap (native) |
| `--lx-shadow-soft` | `shadows.soft` | `0 4px 12px rgba(0,0,0,0.15)` | RoleCard shadow |
| `--lx-shadow-float` | `shadows.float` | `0 8px 24px rgba(0,0,0,0.25)` | Hover shadow on RoleCard |
| `--lx-focus-ring` | `shadows.focusRing` | `0 0 0 2px #4F46E5, 0 0 0 6px rgba(99,102,241,0.45)` | Keyboard focus on all interactive elements |
| `--lx-grad-levelup` | `gradients.levelup` | `linear-gradient(135deg,#A855F7,#6366F1)` | Brand tile gradient |
| `--lx-ease-spring` | `bezier.spring` | `cubic-bezier(0.34,1.56,0.64,1)` | Card hover scale-up |
| `--lx-dur-fast` | `durations.fast` | `120ms` | — |
| `--lx-dur-base` | `durations.base` | `240ms` | Standard transition |

---

## 6. Design gaps (do not silently invent — list here)

| Gap ID | Element | Issue | Recommended handling |
|---|---|---|---|
| DS-A-01 | `RoleCard` label font-size 17px (native) / 19px (web) | No exact token step between `$body-sm` (14) and `$h3` (18). 17px and 19px are both non-standard. | Use raw `fontSize: 17` / `19` on native/web respectively. Flag for the token scale if these sizes recur. |
| DS-A-02 | `RoleBadge` background `#15161D` | "Deepest" surface — between `$bg` and `$card`. Not in current `colors.ts`. | Add `bgDeepest: '#15161D'` to `packages/design-system/src/tokens/colors.ts`. Or use raw hex for this batch and promote. |
| DS-A-03 | Disabled CTA button background `#2A2D3E` | Between `$card` (#1E293B) and `$cardSoft` (#334155). Not a current token. | Use raw hex for now. Existing `login.tsx` already uses it; do not introduce a new token unilaterally. |
| DS-A-04 | Amber notice background `rgba(245,158,11,0.06)` (native) vs `rgba(245,158,11,0.08)` (web) | `$warningSoft` = `rgba(245,158,11,0.18)` — both kit values are at lower alpha. Native uses 0.06, web uses 0.08. | Adopt `rgba(245,158,11,0.08)` consistently (web value is slightly more visible; native difference is negligible). No new token needed — use raw rgba. |
| DS-A-05 | Amber notice border `rgba(245,158,11,0.18)` (native) vs `0.25` (web) | Minor alpha difference. | Use `rgba(245,158,11,0.25)` consistently (web value, more legible). No new token. |
| DS-A-06 | Purple radial top-glow on login | The `colors.ts` has `$purpleSoft` = `rgba(168,85,247,0.18)` but the glow uses `rgba(168,85,247,0.35)` (native) / `rgba(168,85,247,0.28)` (role-select web). No token for 0.35. | Use raw rgba. The glow is a background decal, not a border or shadow, so raw is acceptable. |
| DS-A-07 | `RoleCard` hover background `#243349` | Not a current token (midpoint between `$card` #1E293B and `$cardSoft` #334155). | Use raw hex for hover state. Consistent with the kit source. |

---

## 7. Motion spec

| Interaction | Duration | Easing | Detail |
|---|---|---|---|
| RoleCard hover scale-up (web) | 200ms | `cubic-bezier(0.34,1.56,0.64,1)` (`--lx-ease-spring`) | `scale(1.02)` overshoot spring |
| RoleCard hover border/bg | 200ms | ease-out | color transition |
| RoleCard press | 80ms | ease-out | `scale(0.95)` |
| RoleCard press release | 80ms | `--lx-ease-spring` | back to 1.0 |
| Route transition (role-select → login) | 250–300ms | slide + fade | existing `animation:'fade'` in `_layout.tsx`; `fade` is correct |
| Route transition (login → role-select via "Change") | 250–300ms | slide + fade | same — `router.replace` |
| "Change" link press | 80ms | ease-out | `opacity: 0.7` (text link, no scale) |
| Login CTA press | 80ms | ease-out | `scale(0.95)` |
| Login CTA hover | 120ms | ease-out | `scale(1.02)` + brightness 8% |

No new animation primitives are needed. All of the above use existing `--lx-ease-spring` / `--lx-dur-base` / `--lx-dur-fast`.

---

## 8. Accessibility / kid-UX checklist

| Rule | Application |
|---|---|
| Touch targets ≥44px | RoleCard buttons: ~92px tall (native). Back button: 40×40px (just below — wrap in a 44px touch area via `hitSlop`). "Change" link: wrap in a 44px-minimum pressable with `hitSlop`. |
| Focus order (web / keyboard) | Splash → Role Select (tab to Parent card → Student card → footnote (non-interactive, skip)). Login: back button → locale/theme → brand tile (non-interactive) → role badge / "Change" → email → password → remember-me → forgot-password → login button → or-divider → Google → Apple → (Microsoft tablet+) → register-link / amber-notice |
| Focus ring | All interactive elements: `--lx-focus-ring` (2px indigo + 6px indigo-glow). `tabIndex={0}` on the "Change" link. |
| ARIA roles | Role Select: `accessibilityRole="radiogroup"` not applicable (cards are full-nav picks, not radio buttons) — use `accessibilityRole="button"` per card. Login form: existing roles unchanged. |
| `accessibilityLabel` | RoleCard: `auth.roleSelect.parentA11y` / `auth.roleSelect.studentA11y`. RoleBadge: `auth.login.signingInAsParent` / `auth.login.signingInAsStudent`. "Change": `auth.login.change`. |
| High contrast | All foreground colors are ≥4.5:1 against `$bg` at the values specified. `#94A3B8` on `#0F172A` ≈ 5.1:1 (passes AA). `#64748B` on `#0F172A` ≈ 3.8:1 (fails AA on small text) — the footnote is ≥12px which is the minimum; flag this if WCAG AA strict compliance is required. |
| No student self-register | Student login path: no `/(auth)/register` link, no mention of account creation (only the amber notice directing to a parent). `router.push('/(auth)/register')` must not appear anywhere in the student code path. |
| Emoji semantics | 🎮 (role select header), 👨‍👩‍👦 (parent), 🎓 (student), 🌟 (brand), 🔥 (streak in subtitle) — all from the brand semantic set. None are decorative — they carry meaning. Each has a natural textual context so `accessibilityElementsHidden` is NOT set (they should be announced by screen-reader in context). |

---

## 9. RTL notes per screen — full parity table

### Role Select RTL (AR)

| Element | LTR | RTL | Implementation |
|---|---|---|---|
| Screen `dir` | — | `dir="rtl"` | `writingDirection={direction}` prop on root |
| Header title | left-aligned text center | right-aligned center | `textAlign:'center'` in both — no change; centering is locale-neutral |
| Title "Learnexia" brand word | part of normal flow | stays Latin/LTR in mid-sentence | wrap `<Text dir="ltr" style={{unicodeBidi:'embed'}}>Learnexia</Text>` island |
| RoleCard row order | icon → text → chevron (visual L→R) | chevron → text → icon (visual R→L) | natural RTL (`dir="rtl"` on card container) |
| Chevron | `›` | `‹` | conditional on `direction` prop |
| Card text-align | `text-align: left` (LTR default) | `text-align: right` (RTL default) | `textAlign: direction==='rtl'?'right':'left'` — or `auto` in RN |
| Footnote | center, LTR | center, RTL | `textAlign:'center'` both |

### Login RTL (AR)

| Element | LTR | RTL | Implementation |
|---|---|---|---|
| Screen `dir` | — | `dir="rtl"` | prop on container |
| Back button position | absolute `left: 24` | absolute `right: 24` | conditional style |
| Back button chevron | `‹` | `›` | conditional on `direction` |
| Brand tile | centered, no flip | centered, no flip | — |
| "Welcome back" (AR) | — | "أهلاً بعودتك" | `t('auth.login.title')` in AR already exists in `resources.ts` (check AR block) |
| Subtitle (AR, parent) | — | "سجّل الدخول لمتابعة تقدّم أطفالك" | `auth.login.subtitleParent` AR |
| Subtitle (AR, student) | — | "سجّل الدخول للحفاظ على سلسلتك 🔥" | `auth.login.subtitle` AR |
| RoleBadge row | emoji → label → Change | same source order, RTL direction auto-reverses | `dir="rtl"` on badge; no row-reverse |
| RoleBadge "Change" (AR) | "Change" (right side) | "تغيير" (left side in LTR terms = trailing in RTL = start of row visually) | copy from `auth.login.change` AR |
| Email field | LTR input | **ALWAYS LTR island** — even in RTL locale | `direction:'ltr'` on email `TextInput`, `textAlign:'left'` |
| Email placeholder (AR parent) | — | "parent@email.com" | stays Latin (email addresses are technical strings — brand law) |
| Password field | standard | no special LTR needed (passwords are opaque) | — |
| Remember-me / Forgot-password | [checkbox label] [forgot link] L→R | [forgot link] [checkbox label] R→L | natural RTL; source order unchanged |
| Remember-me (AR) | "Remember me" | "تذكّرني" | existing AR twin |
| Forgot password (AR) | "Forgot password?" | "نسيت كلمة المرور؟" | existing AR twin |
| OR divider (AR) | "Or" | "أو" | existing AR twin |
| CTA arrow (AR) | "Log in →" | "تسجيل الدخول ←" | existing AR twin |
| Social row | Google · Apple · Microsoft L→R | same visual order; social brand names stay in LTR | `labelLtr` prop already on `SocialButton` |
| Register link row (parent, AR) | "New to Learnexia? [Create account]" | "جديد هنا؟ [أنشئ حساباً]" | existing AR twins |
| Amber notice (student, AR) | "Need an account? Ask a parent…" | "هل تحتاج إلى حساب؟ اطلب من ولي أمرك إضافتك." | `auth.login.studentNoAccountTitle` + `auth.login.studentNoAccountBody` AR |
| Numerals in copy | N/A (no numbers on login) | N/A | — |

**Key RTL rule summary:** NO `flexDirection:'row-reverse'` anywhere. All mirroring comes from `dir="rtl"` or `writingDirection="rtl"` on containers. Email input uses explicit `direction:'ltr'` island. Brand wordmark uses `dir="ltr"` island. Progress/loading bar on splash uses `direction:'ltr'` wrapper (fills L→R universally per brand law).

---

## 10. Implementation handoff checklist

Mapped to the brief's visual + functional ACs.

### Files to CREATE

| File | AC | Notes |
|---|---|---|
| `apps/student-app/app/(auth)/role-select.tsx` | V2, F1, F2 | New Route Select screen. Imports `RoleCard`. On pick: `router.push({pathname:'/(auth)/login', params:{role}})`. No tab bar. |
| `apps/student-app/app/(auth)/_components/RoleCard.tsx` | V2 | Spec §3.4. Props: `{id, emoji, label, sub, onPress, direction, testID}`. |
| `apps/student-app/app/(auth)/_components/RoleBadge.tsx` | V4 | Spec §4.3. Props: `{role, onChangePress, direction, disabled, testID}`. |

### Files to CHANGE

| File | AC | Change |
|---|---|---|
| `apps/student-app/app/(auth)/_layout.tsx` | F1, R2 | Add `<Stack.Screen name="role-select" />`. Update docstring: "Only three routes exist here: login, register, role-select." |
| `apps/student-app/src/hooks/useAuthRoute.ts` | F1 | Change `router.replace('/(auth)/login')` → `router.replace('/(auth)/role-select')`. Update docstring "signed-out → /(auth)/role-select". Do NOT touch the `navReady` guard. |
| `apps/student-app/app/(auth)/login.tsx` | V3, V4, V5, V6, F2, F3 | Read `role` from `useLocalSearchParams`, default `'parent'`. Render `RoleBadge` with `onChangePress={()=>router.replace('/(auth)/role-select')}`. Make register-link block conditional on `role==='parent'`; render amber notice for `role==='student'`. Switch subtitle key by role. Pass `role` to `LoginForm`. |
| `apps/student-app/app/(auth)/_components/LoginForm.tsx` | V4 | Remove `PersonaToggle` usage + `persona`/`setPersona` `useState` + the import. Accept `role: 'parent' \| 'student'` prop (default `'parent'`). Everything else unchanged. |
| `apps/student-app/app/(auth)/_components/PersonaToggle.tsx` | — | DELETE. Confirm no other importer (grep first). |
| `packages/shared/src/i18n/resources.ts` | V2–V6 | Add all new keys listed in §3.7 and §4.14, in BOTH `en` and `ar` blocks. Retire `auth.login.personaParent/personaStudent/personaToggleLabel`. |
| `packages/design-system/src/tokens/colors.ts` | DS-A-02 | (Optional for Batch A) Add `bgDeepest: '#15161D'` — or defer and use raw hex. |

### No change needed

- `app/index.tsx` (splash — functional only: useAuthRoute redirect)
- `LoginBrandPanel.tsx` — carries over as-is
- `LocaleThemeControls.tsx` — carries over as-is
- `loginParts.tsx` — carries over as-is
- `SocialIcons.tsx` — carries over as-is
- `FormScaffold.tsx` — carries over as-is
- All `packages/ui` exports: `Button`, `TextField`, `Card`, `GradientBox` — reuse as-is

---

## 11. New components (full summary)

### `RoleCard`

No new design pattern. It is a standard `Pressable`/`TouchableOpacity` wrapper with a horizontal flex row inside — the same pattern as `PMChildRow`, `PMRecRow`, and the existing `SocialButton`. No factory, no strategy, no provider.

### `RoleBadge`

No new design pattern. It is a `Stack` (Tamagui) with a nested `Pressable` for the "Change" link — same pattern as the `Checkbox` in `loginParts.tsx`. No new abstraction.

Both components are self-contained; they do not introduce any new state management shape.

---

## 12. Open question dispositions

| Q | Disposition |
|---|---|
| Q1 (traceability) | Proceed off handoff + brief per assumption. |
| Q2 (role transport) | Route param confirmed. No Zustand store, no state machine. |
| Q3 (no-role default) | Default `parent` — matches kit and today's `useState` default. |
| Q4 (component home) | Start in `(auth)/_components`; promote to `packages/ui` when Batch B/C reuse. Flag for Batch B planner. |
| Q5 (dead i18n keys) | Retire `personaParent/personaStudent/personaToggleLabel` in this PR (clean break). |
| Q6 (logout target) | Logout → signed-out → `useAuthRoute` guard → role-select (via splash boot on a fresh session). Confirmed behavior — no code change needed beyond the guard redirect. |

---

Design spec ready for frontend.
