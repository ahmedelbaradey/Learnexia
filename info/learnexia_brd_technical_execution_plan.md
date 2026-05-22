# Learnexia — BRD + Technical Requirements + Execution Plan

> ⚠️ **SUPERSEDED — historical source material.** This is an early synthesis doc. Where it conflicts with the current sources of truth, **those win**: [docs/BRD.md](../docs/BRD.md), [docs/SRS.md](../docs/SRS.md), [docs/architecture.md](../docs/architecture.md), and [CLAUDE.md](../CLAUDE.md). Known divergences in this doc, **do not follow them here**:
> - **AI is framed as the core moat** (§6, §24). Corrected: the moat is the **habit loop + curriculum data + behavior + gamification**, *not* the AI model — see [docs/BRD.md §1](../docs/BRD.md) and [docs/briefs/barrier-to-entry-gap-analysis.md](../docs/briefs/barrier-to-entry-gap-analysis.md).
> - **Curriculum Intelligence listed as "Phase 3 future"** (§23). Corrected: modeled now (SRS §4.8) with a hand-authored MVP skill-graph slice (story P2-11).
> - **5 subjects incl. Social Studies.** Corrected: **4 subjects** (Math, Science, Arabic, English); no Social Studies.
> - **ASP.NET Core 9 / Next.js-only frontend.** Corrected: **.NET 10** modular monolith; frontend is a **Turborepo + Expo universal** student app (Next.js for admin/marketing later) — see [docs/dev/FRONTEND_ARCHITECTURE.md](../docs/dev/FRONTEND_ARCHITECTURE.md).
> - **Teacher role.** Corrected: **no teacher role** in the product.

# 1. Product Overview

## Product Name
Learnexia

## Product Vision
An AI-powered gamified learning platform for school students focused on creating addictive, adaptive, personalized learning experiences using AI tutoring, gamification, learning paths, and reinforcement systems.

The platform combines:
- AI tutoring
- Gamification
- Adaptive learning
- Curriculum alignment
- Parent visibility
- Interactive learning

The goal is to transform learning from:
- boring
- passive
- stressful

Into:
- engaging
- personalized
- game-like
- rewarding

---

# 2. Business Goals

## Primary Goals

### 1. Increase Student Engagement
Create a learning experience students voluntarily return to daily.

### 2. Improve Learning Outcomes
Use adaptive learning and reinforcement to improve understanding and retention.

### 3. Build Daily Learning Habit
Use missions, streaks, XP, and rewards to establish consistent study behavior.

### 4. Parent Visibility
Provide parents with measurable learning insights.

### 5. Scalable AI Educational Platform
Build a reusable architecture capable of expanding into additional grades, subjects, and regions.

---

# 3. Target Audience

## Primary Audience

### Students
- Primary school
- Middle school (prep)
- Arabic-speaking students

### Parents
- Interested in measurable learning improvement
- Need visibility into student performance

---

# 4. MVP Scope

## Included in MVP

### Subjects
- Math
- Science
- Arabic
- English
- Social Studies

### Core Features

#### AI Tutor
- AI explanations
- Follow-up questions
- Hints
- Simplified learning

#### Learning Paths
- Skill trees
- Progressive lessons
- Unlock system

#### Gamification
- XP system
- Levels
- Badges
- Streaks
- Daily missions
- Hearts system

#### Adaptive Learning
- Difficulty adaptation
- Reinforcement
- Student modeling

#### Parent Dashboard
- Weekly reports
- Weak areas
- Progress summaries

#### Quiz Engine
- MCQ
- True/False
- Matching
- Adaptive quizzes

---

# 5. Out of Scope (Phase 1)

- Video generation
- Voice AI
- Teacher dashboard
- Marketplace
- Multi-agent orchestration in critical flows
- Real-time multiplayer
- School ERP integrations
- Autonomous AI systems

---

# 6. Product Architecture

# High-Level Architecture

```text
Frontend (Next.js PWA)
        ↓
API Layer
        ↓
Core Backend Services
        ↓
AI Tutor Engine
        ↓
RAG + Prompt Builder
        ↓
Foundation LLM APIs
```

---

# 7. System Modules

# 7.1 Authentication Module

## Features
- Student login
- Parent login
- JWT authentication
- Refresh tokens
- Email/password authentication

## Future
- Social login
- School SSO

---

# 7.2 Student Profile Module

## Responsibilities
- XP tracking
- Level tracking
- Hearts
- Streaks
- Subject progress
- Weakness tracking

## Entities
- StudentProfile
- StudentStats
- StudentSkillProgress

---

# 7.3 Learning Module

## Responsibilities
- Lessons
- Topics
- Skill trees
- Learning paths
- Curriculum hierarchy

## Structure

```text
Grade
  → Subject
      → Unit
          → Lesson
              → Skill
```

---

# 7.4 Quiz Engine

## Responsibilities
- Generate quizzes
- Validate answers
- Adaptive difficulty
- Reinforcement questions

## Question Types
- MCQ
- True/False
- Matching
- Fill in blanks

---

# 7.5 Gamification Module

## Features
- XP engine
- Level engine
- Badges
- Daily missions
- Streaks
- Hearts system

## Event-Driven Flow

```text
Lesson Completed
   ↓
XP Awarded
   ↓
Badge Evaluation
   ↓
Streak Update
```

---

# 7.6 Parent Module

## Features
- Progress summaries
- Weak area reports
- Weekly reports
- Learning activity summaries

---

# 7.7 Analytics Module

## Track
- Daily active users
- Session duration
- Mission completion
- Quiz accuracy
- Retention
- Subject engagement

---

# 8. AI System Architecture

# 8.1 AI Tutor Engine

## Responsibilities
- Explain concepts
- Simplify lessons
- Generate examples
- Generate quizzes
- Encourage students

## AI Flow

```text
Student Input
     ↓
Curriculum Context Retrieval
     ↓
Student Profile Context
     ↓
Prompt Builder
     ↓
LLM
     ↓
Safety Layer
     ↓
Response Formatter
```

---

# 8.2 Learning Intelligence Layer

## Components

### Learning Path Engine
Responsible for:
- sequencing lessons
- skill dependencies
- lesson unlocking

### Adaptivity Engine
Responsible for:
- difficulty adjustment
- pacing
- remediation

### Student Modeling Engine
Responsible for:
- tracking knowledge
- skill mastery
- weak areas
- learning behavior

---

# 8.3 Subject Adapter Layer

Each subject has:
- custom prompts
- quiz strategies
- difficulty strategies
- explanation style

## Example

### Math
- step-by-step solving
- procedural reasoning
- adaptive difficulty

### Science
- visual explanations
- conceptual understanding

### Languages
- vocabulary
- grammar
- comprehension

---

# 8.4 RAG Layer

## Purpose
Ground AI responses in curriculum content.

## Sources
- Curriculum PDFs
- Lesson content
- Exercises
- Educational summaries

## Stack
- PostgreSQL
- pgvector

---

# 8.5 Prompt Builder

## Responsibilities
- inject grade level
- inject age group
- inject curriculum context
- enforce child-safe tone
- apply subject strategy

## Example Prompt

```text
Explain fractions to a 10-year-old Arabic-speaking student.
Use:
- visual examples
- simple language
- short sentences
- encouragement
```

---

# 8.6 Safety Layer

## Requirements
- Toxicity filtering
- Unsafe content blocking
- Age-appropriate responses
- Hallucination minimization

---

# 9. Technical Stack

# Frontend

## Core
- Next.js
- React
- TypeScript
- TailwindCSS
- shadcn/ui

## State Management
- Zustand
- React Query

## Animations
- Framer Motion

---

# Backend

## Core
- ASP.NET Core 9
- Clean Architecture
- Modular Monolith

## Libraries
- FluentValidation
- AutoMapper
- JWT Authentication
- Serilog

---

# Database

## Primary DB
- PostgreSQL

## Vector Search
- pgvector

## Cache
- Redis

---

# AI Layer

## Foundation Models
- GPT APIs
- Gemini APIs

## AI Strategy
- Prompt engineering first
- RAG first
- No fine-tuning initially

---

# Hosting

## MVP
- Azure
- Railway
- Render

## Future
- Kubernetes
- multi-region scaling

---

# 10. Database Design

# Core Tables

```text
Users
Roles
StudentProfiles
Subjects
Grades
Units
Lessons
Skills
SkillNodes
Questions
QuizAttempts
MissionAssignments
XPTransactions
Badges
StudentBadges
Streaks
ParentReports
StudentAnalytics
```

---

# 11. API Requirements

# Authentication APIs

- POST /auth/register
- POST /auth/login
- POST /auth/refresh

---

# Learning APIs

- GET /subjects
- GET /lessons
- GET /skills/tree
- POST /lesson/start
- POST /lesson/complete

---

# Quiz APIs

- POST /quiz/generate
- POST /quiz/submit
- GET /quiz/results

---

# AI APIs

- POST /ai/explain
- POST /ai/hint
- POST /ai/generate-question

---

# Gamification APIs

- GET /xp
- GET /badges
- GET /streaks
- GET /missions

---

# Parent APIs

- GET /parent/report
- GET /parent/weak-areas

---

# 12. Frontend Architecture

# Pages

## Student
- Login
- Home Dashboard
- Skill Tree
- Lesson Screen
- Quiz Screen
- Rewards Screen
- Profile

## Parent
- Parent Dashboard
- Reports
- Weak Areas

---

# Component Structure

```text
components/
  ui/
  learning/
  quiz/
  gamification/
  ai/
  parent/
```

---

# 13. UX Requirements

## Core UX Principles
- One primary action per screen
- Fast feedback
- Visual reinforcement
- Minimal cognitive load
- Gamified interactions

## Student Session Goal
Each session should:
- take under 10 minutes
- feel rewarding
- create progress feeling

---

# 14. Performance Requirements

## Response Time
- API response < 500ms
- AI response < 4 seconds

## Availability
- 99.5% uptime target

## Scalability
- support 100k+ users architecture-wise

---

# 15. Security Requirements

## Requirements
- JWT auth
- encrypted passwords
- HTTPS only
- role-based access
- secure API rate limiting

## Child Safety
- content moderation
- restricted prompts
- protected conversations

---

# 16. AI Requirements

## Functional Requirements
- Generate explanations
- Generate quizzes
- Generate hints
- Adapt difficulty
- Encourage students

## Non-Functional Requirements
- safe
- fast
- age-appropriate
- deterministic enough

---

# 17. Gamification Requirements

## XP Rules
- lesson completion
- quiz completion
- streak maintenance
- mission completion

## Reward System
- badges
- levels
- visual celebrations

## Hearts System
- limit mistakes
- encourage retrying

---

# 18. KPIs

# Product KPIs

## Retention
- D1 retention
- D7 retention
- WAU

## Engagement
- sessions/day
- session duration
- mission completion

## Learning Metrics
- quiz accuracy
- mastery improvement
- weak area reduction

---

# 19. Technical Execution Plan

# Phase 1 — Foundation (Weeks 1–2)

## Backend
- Project setup
- Clean architecture
- Auth module
- User system
- PostgreSQL setup

## Frontend
- Next.js setup
- UI system
- Design system
- Authentication screens

---

# Phase 2 — Learning Core (Weeks 3–4)

## Backend
- Subjects
- Lessons
- Skill trees
- Quiz engine

## Frontend
- Dashboard
- Lesson screens
- Quiz flow
- Skill tree UI

---

# Phase 3 — AI Tutor (Weeks 5–6)

## Backend
- AI Tutor Engine
- Prompt builder
- RAG setup
- Curriculum retrieval

## Frontend
- AI tutor UI
- Interactive explanations
- Hint system

---

# Phase 4 — Gamification (Week 7)

## Backend
- XP engine
- Streak engine
- Badges
- Missions

## Frontend
- XP animations
- Rewards screens
- Mission screens

---

# Phase 5 — Parent + Analytics (Week 8)

## Backend
- Parent reports
- Analytics collection

## Frontend
- Parent dashboard
- Reports UI

---

# Phase 6 — Stabilization (Week 9)

## Tasks
- Testing
- Performance optimization
- Bug fixing
- Prompt tuning

---

# 20. Team Requirements

# Minimum Team

## Product
- Product owner

## Engineering
- 1 frontend engineer
- 1 backend engineer
- 1 AI engineer

## Design
- 1 product designer

---

# 21. Go-To-Market Strategy

# Launch Market
- Egypt first

# Acquisition Channels
- TikTok
- Facebook parent groups
- Educational influencers
- Small school pilots

# Positioning

"AI-powered gamified learning companion for school students"

---

# 22. Pricing Strategy

# Free Plan
- limited missions
- limited AI tutoring

# Premium Plan
- unlimited tutoring
- advanced analytics
- personalized learning paths

---

# 23. Future Roadmap

# Phase 2
- Deep adaptivity
- Teacher dashboard
- Voice tutoring

# Phase 3
- Agentic workflows
- Curriculum intelligence
- AI-generated learning plans

# Phase 4
- School integrations
- Classroom management
- Regional expansion

---

# 24. Final Strategic Direction

Learnexia should evolve as:

"A gamified educational intelligence platform for students"

Not:
- a generic chatbot
- a simple LMS
- a content library

The core competitive advantage should be:
- engagement
- adaptive learning
- AI personalization
- behavioral design
- Arabic-first educational experience

