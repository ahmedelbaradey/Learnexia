# Learnexia — Localization Architecture (learning language vs UI language)

> **Audience:** backend + frontend engineers and the product lead.
> **Scope:** how Learnexia handles language across three independent axes, and specifically how a
> student's **learning language** (medium of instruction) selects curriculum content — including the
> Arabic/English subject edge case.
> **Status:** design of record for **Phase 8 — Localization**. Supersedes the earlier "translate every
> curriculum row" idea (paired ar/en columns) for curriculum content.
> **Sources:** [../../CLAUDE.md](../../CLAUDE.md), [backend-architecture.md](backend-architecture.md),
> [../dev/CONVENTIONS.md](../dev/CONVENTIONS.md), the Learning entities + `LearningSeeder`.

---

## 1. Three independent language axes

These must not be conflated. Each is decided separately.

![localization-architecture diagram 1](diagrams/localization-architecture-1.svg)

<details>
<summary>Mermaid source — diagram 1</summary>

```mermaid
flowchart TB
    subgraph axes["Three independent language axes"]
        ui["A. App / UI language<br/>buttons, labels, chrome, gamification text, notifications<br/>source: User.PreferredLanguage + FE i18n"]
        learn["B. Learning language (NEW)<br/>medium of instruction for Math & Science<br/>source: User.LearningLanguage (ar/en)"]
        subj["C. Subject content language<br/>the actual language of a subject's content<br/>DERIVED per subject (see resolution)"]
    end
    learn --> subj
    note["Independent: a child may read the UI in one language<br/>while learning Math in the other."]
    ui -.-> note
    learn -.-> note
```

</details>

- **A. App/UI language** — existing `User.PreferredLanguage` (default `ar-EG`) + `IStringLocalizer`
  backend + react-i18next frontend. **Unchanged by this phase.**
- **B. Learning language** — **new** `User.LearningLanguage` (`ar` | `en`). Set by the **parent at
  onboarding**, immutable by the student, changeable **only by the parent** (rare, fresh-start —
  see §5).
- **C. Subject content language** — never stored on the student; **derived** from the subject + the
  student's learning language (§2).

---

## 2. The resolution rule (handles the Arabic/English edge case)

Each `Subject` carries a stable `SubjectCode`. Content language is resolved per subject:

![localization-architecture diagram 2](diagrams/localization-architecture-2.svg)

<details>
<summary>Mermaid source — diagram 2</summary>

```mermaid
flowchart TD
    start["Resolve content language for (subject, student)"] --> code{"subject.SubjectCode"}
    code -->|"ARABIC"| ar["ar (pinned — language-learning subject)"]
    code -->|"ENGLISH"| en["en (pinned — language-learning subject)"]
    code -->|"MATH"| follow["= student.LearningLanguage"]
    code -->|"SCIENCE"| follow
    follow --> pick["pick the Subject tree whose<br/>Language == resolved language"]
    ar --> pick
    en --> pick
    pick --> serve["serve that tree (Units/Lessons/Concepts/Skills/Quizzes)"]
```

</details>

So for the **same grade**:

| Subject | Arabic-medium student sees | English-medium student sees |
|---|---|---|
| Math | Math **(ar)** | Math **(en)** |
| Science | Science **(ar)** | Science **(en)** |
| Arabic | Arabic **(ar)** | Arabic **(ar)** |
| English | English **(en)** | English **(en)** |

**Both school types take both language subjects.** Arabic and English subjects are identical for both
students — their language comes from the subject, not the student. Fallback if a resolved tree is
missing: fall back to the other language tree and log (should not happen once both are seeded).

---

## 3. Data model

No per-row translation columns. Curriculum content lives as **parallel trees rooted at a
language-specific `Subject`**; language is carried only on `Subject` and inherited by everything below
it.

![localization-architecture diagram 3](diagrams/localization-architecture-3.svg)

<details>
<summary>Mermaid source — diagram 3</summary>

```mermaid
erDiagram
    User ||--o{ Subject : "LearningLanguage selects medium subjects"
    Grades ||--o{ Subject : groups
    Subject ||--o{ Unit : contains
    Unit ||--o{ Lesson : contains
    Subject ||--o{ Concept : contains
    Concept ||--o{ Skill : contains

    User {
        int Id PK
        string PreferredLanguage "UI language (existing)"
        string LearningLanguage "NEW: ar | en (medium of instruction)"
    }
    Subject {
        int Id PK
        int GradeId FK
        int SubjectCode "NEW: MATH/SCIENCE/ARABIC/ENGLISH"
        int Language "NEW: ar | en (tree language)"
        string Name
    }
    Unit {
        int Id PK
        int SubjectId FK "language inherited from Subject"
    }
    Lesson {
        int Id PK
        int UnitId FK
    }
    Concept {
        int Id PK
        int SubjectId FK
    }
    Skill {
        int Id PK
        int ConceptId FK
    }
```

</details>

Per grade the seeder authors **6 Subject roots** (instead of 4): `MATH/ar`, `MATH/en`, `SCIENCE/ar`,
`SCIENCE/en`, `ARABIC/ar`, `ENGLISH/en`.

> `User.LearningLanguage` lives in the **Identity** schema; the Learning module never FKs to it — it
> reads the value from a **JWT claim** (`learning_language`) on the authenticated student, consistent
> with how Learning already reads the student id from the token.

---

## 4. Read path — serving the right tree

![localization-architecture diagram 4](diagrams/localization-architecture-4.svg)

<details>
<summary>Mermaid source — diagram 4</summary>

```mermaid
sequenceDiagram
    actor Student
    participant Ctrl as Learning SubjectsController
    participant Hand as GetSubjectsForGradeQueryHandler
    participant Res as Language resolver
    participant Db as Learning DbContext

    Student->>Ctrl: GET subjects for my grade (JWT carries grade + learning_language)
    Ctrl->>Hand: Send(query)
    Hand->>Res: for each SubjectCode, resolve effective language
    Note over Res: ARABIC->ar, ENGLISH->en,<br/>MATH/SCIENCE->learning_language
    Res-->>Hand: { MATH:lang, SCIENCE:lang, ARABIC:ar, ENGLISH:en }
    Hand->>Db: WHERE GradeId = g AND (SubjectCode, Language) IN resolved set
    Db-->>Hand: 4 subject trees (one per code, correct language)
    Hand-->>Student: BaseResponse with the student's curriculum
```

</details>

The same resolution applies to skill-tree, lessons-in-unit, quiz, and dashboard queries — anywhere a
subject's content is read for a student.

---

## 5. Changing the learning language (parent-only, fresh start)

The learning language is **immutable by the student** and changeable **only by the parent**, with an
explicit fresh-start warning. It happens rarely, normally only at the **start of a school year**.
Switching changes which Math/Science trees the child sees, so their **Math/Science progress no longer
maps and is reset**; Arabic/English subjects are unaffected.

![localization-architecture diagram 5](diagrams/localization-architecture-5.svg)

<details>
<summary>Mermaid source — diagram 5</summary>

```mermaid
sequenceDiagram
    actor Parent
    participant PCtrl as Parent / Identity controller
    participant Hand as ChangeLearningLanguageCommandHandler
    participant Id as Identity (User)
    participant Bus as Integration event
    participant Lrn as Learning consumer

    Parent->>PCtrl: change child learning language (confirmed warning)
    Note over PCtrl: requires explicit confirm flag<br/>(FRESH START acknowledged)
    PCtrl->>Hand: Send(ChangeLearningLanguageCommand)
    Hand->>Id: update User.LearningLanguage (family-scope authz)
    Hand->>Bus: publish LearningLanguageChangedIntegrationEvent (post-commit)
    Bus->>Lrn: reset Math/Science attempts + mastery for this student
    Note over Lrn: Arabic/English progress untouched.<br/>Next sign-in JWT carries the new learning_language.
```

</details>

> **Module isolation:** the change happens in Identity (the `User` field) and fans out to Learning via
> a `Shared.Contracts` integration event — no cross-module FK, dispatched after commit per
> [../dev/adr/0002-domain-events-and-dispatch.md](../dev/adr/0002-domain-events-and-dispatch.md).

---

## 6. Locked product decisions

1. `LearningLanguage` is a **separate field** from `PreferredLanguage` (medium ≠ UI language).
2. **Student cannot change** it; **parent only**, behind an explicit fresh-start warning; rare,
   start-of-year.
3. **Both school types take both language subjects** (Arabic-medium students still study English, and
   vice-versa).
4. Curriculum content uses **parallel language trees keyed by `Subject.Language`**, not per-row
   translations.

## 7. Open / to-confirm during build

- **Fresh-start scope:** confirm reset covers Math/Science **Learning** attempts + mastery only;
  global gamification (XP/streak/badges) is engagement and is **retained**.
- **Onboarding default:** default the UI `PreferredLanguage` to match the chosen `LearningLanguage`,
  but keep them editable independently.
- Whether `SubjectCode` should also drive ordering/iconography (likely yes, FE concern).

---

## Related documents

- [backend-architecture.md](backend-architecture.md) — modules, schemas, integration events
- [technical-architecture.md](technical-architecture.md) — pipeline, domain-event dispatch, isolation
- [business-architecture.md](business-architecture.md) — capabilities, value streams
- Phase 8 stories/tasks: `user-stories/Phase-8-Localization/`, `tasks/Backend/Phase-8-Localization/`
