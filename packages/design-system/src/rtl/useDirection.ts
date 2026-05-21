/**
 * `useDirection` — resolve text direction for a locale.
 *
 * Delegates entirely to `@learnexia/shared/i18n` (`directionForLocale`). Does
 * NOT re-implement RTL detection. Components call this to flip direction-aware
 * layout props (e.g. `flexDirection: dir === 'rtl' ? 'row-reverse' : 'row'`).
 */
import { directionForLocale, type Direction } from '@learnexia/shared/i18n';
import { useMemo } from 'react';

export function useDirection(locale: string): Direction {
  return useMemo(() => directionForLocale(locale), [locale]);
}

export type { Direction };
