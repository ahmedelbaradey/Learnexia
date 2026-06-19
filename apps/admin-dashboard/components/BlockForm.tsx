'use client';

/**
 * BlockForm — per-type create/edit modal for content blocks.
 * Design Spec §8.5 — P7-02.
 *
 * Mirrors LessonForm modal shell (same focus-trap, same motion).
 * Payload serialized to exact shapes before submission.
 * Payload total length validated ≤ 65536 chars (P7-SEC-2).
 * URL client hints for SEC-3 (backend is authoritative).
 */

import { useState, useEffect, useRef, useCallback } from 'react';
import type { FormEvent } from 'react';
import { useAddContentBlock, useEditContentBlock, type AdminContentBlockDto } from '@learnexia/api-client';
import { CONTENT_BLOCK_TYPE, type ContentBlockTypeValue } from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';
import { BlockTypeBadge } from './BlockTypeBadge';

const strings = getStrings(ADMIN_LOCALE);

const PAYLOAD_CAP = 65536;
const MARKDOWN_CAP = 32768;
const URL_CAP = 2048;
const TEXT_CAP = 1024;

// ── Focus trap helper ──────────────────────────────────────────────────────────
function getFocusableElements(container: HTMLElement): HTMLElement[] {
  return Array.from(
    container.querySelectorAll<HTMLElement>(
      'a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])',
    ),
  ).filter((el) => !el.hasAttribute('aria-hidden'));
}

// ── Icons ──────────────────────────────────────────────────────────────────────
function PlusCircleIcon() {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="8" x2="12" y2="16" /><line x1="8" y1="12" x2="16" y2="12" />
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
    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" /><line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  );
}
function AlertTriangleIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="#F59E0B" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
      <line x1="12" y1="9" x2="12" y2="13" /><line x1="12" y1="17" x2="12.01" y2="17" />
    </svg>
  );
}

// ── URL validation (client hints — backend is authoritative) ──────────────────
function validateUrl(url: string): string | null {
  if (!url.trim()) return strings.blockFormUrlRequired;
  if (!url.startsWith('https://')) return strings.blockFormUrlHttpsRequired;
  if (/^https?:\/\/(localhost|127\.|10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|169\.254\.|0\.0\.0\.0)/.test(url)) {
    return strings.blockFormUrlPrivateNotAllowed;
  }
  // IPv6 loopback [::1], link-local [fe80::…], and ULA [fc00:: / fd00::]
  if (/^https?:\/\/\[?(::1|fe[89ab][0-9a-f]:|fc[0-9a-f]{2}:|fd[0-9a-f]{2}:)/i.test(url)) {
    return strings.blockFormUrlPrivateNotAllowed;
  }
  return null;
}

// ── Per-type form state ───────────────────────────────────────────────────────

interface TextState    { markdown: string; }
interface ImageState   { url: string; altText: string; }
interface VideoState   { url: string; caption: string; }
interface CalloutState { variant: 'info' | 'warning' | 'tip' | ''; markdown: string; }

type TypeState = TextState | ImageState | VideoState | CalloutState;

function initState(blockType: ContentBlockTypeValue, existingBlock?: AdminContentBlockDto): TypeState {
  if (!existingBlock) {
    if (blockType === CONTENT_BLOCK_TYPE.Text)    return { markdown: '' };
    if (blockType === CONTENT_BLOCK_TYPE.Image)   return { url: '', altText: '' };
    if (blockType === CONTENT_BLOCK_TYPE.Video)   return { url: '', caption: '' };
    return { variant: '', markdown: '' };
  }
  try {
    const p = JSON.parse(existingBlock.payload);
    if (blockType === CONTENT_BLOCK_TYPE.Text)    return { markdown: p.markdown ?? '' };
    if (blockType === CONTENT_BLOCK_TYPE.Image)   return { url: p.url ?? '', altText: p.altText ?? '' };
    if (blockType === CONTENT_BLOCK_TYPE.Video)   return { url: p.url ?? '', caption: p.caption ?? '' };
    return { variant: p.variant ?? '', markdown: p.markdown ?? '' };
  } catch {
    if (blockType === CONTENT_BLOCK_TYPE.Text)    return { markdown: '' };
    if (blockType === CONTENT_BLOCK_TYPE.Image)   return { url: '', altText: '' };
    if (blockType === CONTENT_BLOCK_TYPE.Video)   return { url: '', caption: '' };
    return { variant: '', markdown: '' };
  }
}

function buildPayload(blockType: ContentBlockTypeValue, state: TypeState): string {
  if (blockType === CONTENT_BLOCK_TYPE.Text) {
    const s = state as TextState;
    return JSON.stringify({ markdown: s.markdown });
  }
  if (blockType === CONTENT_BLOCK_TYPE.Image) {
    const s = state as ImageState;
    return JSON.stringify({ url: s.url, ...(s.altText ? { altText: s.altText } : {}) });
  }
  if (blockType === CONTENT_BLOCK_TYPE.Video) {
    const s = state as VideoState;
    return JSON.stringify({ url: s.url, ...(s.caption ? { caption: s.caption } : {}) });
  }
  const s = state as CalloutState;
  return JSON.stringify({ variant: s.variant, markdown: s.markdown });
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface BlockFormProps {
  open: boolean;
  onClose: () => void;
  lessonId: number;
  /** The type selected in the picker (create) or existing block type (edit). */
  blockType: ContentBlockTypeValue;
  /** If provided, opens in edit mode. */
  editBlock?: AdminContentBlockDto;
}

const inputStyle: React.CSSProperties = {
  height: 44, backgroundColor: 'var(--lx-bg)', borderRadius: 8,
  border: '1px solid var(--lx-border)', paddingLeft: 12, paddingRight: 12,
  fontSize: 14, color: 'var(--lx-fg1)', fontFamily: 'inherit',
  width: '100%', boxSizing: 'border-box', outline: 'none',
};

const textareaStyle: React.CSSProperties = {
  backgroundColor: 'var(--lx-bg)', borderRadius: 8,
  border: '1px solid var(--lx-border)', padding: '10px 12px',
  fontSize: 14, fontFamily: 'var(--lx-font-mono, monospace)', color: 'var(--lx-fg1)',
  lineHeight: 1.6, width: '100%', boxSizing: 'border-box', outline: 'none',
  resize: 'vertical', minHeight: 160, maxHeight: 400,
};

const hintStyle: React.CSSProperties = { fontSize: 12, color: 'var(--lx-fg3)', marginTop: 2 };
const counterStyle = (over: boolean): React.CSSProperties => ({
  fontSize: 12, color: over ? 'var(--lx-danger)' : 'var(--lx-fg3)',
  textAlign: 'end', marginTop: 2,
});

export function BlockForm({ open, onClose, lessonId, blockType, editBlock }: BlockFormProps) {
  const isEdit = !!editBlock;
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

  const [currentType, setCurrentType] = useState<ContentBlockTypeValue>(blockType);
  const [typeChanged, setTypeChanged] = useState(false);
  const [state, setState] = useState<TypeState>(() => initState(blockType, editBlock));
  const [serverError, setServerError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (open) {
      setCurrentType(blockType);
      setTypeChanged(false);
      setState(initState(blockType, editBlock));
      setServerError(null);
      setFieldErrors({});
    }
  }, [open]); // intentional: reset state only on open/close toggle

  const addMutation = useAddContentBlock();
  const editMutation = useEditContentBlock();
  const isPending = addMutation.isPending || editMutation.isPending;

  const handleTypeChange = (newType: ContentBlockTypeValue) => {
    if (newType === currentType) return;
    setCurrentType(newType);
    setTypeChanged(true);
    setState(initState(newType));
    setFieldErrors({});
  };

  const validate = useCallback((): Record<string, string> => {
    const errs: Record<string, string> = {};
    const payload = buildPayload(currentType, state);
    if (payload.length > PAYLOAD_CAP) {
      errs.payload = strings.blockFormPayloadTooLarge;
      return errs;
    }
    if (currentType === CONTENT_BLOCK_TYPE.Text) {
      const s = state as TextState;
      if (!s.markdown.trim()) errs.markdown = strings.blockFormMarkdownRequired;
    } else if (currentType === CONTENT_BLOCK_TYPE.Image || currentType === CONTENT_BLOCK_TYPE.Video) {
      const s = state as ImageState | VideoState;
      const urlErr = validateUrl(s.url);
      if (urlErr) errs.url = urlErr;
    } else if (currentType === CONTENT_BLOCK_TYPE.Callout) {
      const s = state as CalloutState;
      if (!s.variant) errs.variant = strings.blockFormVariantRequired;
      if (!s.markdown.trim()) errs.markdown = strings.blockFormMarkdownRequired;
    }
    return errs;
  }, [currentType, state]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setServerError(null);
    const errs = validate();
    if (Object.keys(errs).length) { setFieldErrors(errs); return; }
    setFieldErrors({});
    const payload = buildPayload(currentType, state);
    try {
      if (isEdit && editBlock) {
        await editMutation.mutateAsync({ id: editBlock.id, blockType: currentType, payload, lessonId });
      } else {
        await addMutation.mutateAsync({ lessonId, blockType: currentType, payload });
      }
      onClose();
    } catch (err) {
      setServerError((err as Error).message || strings.curriculumNetworkError);
    }
  };

  if (!open) return null;

  const titleId = 'block-form-title';

  // ── Render per-type fields ──────────────────────────────────────────────────
  const renderFields = () => {
    if (currentType === CONTENT_BLOCK_TYPE.Text) {
      const s = state as TextState;
      return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <label htmlFor="block-text-markdown" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            {strings.blockFormMarkdownLabel} <span style={{ color: '#EF4444' }}>*</span>
          </label>
          <textarea
            id="block-text-markdown" name="markdown" rows={8} dir="auto"
            value={s.markdown} placeholder={strings.blockFormMarkdownPlaceholder}
            onChange={(e) => setState({ ...s, markdown: e.target.value })}
            maxLength={MARKDOWN_CAP * 2} // soft cap; counter warns at MARKDOWN_CAP
            data-testid="block-text-markdown"
            style={{ ...textareaStyle, borderColor: fieldErrors.markdown ? '#EF4444' : undefined }}
            aria-invalid={!!fieldErrors.markdown}
          />
          <div style={counterStyle(s.markdown.length > MARKDOWN_CAP)}>{s.markdown.length}/{MARKDOWN_CAP}</div>
          {fieldErrors.markdown && <span role="alert" style={{ fontSize: 12, color: '#EF4444' }}>{fieldErrors.markdown}</span>}
        </div>
      );
    }

    if (currentType === CONTENT_BLOCK_TYPE.Image) {
      const s = state as ImageState;
      return (
        <>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="block-image-url" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.blockFormImageUrlLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input id="block-image-url" type="url" name="url" dir="ltr"
              placeholder="https://…" maxLength={URL_CAP}
              value={s.url} onChange={(e) => setState({ ...s, url: e.target.value })}
              data-testid="block-image-url"
              style={{ ...inputStyle, borderColor: fieldErrors.url ? '#EF4444' : undefined }}
              aria-invalid={!!fieldErrors.url}
            />
            <div style={counterStyle(s.url.length > URL_CAP)}>{s.url.length}/{URL_CAP}</div>
            <div style={{ ...hintStyle, display: 'flex', flexDirection: 'row', alignItems: 'flex-start', gap: 4 }}>
              <InfoIcon /><span>{strings.blockFormUrlHint}</span>
            </div>
            {fieldErrors.url && <span role="alert" style={{ fontSize: 12, color: '#EF4444' }}>{fieldErrors.url}</span>}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="block-image-alt" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.blockFormImageAltLabel}
            </label>
            <input id="block-image-alt" type="text" name="altText" dir="auto"
              maxLength={TEXT_CAP} value={s.altText}
              onChange={(e) => setState({ ...s, altText: e.target.value })}
              placeholder={strings.blockFormImageAltPlaceholder}
              data-testid="block-image-alt"
              style={inputStyle}
            />
            <div style={counterStyle(s.altText.length > TEXT_CAP)}>{s.altText.length}/{TEXT_CAP}</div>
            <span style={hintStyle}>{strings.blockFormImageAltHint}</span>
          </div>
        </>
      );
    }

    if (currentType === CONTENT_BLOCK_TYPE.Video) {
      const s = state as VideoState;
      return (
        <>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="block-video-url" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.blockFormVideoUrlLabel} <span style={{ color: '#EF4444' }}>*</span>
            </label>
            <input id="block-video-url" type="url" name="url" dir="ltr"
              placeholder="https://…" maxLength={URL_CAP}
              value={s.url} onChange={(e) => setState({ ...s, url: e.target.value })}
              data-testid="block-video-url"
              style={{ ...inputStyle, borderColor: fieldErrors.url ? '#EF4444' : undefined }}
              aria-invalid={!!fieldErrors.url}
            />
            <div style={counterStyle(s.url.length > URL_CAP)}>{s.url.length}/{URL_CAP}</div>
            <div style={{ ...hintStyle, display: 'flex', flexDirection: 'row', alignItems: 'flex-start', gap: 4 }}>
              <InfoIcon /><span>{strings.blockFormUrlHint}</span>
            </div>
            {fieldErrors.url && <span role="alert" style={{ fontSize: 12, color: '#EF4444' }}>{fieldErrors.url}</span>}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="block-video-caption" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              {strings.blockFormVideoCaptionLabel}
            </label>
            <input id="block-video-caption" type="text" name="caption" dir="auto"
              maxLength={TEXT_CAP} value={s.caption}
              onChange={(e) => setState({ ...s, caption: e.target.value })}
              placeholder={strings.blockFormVideoCaptionPlaceholder}
              data-testid="block-video-caption"
              style={inputStyle}
            />
            <div style={counterStyle(s.caption.length > TEXT_CAP)}>{s.caption.length}/{TEXT_CAP}</div>
          </div>
        </>
      );
    }

    // Callout
    const s = state as CalloutState;
    return (
      <>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <label htmlFor="block-callout-variant" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            {strings.blockFormCalloutVariantLabel} <span style={{ color: '#EF4444' }}>*</span>
          </label>
          <select id="block-callout-variant" name="variant"
            value={s.variant} onChange={(e) => setState({ ...s, variant: e.target.value as CalloutState['variant'] })}
            data-testid="block-callout-variant"
            style={{ ...inputStyle, borderColor: fieldErrors.variant ? '#EF4444' : undefined, cursor: 'pointer' }}
            aria-invalid={!!fieldErrors.variant}
          >
            <option value="">{strings.blockFormCalloutVariantLabel}</option>
            <option value="info">{strings.blockFormCalloutVariantInfo}</option>
            <option value="warning">{strings.blockFormCalloutVariantWarning}</option>
            <option value="tip">{strings.blockFormCalloutVariantTip}</option>
          </select>
          {fieldErrors.variant && <span role="alert" style={{ fontSize: 12, color: '#EF4444' }}>{fieldErrors.variant}</span>}
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <label htmlFor="block-callout-markdown" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
            {strings.blockFormCalloutMarkdownLabel} <span style={{ color: '#EF4444' }}>*</span>
          </label>
          <textarea id="block-callout-markdown" name="markdown" rows={6} dir="auto"
            value={s.markdown} placeholder={strings.blockFormMarkdownPlaceholder}
            onChange={(e) => setState({ ...s, markdown: e.target.value })}
            maxLength={MARKDOWN_CAP * 2}
            data-testid="block-callout-markdown"
            style={{ ...textareaStyle, borderColor: fieldErrors.markdown ? '#EF4444' : undefined }}
            aria-invalid={!!fieldErrors.markdown}
          />
          <div style={counterStyle(s.markdown.length > MARKDOWN_CAP)}>{s.markdown.length}/{MARKDOWN_CAP}</div>
          {fieldErrors.markdown && <span role="alert" style={{ fontSize: 12, color: '#EF4444' }}>{fieldErrors.markdown}</span>}
        </div>
      </>
    );
  };

  return (
    <div
      role="dialog" aria-modal="true" aria-labelledby={titleId}
      style={{
        position: 'fixed', inset: 0, backgroundColor: 'rgba(15,23,42,0.72)',
        display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 600,
      }}
    >
      <div
        ref={dialogRef}
        style={{
          backgroundColor: 'var(--lx-card)', borderRadius: 24,
          border: '1px solid rgba(255,255,255,0.16)',
          boxShadow: 'var(--lx-shadow-popup)',
          padding: 32, width: 'calc(100vw - 64px)', maxWidth: 560,
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
            {isEdit ? <PencilIcon /> : <PlusCircleIcon />}
          </div>
          <div>
            <h2 id={titleId} style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)', lineHeight: 1.3 }}>
              {isEdit ? strings.blockFormEditTitle : strings.blockFormAddTitle}
            </h2>
            <div style={{ marginTop: 4 }}>
              <BlockTypeBadge blockType={currentType} />
            </div>
          </div>
        </div>

        {/* Edit-mode type selector */}
        {isEdit && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            <label htmlFor="block-type-select" style={{ fontSize: 12, fontWeight: 600, color: 'var(--lx-fg3)', textTransform: 'uppercase', letterSpacing: '0.04em' }}>
              Block Type
            </label>
            <select
              id="block-type-select"
              value={currentType}
              onChange={(e) => handleTypeChange(Number(e.target.value) as ContentBlockTypeValue)}
              style={{ ...inputStyle, cursor: 'pointer' }}
            >
              <option value={CONTENT_BLOCK_TYPE.Text}>{strings.blockPickerText}</option>
              <option value={CONTENT_BLOCK_TYPE.Image}>{strings.blockPickerImage}</option>
              <option value={CONTENT_BLOCK_TYPE.Video}>{strings.blockPickerVideo}</option>
              <option value={CONTENT_BLOCK_TYPE.Callout}>{strings.blockPickerCallout}</option>
            </select>
            {typeChanged && (
              <div style={{
                padding: '10px 12px', borderRadius: 8,
                backgroundColor: 'rgba(245,158,11,0.08)',
                border: '1px solid rgba(245,158,11,0.2)',
                display: 'flex', flexDirection: 'row', gap: 8, alignItems: 'flex-start',
              }}>
                <AlertTriangleIcon />
                <span style={{ fontSize: 13, color: 'var(--lx-fg2)' }}>{strings.blockFormTypeChangeWarning}</span>
              </div>
            )}
          </div>
        )}

        {/* Per-type fields */}
        <form onSubmit={handleSubmit} noValidate style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          {renderFields()}
          {fieldErrors.payload && <span role="alert" style={{ fontSize: 12, color: '#EF4444' }}>{fieldErrors.payload}</span>}
          {serverError && <AdminErrorBanner variant="error" message={serverError} />}

          {/* Actions */}
          <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-end', gap: 12, marginTop: 8 }}>
            <button type="button" onClick={onClose} data-testid="block-form-cancel"
              disabled={isPending}
              style={{
                height: 36, paddingLeft: 16, paddingRight: 16, borderRadius: 16,
                border: '1px solid rgba(255,255,255,0.16)', backgroundColor: 'transparent',
                color: 'var(--lx-fg2)', fontSize: 14, cursor: isPending ? 'not-allowed' : 'pointer',
                fontFamily: 'inherit', opacity: isPending ? 0.5 : 1,
              }}>
              {strings.blockFormCancelBtn}
            </button>
            <button type="submit" data-testid="block-form-save"
              aria-busy={isPending} disabled={isPending}
              style={{
                height: 40, paddingLeft: 20, paddingRight: 20, borderRadius: 16,
                backgroundColor: '#4F46E5', border: 'none',
                color: '#F8FAFC', fontSize: 14, fontWeight: 600,
                cursor: isPending ? 'not-allowed' : 'pointer', fontFamily: 'inherit',
                opacity: isPending ? 0.7 : 1,
              }}>
              {isPending ? '…' : isEdit ? strings.blockFormSaveConfirm : strings.blockFormAddConfirm}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
