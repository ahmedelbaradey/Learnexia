/**
 * Product constants — authoritative per CLAUDE.md product overrides.
 *
 * - 4 subjects only: Math, Science, Arabic, English. NO Social Studies.
 * - Grades 1–6.
 * - Roles: parent, student, admin, superadmin. NO teacher role.
 */

/* ------------------------------------------------------------------ */
/* Roles (Identity)                                                    */
/* ------------------------------------------------------------------ */

/**
 * Identity roles. `superadmin` is the platform owner; `admin` is staff;
 * `parent` and `student` are the end-user roles (parent-driven onboarding).
 * There is intentionally NO teacher role.
 */
export const ROLES = {
  SuperAdmin: 'superadmin',
  Admin: 'admin',
  Parent: 'parent',
  Student: 'student',
} as const;

export type Role = (typeof ROLES)[keyof typeof ROLES];

export const ALL_ROLES: readonly Role[] = Object.values(ROLES);

/**
 * Login persona — which kind of account the user is signing in as on the shared
 * login screen. This is a UI-only hint for the login form (e.g. autofill /
 * future routing); it does NOT enable student self-registration (parent-driven
 * onboarding stands). Fixed value set → enum-style const, never a raw literal.
 */
export const LOGIN_PERSONAS = {
  Parent: 'parent',
  Student: 'student',
} as const;

export type LoginPersona = (typeof LOGIN_PERSONAS)[keyof typeof LOGIN_PERSONAS];

export const ALL_LOGIN_PERSONAS: readonly LoginPersona[] =
  Object.values(LOGIN_PERSONAS);

/* ------------------------------------------------------------------ */
/* Grades                                                              */
/* ------------------------------------------------------------------ */

/** Supported grade levels: 1 through 6 inclusive. */
export const GRADES = [1, 2, 3, 4, 5, 6] as const;

export type Grade = (typeof GRADES)[number];

export const MIN_GRADE: Grade = 1;
export const MAX_GRADE: Grade = 6;

/* ------------------------------------------------------------------ */
/* Subjects (4 only — no Social Studies)                               */
/* ------------------------------------------------------------------ */

export const SUBJECTS = ['Math', 'Science', 'Arabic', 'English'] as const;

export type SubjectKey = (typeof SUBJECTS)[number];

/** Localizable labels keyed by locale; full strings live in i18n resources. */
export const SUBJECT_LABELS: Record<SubjectKey, { en: string; ar: string }> = {
  Math: { en: 'Math', ar: 'الرياضيات' },
  Science: { en: 'Science', ar: 'العلوم' },
  Arabic: { en: 'Arabic', ar: 'اللغة العربية' },
  English: { en: 'English', ar: 'اللغة الإنجليزية' },
};

/* ------------------------------------------------------------------ */
/* Locales                                                             */
/* ------------------------------------------------------------------ */

export const LOCALES = ['ar', 'en'] as const;

export type Locale = (typeof LOCALES)[number];

/** Arabic-first: the default app locale. */
export const DEFAULT_LOCALE: Locale = 'ar';

/** Locales that render right-to-left. */
export const RTL_LOCALES: readonly Locale[] = ['ar'];

/* ------------------------------------------------------------------ */
/* Countries — parent registration / add-child country picker.         */
/* ------------------------------------------------------------------ */

/**
 * Supported countries (ISO 3166-1 alpha-2) with bilingual display names.
 * These are data (proper nouns), not UI copy, so the localized labels live here
 * as an `as const` map rather than i18n keys — the picker resolves the label
 * for the active locale. Ordered Arabic-market-first to match the audience.
 */
export const COUNTRIES = [
  { code: 'SA', en: 'Saudi Arabia', ar: 'السعودية' },
  { code: 'EG', en: 'Egypt', ar: 'مصر' },
  { code: 'AE', en: 'United Arab Emirates', ar: 'الإمارات العربية المتحدة' },
  { code: 'KW', en: 'Kuwait', ar: 'الكويت' },
  { code: 'QA', en: 'Qatar', ar: 'قطر' },
  { code: 'BH', en: 'Bahrain', ar: 'البحرين' },
  { code: 'OM', en: 'Oman', ar: 'عُمان' },
  { code: 'JO', en: 'Jordan', ar: 'الأردن' },
  { code: 'LB', en: 'Lebanon', ar: 'لبنان' },
  { code: 'IQ', en: 'Iraq', ar: 'العراق' },
  { code: 'MA', en: 'Morocco', ar: 'المغرب' },
  { code: 'DZ', en: 'Algeria', ar: 'الجزائر' },
  { code: 'TN', en: 'Tunisia', ar: 'تونس' },
  { code: 'US', en: 'United States', ar: 'الولايات المتحدة' },
  { code: 'GB', en: 'United Kingdom', ar: 'المملكة المتحدة' },
] as const;

export type CountryCode = (typeof COUNTRIES)[number]['code'];

/* ------------------------------------------------------------------ */
/* AccountStatus — governance lifecycle state (P7-06/07/08)           */
/* ------------------------------------------------------------------ */

/**
 * Governance lifecycle status for a user account. Mirrors the backend
 * `AccountStatus` enum (Identity module, Domain/Enums) which is stored as int
 * and serialised to the wire as the numeric value (0, 1, 2).
 *
 * Defined ONCE here so P7-06 read hooks, P7-07 mutation hooks, and the
 * admin-dashboard UI all import from a single source instead of hardcoding
 * the raw integers in multiple files (decision D2 in `docs/plans/P7-admin-users-wave.md`).
 *
 * Wire values match the C# enum exactly:
 *   Active    = 0  — account in good standing; sign-in permitted.
 *   Suspended = 1  — admin-suspended; sign-in blocked, terminal refresh revoked.
 *   Deleted   = 2  — soft-deleted; terminal state, history retained, PII not yet erased.
 */
export const ACCOUNT_STATUS = {
  Active: 0,
  Suspended: 1,
  Deleted: 2,
} as const;

export type AccountStatusValue =
  (typeof ACCOUNT_STATUS)[keyof typeof ACCOUNT_STATUS];

// ── P7-01 Curriculum enum const maps ─────────────────────────────────────────
//
// Wire values mirror the backend Learning domain enums (stored + serialised as int
// via HasConversion<int>). Never compare raw ints inline — use these constants.

/**
 * SubjectCode — the 4 permitted content domains (no Social Studies).
 * Mirrors `Domain/Enums/SubjectCode.cs` in the Learning module.
 *
 * Wire value: int. Backend rejects any value outside {0,1,2,3}.
 * Pinned language rules:
 *   ARABIC  → ContentLanguage.Ar (0) only
 *   ENGLISH → ContentLanguage.En (1) only
 *   MATH / SCIENCE → both languages permitted
 */
export const SUBJECT_CODE = {
  MATH:    0,
  SCIENCE: 1,
  ARABIC:  2,
  ENGLISH: 3,
} as const;

export type SubjectCodeValue = (typeof SUBJECT_CODE)[keyof typeof SUBJECT_CODE];

/**
 * ContentLanguage — the two content/learning languages.
 * Mirrors `Domain/Enums/ContentLanguage.cs` in the Learning module.
 *
 * This is the CONTENT language (curriculum medium), distinct from the admin/
 * student UI locale (`Locale` = 'ar' | 'en').
 *
 * Wire value: int. Units inherit language from their owning Subject — no
 * `language` column below the Subject level.
 */
export const CONTENT_LANGUAGE = {
  Ar: 0,
  En: 1,
} as const;

export type ContentLanguageValue = (typeof CONTENT_LANGUAGE)[keyof typeof CONTENT_LANGUAGE];

// ── P7-05 Lifecycle enum const maps ──────────────────────────────────────────
//
// Appended after P7-01's SUBJECT_CODE/CONTENT_LANGUAGE block (disjoint region).
// Wire values mirror the backend Learning domain enums. NOT 0-based.

/**
 * LifecycleState — curriculum publish lifecycle axis (P7-05).
 * Mirrors `Learning.Domain.Enums.LifecycleState`. NOT 0-based.
 *
 * This is the Draft/Published/Archived axis, entirely separate from the
 * IsActive boolean axis owned by P7-01..04. Never conflate the two.
 *
 * Wire values:
 *   Draft     = 1  — content not yet ready for students.
 *   Published = 2  — content live and visible to students.
 *   Archived  = 3  — content removed from active curriculum; reversible.
 */
export const LIFECYCLE_STATE = {
  Draft:     1,
  Published: 2,
  Archived:  3,
} as const;

export type LifecycleStateValue =
  (typeof LIFECYCLE_STATE)[keyof typeof LIFECYCLE_STATE];

/**
 * VersionedEntityType — curriculum entity type discriminator (P7-05).
 * Mirrors `Learning.Domain.Enums.VersionedEntityType`. NOT 0-based.
 *
 * Used in every ContentLifecycle endpoint as `entityType: number`.
 * Route slug mapping: subject=1, unit=2, lesson=3, quiz=4.
 */
export const VERSIONED_ENTITY_TYPE = {
  Subject:      1,
  Unit:         2,
  Lesson:       3,
  QuizQuestion: 4,
} as const;

export type VersionedEntityTypeValue =
  (typeof VERSIONED_ENTITY_TYPE)[keyof typeof VERSIONED_ENTITY_TYPE];

// ── P7-02 Lesson / ContentBlock enum const maps ───────────────────────────────
//
// Appended after P7-05's LIFECYCLE_STATE/VERSIONED_ENTITY_TYPE block (disjoint).
// Wire values mirror the backend Learning domain enums. NOT 0-based.

/**
 * DifficultyLevel — lesson and question difficulty axis (P7-02; also imported by P7-04).
 * Mirrors `Learning.Domain.Enums.DifficultyLevel`. NOT 0-based.
 *
 * Wire values:
 *   Easy   = 1
 *   Medium = 2
 *   Hard   = 3
 */
export const DIFFICULTY_LEVEL = {
  Easy:   1,
  Medium: 2,
  Hard:   3,
} as const;

export type DifficultyLevelValue = (typeof DIFFICULTY_LEVEL)[keyof typeof DIFFICULTY_LEVEL];

/**
 * ContentBlockType — block payload discriminant (P7-02).
 * Mirrors `Learning.Domain.Enums.ContentBlockType`. NOT 0-based.
 *
 * Wire values:
 *   Text    = 1  — { markdown: string }
 *   Image   = 2  — { url: string, altText?: string }
 *   Video   = 3  — { url: string, caption?: string }
 *   Callout = 4  — { variant: "info"|"warning"|"tip", markdown: string }
 */
export const CONTENT_BLOCK_TYPE = {
  Text:    1,
  Image:   2,
  Video:   3,
  Callout: 4,
} as const;

export type ContentBlockTypeValue = (typeof CONTENT_BLOCK_TYPE)[keyof typeof CONTENT_BLOCK_TYPE];

// ── P7-04 Question enum const maps ─────────────────────────────────────────────
//
// Disjoint append after P7-02's DIFFICULTY_LEVEL/CONTENT_BLOCK_TYPE block.
// Wire values mirror the backend Learning domain enums. NOT 0-based.

/**
 * QuestionType — the four question types in the Learning domain (P7-04).
 * Mirrors `Learning.Domain.Enums.QuestionType`. NOT 0-based.
 *
 * Wire values:
 *   MCQ         = 1  — multiple choice
 *   TrueFalse   = 2  — true/false
 *   Matching    = 3  — matching pairs
 *   FillInBlank = 4  — fill in the blank
 */
export const QUESTION_TYPE = {
  MCQ:         1,
  TrueFalse:   2,
  Matching:    3,
  FillInBlank: 4,
} as const;

export type QuestionTypeValue = (typeof QUESTION_TYPE)[keyof typeof QUESTION_TYPE];

/**
 * GeneratedBy — content origin discriminant (P7-04).
 * Mirrors `Learning.Domain.Enums.GeneratedBy`. NOT 0-based.
 *
 * Wire values:
 *   Curated = 1  — human-authored via the admin dashboard
 *   AI      = 2  — generated by the AI pipeline
 *
 * Admin-authored questions MUST always send Curated (=1).
 */
export const GENERATED_BY = {
  Curated: 1,
  AI:      2,
} as const;

export type GeneratedByValue = (typeof GENERATED_BY)[keyof typeof GENERATED_BY];

// ── P7-03 Knowledge Graph enum const maps ─────────────────────────────────────
//
// Disjoint append after P7-04's QUESTION_TYPE/GENERATED_BY block.
// Wire values mirror the backend Learning domain enums. 0-based.

/**
 * NodeType — KnowledgeNode.NodeType discriminant (P7-03).
 * Mirrors `Learning.Domain.Enums.NodeType`. 0-based.
 *
 * Wire values:
 *   Skill   = 0  — wraps a Skill entity (skillId is non-null)
 *   Concept = 1  — wraps a Concept entity
 *   Review  = 2  — review/checkpoint node
 */
export const NODE_TYPE = {
  Skill:   0,
  Concept: 1,
  Review:  2,
} as const;

export type NodeTypeValue = (typeof NODE_TYPE)[keyof typeof NODE_TYPE];

/**
 * RelationshipType — KnowledgeEdge.RelationshipType discriminant (P7-03).
 * Mirrors `Learning.Domain.Enums.RelationshipType`. 0-based.
 *
 * Wire values:
 *   Prerequisite = 0  — A must be mastered before B (cycle guard applies)
 *   Related      = 1  — A and B are related (no cycle guard)
 *
 * v1 hard-codes Prerequisite (0). Related is supported by the backend but
 * the admin UI does not expose it yet (plan D-OQ6 deferred).
 */
export const RELATIONSHIP_TYPE = {
  Prerequisite: 0,
  Related:      1,
} as const;

export type RelationshipTypeValue = (typeof RELATIONSHIP_TYPE)[keyof typeof RELATIONSHIP_TYPE];
