# Learnexia — Figma Design Structure

# 🎯 الهدف

بناء Figma Design Structure احترافي وقابل للتوسع لمنصة:

> 🎮 Learnexia — Gamified AI Educational Platform

---

# 🧠 Design Philosophy

الـ UI يجب أن يكون:

- بسيط للأطفال
- ممتع بصريًا
- سريع الفهم
- مليء بالمكافآت البصرية
- شبيه بالألعاب
- Gamified-first

---

# 🏗️ Figma File Structure

# الصفحات الرئيسية

```text
1. Design System
2. Components Library
3. Mobile Screens
4. Gamification Screens
5. AI Interaction Flows
6. Parent Dashboard
7. Motion & Animations
8. Prototypes
```

---

# 1. Design System Page

# تحتوي على:

- Colors
- Typography
- Spacing
- Radius system
- Shadows
- Icons
- Grid system

---

# 🎨 Color Palette

## Primary Colors

| Usage | Color |
|---|---|
| Primary | #4F46E5 |
| Success | #22C55E |
| Warning | #F59E0B |
| Danger | #EF4444 |

---

# Background Colors

| Usage | Color |
|---|---|
| Main Background | #0F172A |
| Card Background | #1E293B |
| Soft Card | #334155 |

---

# Typography

## Fonts

- Poppins
- Cairo
- Tajawal

---

# Typography Scale

| Type | Size |
|---|---|
| H1 | 32px |
| H2 | 24px |
| H3 | 18px |
| Body | 14–16px |
| Small | 12px |

---

# Spacing System

```text
4px
8px
16px
24px
32px
48px
```

---

# Radius System

| Type | Radius |
|---|---|
| Small | 8px |
| Medium | 16px |
| Large | 20px |
| Cards | 24px |

---

# 2. Components Library

# 🎯 الهدف

Reusable Components System

---

# Core Components

## Buttons

### Variants

- Primary
- Secondary
- Success
- Danger
- Disabled

---

# Progress Components

- XP Bar
- Level Progress
- Loading States

---

# Gamification Components

- Badge
- Streak Flame 🔥
- Hearts ❤️
- Reward Popup
- Mission Card

---

# Learning Components

- Lesson Card
- Quiz Card
- Skill Node
- AI Tutor Bubble
- Hint Card

---

# Parent Components

- Report Card
- Progress Summary
- Weak Areas Chart

---

# Component Naming Convention

```text
Component / Category / Variant
```

---

# أمثلة

```text
Button / Primary / Default
Card / Lesson / Active
Badge / Gold / Earned
Progress / XP / Animated
```

---

# 3. Mobile Screens Page

# 📱 Mobile First

Frame Size:

```text
390 x 844
```

---

# Screens Structure

## Authentication

- Splash Screen
- Login
- Register
- Role Selection

---

# Student Screens

## Home Dashboard

يحتوي على:
- XP bar
- streak
- daily mission
- continue learning
- league preview

---

# Skill Tree Screen

```text
Math
 ├── Numbers
 ├── Addition
 ├── Fractions
 └── Geometry
```

---

# Lesson Screen

## يحتوي على:

- AI explanation
- visual examples
- quiz section
- hints
- hearts system

---

# Quiz Screen

## يحتوي على:

- progress indicator
- question card
- answers
- instant feedback

---

# Reward Screen

## يحتوي على:

- XP gained
- badges
- streak update
- level progress

---

# League Screen

## يحتوي على:

- rankings
- avatars
- XP
- promotions

---

# Mission Screen

## يحتوي على:

- daily tasks
- progress
- rewards

---

# Profile Screen

## يحتوي على:

- level
- XP
- badges
- statistics

---

# 4. Gamification Screens

# الهدف

عرض أنظمة اللعب بشكل منفصل.

---

# Screens

## XP System

- progress animation
- level transitions

---

# Streak System

- daily streak
- streak rewards

---

# Badge Collection

- earned badges
- locked badges

---

# Hearts System

- heart loss
- practice mode

---

# League System

- promotions
- rankings
- seasonal resets

---

# 5. AI Interaction Flows

# 🎯 الهدف

تصميم تجربة التفاعل مع الـ AI.

---

# Flow 1 — Ask AI

```text
Student Question
      ↓
AI Explanation
      ↓
Visual Example
      ↓
Follow-up Quiz
```

---

# Flow 2 — Lesson Flow

```text
Lesson Start
      ↓
AI Explanation
      ↓
Interactive Quiz
      ↓
Reward
```

---

# Flow 3 — Adaptive Learning

```text
Wrong Answers
      ↓
Simplified Explanation
      ↓
Reinforcement Questions
```

---

# 6. Parent Dashboard Page

# يحتوي على:

- weekly reports
- weak areas
- learning activity
- progress charts
- recommendations

---

# 7. Motion & Animation Page

# 🎯 مهم جدًا للأطفال

---

# Animations

## XP Animation

- smooth progress fill

---

# Badge Animation

- pop-in effect
- glow effect

---

# Correct Answer Animation

- confetti
- success pulse

---

# Wrong Answer Animation

- shake effect
- soft feedback

---

# Streak Animation

- animated flame

---

# 8. Prototype Page

# يحتوي على:

- navigation flow
- onboarding flow
- lesson flow
- mission flow
- reward flow

---

# 🧠 Auto Layout Rules

# Cards

- Vertical Auto Layout
- Padding: 16–24px
- Gap: 8–12px

---

# Buttons

- Fixed height
- Center aligned
- Large touch area

---

# Lists

- consistent spacing
- simple hierarchy

---

# 🧠 UX Rules

# يجب دائمًا:

- one primary action per screen
- minimal cognitive load
- visual feedback
- large touch targets
- clear progress visibility

---

# ❌ تجنب:

- complex navigation
- dense text
- small buttons
- crowded screens

---

# 🧠 Design Identity

Learnexia يجب أن يشعر كأنه:

> 🎮 Educational Game World

وليس:
- LMS
- Dashboard System
- Generic AI Chatbot

---

# Final Structure Summary

```text
Learnexia.fig
│
├── Design System
├── Components Library
├── Mobile Screens
├── Gamification
├── AI Flows
├── Parent Dashboard
├── Motion
└── Prototypes
```

---

# 🚀 Recommended Plugins

- Iconify
- Tokens Studio
- Figmotion
- Design Lint
- Autoflow

---

# 🎯 Final Strategic Insight

نجاح الـ UI لن يأتي من:
- كثرة الشاشات
- complexity

لكن من:
- gamification
- emotional UX
- child-friendly interaction
- reward-driven behavior
