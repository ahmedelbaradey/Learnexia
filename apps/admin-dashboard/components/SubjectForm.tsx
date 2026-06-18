'use client';

/**
 * SubjectForm — create/edit modal dialog for subjects (Design Spec §3.2).
 *
 * Pinned-language rule:
 *   ARABIC (2)  → language locked to Ar (0)
 *   ENGLISH (3) → language locked to En (1)
 *   MATH / SCIENCE → free choice
 *
 * No optimistic updates; on success invalidates via the hooks.
 * Reuses AdminConfirmDialog-style modal shell (same tokens/motion/focus-trap).
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import type { FormEvent } from 'react';
import {
  useCreateSubject,
  useUpdateSubject,
  useAdminGrades,
  type SubjectDto,
  type AddSubjectDto,
  type EditSubjectDto,
} from '@learnexia/api-client';
import { SUBJECT_CODE, CONTENT_LANGUAGE, type SubjectCodeValue, type ContentLanguageValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';

const strings = getStrings(ADMIN_LOCALE);

// ── Focus trap helper (mirrors AdminConfirmDialog) ────────────────────────────

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────

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

// ── Pinned language rule ───────────────────────────────────────────────────────

function getPinnedLanguage(code: SubjectCodeValue | ''): ContentLanguageValue | null {
  if (code === SUBJECT_CODE.ARABIC) return CONTENT_LANGUAGE.Ar;
  if (code === SUBJECT_CODE.ENGLISH) return CONTENT_LANGUAGE.En;
  return null;
}

// ── Form errors type ──────────────────────────────────────────────────────────

interface FormErrors {
  name?: string;
  gradeId?: string;
  subjectCode?: string;
  language?: string;
  sequenceOrder?: string;
}

// ── Props ──────────────────────────────────────────────────────────────────────

export interface SubjectFormProps {
  open: boolean;
  onClose: () => void;
  /** If provided, opens the form in edit mode pre-filled with this subject. */
  editSubject?: SubjectDto;
  /** Optional grade pre-fill (used by coverage slot "Create" shortcut). */
  prefillGradeId?: number;
  /** Optional SubjectCode pre-fill (used by coverage slot "Create" shortcut). */
  prefillSubjectCode?: SubjectCodeValue;
  /** Optional language pre-fill (used by coverage slot "Create" shortcut). */
  prefillLanguage?: ContentLanguageValue;
}

export function SubjectForm({
  open,
  onClose,
  editSubject,
  prefillGradeId,
  prefillSubjectCode,
  prefillLanguage,
}: SubjectFormProps) {
  const isEdit = !!editSubject;
  const dialogRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<Element | null>(null);

  // Track trigger so focus can be returned on close
  useEffect(() => {
    if (open) triggerRef.current = document.activeElement;
  }, [open]);

  // Focus trap + ESC handler
  useEffect(() => {
    if (!open || !dialogRef.current) return;
    const container = dialogRef.current;
    const focusables = getFocusableElements(container);
    focusables[0]?.focus();

    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
      }
      if (e.key === 'Tab') {
        const foc = getFocusableElements(container);
        if (!foc.length) return;
        const first = foc[0]!;
        const last = foc[foc.length - 1]!;
        if (e.shiftKey) {
          if (document.activeElement === first) { e.preventDefault(); last.focus(); }
        } else {
          if (document.activeElement === last) { e.preventDefault(); first.focus(); }
        }
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [open, onClose]);

  // Return focus on close
  useEffect(() => {
    if (!open && triggerRef.current instanceof HTMLElement) {
      triggerRef.current.focus();
    }
  }, [open]);

  // Form state
  const initialCode = (isEdit ? editSubject?.subjectCode : prefillSubjectCode) ?? '';
  const initialLang = (isEdit ? editSubject?.language : prefillLanguage) ?? '';

  const [name, setName] = useState(isEdit ? (editSubject?.name ?? '') : '');
  const [gradeId, setGradeId] = useState<number | ''>(
    isEdit ? (editSubject?.gradeId ?? '') : (prefillGradeId ?? ''),
  );
  const [subjectCode, setSubjectCode] = useState<SubjectCodeValue | ''>(initialCode as SubjectCodeValue | '');
  const [language, setLanguage] = useState<ContentLanguageValue | ''>(initialLang as ContentLanguageValue | '');
  const [sequenceOrder, setSequenceOrder] = useState<number | ''>(
    isEdit ? (editSubject?.sequenceOrder ?? 0) : 0,
  );
  const [isActive, setIsActive] = useState(isEdit ? (editSubject?.isActive ?? true) : true);
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  // Reset on open
  useEffect(() => {
    if (open) {
      const code = (isEdit ? editSubject?.subjectCode : prefillSubjectCode) ?? '';
      const lang = (isEdit ? editSubject?.language : prefillLanguage) ?? '';
      setName(isEdit ? (editSubject?.name ?? '') : '');
      setGradeId(isEdit ? (editSubject?.gradeId ?? '') : (prefillGradeId ?? ''));
      setSubjectCode(code as SubjectCodeValue | '');
      setLanguage(lang as ContentLanguageValue | '');
      setSequenceOrder(isEdit ? (editSubject?.sequenceOrder ?? 0) : 0);
      setIsActive(isEdit ? (editSubject?.isActive ?? true) : true);
      setErrors({});
      setServerError(null);
    }
  }, [open]); // intentional: only re-run when dialog opens/closes

  // Apply pinned-language rule when subjectCode changes
  useEffect(() => {
    const pinned = getPinnedLanguage(subjectCode);
    if (pinned !== null) setLanguage(pinned);
  }, [subjectCode]);

  const { data: gradesData } = useAdminGrades();
  const grades = gradesData?.data ?? [];

  const createMutation = useCreateSubject();
  const updateMutation = useUpdateSubject();
  const isPending = createMutation.isPending || updateMutation.isPending;

  const validate = useCallback((): FormErrors => {
    const errs: FormErrors = {};
    if (!name.trim()) errs.name = strings.subjectFormErrNameRequired;
    if (gradeId === '' || gradeId === 0) errs.gradeId = strings.subjectFormErrGradeRequired;
    if (subjectCode === '') errs.subjectCode = strings.subjectFormErrCodeRequired;
    if (language === '') errs.language = strings.subjectFormErrLangRequired;
    if (sequenceOrder === '' || sequenceOrder < 0 || !Number.isInteger(sequenceOrder)) {
      errs.sequenceOrder = strings.subjectFormErrOrderInvalid;
    }
    return errs;
  }, [name, gradeId, subjectCode, language, sequenceOrder]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) {
      setErrors(errs);
      return;
    }
    setErrors({});
    try {
      if (isEdit && editSubject) {
        const body: EditSubjectDto = {
          id: editSubject.id,
          name: name.trim(),
          gradeId: gradeId as number,
          subjectCode: subjectCode as SubjectCodeValue,
          language: language as ContentLanguageValue,
          sequenceOrder: sequenceOrder as number,
          isActive,
        };
        await updateMutation.mutateAsync(body);
      } else {
        const body: AddSubjectDto = {
          name: name.trim(),
          gradeId: gradeId as number,
          subjectCode: subjectCode as SubjectCodeValue,
          language: language as ContentLanguageValue,
          sequenceOrder: sequenceOrder as number,
          isActive,
        };
        await createMutation.mutateAsync(body);
      }
      onClose();
    } catch (err) {
      const e = err as Error;
      setServerError(e.message || strings.curriculumNetworkError);
    }
  };

  if (!open) return null;

  const titleId = 'subject-form-title';
  const isLanguagePinned = getPinnedLanguage(subjectCode) !== null;

  const inputStyle: React.CSSProperties = {
    height: 44,
    backgroundColor: 'var(--lx-bg)',
    borderRadius: 8,
    border: '1px solid var(--lx-border)',
    paddingLeft: 12,
    paddingRight: 12,
    fontSize: 14,
    color: 'var(--lx-fg1)',
    fontFamily: 'inherit',
    width: '100%',
    boxSizing: 'border-box',
    outline: 'none',
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby={titleId}
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(15,23,42,0.72)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 500,
      }}
    >
      <div
        ref={dialogRef}
        style={{
          backgroundColor: 'var(--lx-card)',
          borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: 'var(--lx-shadow-popup)',
          padding: 32,
          width: 'calc(100vw - 64px)',
          maxWidth: 520,
          display: 'flex',
          flexDirection: 'column',
          gap: 24,
        }}
      >
        {/* Dialog header */}
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
            <h2 id={titleId} style={{
              margin: 0, fontSize: 18, fontWeight: 700,
              color: 'var(--lx-fg1)', lineHeight: 1.3, fontFamily: 'inherit',
            }}>
              {isEdit ? strings.subjectFormEditTitle : strings.subjectFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', lineHeight: 1.5, marginTop: 4 }}>
              {isEdit ? strings.subjectFormEditSubtitle : strings.subjectFormCreateSubtitle}
            </p>
          </div>
        </div>

        {/* Form body */}
        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* F1: Name */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <label htmlFor="subject-name" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
                {strings.subjectFormNameLabel} <span style={{ color: '#EF4444' }}>*</span>
              </label>
            </div>
            <input
              id="subject-name"
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={strings.subjectFormNamePlaceholder}
              data-testid="subject-form-name"
              style={{ ...inputStyle, borderColor: errors.name ? '#EF4444' : undefined }}
              aria-invalid={!!errors.name}
              aria-describedby={errors.name ? 'subject-name-err' : undefined}
            />
            {errors.name && (
              <span id="subject-name-err" role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>
                {errors.name}
              </span>
            )}
          </div>

          {/* F2: Grade */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="subject-grade" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.subjectFormGradeLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="subject-grade"
              value={gradeId}
              onChange={(e) => setGradeId(e.target.value === '' ? '' : Number(e.target.value))}
              data-testid="subject-form-grade"
              style={{ ...inputStyle, borderColor: errors.gradeId ? '#EF4444' : undefined }}
              aria-invalid={!!errors.gradeId}
            >
              <option value="">{strings.subjectFormGradePlaceholder}</option>
              {grades.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.name ?? `Grade ${g.gradeNumber}`}
                </option>
              ))}
            </select>
            {errors.gradeId && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.gradeId}</span>
            )}
          </div>

          {/* F3: Subject Code */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="subject-code" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.subjectFormCodeLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="subject-code"
              value={subjectCode}
              onChange={(e) => setSubjectCode(e.target.value === '' ? '' : Number(e.target.value) as SubjectCodeValue)}
              data-testid="subject-form-code"
              style={{ ...inputStyle, borderColor: errors.subjectCode ? '#EF4444' : undefined }}
              aria-invalid={!!errors.subjectCode}
            >
              <option value="">{strings.subjectFormCodePlaceholder}</option>
              <option value={SUBJECT_CODE.MATH}>Math</option>
              <option value={SUBJECT_CODE.SCIENCE}>Science</option>
              <option value={SUBJECT_CODE.ARABIC}>Arabic</option>
              <option value={SUBJECT_CODE.ENGLISH}>English</option>
            </select>
            {errors.subjectCode && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.subjectCode}</span>
            )}
          </div>

          {/* F4: Language (with pinned-language rule) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, opacity: isLanguagePinned ? 0.5 : 1 }}>
            <label htmlFor="subject-language" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.subjectFormLangLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="subject-language"
              value={language}
              onChange={(e) => setLanguage(e.target.value === '' ? '' : Number(e.target.value) as ContentLanguageValue)}
              disabled={isLanguagePinned}
              data-testid="subject-form-language"
              style={{ ...inputStyle, cursor: isLanguagePinned ? 'not-allowed' : 'default', borderColor: errors.language ? '#EF4444' : undefined }}
              aria-invalid={!!errors.language}
            >
              <option value="">{strings.subjectFormLangPlaceholder}</option>
              <option value={CONTENT_LANGUAGE.Ar}>Arabic (Ar)</option>
              <option value={CONTENT_LANGUAGE.En}>English (En)</option>
            </select>
            {isLanguagePinned && (
              <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 4 }}>
                {strings.subjectFormPinnedLangHint}
              </span>
            )}
            {errors.language && !isLanguagePinned && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.language}</span>
            )}
          </div>

          {/* F5: Sequence Order */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="subject-order" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.subjectFormOrderLabel}
            </label>
            <input
              id="subject-order"
              type="number"
              min={0}
              step={1}
              value={sequenceOrder}
              onChange={(e) => setSequenceOrder(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              placeholder="0"
              data-testid="subject-form-order"
              style={{ ...inputStyle, borderColor: errors.sequenceOrder ? '#EF4444' : undefined }}
              dir="ltr"
            />
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>
              {strings.subjectFormOrderHint}
            </span>
            {errors.sequenceOrder && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.sequenceOrder}</span>
            )}
          </div>

          {/* F6: Is-active toggle */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <label htmlFor="subject-active" style={{ fontSize: 14, color: 'var(--lx-fg2)' }}>
                {strings.subjectFormActiveLabel}
              </label>
              <button
                type="button"
                role="switch"
                id="subject-active"
                aria-checked={isActive}
                data-testid="subject-form-active"
                onClick={() => setIsActive(!isActive)}
                style={{
                  width: 40, height: 24, borderRadius: 9999, border: 'none',
                  cursor: 'pointer', position: 'relative',
                  backgroundColor: isActive ? '#22C55E' : 'var(--lx-card-soft)',
                  transition: 'background-color 200ms var(--lx-ease-out)',
                  flexShrink: 0,
                  outline: 'none',
                }}
                onFocus={(e) => { e.currentTarget.style.boxShadow = 'var(--lx-focus-ring)'; }}
                onBlur={(e) => { e.currentTarget.style.boxShadow = 'none'; }}
              >
                <span style={{
                  position: 'absolute',
                  width: 18, height: 18, borderRadius: '50%',
                  backgroundColor: '#F8FAFC',
                  top: 3,
                  left: isActive ? 18 : 2,
                  transition: 'left 200ms var(--lx-ease-out)',
                }} />
              </button>
            </div>
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>
              {isActive ? strings.subjectFormActiveLabelOn : strings.subjectFormActiveLabelOff}
            </span>
          </div>

          {/* Server error */}
          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button
              type="button"
              onClick={onClose}
              data-testid="subject-form-cancel"
              disabled={isPending}
              style={{
                height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}
            >
              {strings.subjectFormCancelBtn}
            </button>
            <button
              type="submit"
              data-testid="subject-form-save"
              aria-busy={isPending}
              disabled={isPending}
              style={{
                height: 40, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer', fontFamily: 'inherit',
                opacity: isPending ? 0.7 : 1,
              }}
            >
              {isPending ? '…' : isEdit ? strings.subjectFormSaveBtn : strings.subjectFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
