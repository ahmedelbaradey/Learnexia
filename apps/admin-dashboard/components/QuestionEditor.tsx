'use client';

/**
 * QuestionEditor — full-page slide-in modal for creating/editing questions.
 * Design Spec §S5 — P7-04.
 *
 * CorrectAnswer/Options jsonb round-trip contract (LOAD-BEARING):
 *   MCQ:
 *     encode: options = JSON.stringify(string[])
 *             correctAnswer = the selected string literal (NO JSON.stringify)
 *     decode: options = JSON.parse(raw) as string[]
 *             correctAnswer = raw (DO NOT JSON.parse)
 *   TrueFalse:
 *     encode: options = JSON.stringify(["true","false"]) always (lowercase — contract)
 *             correctAnswer = "true" | "false" (raw scalar lowercase — NO JSON.stringify)
 *     decode: options = ignored (fixed set)
 *             correctAnswer = raw — normalised to lowercase before comparison
 *   FillInBlank:
 *     encode: options = JSON.stringify([]) always
 *             correctAnswer = the typed answer string (raw scalar — NO JSON.stringify)
 *     decode: options = ignored (always empty)
 *             correctAnswer = raw (DO NOT JSON.parse)
 *   Matching:
 *     encode: options = JSON.stringify({left:[{id,text}],right:[{id,text}]})
 *             correctAnswer = JSON.stringify({pairs:[{leftId,rightId}]})
 *     decode: options = JSON.parse(raw) → MatchingOptions
 *             correctAnswer = JSON.parse(raw) → MatchingAnswer
 *     BOTH sides must parse with try/catch; show error banner on parse failure.
 */

import { useState, useId, useCallback } from 'react';
import {
  useAddQuestion,
  useEditQuestion,
  type EditQuestionInput,
} from '@learnexia/api-client';
import type { AddQuestionPayload } from '@learnexia/api-client';
import type { AdminQuestionDto } from '@learnexia/api-client';
import {
  QUESTION_TYPE,
  DIFFICULTY_LEVEL,
  GENERATED_BY,
  type QuestionTypeValue,
  type DifficultyLevelValue,
} from '@learnexia/shared/constants';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { AdminErrorBanner } from './AdminErrorBanner';

const strings = getStrings(ADMIN_LOCALE);

// ── Types ─────────────────────────────────────────────────────────────────────

interface MatchingItem { id: string; text: string; }
interface MatchingOptions { left: MatchingItem[]; right: MatchingItem[]; }
interface MatchingPair { leftId: string; rightId: string; }
interface MatchingAnswer { pairs: MatchingPair[]; }

// Internal form shape per type
interface McqForm {
  options: string[];
  correctIndex: number; // index into options[]
}
interface TrueFalseForm {
  correct: 'True' | 'False' | '';
}
interface FillInBlankForm {
  answer: string;
}
interface MatchingForm {
  left: MatchingItem[];
  right: MatchingItem[];
  pairs: MatchingPair[]; // leftId → rightId mapping
}

// ── Encode helpers ─────────────────────────────────────────────────────────────

function encodeMcq(form: McqForm): { options: string; correctAnswer: string } {
  const selectedOption = form.options[form.correctIndex] ?? '';
  return {
    options: JSON.stringify(form.options),
    correctAnswer: selectedOption, // raw scalar — DO NOT JSON.stringify
  };
}

function encodeTrueFalse(form: TrueFalseForm): { options: string; correctAnswer: string } {
  return {
    options: JSON.stringify(['true', 'false']), // lowercase — authoritative contract
    correctAnswer: (form.correct as string).toLowerCase(), // raw scalar lowercase — DO NOT JSON.stringify
  };
}

function encodeFillInBlank(form: FillInBlankForm): { options: string; correctAnswer: string } {
  return {
    options: JSON.stringify([]),
    correctAnswer: form.answer, // raw scalar — DO NOT JSON.stringify
  };
}

function encodeMatching(form: MatchingForm): { options: string; correctAnswer: string } {
  const optObj: MatchingOptions = { left: form.left, right: form.right };
  const ansObj: MatchingAnswer = { pairs: form.pairs };
  return {
    options: JSON.stringify(optObj),
    correctAnswer: JSON.stringify(ansObj),
  };
}

// ── Decode helpers ─────────────────────────────────────────────────────────────

function decodeMcq(dto: AdminQuestionDto): McqForm | null {
  try {
    const opts = JSON.parse(dto.options) as string[];
    const idx = opts.indexOf(dto.correctAnswer); // correctAnswer is raw scalar
    return { options: opts, correctIndex: idx >= 0 ? idx : 0 };
  } catch {
    return null;
  }
}

function decodeTrueFalse(dto: AdminQuestionDto): TrueFalseForm {
  // correctAnswer is raw scalar — normalise to title-case for internal form state.
  // Accepts both legacy "True"/"False" and the new lowercase "true"/"false" contract.
  const val = dto.correctAnswer.toLowerCase();
  if (val === 'true') return { correct: 'True' };
  if (val === 'false') return { correct: 'False' };
  return { correct: '' };
}

function decodeFillInBlank(dto: AdminQuestionDto): FillInBlankForm {
  return { answer: dto.correctAnswer }; // raw scalar
}

function decodeMatching(dto: AdminQuestionDto): MatchingForm | null {
  try {
    const opts = JSON.parse(dto.options) as MatchingOptions;
    const ans = JSON.parse(dto.correctAnswer) as MatchingAnswer;
    return {
      left: Array.isArray(opts.left) ? opts.left : [],
      right: Array.isArray(opts.right) ? opts.right : [],
      pairs: Array.isArray(ans.pairs) ? ans.pairs : [],
    };
  } catch {
    return null;
  }
}

// ── Validation ─────────────────────────────────────────────────────────────────

function validateMcq(form: McqForm, questionText: string): string[] {
  const errs: string[] = [];
  if (!questionText.trim()) errs.push(strings.questionErrTextRequired);
  if (questionText.length > 4096) errs.push(strings.questionErrTextTooLong);
  if (form.options.length < 2) errs.push(strings.questionErrMcqMinOptions);
  if (form.options.some((o) => !o.trim())) errs.push(strings.questionErrMcqEmptyOptions);
  if (form.correctIndex < 0 || form.correctIndex >= form.options.length) errs.push(strings.questionErrMcqNoCorrect);
  return errs;
}

function validateTrueFalse(form: TrueFalseForm, questionText: string): string[] {
  const errs: string[] = [];
  if (!questionText.trim()) errs.push(strings.questionErrTextRequired);
  if (questionText.length > 4096) errs.push(strings.questionErrTextTooLong);
  if (!form.correct) errs.push(strings.questionErrTfNoChoice);
  return errs;
}

function validateFillInBlank(form: FillInBlankForm, questionText: string): string[] {
  const errs: string[] = [];
  if (!questionText.trim()) errs.push(strings.questionErrTextRequired);
  if (questionText.length > 4096) errs.push(strings.questionErrTextTooLong);
  if (!form.answer.trim()) errs.push(strings.questionErrFibEmpty);
  return errs;
}

function validateMatching(form: MatchingForm, questionText: string): string[] {
  const errs: string[] = [];
  if (!questionText.trim()) errs.push(strings.questionErrTextRequired);
  if (questionText.length > 4096) errs.push(strings.questionErrTextTooLong);
  if (form.left.length < 1) errs.push(strings.questionErrMatchMinLeft);
  if (form.right.length < 1) errs.push(strings.questionErrMatchMinRight);
  if (form.left.length !== form.right.length) errs.push(strings.questionErrMatchEqualCount);
  if (form.left.some((l) => !l.text.trim())) errs.push(strings.questionErrMatchEmptyLeft);
  if (form.right.some((r) => !r.text.trim())) errs.push(strings.questionErrMatchEmptyRight);
  const pairedLeftIds = form.pairs.map((p) => p.leftId);
  if (pairedLeftIds.length < form.left.length || form.left.some((l) => !pairedLeftIds.includes(l.id))) {
    errs.push(strings.questionErrMatchAllPaired);
  }
  const rightIds = form.pairs.map((p) => p.rightId).filter(Boolean);
  if (new Set(rightIds).size !== rightIds.length) errs.push(strings.questionErrMatchDuplicatePair);
  return errs;
}

// ── Counter helper for unique matching IDs ────────────────────────────────────
let _matchId = 0;
function nextMatchId() { return `m${++_matchId}`; }

// ── Icons ─────────────────────────────────────────────────────────────────────
function XIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  );
}
function PlusIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}
function TrashIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="3 6 5 6 21 6" /><path d="M19 6l-1 14H6L5 6" />
      <path d="M10 11v6" /><path d="M14 11v6" /><path d="M9 6V4h6v2" />
    </svg>
  );
}

// ── Sub-form: MCQ ─────────────────────────────────────────────────────────────
function McqSubForm({ form, onChange }: { form: McqForm; onChange: (f: McqForm) => void }) {
  const baseId = useId();
  const updateOption = (idx: number, val: string) => {
    const opts = [...form.options];
    opts[idx] = val;
    onChange({ ...form, options: opts });
  };
  const addOption = () => onChange({ ...form, options: [...form.options, ''] });
  const removeOption = (idx: number) => {
    const opts = form.options.filter((_, i) => i !== idx);
    const correct = form.correctIndex >= opts.length ? Math.max(0, opts.length - 1) : form.correctIndex;
    onChange({ ...form, options: opts, correctIndex: correct });
  };

  return (
    <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
      <legend style={{ fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)', marginBottom: 8, padding: 0 }}>
        {strings.questionMcqOptionsLegend}
      </legend>
    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
      {form.options.map((opt, idx) => {
        const radioId = `${baseId}-opt-${idx}`;
        const label = strings.questionMcqOptionPlaceholder.replace('{N}', String(idx + 1));
        const removeLabel = strings.questionMcqRemoveAriaLabel.replace('{N}', String(idx + 1));
        return (
          <div key={idx} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <input
              type="radio"
              id={radioId}
              name={`${baseId}-correct`}
              aria-label={strings.questionMcqCorrectRadioAriaLabel.replace('{N}', String(idx + 1))}
              checked={form.correctIndex === idx}
              onChange={() => onChange({ ...form, correctIndex: idx })}
              data-testid={`mcq-correct-radio-${idx}`}
              style={{ flexShrink: 0 }}
            />
            <input
              type="text"
              dir="auto"
              placeholder={label}
              value={opt}
              onChange={(e) => updateOption(idx, e.target.value)}
              data-testid={`mcq-option-input-${idx}`}
              style={{
                flex: 1, height: 38, borderRadius: 10, border: '1px solid var(--lx-border)',
                padding: '0 12px', fontSize: 14, color: 'var(--lx-fg1)',
                backgroundColor: 'var(--lx-surface)', fontFamily: 'inherit',
              }}
            />
            {form.options.length > 2 && (
              <button
                type="button"
                aria-label={removeLabel}
                data-testid={`mcq-remove-option-${idx}`}
                onClick={() => removeOption(idx)}
                style={{
                  width: 28, height: 28, border: 'none', borderRadius: 8,
                  backgroundColor: 'rgba(239,68,68,0.12)', color: '#EF4444',
                  cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <TrashIcon />
              </button>
            )}
          </div>
        );
      })}
      <div style={{ paddingTop: 4 }}>
        <p style={{ margin: '0 0 8px', fontSize: 12, color: 'var(--lx-fg3)' }}>
          {strings.questionMcqCorrectSummary} {form.options[form.correctIndex] || '—'}
        </p>
        <button
          type="button"
          data-testid="mcq-add-option"
          onClick={addOption}
          style={{
            height: 32, paddingLeft: 12, paddingRight: 12, borderRadius: 8,
            border: '1px dashed var(--lx-border)', backgroundColor: 'transparent',
            color: 'var(--lx-fg2)', fontSize: 13, cursor: 'pointer',
            display: 'inline-flex', alignItems: 'center', gap: 5, fontFamily: 'inherit',
          }}
        >
          <PlusIcon />
          {strings.questionMcqAddOption}
        </button>
      </div>
    </div>
    </fieldset>
  );
}

// ── Sub-form: TrueFalse ───────────────────────────────────────────────────────
function TrueFalseSubForm({ form, onChange }: { form: TrueFalseForm; onChange: (f: TrueFalseForm) => void }) {
  const baseId = useId();
  return (
    <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
      <legend style={{ fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)', marginBottom: 8, padding: 0 }}>
        {strings.questionTfCorrectLabel}
      </legend>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
        {(['True', 'False'] as const).map((val) => {
          const radioId = `${baseId}-tf-${val}`;
          const label = val === 'True' ? strings.questionTfTrueLabel : strings.questionTfFalseLabel;
          return (
            <label key={val} htmlFor={radioId} style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 14, color: 'var(--lx-fg1)' }}>
              <input
                type="radio"
                id={radioId}
                name={`${baseId}-tf`}
                value={val}
                checked={form.correct === val}
                onChange={() => onChange({ correct: val })}
                data-testid={`tf-option-${val.toLowerCase()}`}
              />
              {label}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

// ── Sub-form: FillInBlank ─────────────────────────────────────────────────────
function FillInBlankSubForm({ form, onChange }: { form: FillInBlankForm; onChange: (f: FillInBlankForm) => void }) {
  const inputId = useId();
  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
      <label htmlFor={inputId} style={{ fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)' }}>
        {strings.questionFibCorrectLabel}
      </label>
      <input
        id={inputId}
        type="text"
        dir="auto"
        placeholder={strings.questionFibPlaceholder}
        value={form.answer}
        onChange={(e) => onChange({ answer: e.target.value })}
        data-testid="fib-answer-input"
        style={{
          height: 40, borderRadius: 10, border: '1px solid var(--lx-border)',
          padding: '0 12px', fontSize: 14, color: 'var(--lx-fg1)',
          backgroundColor: 'var(--lx-surface)', fontFamily: 'inherit',
        }}
      />
      <p style={{ margin: 0, fontSize: 12, color: 'var(--lx-fg3)' }}>{strings.questionFibHint}</p>
    </div>
  );
}

// ── Sub-form: Matching ────────────────────────────────────────────────────────
function MatchingSubForm({ form, onChange }: { form: MatchingForm; onChange: (f: MatchingForm) => void }) {
  const addLeft = () => {
    const id = nextMatchId();
    const left = [...form.left, { id, text: '' }];
    // Auto-add a pair slot for this new left item
    const pairs = [...form.pairs, { leftId: id, rightId: '' }];
    onChange({ ...form, left, pairs });
  };

  const addRight = () => {
    const id = nextMatchId();
    onChange({ ...form, right: [...form.right, { id, text: '' }] });
  };

  const updateLeft = (idx: number, text: string) => {
    const left = form.left.map((l, i) => i === idx ? { ...l, text } : l);
    onChange({ ...form, left });
  };

  const updateRight = (idx: number, text: string) => {
    const right = form.right.map((r, i) => i === idx ? { ...r, text } : r);
    onChange({ ...form, right });
  };

  const removeLeft = (idx: number) => {
    const removed = form.left[idx];
    const left = form.left.filter((_, i) => i !== idx);
    const pairs = form.pairs.filter((p) => p.leftId !== removed?.id);
    onChange({ ...form, left, pairs });
  };

  const removeRight = (idx: number) => {
    const removed = form.right[idx];
    const right = form.right.filter((_, i) => i !== idx);
    // Clear any pair referencing this right item
    const pairs = form.pairs.map((p) => p.rightId === removed?.id ? { ...p, rightId: '' } : p);
    onChange({ ...form, right, pairs });
  };

  const setPairRight = (leftId: string, rightId: string) => {
    // If this leftId already has a pair, update it; otherwise add
    const exists = form.pairs.findIndex((p) => p.leftId === leftId);
    const pairs = exists >= 0
      ? form.pairs.map((p) => p.leftId === leftId ? { ...p, rightId } : p)
      : [...form.pairs, { leftId, rightId }];
    onChange({ ...form, pairs });
  };

  const colStyle: React.CSSProperties = { flex: 1, display: 'flex', flexDirection: 'column', gap: 8 };
  const itemInputStyle: React.CSSProperties = {
    flex: 1, height: 36, borderRadius: 8, border: '1px solid var(--lx-border)',
    padding: '0 10px', fontSize: 13, color: 'var(--lx-fg1)',
    backgroundColor: 'var(--lx-surface)', fontFamily: 'inherit',
  };

  return (
    <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
      <legend style={{ fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)', marginBottom: 8, padding: 0 }}>
        {strings.questionMatchConfigLegend}
      </legend>
    <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
      {/* Left + Right columns */}
      <div style={{ display: 'flex', gap: 16 }}>
        {/* Left column */}
        <div style={colStyle}>
          <p style={{ margin: 0, fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)' }}>
            {strings.questionMatchLeftHeader}
          </p>
          {form.left.map((item, idx) => (
            <div key={item.id} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              <input
                type="text"
                dir="auto"
                placeholder={strings.questionMatchLeftPlaceholder.replace('{N}', String(idx + 1))}
                value={item.text}
                onChange={(e) => updateLeft(idx, e.target.value)}
                data-testid={`match-left-input-${idx}`}
                style={itemInputStyle}
              />
              <button
                type="button"
                aria-label={strings.questionMatchRemoveLeftAriaLabel.replace('{N}', String(idx + 1))}
                data-testid={`match-remove-left-${idx}`}
                onClick={() => removeLeft(idx)}
                style={{
                  width: 26, height: 26, border: 'none', borderRadius: 6,
                  backgroundColor: 'rgba(239,68,68,0.12)', color: '#EF4444',
                  cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <TrashIcon />
              </button>
            </div>
          ))}
          <button
            type="button"
            data-testid="match-add-left"
            onClick={addLeft}
            style={{
              height: 30, paddingLeft: 10, paddingRight: 10, borderRadius: 8,
              border: '1px dashed var(--lx-border)', backgroundColor: 'transparent',
              color: 'var(--lx-fg3)', fontSize: 12, cursor: 'pointer',
              display: 'inline-flex', alignItems: 'center', gap: 4, fontFamily: 'inherit',
            }}
          >
            <PlusIcon />{strings.questionMatchAddLeft}
          </button>
        </div>

        {/* Right column */}
        <div style={colStyle}>
          <p style={{ margin: 0, fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)' }}>
            {strings.questionMatchRightHeader}
          </p>
          <p style={{ margin: '-4px 0 0', fontSize: 11, color: 'var(--lx-fg3)' }}>
            {strings.questionMatchRightHint}
          </p>
          {form.right.map((item, idx) => (
            <div key={item.id} style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
              <input
                type="text"
                dir="auto"
                placeholder={strings.questionMatchRightPlaceholder.replace('{N}', String(idx + 1))}
                value={item.text}
                onChange={(e) => updateRight(idx, e.target.value)}
                data-testid={`match-right-input-${idx}`}
                style={itemInputStyle}
              />
              <button
                type="button"
                aria-label={strings.questionMatchRemoveRightAriaLabel.replace('{N}', String(idx + 1))}
                data-testid={`match-remove-right-${idx}`}
                onClick={() => removeRight(idx)}
                style={{
                  width: 26, height: 26, border: 'none', borderRadius: 6,
                  backgroundColor: 'rgba(239,68,68,0.12)', color: '#EF4444',
                  cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
                  flexShrink: 0,
                }}
              >
                <TrashIcon />
              </button>
            </div>
          ))}
          <button
            type="button"
            data-testid="match-add-right"
            onClick={addRight}
            style={{
              height: 30, paddingLeft: 10, paddingRight: 10, borderRadius: 8,
              border: '1px dashed var(--lx-border)', backgroundColor: 'transparent',
              color: 'var(--lx-fg3)', fontSize: 12, cursor: 'pointer',
              display: 'inline-flex', alignItems: 'center', gap: 4, fontFamily: 'inherit',
            }}
          >
            <PlusIcon />{strings.questionMatchAddRight}
          </button>
        </div>
      </div>

      {/* Pairs section */}
      {form.left.length > 0 && form.right.length > 0 && (
        <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
          <legend style={{ fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)', marginBottom: 4, padding: 0 }}>
            {strings.questionMatchPairsHeader}
          </legend>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <p style={{ margin: '0 0 4px', fontSize: 11, color: 'var(--lx-fg3)' }}>
              {strings.questionMatchPairsHint}
            </p>
            {form.left.map((leftItem, idx) => {
              const pair = form.pairs.find((p) => p.leftId === leftItem.id);
              const selectLabel = strings.questionMatchPairSelectAriaLabel.replace('{text}', leftItem.text || String(idx + 1));
              return (
                <div key={leftItem.id} style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                  <span style={{ flex: 1, fontSize: 13, color: 'var(--lx-fg1)', fontWeight: 500 }}>
                    {leftItem.text || `${idx + 1}`}
                  </span>
                  <span style={{ fontSize: 16, color: 'var(--lx-fg3)' }} aria-hidden="true">→</span>
                  <select
                    aria-label={selectLabel}
                    value={pair?.rightId ?? ''}
                    onChange={(e) => setPairRight(leftItem.id, e.target.value)}
                    data-testid={`match-pair-select-${idx}`}
                    style={{
                      flex: 1, height: 36, borderRadius: 8, border: '1px solid var(--lx-border)',
                      padding: '0 10px', fontSize: 13, color: 'var(--lx-fg1)',
                      backgroundColor: 'var(--lx-surface)', fontFamily: 'inherit',
                    }}
                  >
                    <option value="">{strings.questionMatchSelectPlaceholder}</option>
                    {form.right.map((r) => (
                      <option key={r.id} value={r.id}>{r.text || r.id}</option>
                    ))}
                  </select>
                </div>
              );
            })}
          </div>
        </fieldset>
      )}
    </div>
    </fieldset>
  );
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface QuestionEditorProps {
  /** Open state — controlled by parent */
  open: boolean;
  onClose: () => void;
  /** Lesson this question belongs to */
  lessonId: number;
  /** If provided, editing existing question; otherwise creating */
  editQuestion?: AdminQuestionDto;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function QuestionEditor({ open, onClose, lessonId, editQuestion }: QuestionEditorProps) {
  const isEdit = Boolean(editQuestion);

  // Shared fields
  const [questionType, setQuestionType] = useState<QuestionTypeValue | ''>(
    editQuestion ? editQuestion.questionType : ''
  );
  const [questionText, setQuestionText] = useState(editQuestion?.questionText ?? '');
  const [difficulty, setDifficulty] = useState<DifficultyLevelValue | ''>(
    editQuestion ? editQuestion.difficulty : ''
  );

  // Sub-form states — initialised from editQuestion if editing
  const [mcqForm, setMcqForm] = useState<McqForm>(() => {
    if (editQuestion?.questionType === QUESTION_TYPE.MCQ) {
      return decodeMcq(editQuestion) ?? { options: ['', ''], correctIndex: 0 };
    }
    return { options: ['', ''], correctIndex: 0 };
  });
  const [tfForm, setTfForm] = useState<TrueFalseForm>(() => {
    if (editQuestion?.questionType === QUESTION_TYPE.TrueFalse) {
      return decodeTrueFalse(editQuestion);
    }
    return { correct: '' };
  });
  const [fibForm, setFibForm] = useState<FillInBlankForm>(() => {
    if (editQuestion?.questionType === QUESTION_TYPE.FillInBlank) {
      return decodeFillInBlank(editQuestion);
    }
    return { answer: '' };
  });
  const [matchForm, setMatchForm] = useState<MatchingForm>(() => {
    if (editQuestion?.questionType === QUESTION_TYPE.Matching) {
      return decodeMatching(editQuestion) ?? { left: [], right: [], pairs: [] };
    }
    return { left: [], right: [], pairs: [] };
  });
  // matchParseError is computed once on mount from the editQuestion (read-only flag).
  // It cannot change during the lifetime of this editor instance (type is locked on edit).
  const matchParseError: boolean =
    editQuestion?.questionType === QUESTION_TYPE.Matching
      ? decodeMatching(editQuestion) === null
      : false;

  const [errors, setErrors] = useState<string[]>([]);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const addMutation = useAddQuestion();
  const editMutation = useEditQuestion();
  const isPending = addMutation.isPending || editMutation.isPending;

  // ── ID refs for accessibility ──────────────────────────────────────────────
  const typeId = useId();
  const textId = useId();
  const diffId = useId();

  // ── Validate + encode + submit ─────────────────────────────────────────────
  const handleSubmit = useCallback(async () => {
    if (!questionType) {
      setErrors([strings.questionErrTypeRequired]);
      return;
    }
    if (!difficulty) {
      setErrors([strings.questionErrDiffRequired]);
      return;
    }
    const qt = questionType as QuestionTypeValue;
    const diff = difficulty as DifficultyLevelValue;

    let errs: string[] = [];
    let encoded: { options: string; correctAnswer: string } | null = null;

    if (qt === QUESTION_TYPE.MCQ) {
      errs = validateMcq(mcqForm, questionText);
      if (!errs.length) encoded = encodeMcq(mcqForm);
    } else if (qt === QUESTION_TYPE.TrueFalse) {
      errs = validateTrueFalse(tfForm, questionText);
      if (!errs.length) encoded = encodeTrueFalse(tfForm);
    } else if (qt === QUESTION_TYPE.FillInBlank) {
      errs = validateFillInBlank(fibForm, questionText);
      if (!errs.length) encoded = encodeFillInBlank(fibForm);
    } else if (qt === QUESTION_TYPE.Matching) {
      errs = validateMatching(matchForm, questionText);
      if (!errs.length) encoded = encodeMatching(matchForm);
    }

    if (errs.length) { setErrors(errs); return; }
    if (!encoded) return;

    setErrors([]);
    setSubmitError(null);

    try {
      if (isEdit && editQuestion) {
        const payload: EditQuestionInput = {
          id: editQuestion.id,
          lessonId,
          questionType: qt,
          questionText,
          options: encoded.options,
          correctAnswer: encoded.correctAnswer,
          difficulty: diff,
        };
        await editMutation.mutateAsync(payload);
      } else {
        const payload: AddQuestionPayload = {
          lessonId,
          questionType: qt,
          questionText,
          options: encoded.options,
          correctAnswer: encoded.correctAnswer,
          difficulty: diff,
          generatedBy: GENERATED_BY.Curated,
        };
        await addMutation.mutateAsync(payload);
      }
      onClose();
    } catch (err) {
      setSubmitError((err as Error).message || strings.curriculumNetworkError);
    }
  }, [
    questionType, difficulty, questionText,
    mcqForm, tfForm, fibForm, matchForm,
    isEdit, editQuestion, lessonId,
    addMutation, editMutation, onClose,
  ]);

  if (!open) return null;

  const title = isEdit ? strings.questionEditorEditTitle : strings.questionEditorCreateTitle;
  const subtitle = isEdit ? strings.questionEditorEditSubtitle : strings.questionEditorCreateSubtitle;
  const saveLabel = isEdit ? strings.questionEditorSaveChangesBtn : strings.questionEditorSaveBtn;

  // Overlay + sheet styles
  const overlayStyle: React.CSSProperties = {
    position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)',
    zIndex: 800, display: 'flex', alignItems: 'flex-start', justifyContent: 'flex-end',
  };
  const sheetStyle: React.CSSProperties = {
    width: '100%', maxWidth: 560, height: '100dvh', overflowY: 'auto',
    backgroundColor: 'var(--lx-surface)', display: 'flex', flexDirection: 'column',
    boxShadow: '-4px 0 40px rgba(0,0,0,0.25)',
  };

  const fieldLabelStyle: React.CSSProperties = {
    fontSize: 13, fontWeight: 600, color: 'var(--lx-fg2)', marginBottom: 6, display: 'block',
  };
  const inputStyle: React.CSSProperties = {
    width: '100%', height: 40, borderRadius: 10, border: '1px solid var(--lx-border)',
    padding: '0 12px', fontSize: 14, color: 'var(--lx-fg1)',
    backgroundColor: 'var(--lx-card)', fontFamily: 'inherit', boxSizing: 'border-box',
  };
  const selectStyle: React.CSSProperties = { ...inputStyle, cursor: 'pointer' };
  const sectionStyle: React.CSSProperties = {
    padding: '0 24px 20px', display: 'flex', flexDirection: 'column', gap: 16,
  };
  const dividerStyle: React.CSSProperties = {
    borderTop: '1px solid var(--lx-border)', margin: '0 24px',
  };

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={title}
      data-testid="question-editor-dialog"
      style={overlayStyle}
      onClick={(e) => { if (e.target === e.currentTarget) onClose(); }}
    >
      <div style={sheetStyle}>
        {/* Header */}
        <div style={{
          padding: '20px 24px 16px', borderBottom: '1px solid var(--lx-border)',
          display: 'flex', alignItems: 'flex-start', gap: 12, flexShrink: 0,
        }}>
          <div style={{ flex: 1 }}>
            <h2 style={{ margin: 0, fontSize: 18, fontWeight: 700, color: 'var(--lx-fg1)' }}>{title}</h2>
            <p style={{ margin: '4px 0 0', fontSize: 13, color: 'var(--lx-fg3)' }}>{subtitle}</p>
          </div>
          <button
            type="button"
            aria-label={strings.questionEditorCloseAriaLabel}
            data-testid="question-editor-close"
            onClick={onClose}
            style={{
              width: 36, height: 36, border: 'none', borderRadius: 10,
              backgroundColor: 'var(--lx-card)', color: 'var(--lx-fg2)',
              cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
              flexShrink: 0,
            }}
          >
            <XIcon />
          </button>
        </div>

        {/* Body */}
        <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: 0 }}>
          {/* Error banners */}
          {(errors.length > 0 || submitError) && (
            <div style={{ padding: '16px 24px 0', display: 'flex', flexDirection: 'column', gap: 8 }}>
              {errors.map((e) => (
                <AdminErrorBanner key={e} variant="error" message={e} />
              ))}
              {submitError && <AdminErrorBanner variant="error" message={submitError} />}
            </div>
          )}

          {/* Matching parse error */}
          {matchParseError && (
            <div style={{ padding: '16px 24px 0' }}>
              <AdminErrorBanner variant="error" message={strings.questionMatchParseError} />
            </div>
          )}

          {/* Section: shared fields */}
          <div style={{ ...sectionStyle, paddingTop: 20 }}>
            {/* Question Type */}
            <div>
              <label htmlFor={typeId} style={fieldLabelStyle}>
                {strings.questionFieldTypeLabel}
              </label>
              {isEdit ? (
                <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                  <div style={{
                    height: 40, borderRadius: 10, border: '1px solid var(--lx-border)',
                    padding: '0 12px', fontSize: 14, color: 'var(--lx-fg3)',
                    backgroundColor: 'var(--lx-card)', display: 'flex', alignItems: 'center',
                  }}>
                    {questionType === QUESTION_TYPE.MCQ && strings.questionTypeMcq}
                    {questionType === QUESTION_TYPE.TrueFalse && strings.questionTypeTrueFalse}
                    {questionType === QUESTION_TYPE.Matching && strings.questionTypeMatching}
                    {questionType === QUESTION_TYPE.FillInBlank && strings.questionTypeFillInBlank}
                  </div>
                  <p style={{ margin: 0, fontSize: 11, color: 'var(--lx-fg3)' }}>{strings.questionFieldTypeLockedHint}</p>
                </div>
              ) : (
                <select
                  id={typeId}
                  value={questionType}
                  onChange={(e) => {
                    const v = Number(e.target.value) as QuestionTypeValue;
                    setQuestionType(v || '');
                    setErrors([]);
                  }}
                  data-testid="question-type-select"
                  style={selectStyle}
                >
                  <option value="">{strings.questionFieldTypePlaceholder}</option>
                  <option value={QUESTION_TYPE.MCQ}>{strings.questionTypeMcq}</option>
                  <option value={QUESTION_TYPE.TrueFalse}>{strings.questionTypeTrueFalse}</option>
                  <option value={QUESTION_TYPE.Matching}>{strings.questionTypeMatching}</option>
                  <option value={QUESTION_TYPE.FillInBlank}>{strings.questionTypeFillInBlank}</option>
                </select>
              )}
            </div>

            {/* Question Text */}
            <div>
              <label htmlFor={textId} style={fieldLabelStyle}>
                {strings.questionFieldTextLabel}
              </label>
              <textarea
                id={textId}
                dir="auto"
                placeholder={strings.questionFieldTextPlaceholder}
                value={questionText}
                onChange={(e) => setQuestionText(e.target.value)}
                data-testid="question-text-input"
                rows={3}
                style={{
                  width: '100%', borderRadius: 10, border: '1px solid var(--lx-border)',
                  padding: '10px 12px', fontSize: 14, color: 'var(--lx-fg1)',
                  backgroundColor: 'var(--lx-card)', fontFamily: 'inherit',
                  resize: 'vertical', boxSizing: 'border-box', lineHeight: 1.5,
                }}
              />
            </div>

            {/* Difficulty */}
            <div>
              <label htmlFor={diffId} style={fieldLabelStyle}>
                {strings.questionFieldDiffLabel}
              </label>
              <select
                id={diffId}
                value={difficulty}
                onChange={(e) => setDifficulty(Number(e.target.value) as DifficultyLevelValue || '')}
                data-testid="question-difficulty-select"
                style={selectStyle}
              >
                <option value="">{strings.questionFieldDiffPlaceholder}</option>
                <option value={DIFFICULTY_LEVEL.Easy}>{strings.difficultyEasy}</option>
                <option value={DIFFICULTY_LEVEL.Medium}>{strings.difficultyMedium}</option>
                <option value={DIFFICULTY_LEVEL.Hard}>{strings.difficultyHard}</option>
              </select>
            </div>
          </div>

          {/* Sub-form: type-specific */}
          {questionType !== '' && (
            <>
              <div style={dividerStyle} />
              <div style={{ ...sectionStyle, paddingTop: 20 }}>
                {questionType === QUESTION_TYPE.MCQ && (
                  <McqSubForm form={mcqForm} onChange={setMcqForm} />
                )}
                {questionType === QUESTION_TYPE.TrueFalse && (
                  <TrueFalseSubForm form={tfForm} onChange={setTfForm} />
                )}
                {questionType === QUESTION_TYPE.FillInBlank && (
                  <FillInBlankSubForm form={fibForm} onChange={setFibForm} />
                )}
                {questionType === QUESTION_TYPE.Matching && !matchParseError && (
                  <MatchingSubForm form={matchForm} onChange={setMatchForm} />
                )}
              </div>
            </>
          )}

          {/* Lifecycle slot — placeholder for future lifecycleState field */}
          <div style={dividerStyle} />
          <div style={{ ...sectionStyle, paddingTop: 16 }}>
            <p style={{ margin: 0, fontSize: 12, color: 'var(--lx-fg3)' }}>
              {strings.questionLifecycleSectionLabel}
            </p>
            {/* data-slot="lifecycle" — placeholder; AdminQuestionDto lacks lifecycleState */}
            <div data-slot="lifecycle" style={{ display: 'none' }} />
          </div>
        </div>

        {/* Footer */}
        <div style={{
          borderTop: '1px solid var(--lx-border)', padding: '16px 24px',
          display: 'flex', gap: 12, justifyContent: 'flex-end', flexShrink: 0,
        }}>
          <button
            type="button"
            data-testid="question-editor-cancel"
            onClick={onClose}
            disabled={isPending}
            style={{
              height: 40, paddingLeft: 20, paddingRight: 20, borderRadius: 12,
              border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
              color: 'var(--lx-fg2)', fontSize: 14, fontWeight: 600, cursor: isPending ? 'not-allowed' : 'pointer',
              fontFamily: 'inherit',
            }}
          >
            {strings.questionEditorCancelBtn}
          </button>
          <button
            type="button"
            data-testid="question-editor-save"
            onClick={handleSubmit}
            disabled={isPending || matchParseError}
            aria-busy={isPending}
            style={{
              height: 40, paddingLeft: 24, paddingRight: 24, borderRadius: 12,
              backgroundColor: '#4F46E5', border: 'none', color: '#F8FAFC',
              fontSize: 14, fontWeight: 600,
              cursor: (isPending || matchParseError) ? 'not-allowed' : 'pointer',
              opacity: (isPending || matchParseError) ? 0.6 : 1,
              fontFamily: 'inherit',
            }}
          >
            {isPending ? '…' : saveLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
