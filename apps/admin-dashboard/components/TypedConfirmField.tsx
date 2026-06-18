'use client';

/**
 * TypedConfirmField — single-line typed-confirmation input.
 *
 * Gates a destructive action by requiring the user to type a target value
 * (typically an email address or the word "CONFIRM") before the Confirm button
 * is enabled.
 *
 * Design Spec Part A §A.4.
 *
 * Comparison:
 *   - For email targets: case-insensitive (`toLowerCase()` on both sides).
 *   - For "CONFIRM" tokens: case-SENSITIVE (the capital letters add friction).
 *     Pass `caseSensitive={true}` for this mode.
 *
 * RTL:
 *   - Target display box and input value are always `dir="ltr"` (technical
 *     strings — emails and "CONFIRM" are ASCII/Latin regardless of page dir).
 *   - Instruction text and match indicator follow the page direction.
 *
 * Props:
 *   id             — input id.
 *   instructionText — the prompt sentence (e.g. "Type the account email to confirm:").
 *   targetValue    — the expected value to type (rendered in the display box).
 *   value          — controlled input value.
 *   onChange       — controlled setter.
 *   placeholder    — input placeholder.
 *   matchLabel     — label for the "confirmed" indicator ("Confirmed" / "تم التأكيد").
 *   caseSensitive  — if true, comparison is exact; otherwise case-insensitive.
 */

import { useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

export interface TypedConfirmFieldProps {
  id: string;
  instructionText: string;
  targetValue: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  matchLabel?: string;
  caseSensitive?: boolean;
}

export function TypedConfirmField({
  id,
  instructionText,
  targetValue,
  value,
  onChange,
  placeholder,
  matchLabel = 'Confirmed',
  caseSensitive = false,
}: TypedConfirmFieldProps) {
  const isMatch = caseSensitive
    ? value.trim() === targetValue
    : value.trim().toLowerCase() === targetValue.toLowerCase();

  const hasTyped = value.trim().length > 0;

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      onChange(e.target.value);
    },
    [onChange],
  );

  return (
    <Stack flexDirection="column" gap="$2">
      {/* Instruction text — follows page direction */}
      <Text fontFamily="$body" fontSize={14} color="$fg2" style={{ lineHeight: 1.5 }}>
        {instructionText}
      </Text>

      {/* Target display box — always LTR (ASCII/Latin technical string) */}
      <div
        dir="ltr"
        data-testid="typed-confirm-target"
        style={{
          fontFamily: 'var(--lx-font-mono, monospace)',
          fontSize: 13,
          color: 'var(--lx-fg1)',
          backgroundColor: 'var(--lx-bg)',
          padding: '8px 12px',
          borderRadius: 'var(--lx-radius-sm)',
          border: '1px solid var(--lx-border)',
          userSelect: 'all',
          wordBreak: 'break-all',
        }}
      >
        {targetValue}
      </div>

      {/* Input — dir="ltr" because the value typed must match a technical string */}
      <div>
        <label
          htmlFor={id}
          className="sr-only"
        >
          {instructionText}
        </label>
        <input
          id={id}
          type="text"
          dir="ltr"
          data-testid="typed-confirm-input"
          value={value}
          onChange={handleChange}
          placeholder={placeholder}
          autoComplete="off"
          autoCorrect="off"
          autoCapitalize="off"
          spellCheck={false}
          aria-invalid={hasTyped && !isMatch}
          aria-describedby={`${id}-match`}
          style={{
            display: 'block',
            width: '100%',
            height: 44,
            backgroundColor: 'var(--lx-bg)',
            borderRadius: 'var(--lx-radius-sm)',
            border: `1px solid ${hasTyped && !isMatch ? 'var(--lx-danger)' : 'var(--lx-border)'}`,
            paddingInline: 16,
            color: 'var(--lx-fg1)',
            fontSize: 14,
            fontFamily: 'inherit',
            outline: 'none',
            transition:
              'border-color var(--lx-dur-fast) var(--lx-ease-out), box-shadow var(--lx-dur-fast) var(--lx-ease-out)',
            boxSizing: 'border-box',
          }}
          onFocus={(e) => {
            e.currentTarget.style.borderColor = 'var(--lx-primary)';
            e.currentTarget.style.boxShadow = 'var(--lx-focus-ring-admin)';
          }}
          onBlur={(e) => {
            const errored = hasTyped && !isMatch;
            e.currentTarget.style.borderColor = errored ? 'var(--lx-danger)' : 'var(--lx-border)';
            e.currentTarget.style.boxShadow = errored ? 'var(--lx-focus-ring-error)' : 'none';
          }}
        />
      </div>

      {/* Match indicator — only when there is a match */}
      <div id={`${id}-match`} aria-live="polite" data-testid="typed-confirm-match">
        {isMatch && (
          <Text
            fontFamily="$body"
            fontSize={12}
            style={{ color: 'var(--lx-secondary, #22C55E)' }}
          >
            {matchLabel}
          </Text>
        )}
        {hasTyped && !isMatch && (
          <Text
            fontFamily="$body"
            fontSize={12}
            style={{ color: 'var(--lx-danger)' }}
            aria-live="polite"
          >
            {/* Non-matching state: no text shown — the red border is the signal */}
          </Text>
        )}
      </div>
    </Stack>
  );
}
