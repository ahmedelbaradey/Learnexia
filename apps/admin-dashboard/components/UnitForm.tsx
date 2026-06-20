'use client';

/**
 * UnitForm — create/edit modal dialog for units (Design Spec §S7).
 *
 * Units inherit language from the owning Subject — there is no language picker.
 * The parent subject's language is shown as a read-only notice.
 * subjectId is always fixed to the context subject and sent in the body.
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import type { FormEvent } from 'react';
import {
  useCreateUnit,
  useUpdateUnit,
  type UnitDto,
  type AddUnitDto,
  type EditUnitDto,
} from '@learnexia/api-client';
import { CONTENT_LANGUAGE, type ContentLanguageValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';

const strings = getStrings(ADMIN_LOCALE);

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

function LayersPlusIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polygon points="12 2 2 7 12 12 22 7 12 2" />
      <polyline points="2 17 12 22 22 17" />
      <polyline points="2 12 12 17 22 12" />
      <line x1="12" y1="22" x2="12" y2="16" />
      <line x1="9" y1="19" x2="15" y2="19" />
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
      <circle cx="12" cy="12" r="10" /><line x1="12" y1="16" x2="12" y2="12" /><line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  );
}

interface FormErrors {
  name?: string;
}

export interface UnitFormProps {
  open: boolean;
  onClose: () => void;
  subjectId: number;
  /** The owning subject's content language (inherited — shown as read-only notice). */
  subjectLanguage: ContentLanguageValue;
  /** If provided, opens in edit mode. */
  editUnit?: UnitDto;
}

export function UnitForm({ open, onClose, subjectId, subjectLanguage, editUnit }: UnitFormProps) {
  const isEdit = !!editUnit;
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

  const [name, setName] = useState(isEdit ? (editUnit?.name ?? '') : '');
  const [sequenceOrder, setSequenceOrder] = useState<number | ''>(isEdit ? (editUnit?.sequenceOrder ?? 0) : 0);
  const [isActive, setIsActive] = useState(isEdit ? (editUnit?.isActive ?? true) : true);
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  useEffect(() => {
    if (open) {
      setName(isEdit ? (editUnit?.name ?? '') : '');
      setSequenceOrder(isEdit ? (editUnit?.sequenceOrder ?? 0) : 0);
      setIsActive(isEdit ? (editUnit?.isActive ?? true) : true);
      setErrors({});
      setServerError(null);
    }
  }, [open]); // intentional: only re-run when dialog opens/closes

  const createMutation = useCreateUnit();
  const updateMutation = useUpdateUnit();
  const isPending = createMutation.isPending || updateMutation.isPending;

  const validate = useCallback((): FormErrors => {
    const errs: FormErrors = {};
    if (!name.trim()) errs.name = strings.unitFormErrNameRequired;
    return errs;
  }, [name]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});
    try {
      if (isEdit && editUnit) {
        const body: EditUnitDto = {
          id: editUnit.id,
          name: name.trim(),
          subjectId,
          sequenceOrder: sequenceOrder as number,
          isActive,
        };
        await updateMutation.mutateAsync(body);
      } else {
        const body: AddUnitDto = {
          name: name.trim(),
          subjectId,
          sequenceOrder: sequenceOrder as number,
          isActive,
        };
        await createMutation.mutateAsync(body);
      }
      onClose();
    } catch (err) {
      setServerError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  if (!open) return null;

  const titleId = 'unit-form-title';
  const langLabel = subjectLanguage === CONTENT_LANGUAGE.Ar ? 'Ar' : 'En';

  const inputStyle: React.CSSProperties = {
    height: 44, backgroundColor: 'var(--lx-bg)', borderRadius: 8,
    border: '1px solid var(--lx-border)', paddingLeft: 12, paddingRight: 12,
    fontSize: 14, color: 'var(--lx-fg1)', fontFamily: 'inherit',
    width: '100%', boxSizing: 'border-box', outline: 'none',
  };

  return (
    <div
      role="dialog" aria-modal="true" aria-labelledby={titleId}
      data-testid="unit-form-dialog"
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
          padding: 32, width: 'calc(100vw - 64px)', maxWidth: 440,
          display: 'flex', flexDirection: 'column', gap: 24,
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
            {isEdit ? <PencilIcon /> : <LayersPlusIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', lineHeight: 1.3, fontFamily: 'inherit' }}>
              {isEdit ? strings.unitFormEditTitle : strings.unitFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', lineHeight: 1.5, marginTop: 4 }}>
              {isEdit ? strings.unitFormEditSubtitle : strings.unitFormCreateSubtitle}
            </p>
          </div>
        </div>

        {/* Inherited language notice */}
        <div style={{
          padding: 12, backgroundColor: 'rgba(79,70,229,0.08)', borderRadius: 8,
          border: '1px solid rgba(79,70,229,0.15)',
          display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'center',
        }}>
          <InfoIcon />
          <span style={{ fontSize: 13, color: 'var(--lx-fg3)', lineHeight: 1.5 }}>
            {strings.unitFormInheritedLangNotice.replace('{lang}', langLabel)}
          </span>
        </div>

        {/* Form body */}
        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* Name */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="unit-name" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.unitFormNameLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="unit-name" type="text"
              value={name} onChange={(e) => setName(e.target.value)}
              placeholder={strings.unitFormNamePlaceholder}
              data-testid="unit-form-name"
              style={{ ...inputStyle, borderColor: errors.name ? '#EF4444' : undefined }}
              aria-invalid={!!errors.name}
            />
            {errors.name && (
              <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.name}</span>
            )}
          </div>

          {/* Sequence order */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="unit-order" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.unitFormOrderLabel}
            </label>
            <input
              id="unit-order" type="number" min={0} step={1}
              value={sequenceOrder}
              onChange={(e) => setSequenceOrder(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              placeholder="0" data-testid="unit-form-order"
              style={inputStyle} dir="ltr"
            />
            <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>{strings.unitFormOrderHint}</span>
          </div>

          {/* Is-active toggle */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' }}>
              <label htmlFor="unit-active" style={{ fontSize: 14, color: 'var(--lx-fg2)' }}>
                {strings.unitFormActiveLabel}
              </label>
              <button
                type="button" role="switch" id="unit-active"
                aria-checked={isActive} data-testid="unit-form-active"
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
              {isActive ? strings.unitFormActiveLabelOn : strings.unitFormActiveLabelOff}
            </span>
          </div>

          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button type="button" onClick={onClose} data-testid="unit-form-cancel"
              disabled={isPending}
              style={{
                height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}>
              {strings.unitFormCancelBtn}
            </button>
            <button type="submit" data-testid="unit-form-save"
              aria-busy={isPending} disabled={isPending}
              style={{
                height: 40, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer', fontFamily: 'inherit',
                opacity: isPending ? 0.7 : 1,
              }}>
              {isPending ? '…' : isEdit ? strings.unitFormSaveBtn : strings.unitFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
