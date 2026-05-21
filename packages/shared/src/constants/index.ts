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
