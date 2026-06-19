'use client';

/**
 * SkillForm — create/edit modal dialog for skills (P7-03 Design Spec §S2).
 *
 * Fields: name (required), masteryThreshold (0–100), estimatedTimeMinutes (≥ 0),
 * conceptId (required, > 0 — concept picker from GET /api/learning/Concepts/List).
 *
 * isActive toggle: included because SingleSkillResponse.isActive IS present on
 * the wire (Gap 2 RESOLVED — AdminOnly endpoint exposes it).
 * The activate/deactivate toggle uses PUT /api/learning/Skills/{id}/Active
 * (not part of create/update commands — handled separately in the page).
 *
 * No drag, no new pattern, no new dependency. Focus trap mirrors SubjectForm /
 * UnitForm (reuses getFocusableElements pattern).
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import type { FormEvent } from 'react';
import {
  useCreateSkill,
  useUpdateSkill,
  useConceptList,
  type SingleSkillResponse,
  type AddSkillDto,
  type EditSkillDto,
} from '@learnexia/api-client';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';

const strings = getStrings(ADMIN_LOCALE);

// ── Focus trap helper (mirrors SubjectForm / UnitForm) ────────────────────────

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

// ── Inline SVG icons ──────────────────────────────────────────────────────────

function LayersIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 2 7 12 12 22 7 12 2" />
      <polyline points="2 17 12 22 22 17" />
      <polyline points="2 12 12 17 22 12" />
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

function SpinnerIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" aria-hidden="true"
      style={{ animation: 'spin 0.8s linear infinite' }}>
      <circle cx="12" cy="12" r="10" stroke="white" strokeWidth="3" fill="none" strokeDasharray="50" strokeDashoffset="20" />
    </svg>
  );
}

// ── Form errors ───────────────────────────────────────────────────────────────

interface FormErrors {
  name?: string;
  masteryThreshold?: string;
  estimatedTimeMinutes?: string;
  conceptId?: string;
}

// ── Component ─────────────────────────────────────────────────────────────────

export interface SkillFormProps {
  open: boolean;
  /** Null = create mode, non-null = edit mode. */
  skill: SingleSkillResponse | null;
  /** Optional: current subject id — used to invalidate graph on success. */
  currentSubjectId?: number;
  onClose: () => void;
  onSuccess?: () => void;
}

const INPUT_STYLE: React.CSSProperties = {
  height: 44,
  width: '100%',
  paddingLeft: 12,
  paddingRight: 12,
  backgroundColor: 'var(--lx-bg)',
  borderRadius: 8,
  border: '1px solid var(--lx-border)',
  color: 'var(--lx-fg1)',
  fontSize: 14,
  fontFamily: 'inherit',
  boxSizing: 'border-box',
  outline: 'none',
};

const LABEL_STYLE: React.CSSProperties = {
  fontSize: 12,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase' as const,
  letterSpacing: '0.04em',
};

export function SkillForm({ open, skill, currentSubjectId, onClose, onSuccess }: SkillFormProps) {
  const isEdit = skill != null;
  const titleId = 'skill-form-title';

  const overlayRef = useRef<HTMLDivElement>(null);
  const cardRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLElement | null>(null);

  const [name, setName] = useState('');
  const [masteryThreshold, setMasteryThreshold] = useState('');
  const [estimatedTimeMinutes, setEstimatedTimeMinutes] = useState('');
  const [conceptId, setConceptId] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  const createMutation = useCreateSkill();
  const updateMutation = useUpdateSkill();
  const conceptQuery = useConceptList(currentSubjectId);

  const isPending = createMutation.isPending || updateMutation.isPending;

  // Populate form in edit mode
  useEffect(() => {
    if (open) {
      triggerRef.current = document.activeElement as HTMLElement;
      if (skill) {
        setName(skill.name);
        setMasteryThreshold(String(skill.masteryThreshold));
        setEstimatedTimeMinutes(String(skill.estimatedTimeMinutes));
        setConceptId(String(skill.conceptId));
      } else {
        setName('');
        setMasteryThreshold('');
        setEstimatedTimeMinutes('');
        setConceptId('');
      }
      setErrors({});
      setServerError(null);
    }
  }, [open, skill]);

  // Focus first input on open
  useEffect(() => {
    if (open && cardRef.current) {
      const focusable = getFocusableElements(cardRef.current);
      focusable[0]?.focus();
    }
  }, [open]);

  // Focus trap + ESC
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onClose();
        triggerRef.current?.focus();
        return;
      }
      if (e.key === 'Tab' && cardRef.current) {
        const focusable = getFocusableElements(cardRef.current);
        if (focusable.length === 0) return;
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last?.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first?.focus();
        }
      }
    },
    [onClose],
  );

  const validate = (): boolean => {
    const errs: FormErrors = {};
    if (!name.trim()) errs.name = strings.skillFormNameRequired;

    const thresh = parseInt(masteryThreshold, 10);
    if (!masteryThreshold.trim()) {
      errs.masteryThreshold = strings.skillFormThresholdRequired;
    } else if (isNaN(thresh) || thresh < 0 || thresh > 100) {
      errs.masteryThreshold = strings.skillFormThresholdRange;
    }

    const time = parseInt(estimatedTimeMinutes, 10);
    if (!estimatedTimeMinutes.trim()) {
      errs.estimatedTimeMinutes = strings.skillFormTimeRequired;
    } else if (isNaN(time) || time < 0) {
      errs.estimatedTimeMinutes = strings.skillFormTimeRange;
    }

    if (!conceptId || parseInt(conceptId, 10) <= 0) {
      errs.conceptId = strings.skillFormConceptRequired;
    }

    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!validate()) return;
    setServerError(null);

    const thresh = parseInt(masteryThreshold, 10);
    const time = parseInt(estimatedTimeMinutes, 10);
    const cid = parseInt(conceptId, 10);

    try {
      if (isEdit && skill) {
        const payload: EditSkillDto = {
          id: skill.id,
          name: name.trim(),
          masteryThreshold: thresh,
          estimatedTimeMinutes: time,
          conceptId: cid,
        };
        await updateMutation.mutateAsync({ ...payload, _subjectId: currentSubjectId });
      } else {
        const payload: AddSkillDto = {
          name: name.trim(),
          masteryThreshold: thresh,
          estimatedTimeMinutes: time,
          conceptId: cid,
        };
        await createMutation.mutateAsync({ ...payload, _subjectId: currentSubjectId });
      }
      onSuccess?.();
      onClose();
      triggerRef.current?.focus();
    } catch (err) {
      setServerError((err as Error).message || strings.skillGraphErrGeneric);
    }
  };

  if (!open) return null;

  const concepts = conceptQuery.data?.data ?? [];

  return (
    <div
      ref={overlayRef}
      role="presentation"
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'var(--lx-overlay)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        zIndex: 500,
        animation: 'lx-dialog-overlay-in 200ms var(--lx-ease-out)',
      }}
      onKeyDown={handleKeyDown}
    >
      <div
        ref={cardRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        data-testid="skill-form-dialog"
        style={{
          backgroundColor: 'var(--lx-card)',
          borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: 'var(--lx-shadow-popup)',
          padding: 32,
          width: 'calc(100vw - 64px)',
          maxWidth: 500,
          display: 'flex',
          flexDirection: 'column',
          gap: 24,
          animation: 'lx-dialog-card-in 200ms var(--lx-ease-out)',
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 9999,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            backgroundColor: isEdit ? 'rgba(245,158,11,0.15)' : 'rgba(79,70,229,0.15)',
            color: isEdit ? 'var(--lx-accent)' : 'var(--lx-primary)',
          }}>
            {isEdit ? <PencilIcon /> : <LayersIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{
              margin: 0,
              fontFamily: 'var(--lx-font-display)',
              fontSize: 18,
              fontWeight: 700,
              color: 'var(--lx-fg1)',
              lineHeight: 1.3,
            }}>
              {isEdit ? strings.skillFormEditTitle : strings.skillFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', marginTop: 4 }}>
              {isEdit ? strings.skillFormEditSubtitle : strings.skillFormCreateSubtitle}
            </p>
          </div>
        </div>

        {/* Form */}
        <form onSubmit={(e) => void handleSubmit(e)} noValidate
          style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* Name */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <label htmlFor="skill-name" style={LABEL_STYLE}>
                {strings.skillFormNameLabel} <span style={{ color: 'var(--lx-danger)' }}>*</span>
              </label>
            </div>
            <input
              id="skill-name"
              type="text"
              data-testid="skill-form-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={strings.skillFormNamePlaceholder}
              dir="auto"
              style={{
                ...INPUT_STYLE,
                borderColor: errors.name ? '#EF4444' : 'var(--lx-border)',
                boxShadow: errors.name ? '0 0 0 2px #EF4444, 0 0 0 4px rgba(239,68,68,0.2)' : 'none',
              }}
              aria-invalid={!!errors.name}
              aria-describedby={errors.name ? 'skill-name-error' : undefined}
            />
            {errors.name && (
              <p id="skill-name-error" role="alert" style={{ margin: 0, fontSize: 12, color: 'var(--lx-danger)', marginTop: 2 }}>
                {errors.name}
              </p>
            )}
          </div>

          {/* Mastery Threshold */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="skill-threshold" style={LABEL_STYLE}>
              {strings.skillFormThresholdLabel} <span style={{ color: 'var(--lx-danger)' }}>*</span>
            </label>
            <input
              id="skill-threshold"
              type="number"
              data-testid="skill-form-threshold"
              value={masteryThreshold}
              onChange={(e) => setMasteryThreshold(e.target.value)}
              min={0}
              max={100}
              step={1}
              placeholder="80"
              style={{
                ...INPUT_STYLE,
                borderColor: errors.masteryThreshold ? '#EF4444' : 'var(--lx-border)',
                boxShadow: errors.masteryThreshold ? '0 0 0 2px #EF4444, 0 0 0 4px rgba(239,68,68,0.2)' : 'none',
              }}
              aria-invalid={!!errors.masteryThreshold}
              aria-describedby={errors.masteryThreshold ? 'skill-threshold-error' : 'skill-threshold-hint'}
            />
            <p id="skill-threshold-hint" style={{ margin: 0, fontSize: 12, color: 'var(--lx-fg3)' }}>
              {strings.skillFormThresholdHint}
            </p>
            {errors.masteryThreshold && (
              <p id="skill-threshold-error" role="alert" style={{ margin: 0, fontSize: 12, color: 'var(--lx-danger)', marginTop: 2 }}>
                {errors.masteryThreshold}
              </p>
            )}
          </div>

          {/* Estimated Time */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="skill-time" style={LABEL_STYLE}>
              {strings.skillFormTimeLabel} <span style={{ color: 'var(--lx-danger)' }}>*</span>
            </label>
            <input
              id="skill-time"
              type="number"
              data-testid="skill-form-time"
              value={estimatedTimeMinutes}
              onChange={(e) => setEstimatedTimeMinutes(e.target.value)}
              min={0}
              step={1}
              placeholder="15"
              style={{
                ...INPUT_STYLE,
                borderColor: errors.estimatedTimeMinutes ? '#EF4444' : 'var(--lx-border)',
                boxShadow: errors.estimatedTimeMinutes ? '0 0 0 2px #EF4444, 0 0 0 4px rgba(239,68,68,0.2)' : 'none',
              }}
              aria-invalid={!!errors.estimatedTimeMinutes}
              aria-describedby={errors.estimatedTimeMinutes ? 'skill-time-error' : 'skill-time-hint'}
            />
            <p id="skill-time-hint" style={{ margin: 0, fontSize: 12, color: 'var(--lx-fg3)' }}>
              {strings.skillFormTimeHint}
            </p>
            {errors.estimatedTimeMinutes && (
              <p id="skill-time-error" role="alert" style={{ margin: 0, fontSize: 12, color: 'var(--lx-danger)', marginTop: 2 }}>
                {errors.estimatedTimeMinutes}
              </p>
            )}
          </div>

          {/* Concept picker */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="skill-concept" style={LABEL_STYLE}>
              {strings.skillFormConceptLabel} <span style={{ color: 'var(--lx-danger)' }}>*</span>
            </label>
            <select
              id="skill-concept"
              data-testid="skill-form-concept"
              value={conceptId}
              onChange={(e) => setConceptId(e.target.value)}
              disabled={conceptQuery.isLoading || conceptQuery.isError}
              style={{
                ...INPUT_STYLE,
                borderColor: errors.conceptId ? '#EF4444' : 'var(--lx-border)',
                boxShadow: errors.conceptId ? '0 0 0 2px #EF4444, 0 0 0 4px rgba(239,68,68,0.2)' : 'none',
                cursor: conceptQuery.isLoading ? 'not-allowed' : 'pointer',
              }}
              aria-invalid={!!errors.conceptId}
            >
              <option value="">
                {conceptQuery.isLoading
                  ? strings.skillFormConceptLoading
                  : conceptQuery.isError
                    ? strings.skillFormConceptError
                    : strings.skillFormConceptPlaceholder}
              </option>
              {concepts.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
            {errors.conceptId && (
              <p role="alert" style={{ margin: 0, fontSize: 12, color: 'var(--lx-danger)', marginTop: 2 }}>
                {errors.conceptId}
              </p>
            )}
            {conceptQuery.isError && (
              <p role="alert" style={{ margin: 0, fontSize: 12, color: 'var(--lx-danger)', marginTop: 2 }}>
                {strings.skillFormConceptError}
              </p>
            )}
          </div>

          {/* Server error */}
          {serverError && (
            <AdminErrorBanner variant="error" message={serverError} />
          )}

          {/* Actions */}
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button
              type="button"
              data-testid="skill-form-cancel"
              onClick={() => {
                onClose();
                triggerRef.current?.focus();
              }}
              style={{
                height: 36,
                paddingLeft: 16,
                paddingRight: 16,
                borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)',
                backgroundColor: 'transparent',
                color: 'var(--lx-fg2)',
                fontSize: 14,
                fontWeight: 500,
                cursor: 'pointer',
                fontFamily: 'inherit',
              }}
            >
              {strings.skillFormCancel}
            </button>
            <button
              type="submit"
              data-testid="skill-form-save"
              disabled={isPending}
              aria-busy={isPending}
              style={{
                height: 40,
                paddingLeft: 20,
                paddingRight: 20,
                borderRadius: 16,
                border: 'none',
                backgroundColor: 'var(--lx-primary)',
                color: 'var(--lx-fg1)',
                fontSize: 14,
                fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit',
                opacity: isPending ? 0.7 : 1,
                display: 'flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              {isPending ? <SpinnerIcon /> : null}
              {isEdit ? strings.skillFormSaveBtn : strings.skillFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
