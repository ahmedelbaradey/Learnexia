/**
 * Learning-activity domain types (from SRS / architecture.md).
 *
 * Attempts and answers model a student's interaction with assessment items.
 * Gamification (XP, streaks, badges) and child/parent linkage live here too,
 * since they are cross-cutting domain vocabulary used by many screens.
 */

import type { Grade } from '../constants';

export type AttemptStatus = 'in-progress' | 'completed' | 'abandoned';

export interface Attempt {
  id: string;
  studentId: string;
  lessonId: string;
  status: AttemptStatus;
  /** ISO date-time string. */
  startedAt: string;
  /** ISO date-time string; null while in progress. */
  completedAt?: string | null;
  score?: number | null;
  xpEarned?: number | null;
}

export interface StudentAnswer {
  id: string;
  attemptId: string;
  questionId: string;
  /** Raw learner response; shape depends on the question type. */
  response: unknown;
  isCorrect?: boolean | null;
  /** Milliseconds spent on this answer. */
  timeSpentMs?: number | null;
}

/**
 * A child managed by a parent (parent-driven onboarding). This is the active
 * learner the parent context can switch between.
 */
export interface Child {
  id: string;
  parentId: string;
  displayName: string;
  grade: Grade;
  avatarUrl?: string | null;
}

export interface GamificationSummary {
  studentId: string;
  totalXp: number;
  level: number;
  currentStreakDays: number;
  hearts: number;
  badgeCount: number;
}

export type NotificationType =
  | 'achievement'
  | 'streak'
  | 'reminder'
  | 'system'
  | 'parent';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  body?: string | null;
  /** ISO date-time string. */
  createdAt: string;
  isRead: boolean;
  /** Optional deep-link route for the app to navigate to. */
  actionRoute?: string | null;
}
