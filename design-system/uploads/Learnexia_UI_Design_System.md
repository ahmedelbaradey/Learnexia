# Learnexia — UI Design System

# 🎯 الهدف

بناء UI Design System احترافي لمنصة:

> 🎮 Learnexia — Gamified AI Educational Platform

يركز على:
- الأطفال
- Gamification
- Emotional UX
- AI-powered learning
- Simplicity
- Engagement

---

# 🧠 Design Philosophy

الـ UI يجب أن يكون:

- بسيط جدًا
- ممتع بصريًا
- سريع الفهم
- مليء بالمكافآت البصرية
- يشبه الألعاب
- Child-friendly
- Emotion-driven

---

# 🎨 Color System

# Primary Colors

| Role | Color | Usage |
|---|---|---|
| Primary | #4F46E5 | Main actions |
| Secondary | #22C55E | Success states |
| Accent | #F59E0B | Rewards & highlights |
| Danger | #EF4444 | Errors & heart loss |
| Purple | #A855F7 | Badges & achievements |

---

# Background Colors

| Layer | Color |
|---|---|
| Main Background | #0F172A |
| Card Background | #1E293B |
| Soft Card | #334155 |
| Light Background | #F8FAFC |

---

# Text Colors

| Usage | Color |
|---|---|
| Primary Text | #F8FAFC |
| Secondary Text | #CBD5E1 |
| Muted Text | #94A3B8 |

---

# Gradient System

## XP Gradient

```text
#22C55E → #4F46E5
```

---

# Reward Gradient

```text
#F59E0B → #EF4444
```

---

# Level-Up Gradient

```text
#A855F7 → #6366F1
```

---

# 🔤 Typography System

# Primary Fonts

## Recommended

- Poppins
- Cairo
- Tajawal

---

# Font Usage

| Font | Usage |
|---|---|
| Poppins | English UI |
| Cairo | Arabic UI |
| Tajawal | Alternative Arabic |

---

# Typography Scale

| Type | Size | Weight |
|---|---|---|
| H1 | 32px | Bold |
| H2 | 24px | SemiBold |
| H3 | 18px | Medium |
| Body | 14–16px | Regular |
| Small | 12px | Regular |

---

# Typography Rules

- Short text
- Large readable text
- Minimal paragraphs
- Emojis for emotional reinforcement

---

# 📐 Spacing System

```text
4px
8px
16px
24px
32px
48px
```

---

# 📦 Radius System

| Component | Radius |
|---|---|
| Small Elements | 8px |
| Buttons | 16px |
| Cards | 20px |
| Modals | 24px |

---

# 🌑 Shadow System

# Soft Shadow

```css
0 4px 12px rgba(0,0,0,0.15)
```

---

# Floating Card Shadow

```css
0 8px 24px rgba(0,0,0,0.25)
```

---

# Glow Effects

Used for:
- XP gain
- Rewards
- Badges
- Level-up animations

---

# 🧩 Component System

# 1. Buttons

# Variants

- Primary
- Secondary
- Success
- Danger
- Disabled

---

# Button Rules

- Large touch targets
- Rounded corners
- Strong contrast
- Animated feedback

---

# States

- Default
- Hover
- Active
- Disabled
- Loading

---

# 2. XP Progress Bar

# Features

- Animated fill
- Gradient progress
- Glow effect

---

# Example

```text
████████░░ 780 / 1000 XP
```

---

# 3. Badge Component

# Features

- Circular design
- Glow effects
- Earned state
- Locked state

---

# Badge Types

- Bronze
- Silver
- Gold
- Legendary

---

# 4. Hearts System ❤️

# Features

- 5 hearts max
- Loss animation
- Recovery system

---

# States

- Full
- Empty
- Lost
- Recovering

---

# 5. Streak Component 🔥

# Features

- Animated flame
- Pulse effect
- Streak counter

---

# Example

```text
🔥 7-Day Streak
```

---

# 6. Lesson Card

# Structure

- Title
- Subject
- Progress
- Start Button
- XP Reward

---

# Style

- Large radius
- Soft shadow
- Hover lift effect

---

# 7. Quiz Card

# Features

- Large answers
- Visual feedback
- Progress indicator
- Timer (optional)

---

# 8. Skill Tree Node

# States

- Locked 🔒
- Unlocked 🟢
- Completed ✅
- Boss 🔥

---

# Features

- Connected nodes
- Zoomable tree
- Animated transitions

---

# 9. Mission Card

# Structure

- Task list
- Reward preview
- Progress state

---

# Example

```text
🎯 Daily Mission
- Solve 3 questions
- Complete 1 quiz
```

---

# 10. AI Tutor Bubble

# Types

- AI Message
- Student Message
- Hint Bubble

---

# Features

- Rounded bubbles
- Typing animation
- Friendly appearance

---

# 11. Reward Popup

# Used For

- XP rewards
- Badge unlocks
- Level ups

---

# Features

- Celebration animation
- Glow effects
- Sound support later

---

# 12. Parent Dashboard Cards

# Components

- Progress summary
- Weak areas
- Weekly reports
- Charts

---

# 🎮 Gamification Design Rules

# Every action should create:

- feedback
- progress
- reward
- motivation

---

# Examples

| Action | Reward |
|---|---|
| Correct answer | XP |
| Daily login | Streak |
| Mission complete | Badge |
| Level up | Celebration |

---

# 🧠 Animation System

# Micro Interactions

## Button Press

```text
Scale: 0.95
```

---

# XP Gain

- progress animation
- glow pulse

---

# Correct Answer

- confetti
- success pulse

---

# Wrong Answer

- soft shake
- gentle feedback

---

# Level Up

- burst animation
- glowing effects

---

# 🧠 Layout Principles

# Always

- one primary action per screen
- clear hierarchy
- visual progress
- minimal clutter

---

# Never

- dense text
- small buttons
- complicated navigation
- too many actions

---

# 📱 Responsive Design

# Mobile First

Target:
- tablets
- mobile devices

---

# Recommended Frame Sizes

```text
390 x 844
768 x 1024
```

---

# 🧠 Accessibility Rules

- High contrast
- Large touch areas
- Readable fonts
- Clear color states
- Simple language

---

# 🧠 Emotional UX Principles

The student should feel:

- progress
- achievement
- excitement
- encouragement

---

# Example Feedback

```text
🎉 Great Job!
🔥 Keep your streak alive!
🏆 New badge unlocked!
```

---

# 🏗️ Design Tokens

# Colors

```json
{
  "primary": "#4F46E5",
  "success": "#22C55E",
  "warning": "#F59E0B",
  "danger": "#EF4444",
  "background": "#0F172A",
  "card": "#1E293B"
}
```

---

# Typography

```json
{
  "h1": "32px",
  "h2": "24px",
  "body": "16px",
  "small": "12px"
}
```

---

# Radius

```json
{
  "button": "16px",
  "card": "20px",
  "modal": "24px"
}
```

---

# 🧠 Final UI Identity

Learnexia يجب أن يشعر أنه:

> 🎮 Educational Game World

وليس:
- LMS
- Dashboard system
- Generic chatbot

---

# 🎯 Final Strategic Insight

النجاح الحقيقي للـ UI لن يأتي من:
- كثرة الشاشات
- كثرة الألوان
- complexity

لكن من:
- emotional design
- gamification
- simplicity
- fast feedback
- child-friendly UX
