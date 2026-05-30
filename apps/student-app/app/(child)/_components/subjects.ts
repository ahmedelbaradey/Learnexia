/**
 * subjects.ts — defensive subject resolution helpers (W13 extraction from W11).
 *
 * Moved from `(child)/index.tsx` so both the existing W11 surfaces and the new
 * SubjectsListSection can import from one canonical place.
 *
 * - `SUBJECT_NAME_MAP`: normalises API subject names (EN + AR) to SubjectKey.
 * - `resolveSubjectKey()`: case-insensitive lookup, returns null for unknown names.
 * - `filterSubjects()`: deduplicates, enforces the 4-subject product constraint,
 *   and sorts by canonical order (Math → Science → Arabic → English).
 * - Social Studies is never a valid SubjectKey → can never appear in output.
 */

import type { StudentSubjectDto } from '@learnexia/api-client';
import type { SubjectKey } from '@learnexia/ui';

/** Normalize canonical English + Arabic seeder names to SubjectKey. */
export const SUBJECT_NAME_MAP: Record<string, SubjectKey> = {
  math: 'math',
  mathematics: 'math',
  الرياضيات: 'math',
  science: 'science',
  العلوم: 'science',
  arabic: 'arabic',
  العربية: 'arabic',
  english: 'english',
  الإنجليزية: 'english',
};

const ALLOWED_KEYS: Set<SubjectKey> = new Set(['math', 'science', 'arabic', 'english']);

/** Resolve a raw API subject name to a product SubjectKey, or null if unknown. */
export function resolveSubjectKey(name: string | undefined): SubjectKey | null {
  if (!name) return null;
  const normalized = name.trim().toLowerCase();
  const key = SUBJECT_NAME_MAP[normalized];
  return key && ALLOWED_KEYS.has(key) ? key : null;
}

/** Canonical rendering order. */
const ORDER: SubjectKey[] = ['math', 'science', 'arabic', 'english'];

/**
 * Filter an API subjects array to the 4 allowed product subjects (Social Studies
 * and any unknown subjects are silently dropped), deduplicate by SubjectKey, and
 * sort to the canonical order.
 */
export function filterSubjects(
  subjects: StudentSubjectDto[],
): Array<{ dto: StudentSubjectDto; key: SubjectKey }> {
  const result: Array<{ dto: StudentSubjectDto; key: SubjectKey }> = [];
  const seen = new Set<SubjectKey>();
  for (const dto of subjects) {
    const key = resolveSubjectKey(dto.name);
    if (key && !seen.has(key)) {
      seen.add(key);
      result.push({ dto, key });
    }
  }
  result.sort((a, b) => ORDER.indexOf(a.key) - ORDER.indexOf(b.key));
  return result;
}
