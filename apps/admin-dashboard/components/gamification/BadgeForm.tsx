'use client';

/**
 * BadgeForm — create/edit modal for badge catalog entries (P7-13-FE).
 *
 * Mirrors SubjectForm shell (same overlay, focus trap, token treatment).
 * No optimistic updates; on success invalidates adminGamification.badges().
 * Code field: create-only (disabled + hint in edit mode).
 * Threshold: conditional — shown only for StreakThreshold=2 or LevelThreshold=3.
 *
 * BE validation mirrored in FE for immediate feedback:
 *   code: NotEmpty ≤64 | name: NotEmpty ≤80 | description: NotEmpty ≤240
 *   iconKey: NotEmpty ≤128 | rarity/triggerType in enum | rewardXp >0 | sortOrder ≥0
 */

import { useState, useEffect, useRef, useCallback, type FormEvent } from 'react';
import { isApiError } from '@learnexia/api-client/client';
import {
  useCreateBadge,
  useUpdateBadge,
  type BadgeDefinitionDto,
  type BadgeTriggerType,
} from '@learnexia/api-client';
// BadgeRarity from @learnexia/api-client is the generated numeric enum with _1=1 syntax.
// Use a local numeric union type compatible with our admin gamification DTOs.
type BadgeRarity = 1 | 2 | 3 | 4;
import { AdminErrorBanner } from '../AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Focus trap helper ────────────────────────────────────────────────────────

function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

// ── Lucide icon SVGs ─────────────────────────────────────────────────────────

function AwardIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="6" />
      <path d="M15.477 12.89 17 22l-5-3-5 3 1.523-9.11" />
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

// ── Shared input style ────────────────────────────────────────────────────────

const inputStyle: React.CSSProperties = {
  height: 44,
  backgroundColor: 'var(--lx-bg)',
  borderRadius: 8,
  border: '1px solid var(--lx-border)',
  paddingInline: 12,
  fontSize: 14,
  color: 'var(--lx-fg1)',
  fontFamily: 'inherit',
  width: '100%',
  boxSizing: 'border-box',
  outline: 'none',
};

const labelStyle: React.CSSProperties = {
  fontSize: 12,
  fontWeight: 600,
  color: 'var(--lx-fg3)',
  textTransform: 'uppercase',
  letterSpacing: '0.04em',
  fontFamily: 'inherit',
};

const hintStyle: React.CSSProperties = {
  fontSize: 12,
  color: 'var(--lx-fg3)',
  marginTop: 2,
};

const errorStyle: React.CSSProperties = {
  fontSize: 12,
  color: '#EF4444',
  marginTop: 2,
};

// ── Form errors ───────────────────────────────────────────────────────────────

interface FormErrors {
  code?: string;
  name?: string;
  description?: string;
  iconKey?: string;
  rarity?: string;
  triggerType?: string;
  threshold?: string;
  rewardXp?: string;
  sortOrder?: string;
}

// ── Rarity + trigger options ──────────────────────────────────────────────────

const RARITY_OPTIONS: { value: BadgeRarity; enLabel: string; arLabel: string }[] = [
  { value: 1, enLabel: 'Common', arLabel: 'شائع' },
  { value: 2, enLabel: 'Rare', arLabel: 'نادر' },
  { value: 3, enLabel: 'Epic', arLabel: 'نادر جدًا' },
  { value: 4, enLabel: 'Legendary', arLabel: 'أسطوري' },
];

const TRIGGER_OPTIONS: { value: BadgeTriggerType; enLabel: string; arLabel: string }[] = [
  { value: 1, enLabel: 'First Lesson', arLabel: 'أول درس' },
  { value: 2, enLabel: 'Streak Threshold', arLabel: 'عتبة السلسلة' },
  { value: 3, enLabel: 'Level Threshold', arLabel: 'عتبة المستوى' },
];

// ── Props ──────────────────────────────────────────────────────────────────────

export interface BadgeFormProps {
  open: boolean;
  onClose: () => void;
  editBadge?: BadgeDefinitionDto;
}

// ── Component ──────────────────────────────────────────────────────────────────

export function BadgeForm({ open, onClose, editBadge }: BadgeFormProps) {
  const isEdit = !!editBadge;
  const dialogRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<Element | null>(null);
  const titleId = 'badge-form-title';
  const isAr = ADMIN_LOCALE === 'ar';

  // Form state
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [iconKey, setIconKey] = useState('');
  const [rarity, setRarity] = useState<BadgeRarity | 0>(0);
  const [triggerType, setTriggerType] = useState<BadgeTriggerType | 0>(0);
  const [threshold, setThreshold] = useState<number | ''>('');
  const [rewardXp, setRewardXp] = useState<number | ''>('');
  const [sortOrder, setSortOrder] = useState<number | ''>(0);
  const [errors, setErrors] = useState<FormErrors>({});
  const [serverError, setServerError] = useState<string | null>(null);

  const createMutation = useCreateBadge();
  const updateMutation = useUpdateBadge();
  const isPending = createMutation.isPending || updateMutation.isPending;

  // Track trigger element for focus restoration
  useEffect(() => {
    if (open) triggerRef.current = document.activeElement;
  }, [open]);

  // Reset form state when dialog opens
  useEffect(() => {
    if (open) {
      setCode(isEdit ? (editBadge?.code ?? '') : '');
      setName(isEdit ? (editBadge?.name ?? '') : '');
      setDescription(isEdit ? (editBadge?.description ?? '') : '');
      setIconKey(isEdit ? (editBadge?.iconKey ?? '') : '');
      setRarity(isEdit ? (editBadge?.rarity ?? 0) : 0);
      setTriggerType(isEdit ? (editBadge?.triggerType ?? 0) : 0);
      setThreshold(isEdit ? (editBadge?.threshold ?? '') : '');
      setRewardXp(isEdit ? (editBadge?.rewardXp ?? '') : '');
      setSortOrder(isEdit ? (editBadge?.sortOrder ?? 0) : 0);
      setErrors({});
      setServerError(null);
    }
  }, [open]); // reset form only when dialog opens — intentional

  // Focus trap + ESC
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

  const validate = useCallback((): FormErrors => {
    const errs: FormErrors = {};
    if (!isEdit && !code.trim()) errs.code = 'Code is required.';
    if (!isEdit && code.trim().length > 64) errs.code = 'Code must be 64 characters or fewer.';
    if (!name.trim()) errs.name = 'Name is required.';
    if (name.trim().length > 80) errs.name = 'Name must be 80 characters or fewer.';
    if (!description.trim()) errs.description = 'Description is required.';
    if (description.trim().length > 240) errs.description = 'Description must be 240 characters or fewer.';
    if (!iconKey.trim()) errs.iconKey = 'Icon key is required.';
    if (!rarity) errs.rarity = 'Rarity is required.';
    if (!triggerType) errs.triggerType = 'Trigger type is required.';
    if ((triggerType === 2 || triggerType === 3) && (!threshold || Number(threshold) < 1)) {
      errs.threshold = strings.gamBadgeFormThresholdRequired;
    }
    if (!rewardXp || Number(rewardXp) <= 0) errs.rewardXp = 'Reward XP must be greater than 0.';
    if (sortOrder === '' || Number(sortOrder) < 0) errs.sortOrder = 'Sort order must be 0 or greater.';
    return errs;
  }, [isEdit, code, name, description, iconKey, rarity, triggerType, threshold, rewardXp, sortOrder, strings]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) { setErrors(errs); return; }
    setErrors({});

    try {
      if (isEdit && editBadge) {
        await updateMutation.mutateAsync({
          id: editBadge.id,
          name: name.trim(),
          description: description.trim(),
          iconKey: iconKey.trim(),
          rarity: rarity as BadgeRarity,
          sortOrder: Number(sortOrder),
          triggerType: triggerType as BadgeTriggerType,
          threshold: (triggerType === 2 || triggerType === 3) ? Number(threshold) : null,
          rewardXp: Number(rewardXp),
        });
      } else {
        await createMutation.mutateAsync({
          code: code.trim(),
          name: name.trim(),
          description: description.trim(),
          iconKey: iconKey.trim(),
          rarity: rarity as BadgeRarity,
          sortOrder: Number(sortOrder),
          triggerType: triggerType as BadgeTriggerType,
          threshold: (triggerType === 2 || triggerType === 3) ? Number(threshold) : null,
          rewardXp: Number(rewardXp),
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

  const showThreshold = triggerType === 2 || triggerType === 3;
  const descLen = description.length;

  const fieldStyle = (hasError: boolean): React.CSSProperties => ({
    ...inputStyle,
    borderColor: hasError ? '#EF4444' : undefined,
  });

  const onFocus = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    e.currentTarget.style.borderColor = 'var(--lx-primary)';
    e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
  };
  const onBlur = (e: React.FocusEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    e.currentTarget.style.borderColor = errors[(e.currentTarget.id as keyof FormErrors)] ? '#EF4444' : 'var(--lx-border)';
    e.currentTarget.style.boxShadow = 'none';
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
        animation: 'lx-dialog-overlay-in 200ms var(--lx-ease-out)',
      }}
    >
      <div
        ref={dialogRef}
        data-testid="badge-form-dialog"
        style={{
          backgroundColor: 'var(--lx-card)',
          borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12)',
          padding: 32,
          width: 'calc(100vw - 64px)',
          maxWidth: 520,
          display: 'flex',
          flexDirection: 'column',
          gap: 24,
          maxHeight: '90vh',
          overflowY: 'auto',
          animation: 'lx-dialog-card-in 200ms var(--lx-ease-out)',
        }}
      >
        {/* Header */}
        <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: 16 }}>
          <div style={{
            width: 40, height: 40, borderRadius: 9999,
            display: 'flex', alignItems: 'center', justifyContent: 'center',
            backgroundColor: isEdit ? 'rgba(245,158,11,0.15)' : 'rgba(168,85,247,0.15)',
            color: isEdit ? '#F59E0B' : '#A855F7',
            flexShrink: 0,
          }}>
            {isEdit ? <PencilIcon /> : <AwardIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', lineHeight: 1.3, fontFamily: 'inherit' }}>
              {isEdit ? strings.gamBadgeFormEditTitle : strings.gamBadgeFormCreateTitle}
            </h2>
            <p style={{ margin: 0, fontSize: 14, color: 'var(--lx-fg3)', lineHeight: 1.5, marginTop: 4 }}>
              {isEdit ? strings.gamBadgeFormEditSubtitle : strings.gamBadgeFormCreateSubtitle}
            </p>
          </div>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>

          {/* F1: Code (create-only) */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, opacity: isEdit ? 0.5 : 1 }}>
            <label htmlFor="badge-code" style={labelStyle}>
              {strings.gamBadgeFormCodeLabel} {!isEdit && <span style={{ color: '#EF4444' }}>*</span>}
            </label>
            <input
              id="badge-code"
              type="text"
              data-testid="badge-form-code"
              value={isEdit ? (editBadge?.code ?? '') : code}
              onChange={(e) => !isEdit && setCode(e.target.value)}
              disabled={isEdit}
              placeholder={strings.gamBadgeFormCodePlaceholder}
              maxLength={64}
              aria-required={!isEdit}
              aria-invalid={!!errors.code}
              aria-describedby={errors.code ? 'badge-code-err' : undefined}
              style={fieldStyle(!!errors.code)}
              onFocus={onFocus}
              onBlur={onBlur}
            />
            {isEdit && <span style={hintStyle}>{strings.gamBadgeFormCodeHint}</span>}
            {errors.code && <span id="badge-code-err" role="alert" style={errorStyle}>{errors.code}</span>}
          </div>

          {/* F2: Name */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-name" style={labelStyle}>
              {strings.gamBadgeFormNameLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="badge-name"
              type="text"
              data-testid="badge-form-name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder={strings.gamBadgeFormNamePlaceholder}
              maxLength={80}
              aria-required="true"
              aria-invalid={!!errors.name}
              style={fieldStyle(!!errors.name)}
              onFocus={onFocus}
              onBlur={onBlur}
            />
            {errors.name && <span role="alert" style={errorStyle}>{errors.name}</span>}
          </div>

          {/* F3: Description */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-description" style={labelStyle}>
              {strings.gamBadgeFormDescLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <textarea
              id="badge-description"
              data-testid="badge-form-description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              maxLength={240}
              aria-required="true"
              aria-invalid={!!errors.description}
              style={{
                ...fieldStyle(!!errors.description),
                height: 'auto',
                minHeight: 80,
                resize: 'vertical',
                paddingTop: 10,
                paddingBottom: 10,
              }}
              onFocus={onFocus}
              onBlur={onBlur}
            />
            <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
              <span
                style={{
                  ...hintStyle,
                  fontVariantNumeric: 'tabular-nums',
                  color: descLen >= 216 ? (descLen >= 240 ? '#EF4444' : '#F59E0B') : 'var(--lx-fg3)',
                }}
                dir="ltr"
              >
                {descLen} / 240
              </span>
            </div>
            {errors.description && <span role="alert" style={errorStyle}>{errors.description}</span>}
          </div>

          {/* F4: Icon Key */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-icon-key" style={labelStyle}>
              {strings.gamBadgeFormIconKeyLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="badge-icon-key"
              type="text"
              data-testid="badge-form-icon-key"
              value={iconKey}
              onChange={(e) => setIconKey(e.target.value)}
              placeholder={strings.gamBadgeFormIconKeyPlaceholder}
              maxLength={128}
              aria-required="true"
              aria-invalid={!!errors.iconKey}
              style={fieldStyle(!!errors.iconKey)}
              onFocus={onFocus}
              onBlur={onBlur}
            />
            <span style={hintStyle}>{strings.gamBadgeFormIconKeyHint}</span>
            {errors.iconKey && <span role="alert" style={errorStyle}>{errors.iconKey}</span>}
          </div>

          {/* F5: Rarity */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-rarity" style={labelStyle}>
              {strings.gamBadgeFormRarityLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="badge-rarity"
              data-testid="badge-form-rarity"
              value={rarity === 0 ? '' : String(rarity)}
              onChange={(e) => setRarity(e.target.value ? Number(e.target.value) as BadgeRarity : 0)}
              aria-required="true"
              aria-invalid={!!errors.rarity}
              style={{ ...fieldStyle(!!errors.rarity), cursor: 'pointer' }}
              onFocus={onFocus}
              onBlur={onBlur}
            >
              <option value="">{strings.gamBadgeFormRarityPlaceholder}</option>
              {RARITY_OPTIONS.map((o) => (
                <option key={o.value} value={String(o.value)}>
                  {isAr ? o.arLabel : o.enLabel}
                </option>
              ))}
            </select>
            {errors.rarity && <span role="alert" style={errorStyle}>{errors.rarity}</span>}
          </div>

          {/* F6: Trigger Type */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-trigger" style={labelStyle}>
              {strings.gamBadgeFormTriggerLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <select
              id="badge-trigger"
              data-testid="badge-form-trigger"
              value={triggerType === 0 ? '' : String(triggerType)}
              onChange={(e) => {
                const val = e.target.value ? Number(e.target.value) as BadgeTriggerType : 0;
                setTriggerType(val);
                if (val === 1) setThreshold('');
              }}
              aria-required="true"
              aria-invalid={!!errors.triggerType}
              style={{ ...fieldStyle(!!errors.triggerType), cursor: 'pointer' }}
              onFocus={onFocus}
              onBlur={onBlur}
            >
              <option value="">Select trigger</option>
              {TRIGGER_OPTIONS.map((o) => (
                <option key={o.value} value={String(o.value)}>
                  {isAr ? o.arLabel : o.enLabel}
                </option>
              ))}
            </select>
            {errors.triggerType && <span role="alert" style={errorStyle}>{errors.triggerType}</span>}
          </div>

          {/* F7: Threshold (conditional) */}
          {showThreshold && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <label htmlFor="badge-threshold" style={labelStyle}>
                {strings.gamBadgeFormThresholdLabel}
              </label>
              <input
                id="badge-threshold"
                type="number"
                data-testid="badge-form-threshold"
                value={threshold}
                onChange={(e) => setThreshold(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
                min={1}
                step={1}
                dir="ltr"
                aria-invalid={!!errors.threshold}
                style={fieldStyle(!!errors.threshold)}
                onFocus={onFocus}
                onBlur={onBlur}
              />
              {errors.threshold && <span role="alert" style={errorStyle}>{errors.threshold}</span>}
            </div>
          )}

          {/* F8: Reward XP */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-reward-xp" style={labelStyle}>
              {strings.gamBadgeFormRewardXpLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input
              id="badge-reward-xp"
              type="number"
              data-testid="badge-form-reward-xp"
              value={rewardXp}
              onChange={(e) => setRewardXp(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              min={1}
              step={1}
              dir="ltr"
              aria-required="true"
              aria-invalid={!!errors.rewardXp}
              style={fieldStyle(!!errors.rewardXp)}
              onFocus={onFocus}
              onBlur={onBlur}
            />
            {errors.rewardXp && <span role="alert" style={errorStyle}>{errors.rewardXp}</span>}
          </div>

          {/* F9: Sort Order */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="badge-sort-order" style={labelStyle}>
              {strings.gamBadgeFormSortOrderLabel}
            </label>
            <input
              id="badge-sort-order"
              type="number"
              data-testid="badge-form-sort-order"
              value={sortOrder}
              onChange={(e) => setSortOrder(e.target.value === '' ? '' : parseInt(e.target.value, 10))}
              min={0}
              step={1}
              dir="ltr"
              aria-invalid={!!errors.sortOrder}
              style={fieldStyle(!!errors.sortOrder)}
              onFocus={onFocus}
              onBlur={onBlur}
            />
            <span style={hintStyle}>{strings.gamBadgeFormSortOrderHint}</span>
            {errors.sortOrder && <span role="alert" style={errorStyle}>{errors.sortOrder}</span>}
          </div>

          {/* Server error */}
          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions row */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button
              type="button"
              data-testid="badge-form-cancel"
              onClick={onClose}
              disabled={isPending}
              style={{
                height: 36, paddingInline: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}
            >
              {strings.gamBadgeFormCancelBtn}
            </button>
            <button
              type="submit"
              data-testid="badge-form-save"
              aria-busy={isPending}
              disabled={isPending}
              style={{
                height: 40, paddingInline: 20, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.7 : 1,
              }}
              onMouseEnter={(e) => {
                if (!isPending) (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1';
              }}
              onMouseLeave={(e) => {
                if (!isPending) (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5';
              }}
            >
              {isPending ? '…' : isEdit ? strings.gamBadgeFormSaveBtn : strings.gamBadgeFormCreateBtn}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
