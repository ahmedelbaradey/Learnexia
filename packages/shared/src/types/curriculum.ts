/**
 * Curriculum domain types (from SRS / architecture.md).
 *
 * Core learning hierarchy: Subject → Unit → Lesson → Concept → Skill.
 * Kept to a sensible core; the backend curriculum module is the source of
 * truth and these will be reconciled with generated types once those
 * endpoints exist. IDs are typed as strings to stay agnostic to the
 * backend's int/guid choice per module.
 */

import type { Grade, SubjectKey } from '../constants';

export interface Subject {
  id: string;
  key: SubjectKey;
  name: string;
  /** Subjects are scoped to grade levels in the curriculum. */
  grade: Grade;
  iconUrl?: string | null;
  orderIndex?: number;
}

export interface Unit {
  id: string;
  subjectId: string;
  title: string;
  description?: string | null;
  orderIndex: number;
}

export interface Lesson {
  id: string;
  unitId: string;
  title: string;
  description?: string | null;
  orderIndex: number;
  /** Estimated minutes to complete. */
  estimatedMinutes?: number | null;
}

export interface Concept {
  id: string;
  lessonId: string;
  title: string;
  orderIndex: number;
}

export interface Skill {
  id: string;
  conceptId: string;
  title: string;
  /** Adaptive mastery 0..1 for the active learner, when available. */
  mastery?: number | null;
}
