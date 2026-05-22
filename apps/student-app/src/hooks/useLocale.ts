/**
 * useLocale — convenience accessor for the active locale + its direction.
 * Pairs with `react-i18next`'s `useTranslation` for copy.
 */
import { directionForLocale, type Direction, type Locale } from '@learnexia/shared';

import { useLocaleStore } from '../providers/localeStore';

export interface LocaleInfo {
  locale: Locale;
  direction: Direction;
  isRtl: boolean;
}

export function useLocale(): LocaleInfo {
  const locale = useLocaleStore((s) => s.locale);
  const direction = directionForLocale(locale);
  return { locale, direction, isRtl: direction === 'rtl' };
}
