'use client';

/**
 * LessonForm — create/edit modal dialog for lessons.
 * Design Spec §S5 — P7-02.
 *
 * Mirrors UnitForm.tsx (same focus-trap pattern, same modal geometry, same motion).
 * Language is read-only (inherited from owning Subject — never a picker here).
 * sequenceOrder is managed by the Reorder endpoint — not exposed as a free field.
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import type { FormEvent } from 'react';
import {
  useCreateLesson,
  useEditLesson,
  type AdminSingleLessonResponse,
  type AddLessonDto,
  type EditLessonDto,
} from '@learnexia/api-client';
import { DIFFICULTY_LEVEL, type DifficultyLevelValue, type ContentLanguageValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';
import { InheritedLanguageBadge } from './InheritedLanguageBadge';

const strings = getStrings(ADMIN_LOCALE);

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

function BookPlusIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
      <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
      <line x1="12" y1="7" x2="12" y2="13" /><line x1="9" y1="10" x2="15" y2="10" />
    </svg>
  );
}
function PencilIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  );
}
function InfoIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="#4F46E5" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  );
}

interface FormErrors {
  name?: string;
  difficulty?: string;
  estimatedMinutes?: string;
}

export interface LessonFormProps {
  open: boolean;
  onClose: () => void;
  unitId: number;
  /** Owning Subject's content language (inherited — shown as read-only). */
  language: ContentLanguageValue;
  /** Current lesson count — used to compute initial sequenceOrder on create. */
  lessonCount?: number;
  /** If provided, opens in edit mode. */
  editLesson?: AdminSingleLessonResponse;
}

const inputStyle: React.CSSProperties = {
  height: 44, backgroundColor: 'var(--lx-bg)', borderRadius: 8,
  border: '1px solid var(--lx-border)', paddingLeft: 12, paddingRight: 12,
  fontSize: 14, color: 'var(--lx-fg1)', fontFamily: 'inherit',
  width: '100%', boxSizing: 'border-box', outline: 'none',
};

export function LessonForm({ open, onClose, unitId, language, lessonCount = 0, editLesson }: LessonFormProps) {
  const isEdit = !!editLesson;
  const dialogRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<Element | null>(null);

  useEffect(() => {
    if (open) triggerRef.current = document.activeElement;
  }, [open]);

  useEffect(() => {
    if (!open || !dialogRef.current) return;
    const container = dialogRef.current;
    const focusables = getFocusableElements(container);
    focusables[0]?.focus();

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.preventDefault(); onClose(); }
      if (e.key === 'Tab') {
        const foc = getFocusableElements(container);
        if (!foc.length) return;
        const first = foc[0]!;
        const last = foc[foc.length - 1]!;
        if (e.shiftKey) { if (document.activeElement === first) { e.preventDefault(); last.focus(); } }
        else { if (document.activeElement === last) { e.preventDefault(); first.focus(); } }
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  useEffect(() => {
    if (!open && triggerRef.current instanceof HTMLElement) triggerRef.current.focus();
  }, [open]);

  const [name, setName] = useState('');
  const [difficulty, setDifficulty] = useState<DifficultyLevelValue | ''>('');
  const [estimatedMinutes, setEstimatedMinutes] = useState<number | ''>(0);
  const [isLocked, setIsLocked] = useState(false);
  const [isActive, setIsActive] = useState(true);
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setName(editLesson?.name ?? '');
      setDifficulty(editLesson?.difficulty ?? '');
      setEstimatedMinutes(editLesson?.estimatedMinutes ?? 0);
      setIsLocked(editLesson?.isLocked ?? false);
      setIsActive(editLesson?.isActive ?? true);
      setErrors({});
      setServerError(null);
    }
  }, [open]); // intentional: reset form state only on open/close toggle

  const createMutation = useCreateLesson();
  const updateMutation = useEditLesson();
  const isPending = createMutation.isPending || updateMutation.isPending;

  const validate = useCallback((): FormErrors => {
    const errs: FormErrors = {};
    if (!name.trim()) errs.name = strings.lessonFormErrNameRequired;
    if (difficulty === '' || !Object.values(DIFFICULTY_LEVEL).includes(Number(difficulty) as DifficultyLevelValue)) errs.difficulty = strings.lessonFormErrDifficultyRequired;
    if (estimatedMinutes === '' || Number(estimatedMinutes) < 0 || !Number.isInteger(Number(estimatedMinutes))) {
      errs.estimatedMinutes = strings.lessonFormErrMinutesInvalid;
    }
    return errs;
  }, [name, difficulty, estimatedMinutes]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});
    try {
      if (isEdit && editLesson) {
        const body: EditLessonDto = {
          id: editLesson.id,
          name: name.trim(),
          difficulty: difficulty as DifficultyLevelValue,
          sequenceOrder: editLesson.sequenceOrder,
          isLocked,
          unitId,
          estimatedMinutes: Number(estimatedMinutes),
        };
        await updateMutation.mutateAsync(body);
      } else {
        const body: AddLessonDto = {
          name: name.trim(),
          difficulty: difficulty as DifficultyLevelValue,
          sequenceOrder: lessonCount + 1,
          isLocked,
          unitId,
          estimatedMinutes: Number(estimatedMinutes),
        };
        await createMutation.mutateAsync(body);
      }
      onClose();
    } catch (err) {
      setServerError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  if (!open) return null;

  const titleId = 'lesson-form-title';

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      data-testid="lesson-form-dialog"
      style={{
        position: 'fixed', inset: 0, backgroundColor: 'rgba(15,23,42,0.72)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 500,
      }}
    >
      <div
        ref={dialogRef}
        style={{
          backgroundColor: 'var(--lx-card)', borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: 'var(--lx-shadow-popup)',
          padding: 32, width: 'calc(100vw - 64px)', maxWidth: 540,
          display: 'flex', flexDirection: 'column', gap: 24,
          maxHeight: '90vh', overflowY: 'auto',
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 16 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 9999,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            backgroundColor: isEdit ? 'rgba(245,158,11,0.15)' : 'rgba(79,70,229,0.15)',
            color: isEdit ? '#F59E0B' : '#4F46E5',
          }}>
            {isEdit ? <PencilIcon /> : <BookPlusIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', lineHeight: 1.3 }}>
              {isEdit ? strings.lessonFormEditTitle : strings.lessonFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', lineHeight: 1.5, marginTop: 4 }}>
              {isEdit ? strings.lessonFormEditSubtitle : strings.lessonFormCreateSubtitle}
            </p>
          </div>
        </div>

        {/* Inherited language notice */}
        <div style={{
          padding: '10px 12px', backgroundColor: 'rgba(79,70,229,0.08)', borderRadius: 8,
          border: '1px solid rgba(79,70,229,0.15)',
          display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'center', flexWrap: 'wrap',
        }}>
          <InfoIcon />
          <span style={{ fontSize: 13, color: 'var(--lx-fg3)', lineHeight: 1.5 }}>
            {strings.lessonFormInheritedLangPrefix}
          </span>
          <InheritedLanguageBadge language={language} compact />
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* Name */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="lesson-name" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.lessonFormNameLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="lesson-name" type="text"
              value={name} onChange={(e) => setName(e.target.value)}
              placeholder={strings.lessonFormNamePlaceholder}
              data-testid="lesson-form-name"
              dir="auto"
              style={{ ...inputStyle, borderColor: errors.name ? '#EF4444' : undefined }}
              aria-invalid={!!errors.name}
            />
            {errors.name && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.name}</span>
            )}
          </div>

          {/* Difficulty */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="lesson-difficulty" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.lessonFormDifficultyLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="lesson-difficulty"
              value={difficulty}
              onChange={(e) => setDifficulty(e.target.value === '' ? '' : Number(e.target.value) as DifficultyLevelValue)}
              data-testid="lesson-form-difficulty"
              style={{ ...inputStyle, borderColor: errors.difficulty ? '#EF4444' : undefined, cursor: 'pointer' }}
              aria-invalid={!!errors.difficulty}
            >
              <option value="">{strings.lessonFormDifficultyPlaceholder}</option>
              <option value={DIFFICULTY_LEVEL.Easy}>{strings.lessonDifficultyEasy}</option>
              <option value={DIFFICULTY_LEVEL.Medium}>{strings.lessonDifficultyMedium}</option>
              <option value={DIFFICULTY_LEVEL.Hard}>{strings.lessonDifficultyHard}</option>
            </select>
            {errors.difficulty && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.difficulty}</span>
            )}
          </div>

          {/* Estimated minutes */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="lesson-minutes" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.lessonFormMinutesLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="lesson-minutes" type="number" min={0} step={1}
              value={estimatedMinutes}
              onChange={(e) => setEstimatedMinutes(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              placeholder="0"
              data-testid="lesson-form-minutes"
              dir="ltr"
              style={{ ...inputStyle, borderColor: errors.estimatedMinutes ? '#EF4444' : undefined }}
              aria-invalid={!!errors.estimatedMinutes}
            />
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>{strings.lessonFormMinutesHint}</span>
            {errors.estimatedMinutes && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.estimatedMinutes}</span>
            )}
          </div>

          {/* Locked toggle */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <label htmlFor="lesson-locked" style={{ fontSize: 14, color: 'var(--lx-fg2)' }}>
                {strings.lessonFormLockedLabel}
              </label>
              <button
                type="button" role="switch" id="lesson-locked"
                aria-checked={isLocked} data-testid="lesson-form-locked"
                onClick={() => setIsLocked(!isLocked)}
                style={{
                  width: 40, height: 24, borderRadius: 9999, border: 'none',
                  cursor: 'pointer', position: 'relative',
                  backgroundColor: isLocked ? '#A855F7' : 'var(--lx-card-soft)',
                  transition: 'background-color 200ms var(--lx-ease-out)', outline: 'none',
                }}
                onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
              >
                <span style={{
                  position: 'absolute', width: 18, height: 18, borderRadius: '50%',
                  backgroundColor: '#F8FAFC', top: 3,
                  left: isLocked ? 18 : 2,
                  transition: 'left 200ms var(--lx-ease-out)',
                }} />
              </button>
            </div>
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>
              {isLocked ? strings.lessonFormLockedOnHint : strings.lessonFormLockedOffHint}
            </span>
          </div>

          {/* Active toggle */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <label htmlFor="lesson-active" style={{ fontSize: 14, color: 'var(--lx-fg2)' }}>
                {strings.lessonFormActiveLabel}
              </label>
              <button
                type="button" role="switch" id="lesson-active"
                aria-checked={isActive} data-testid="lesson-form-active"
                onClick={() => setIsActive(!isActive)}
                style={{
                  width: 40, height: 24, borderRadius: 9999, border: 'none',
                  cursor: 'pointer', position: 'relative',
                  backgroundColor: isActive ? '#22C55E' : 'var(--lx-card-soft)',
                  transition: 'background-color 200ms var(--lx-ease-out)', outline: 'none',
                }}
                onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
              >
                <span style={{
                  position: 'absolute', width: 18, height: 18, borderRadius: '50%',
                  backgroundColor: '#F8FAFC', top: 3,
                  left: isActive ? 18 : 2,
                  transition: 'left 200ms var(--lx-ease-out)',
                }} />
              </button>
            </div>
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>
              {isActive ? strings.lessonFormActiveOnHint : strings.lessonFormActiveOffHint}
            </span>
          </div>

          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button type="button" onClick={onClose} data-testid="lesson-form-cancel"
              disabled={isPending}
              style={{
                height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}>
              {strings.lessonFormCancelBtn}
            </button>
            <button type="submit" data-testid="lesson-form-save"
              aria-busy={isPending} disabled={isPending}
              style={{
                height: 40, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer', fontFamily: 'inherit',
                opacity: isPending ? 0.7 : 1,
              }}>
              {isPending ? '…' : isEdit ? strings.lessonFormSaveBtn : strings.lessonFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
