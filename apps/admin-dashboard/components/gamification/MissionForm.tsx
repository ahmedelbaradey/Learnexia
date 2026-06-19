'use client';

/**
 * MissionForm — create/edit modal for mission catalog entries (P7-13-FE).
 *
 * Mirrors BadgeForm shell; same overlay, focus trap, token treatment.
 * Field shape from MissionDefinitionDto / brief:
 *   code (create-only), iconKey, titleKey (i18n key), cadence (Daily=1|Weekly=2),
 *   targetType (1/2/3), target (count), rewardXp, sortOrder.
 * MissionType = Daily=1 | Weekly=2 ONLY (no weekly-challenge).
 * No optimistic updates; on success invalidates adminGamification.missions().
 */

import { useState, useEffect, useRef, useCallback, type FormEvent } from 'react';
import { isApiError } from '@learnexia/api-client/client';
import {
  useCreateMission,
  useUpdateMission,
  type MissionDefinitionDto,
  type MissionType,
  type MissionTargetType,
} from '@learnexia/api-client';
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Focus trap ────────────────────────────────────────────────────────────────

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

// ── Lucide icons ──────────────────────────────────────────────────────────────

function TargetIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><circle cx="12" cy="12" r="6" /><circle cx="12" cy="12" r="2" />
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

// ── Select options ────────────────────────────────────────────────────────────

const CADENCE_OPTIONS: { value: MissionType; enLabel: string; arLabel: string }[] = [
  { value: 1, enLabel: 'Daily', arLabel: 'يومية' },
  { value: 2, enLabel: 'Weekly', arLabel: 'أسبوعية' },
];

const TARGET_TYPE_OPTIONS: { value: MissionTargetType; enLabel: string; arLabel: string }[] = [
  { value: 1, enLabel: 'Complete Lessons', arLabel: 'إكمال دروس' },
  { value: 2, enLabel: 'Correct Answers', arLabel: 'إجابات صحيحة' },
  { value: 3, enLabel: 'Maintain Streak', arLabel: 'الحفاظ على السلسلة' },
];

// ── Form errors ───────────────────────────────────────────────────────────────

interface FormErrors {
  code?: string;
  iconKey?: string;
  titleKey?: string;
  cadence?: string;
  targetType?: string;
  target?: string;
  rewardXp?: string;
  sortOrder?: string;
}

// ── Props ──────────────────────────────────────────────────────────────────────

export interface MissionFormProps {
  open: boolean;
  onClose: () => void;
  editMission?: MissionDefinitionDto;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function MissionForm({ open, onClose, editMission }: MissionFormProps) {
  const isEdit = !!editMission;
  const dialogRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<Element | null>(null);
  const titleId = 'mission-form-title';
  const isAr = ADMIN_LOCALE === 'ar';

  const [code, setCode] = useState('');
  const [iconKey, setIconKey] = useState('');
  const [titleKey, setTitleKey] = useState('');
  const [cadence, setCadence] = useState<MissionType | 0>(0);
  const [targetType, setTargetType] = useState<MissionTargetType | 0>(0);
  const [target, setTarget] = useState<number | ''>('');
  const [rewardXp, setRewardXp] = useState<number | ''>('');
  const [sortOrder, setSortOrder] = useState<number | ''>(0);
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  const createMutation = useCreateMission();
  const updateMutation = useUpdateMission();
  const isPending = createMutation.isPending || updateMutation.isPending;

  useEffect(() => {
    if (open) triggerRef.current = document.activeElement;
  }, [open]);

  useEffect(() => {
    if (open) {
      setCode(isEdit ? (editMission?.code ?? '') : '');
      setIconKey(isEdit ? (editMission?.iconKey ?? '') : '');
      setTitleKey(isEdit ? (editMission?.titleKey ?? '') : '');
      setCadence(isEdit ? (editMission?.cadence ?? 0) : 0);
      setTargetType(isEdit ? (editMission?.targetType ?? 0) : 0);
      setTarget(isEdit ? (editMission?.target ?? '') : '');
      setRewardXp(isEdit ? (editMission?.rewardXp ?? '') : '');
      setSortOrder(isEdit ? (editMission?.sortOrder ?? 0) : 0);
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
    if (!iconKey.trim()) errs.iconKey = 'Icon key is required.';
    if (iconKey.trim().length > 60) errs.iconKey = 'Icon key must be 60 characters or fewer.';
    if (!titleKey.trim()) errs.titleKey = 'Title key is required.';
    if (titleKey.trim().length > 80) errs.titleKey = 'Title key must be 80 characters or fewer.';
    if (!cadence) errs.cadence = 'Cadence is required.';
    if (!targetType) errs.targetType = 'Target type is required.';
    if (!target || Number(target) < 1) errs.target = 'Target must be at least 1.';
    if (!rewardXp || Number(rewardXp) <= 0) errs.rewardXp = 'Reward XP must be greater than 0.';
    if (sortOrder === '' || Number(sortOrder) < 0) errs.sortOrder = 'Sort order must be 0 or greater.';
    return errs;
  }, [isEdit, code, iconKey, titleKey, cadence, targetType, target, rewardXp, sortOrder]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});
    try {
      if (isEdit && editMission) {
        await updateMutation.mutateAsync({
          id: editMission.id,
          iconKey: iconKey.trim(),
          titleKey: titleKey.trim(),
          cadence: cadence as MissionType,
          targetType: targetType as MissionTargetType,
          target: Number(target),
          rewardXp: Number(rewardXp),
          sortOrder: Number(sortOrder),
        });
      } else {
        await createMutation.mutateAsync({
          code: code.trim(),
          iconKey: iconKey.trim(),
          titleKey: titleKey.trim(),
          cadence: cadence as MissionType,
          targetType: targetType as MissionTargetType,
          target: Number(target),
          rewardXp: Number(rewardXp),
          sortOrder: Number(sortOrder),
        });
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
        data-testid="mission-form-dialog"
        style={{
          backgroundColor: 'var(--lx-card)', borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)',
          padding: 32, width: 'calc(100vw - 64px)', maxWidth: 520,
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
            backgroundColor: isEdit ? 'rgba(245,158,11,0.15)' : 'rgba(79,70,229,0.15)',
            color: isEdit ? '#F59E0B' : '#4F46E5', flexShrink: 0,
          }}>
            {isEdit ? <PencilIcon /> : <TargetIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', lineHeight: 1.3, fontFamily: 'inherit' }}>
              {isEdit ? strings.gamMissionFormEditTitle : strings.gamMissionFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', lineHeight: 1.5, marginTop: 4 }}>
              {isEdit ? strings.gamMissionFormEditSubtitle : strings.gamMissionFormCreateSubtitle}
            </p>
          </div>
        </div>

        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* Code (create-only) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, opacity: isEdit ? 0.5 : 1 }}>
            <label htmlFor="mission-code" style={labelStyle}>
              {strings.gamMissionFormCodeLabel} {!isEdit && <span style={{ color: '#EF4444' }}>*</span>}
            </label>
            <input
              id="mission-code" type="text" data-testid="mission-form-code"
              value={isEdit ? (editMission?.code ?? '') : code}
              onChange={(e) => !isEdit && setCode(e.target.value)}
              disabled={isEdit}
              placeholder="e.g. DAILY_LESSONS_3"
              maxLength={40} aria-required={!isEdit}
              style={fieldStyle(!!errors.code)} onFocus={onFocus} onBlur={onBlur}
            />
            {isEdit && <span style={hintStyle}>Code cannot be changed.</span>}
            {errors.code && <span role="alert" style={errorStyle}>{errors.code}</span>}
          </div>

          {/* Icon Key */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-icon-key" style={labelStyle}>
              {strings.gamMissionFormIconKeyLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="mission-icon-key" type="text" data-testid="mission-form-icon-key"
              value={iconKey} onChange={(e) => setIconKey(e.target.value)}
              maxLength={60} aria-required="true"
              style={fieldStyle(!!errors.iconKey)} onFocus={onFocus} onBlur={onBlur}
            />
            {errors.iconKey && <span role="alert" style={errorStyle}>{errors.iconKey}</span>}
          </div>

          {/* Title Key */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-title-key" style={labelStyle}>
              {strings.gamMissionFormTitleKeyLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="mission-title-key" type="text" data-testid="mission-form-title-key"
              value={titleKey} onChange={(e) => setTitleKey(e.target.value)}
              maxLength={80} aria-required="true"
              style={fieldStyle(!!errors.titleKey)} onFocus={onFocus} onBlur={onBlur}
            />
            <span style={hintStyle}>{strings.gamMissionFormTitleKeyHint}</span>
            {errors.titleKey && <span role="alert" style={errorStyle}>{errors.titleKey}</span>}
          </div>

          {/* Cadence */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-cadence" style={labelStyle}>
              {strings.gamMissionFormCadenceLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="mission-cadence" data-testid="mission-form-cadence"
              value={cadence === 0 ? '' : String(cadence)}
              onChange={(e) => setCadence(e.target.value ? Number(e.target.value) as MissionType : 0)}
              aria-required="true" aria-invalid={!!errors.cadence}
              style={{ ...fieldStyle(!!errors.cadence), cursor: 'pointer' }}
              onFocus={onFocus} onBlur={onBlur}
            >
              <option value="">Select cadence</option>
              {CADENCE_OPTIONS.map((o) => (
                <option key={o.value} value={String(o.value)}>
                  {isAr ? o.arLabel : o.enLabel}
                </option>
              ))}
            </select>
            {errors.cadence && <span role="alert" style={errorStyle}>{errors.cadence}</span>}
          </div>

          {/* Target Type */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-target-type" style={labelStyle}>
              {strings.gamMissionFormTargetTypeLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="mission-target-type" data-testid="mission-form-target-type"
              value={targetType === 0 ? '' : String(targetType)}
              onChange={(e) => setTargetType(e.target.value ? Number(e.target.value) as MissionTargetType : 0)}
              aria-required="true" aria-invalid={!!errors.targetType}
              style={{ ...fieldStyle(!!errors.targetType), cursor: 'pointer' }}
              onFocus={onFocus} onBlur={onBlur}
            >
              <option value="">Select target type</option>
              {TARGET_TYPE_OPTIONS.map((o) => (
                <option key={o.value} value={String(o.value)}>
                  {isAr ? o.arLabel : o.enLabel}
                </option>
              ))}
            </select>
            {errors.targetType && <span role="alert" style={errorStyle}>{errors.targetType}</span>}
          </div>

          {/* Target Count */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-target" style={labelStyle}>
              {strings.gamMissionFormTargetLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="mission-target" type="number" data-testid="mission-form-target"
              value={target}
              onChange={(e) => setTarget(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              min={1} step={1} dir="ltr"
              aria-required="true" aria-invalid={!!errors.target}
              style={fieldStyle(!!errors.target)} onFocus={onFocus} onBlur={onBlur}
            />
            {errors.target && <span role="alert" style={errorStyle}>{errors.target}</span>}
          </div>

          {/* Reward XP */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-reward-xp" style={labelStyle}>
              {strings.gamMissionFormRewardXpLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="mission-reward-xp" type="number" data-testid="mission-form-reward-xp"
              value={rewardXp}
              onChange={(e) => setRewardXp(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              min={1} step={1} dir="ltr"
              aria-required="true" aria-invalid={!!errors.rewardXp}
              style={fieldStyle(!!errors.rewardXp)} onFocus={onFocus} onBlur={onBlur}
            />
            {errors.rewardXp && <span role="alert" style={errorStyle}>{errors.rewardXp}</span>}
          </div>

          {/* Sort Order */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="mission-sort-order" style={labelStyle}>
              {strings.gamMissionFormSortOrderLabel}
            </label>
            <input
              id="mission-sort-order" type="number" data-testid="mission-form-sort-order"
              value={sortOrder}
              onChange={(e) => setSortOrder(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              min={0} step={1} dir="ltr"
              style={fieldStyle(!!errors.sortOrder)} onFocus={onFocus} onBlur={onBlur}
            />
            <span style={hintStyle}>{strings.gamMissionFormSortOrderHint}</span>
            {errors.sortOrder && <span role="alert" style={errorStyle}>{errors.sortOrder}</span>}
          </div>

          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button
              type="button" data-testid="mission-form-cancel" onClick={onClose} disabled={isPending}
              style={{
                height: 36, paddingInline: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}
            >
              {strings.gamMissionFormCancelBtn}
            </button>
            <button
              type="submit" data-testid="mission-form-save" aria-busy={isPending} disabled={isPending}
              style={{
                height: 40, paddingInline: 20, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.7 : 1,
              }}
              onMouseEnter={(e) => { if (!isPending) (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1'; }}
              onMouseLeave={(e) => { if (!isPending) (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5'; }}
            >
              {isPending ? '…' : isEdit ? strings.gamMissionFormSaveBtn : strings.gamMissionFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
