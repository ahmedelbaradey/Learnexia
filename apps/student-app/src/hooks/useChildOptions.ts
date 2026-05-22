/**
 * Localized option lists for the Add-Child form pickers. Grade labels come from
 * `onboarding.grade.*`; language labels from `onboarding.language.*`. Labels are
 * NOT hardcoded in the `GradePicker`/`LanguageSelect` components (Design Spec).
 */
import { GRADES, LOCALES } from '@learnexia/shared';
import type { SelectOption } from '@learnexia/ui';
import { useTranslation } from 'react-i18next';

export function useGradeOptions(): SelectOption[] {
  const { t } = useTranslation();
  return GRADES.map((g) => ({ value: g, label: t(`onboarding.grade.${g}`) }));
}

export function useLanguageOptions(): SelectOption[] {
  const { t } = useTranslation();
  return LOCALES.map((l) => ({ value: l, label: t(`onboarding.language.${l}`) }));
}
