'use client';

/**
 * ReasonField — labeled textarea with char counter and error state.
 *
 * Used inside Suspend, Delete, and Grade Override dialogs.
 * Design Spec Part A §A.3.
 *
 * Props:
 *   id          — input id (associate with <label>); caller ensures uniqueness.
 *   label       — visible label text (uppercase micro-label style).
 *   required    — whether the field shows a required marker (*).
 *   value       — controlled value.
 *   onChange    — controlled setter.
 *   maxLength   — char limit (default 500).
 *   error       — optional error message.
 *   disabled    — whether the field is disabled.
 */

import { useCallback } from 'react';
import { Stack, Text } from '@tamagui/core';

const DEFAULT_MAX_LENGTH = 500;
const NEAR_LIMIT_THRESHOLD = 450;

export interface ReasonFieldProps {
  id: string;
  label: string;
  required?: boolean;
  value: string;
  onChange: (value: string) => void;
  maxLength?: number;
  error?: string;
  disabled?: boolean;
  placeholder?: string;
  /** Optional stable test hook applied to the outermost Stack. */
  testId?: string;
}

export function ReasonField({
  id,
  label,
  required = false,
  value,
  onChange,
  maxLength = DEFAULT_MAX_LENGTH,
  error,
  disabled = false,
  placeholder,
  testId,
}: ReasonFieldProps) {
  const charCount = value.length;
  const isNearLimit = charCount >= NEAR_LIMIT_THRESHOLD;
  const isOverLimit = charCount > maxLength;
  const hasError = Boolean(error) || isOverLimit;

  // Counter colour: normal → $fg3, near limit → amber, over → danger
  const counterColor = isOverLimit
    ? 'var(--lx-danger)'
    : isNearLimit
      ? 'var(--lx-accent)'
      : 'var(--lx-fg3)';

  // Border colour: focus and error are handled via CSS via className
  const getBorderColor = () => {
    if (hasError) return 'var(--lx-danger)';
    return 'var(--lx-border)';
  };

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLTextAreaElement>) => {
      onChange(e.target.value);
    },
    [onChange],
  );

  return (
    <Stack flexDirection="column" gap="$1" data-testid={testId ?? 'reason-field'}>
      {/* Label row */}
      <Stack flexDirection="row" justifyContent="space-between" alignItems="baseline">
        <label
          htmlFor={id}
          style={{
            fontSize: 12,
            fontWeight: 600,
            color: 'var(--lx-fg3)',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            fontFamily: 'inherit',
          }}
        >
          {label}
          {required && (
            <span
              aria-hidden="true"
              style={{ color: 'var(--lx-danger)', marginInlineStart: 4 }}
            >
              *
            </span>
          )}
          {required && (
            <span className="sr-only"> (required)</span>
          )}
        </label>
      </Stack>

      {/* Textarea wrapper */}
      <div
        style={{
          backgroundColor: 'var(--lx-bg)',
          borderRadius: 'var(--lx-radius-sm)',
          border: `1px solid ${getBorderColor()}`,
          padding: 12,
          minHeight: 96,
          display: 'flex',
          flexDirection: 'column',
          transition: 'border-color var(--lx-dur-fast) var(--lx-ease-out), box-shadow var(--lx-dur-fast) var(--lx-ease-out)',
        }}
      >
        <textarea
          id={id}
          value={value}
          onChange={handleChange}
          disabled={disabled}
          placeholder={placeholder}
          maxLength={maxLength + 50} // soft-cap; hard visual limit via counter
          aria-required={required}
          aria-invalid={hasError || undefined}
          aria-describedby={`${id}-counter${error ? ` ${id}-error` : ''}`}
          style={{
            flex: 1,
            backgroundColor: 'transparent',
            color: 'var(--lx-fg1)',
            fontSize: 14,
            fontFamily: 'inherit',
            lineHeight: 1.5,
            outline: 'none',
            border: 'none',
            resize: 'vertical',
            minHeight: 72,
            opacity: disabled ? 0.5 : 1,
          }}
          onFocus={(e) => {
            const wrapper = e.currentTarget.parentElement;
            if (wrapper && !hasError) {
              wrapper.style.borderColor = 'var(--lx-primary)';
              wrapper.style.boxShadow = 'var(--lx-focus-ring-admin)';
            }
          }}
          onBlur={(e) => {
            const wrapper = e.currentTarget.parentElement;
            if (wrapper) {
              wrapper.style.borderColor = hasError ? 'var(--lx-danger)' : 'var(--lx-border)';
              wrapper.style.boxShadow = hasError ? 'var(--lx-focus-ring-error)' : 'none';
            }
          }}
        />
      </div>

      {/* Footer row: error + counter */}
      <Stack flexDirection="row" justifyContent="space-between" alignItems="center">
        {error ? (
          <Text
            id={`${id}-error`}
            role="alert"
            fontFamily="$body"
            fontSize={12}
            data-testid="reason-field-error"
            style={{ color: 'var(--lx-danger)' }}
          >
            {error}
          </Text>
        ) : (
          <span aria-live="polite" />
        )}
        <Text
          id={`${id}-counter`}
          fontFamily="$body"
          fontSize={12}
          aria-live="polite"
          aria-label={`${charCount} of ${maxLength} characters`}
          data-testid="reason-field-counter"
          style={{ color: counterColor, fontVariantNumeric: 'tabular-nums' }}
        >
          {charCount} / {maxLength}
        </Text>
      </Stack>
    </Stack>
  );
}
