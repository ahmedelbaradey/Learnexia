'use client';

/**
 * TimedEventForm — create/edit modal for timed events (P7-13-FE).
 *
 * Design: TimedEventListItemDto does NOT include descriptionEn/descriptionAr;
 * no GET-by-id endpoint exists. In edit mode, description fields are empty —
 * a banner informs the admin to re-enter if desired.
 *
 * Scope = 1=Global | 2=SubjectSpecific | 3=GradeSpecific
 * Bilingual fields: nameEn, nameAr, descriptionEn (opt), descriptionAr (opt).
 * Timestamps: ISO 8601 UTC (datetime-local input → append 'Z').
 * No optimistic updates; on success invalidates adminGamification.timedEvents().
 */

import { useState, useEffect, useRef, useCallback, type FormEvent } from 'react';
import { isApiError } from '@learnexia/api-client/client';
import {
  useCreateTimedEvent,
  useUpdateTimedEvent,
  type TimedEventListItemDto,
  type TimedEventScope,
} from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Focus trap ─────────────────────────────────────────────────────────────────

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

// ── Lucide icons ──────────────────────────────────────────────────────────────

function CalendarClockIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 7.5V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3.5" />
      <path d="M16 2v4M8 2v4M3 10h5" />
      <circle cx="17" cy="17" r="5" />
      <path d="M17 14v3l2 2" />
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

// ── Styles ─────────────────────────────────────────────────────────────────────

const inputStyle: React.CSSProperties = {
  height: 44, backgroundColor: 'var(--lx-bg)', borderRadius: 8,
  border: '1px solid var(--lx-border)', paddingInline: 12, fontSize: 14,
  color: 'var(--lx-fg1)', fontFamily: 'inherit', width: '100%',
  boxSizing: 'border-box', outline: 'none',
};

const labelStyle: React.CSSProperties = {
  fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)',
  textTransform: 'uppercase', letterSpacing: '0.04em', fontFamily: 'inherit',
};

const hintStyle: React.CSSProperties = { fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 };
const errorStyle: React.CSSProperties = { fontSize: 12, color: '#EF4444', marginTop: 2 };

// ── Scope options ──────────────────────────────────────────────────────────────

// Map BE string scope names to numeric TimedEventScope values (for edit pre-fill)
const SCOPE_NAME_TO_INT: Record<string, TimedEventScope> = {
  AllXp: 1,
  MissionXp: 2,
  LeagueXp: 3,
};

const SCOPE_OPTIONS: { value: TimedEventScope; enLabel: string; arLabel: string }[] = [
  { value: 1, enLabel: 'All XP', arLabel: 'كل النقاط' },
  { value: 2, enLabel: 'Mission XP', arLabel: 'نقاط المهام' },
  { value: 3, enLabel: 'League XP', arLabel: 'نقاط الدوري' },
];

// ── UTC helpers ────────────────────────────────────────────────────────────────

function toLocalDateTimeValue(isoUtc: string | undefined): string {
  if (!isoUtc) return '';
  try {
    const d = new Date(isoUtc);
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const dd = String(d.getDate()).padStart(2, '0');
    const hh = String(d.getHours()).padStart(2, '0');
    const min = String(d.getMinutes()).padStart(2, '0');
    return `${yyyy}-${mm}-${dd}T${hh}:${min}`;
  } catch {
    return '';
  }
}

function toUtcIso(localDateTimeValue: string): string | undefined {
  if (!localDateTimeValue) return undefined;
  const d = new Date(localDateTimeValue);
  if (isNaN(d.getTime())) return undefined;
  return d.toISOString();
}

// ── Form errors ────────────────────────────────────────────────────────────────

interface FormErrors {
  code?: string;
  nameEn?: string;
  nameAr?: string;
  scope?: string;
  multiplier?: string;
  startUtc?: string;
  endUtc?: string;
  dateRange?: string;
}

// ── Props ──────────────────────────────────────────────────────────────────────

export interface TimedEventFormProps {
  open: boolean;
  onClose: () => void;
  editEvent?: TimedEventListItemDto;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function TimedEventForm({ open, onClose, editEvent }: TimedEventFormProps) {
  const isEdit = !!editEvent;
  const dialogRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<Element | null>(null);
  const titleId = 'timed-event-form-title';
  const isAr = ADMIN_LOCALE === 'ar';

  const [code, setCode] = useState('');
  const [nameEn, setNameEn] = useState('');
  const [nameAr, setNameAr] = useState('');
  const [descriptionEn, setDescriptionEn] = useState('');
  const [descriptionAr, setDescriptionAr] = useState('');
  const [scope, setScope] = useState<TimedEventScope | 0>(0);
  const [multiplier, setMultiplier] = useState<number | ''>(2);
  const [startUtc, setStartUtc] = useState('');
  const [endUtc, setEndUtc] = useState('');
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  const createMutation = useCreateTimedEvent();
  const updateMutation = useUpdateTimedEvent();
  const isPending = createMutation.isPending || updateMutation.isPending;

  useEffect(() => {
    if (open) triggerRef.current = document.activeElement;
  }, [open]);

  useEffect(() => {
    if (open) {
      setCode(isEdit ? (editEvent?.code ?? '') : '');
      setNameEn(isEdit ? (editEvent?.nameEn ?? '') : '');
      setNameAr(isEdit ? (editEvent?.nameAr ?? '') : '');
      // Description fields not in list DTO — always blank in edit mode
      setDescriptionEn('');
      setDescriptionAr('');
      // scope in list DTO is a string name; map to numeric for the form
      const scopeNum: TimedEventScope | 0 = isEdit && editEvent?.scope
        ? (SCOPE_NAME_TO_INT[editEvent.scope] ?? 0)
        : 0;
      setScope(scopeNum);
      setMultiplier(isEdit ? (editEvent?.multiplier ?? 2) : 2);
      setStartUtc(isEdit ? toLocalDateTimeValue(editEvent?.startUtc) : '');
      setEndUtc(isEdit ? toLocalDateTimeValue(editEvent?.endUtc) : '');
      setErrors({});
      setServerError(null);
    }
  }, [open]); // reset form only when dialog opens — intentional

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
        const first = foc[0]!; const last = foc[foc.length - 1]!;
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

  const validate = useCallback((): FormErrors => {
    const errs: FormErrors = {};
    if (!isEdit) {
      if (!code.trim()) errs.code = 'Code is required.';
      if (code.trim().length > 40) errs.code = 'Code must be 40 characters or fewer.';
    }
    if (!nameEn.trim()) errs.nameEn = 'English name is required.';
    if (nameEn.trim().length > 80) errs.nameEn = 'English name must be 80 characters or fewer.';
    if (!nameAr.trim()) errs.nameAr = 'Arabic name is required.';
    if (nameAr.trim().length > 80) errs.nameAr = 'Arabic name must be 80 characters or fewer.';
    if (!scope) errs.scope = 'Scope is required.';
    if (!multiplier || Number(multiplier) < 1) errs.multiplier = 'Multiplier must be at least 1.';
    if (multiplier !== '' && Number(multiplier) > 5) errs.multiplier = 'Multiplier must be at most 5.';
    if (!startUtc) errs.startUtc = 'Start date/time is required.';
    if (!endUtc) errs.endUtc = 'End date/time is required.';
    if (startUtc && endUtc) {
      const s = new Date(startUtc); const end = new Date(endUtc);
      if (!isNaN(s.getTime()) && !isNaN(end.getTime()) && end <= s) {
        errs.dateRange = 'End must be after start.';
      }
    }
    return errs;
  }, [isEdit, code, nameEn, nameAr, scope, multiplier, startUtc, endUtc]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});

    const payload = {
      nameEn: nameEn.trim(),
      nameAr: nameAr.trim(),
      descriptionEn: descriptionEn.trim() || undefined,
      descriptionAr: descriptionAr.trim() || undefined,
      scope: scope as TimedEventScope,
      multiplier: Number(multiplier),
      startUtc: toUtcIso(startUtc)!,
      endUtc: toUtcIso(endUtc)!,
    };

    try {
      if (isEdit && editEvent) {
        await updateMutation.mutateAsync({ id: editEvent.id, ...payload });
      } else {
        await createMutation.mutateAsync({ code: code.trim(), ...payload });
      }
      onClose();
    } catch (err) {
      if (isApiError(err) && err.status === 422) {
        setServerError('Validation error. Please check the form.');
      } else {
        setServerError('Something went wrong. Please try again.');
      }
    }
  };

  if (!open) return null;

  const onFocus = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    e.currentTarget.style.borderColor = 'var(--lx-primary)';
    e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
  };
  const onBlur = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    e.currentTarget.style.borderColor = 'var(--lx-border)';
    e.currentTarget.style.boxShadow = 'none';
  };
  const fieldStyle = (hasError: boolean): React.CSSProperties => ({
    ...inputStyle, borderColor: hasError ? '#EF4444' : undefined,
  });

  return (
    <div
      role="dialog" aria-modal="true" aria-labelledby={titleId}
      style={{
        position: 'fixed', inset: 0, backgroundColor: 'rgba(15,23,42,0.72)',
        display: 'flex', alignItems: 'center', justifyContent: 'center',
        zIndex: 500, animation: 'lx-dialog-overlay-in 200ms var(--lx-ease-out)',
      }}
    >
      <div
        ref={dialogRef}
        data-testid="timed-event-form-dialog"
        style={{
          backgroundColor: 'var(--lx-card)', borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)',
          padding: 32, width: 'calc(100vw - 64px)', maxWidth: 560,
          display: 'flex', flexDirection: 'column', gap: 24,
          maxHeight: '90vh', overflowY: 'auto',
          animation: 'lx-dialog-card-in 200ms var(--lx-ease-out)',
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 16 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 9999,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            backgroundColor: isEdit ? 'rgba(245,158,11,0.15)' : 'rgba(245,158,11,0.15)',
            color: '#F59E0B', flexShrink: 0,
          }}>
            {isEdit ? <PencilIcon /> : <CalendarClockIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', lineHeight: 1.3, fontFamily: 'inherit' }}>
              {isEdit ? strings.gamEventFormEditTitle : strings.gamEventFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', lineHeight: 1.5, marginTop: 4 }}>
              {isEdit ? strings.gamEventFormEditSubtitle : strings.gamEventFormCreateSubtitle}
            </p>
          </div>
        </div>

        {/* Edit gap notice */}
        {isEdit && (
          <div
            data-testid="event-form-edit-gap-notice"
            style={{
              padding: 10, backgroundColor: 'rgba(245,158,11,0.08)',
              borderRadius: 8, border: '1px solid rgba(245,158,11,0.2)',
              fontSize: 12, color: 'var(--lx-fg2)', lineHeight: 1.5,
            }}
          >
            {strings.gamEventFormDescGapNotice}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {/* Code (create-only) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, opacity: isEdit ? 0.5 : 1 }}>
            <label htmlFor="event-code" style={{
              fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)',
              textTransform: 'uppercase' as const, letterSpacing: '0.04em', fontFamily: 'inherit',
            }}>
              Code {!isEdit && <span style={{ color: '#EF4444' }}>*</span>}
            </label>
            <input
              id="event-code" type="text" data-testid="event-form-code"
              value={isEdit ? (editEvent?.code ?? '') : code}
              onChange={(e) => !isEdit && setCode(e.target.value)}
              disabled={isEdit}
              placeholder="e.g. RAMADAN_XP_BOOST"
              maxLength={40} aria-required={!isEdit}
              style={fieldStyle(!!errors.code)} onFocus={onFocus} onBlur={onBlur}
            />
            {isEdit && <span style={{ fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 }}>Code cannot be changed.</span>}
            {errors.code && <span role="alert" style={{ fontSize: 12, color: '#EF4444', marginTop: 2 }}>{errors.code}</span>}
          </div>

          {/* Name EN */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="event-name-en" style={labelStyle}>
              {strings.gamEventFormNameEnLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="event-name-en" type="text" data-testid="event-form-name-en"
              value={nameEn} onChange={(e) => setNameEn(e.target.value)}
              maxLength={80} aria-required="true" aria-invalid={!!errors.nameEn}
              dir="ltr" style={fieldStyle(!!errors.nameEn)} onFocus={onFocus} onBlur={onBlur}
            />
            {errors.nameEn && <span role="alert" style={errorStyle}>{errors.nameEn}</span>}
          </div>

          {/* Name AR */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="event-name-ar" style={labelStyle}>
              {strings.gamEventFormNameArLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="event-name-ar" type="text" data-testid="event-form-name-ar"
              value={nameAr} onChange={(e) => setNameAr(e.target.value)}
              maxLength={80} aria-required="true" aria-invalid={!!errors.nameAr}
              dir="rtl" style={fieldStyle(!!errors.nameAr)} onFocus={onFocus} onBlur={onBlur}
            />
            {errors.nameAr && <span role="alert" style={errorStyle}>{errors.nameAr}</span>}
          </div>

          {/* Description EN (optional) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="event-desc-en" style={labelStyle}>
              {strings.gamEventFormDescEnLabel}
            </label>
            <textarea
              id="event-desc-en" data-testid="event-form-desc-en"
              value={descriptionEn} onChange={(e) => setDescriptionEn(e.target.value)}
              maxLength={240} dir="ltr"
              placeholder={isEdit ? strings.gamEventFormDescGapPlaceholder : undefined}
              style={{ ...inputStyle, height: 'auto', minHeight: 60, resize: 'vertical', paddingTop: 10, paddingBottom: 10 }}
              onFocus={onFocus} onBlur={onBlur}
            />
          </div>

          {/* Description AR (optional) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="event-desc-ar" style={labelStyle}>
              {strings.gamEventFormDescArLabel}
            </label>
            <textarea
              id="event-desc-ar" data-testid="event-form-desc-ar"
              value={descriptionAr} onChange={(e) => setDescriptionAr(e.target.value)}
              maxLength={240} dir="rtl"
              placeholder={isEdit ? strings.gamEventFormDescGapPlaceholder : undefined}
              style={{ ...inputStyle, height: 'auto', minHeight: 60, resize: 'vertical', paddingTop: 10, paddingBottom: 10 }}
              onFocus={onFocus} onBlur={onBlur}
            />
          </div>

          {/* Scope */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="event-scope" style={labelStyle}>
              {strings.gamEventFormScopeLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="event-scope" data-testid="event-form-scope"
              value={scope === 0 ? '' : String(scope)}
              onChange={(e) => setScope(e.target.value ? Number(e.target.value) as TimedEventScope : 0)}
              aria-required="true" aria-invalid={!!errors.scope}
              style={{ ...fieldStyle(!!errors.scope), cursor: 'pointer' }}
              onFocus={onFocus} onBlur={onBlur}
            >
              <option value="">Select scope</option>
              {SCOPE_OPTIONS.map((o) => (
                <option key={o.value} value={String(o.value)}>
                  {isAr ? o.arLabel : o.enLabel}
                </option>
              ))}
            </select>
            {errors.scope && <span role="alert" style={errorStyle}>{errors.scope}</span>}
          </div>

          {/* Multiplier */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="event-multiplier" style={labelStyle}>
              {strings.gamEventFormMultiplierLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="event-multiplier" type="number" data-testid="event-form-multiplier"
              value={multiplier}
              onChange={(e) => setMultiplier(e.target.value === '' ? '' : parseFloat(e.target.value))}
              min={1} max={5} step={0.1} dir="ltr"
              aria-required="true" aria-invalid={!!errors.multiplier}
              style={fieldStyle(!!errors.multiplier)} onFocus={onFocus} onBlur={onBlur}
            />
            <span style={hintStyle}>{strings.gamEventFormMultiplierHint}</span>
            {errors.multiplier && <span role="alert" style={errorStyle}>{errors.multiplier}</span>}
          </div>

          {/* Date range row */}
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 12 }}>
            {/* Start UTC */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label htmlFor="event-start" style={labelStyle}>
                {strings.gamEventFormStartLabel} <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <input
                id="event-start" type="datetime-local" data-testid="event-form-start"
                value={startUtc} onChange={(e) => setStartUtc(e.target.value)}
                aria-required="true" aria-invalid={!!errors.startUtc}
                style={fieldStyle(!!errors.startUtc)} onFocus={onFocus} onBlur={onBlur}
              />
              {errors.startUtc && <span role="alert" style={errorStyle}>{errors.startUtc}</span>}
            </div>

            {/* End UTC */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label htmlFor="event-end" style={labelStyle}>
                {strings.gamEventFormEndLabel} <span style={{ color: '#EF4444' }}>*</span>
              </label>
              <input
                id="event-end" type="datetime-local" data-testid="event-form-end"
                value={endUtc} onChange={(e) => setEndUtc(e.target.value)}
                aria-required="true" aria-invalid={!!errors.endUtc}
                style={fieldStyle(!!errors.endUtc)} onFocus={onFocus} onBlur={onBlur}
              />
              {errors.endUtc && <span role="alert" style={errorStyle}>{errors.endUtc}</span>}
            </div>
          </div>

          {/* Date range cross-field error */}
          {errors.dateRange && <span role="alert" style={errorStyle}>{errors.dateRange}</span>}

          <span style={hintStyle}>{strings.gamEventFormDateHint}</span>

          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button
              type="button" data-testid="event-form-cancel" onClick={onClose} disabled={isPending}
              style={{
                height: 36, paddingInline: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}
            >
              {strings.gamEventFormCancelBtn}
            </button>
            <button
              type="submit" data-testid="event-form-save" aria-busy={isPending} disabled={isPending}
              style={{
                height: 40, paddingInline: 20, borderRadius: 16,
                backgroundColor: '#F59E0B', border: 'none',
                color: '#0F172A', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.7 : 1,
              }}
              onMouseEnter={(e) => { if (!isPending) (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#FBBF24'; }}
              onMouseLeave={(e) => { if (!isPending) (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#F59E0B'; }}
            >
              {isPending ? '…' : isEdit ? strings.gamEventFormSaveBtn : strings.gamEventFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
