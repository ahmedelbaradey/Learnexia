/**
 * AddChildForm — the one-child detail form inside the Add-Child screen (Design
 * Spec Screen 5). Fields: name, login email, password, grade, language, country.
 *
 * Validation (`addChildSchema`) fires on the "Add Child to List" press, not per
 * keystroke. On a valid submit it hands the values up via `onAdd` and resets.
 * Also serves as the Edit form (pre-filled via `initialValues` + `submitLabel`).
 */
import { addChildSchema, type AddChildFormValues, type Locale } from '@learnexia/shared';
import { Button, GradePicker, LanguageSelect, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack } from '@tamagui/core';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useGradeOptions, useLanguageOptions } from '../../../src/hooks/useChildOptions';

const EMPTY: AddChildFormValues = {
  fullName: '',
  email: '',
  password: '',
  grade: 1,
  language: 'ar',
  country: '',
};

export interface AddChildFormProps {
  initialValues?: AddChildFormValues;
  submitLabel: string;
  onAdd: (values: AddChildFormValues) => void;
}

export function AddChildForm({ initialValues, submitLabel, onAdd }: AddChildFormProps) {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const gradeOptions = useGradeOptions();
  const languageOptions = useLanguageOptions();

  const { control, handleSubmit, reset } = useForm<AddChildFormValues>({
    resolver: zodResolver(addChildSchema),
    defaultValues: initialValues ?? EMPTY,
    mode: 'onSubmit',
  });

  const onSubmit = handleSubmit((values) => {
    onAdd(values);
    reset(EMPTY);
  });

  const errText = (msg?: string) => (msg ? t(msg) : undefined);

  return (
    <Stack gap="$4" accessibilityRole={undefined}>
      <Controller
        control={control}
        name="fullName"
        render={({ field, fieldState }) => (
          <TextField
            label={t('onboarding.addChild.labelName')}
            value={field.value}
            onChangeText={field.onChange}
            autoCapitalize="words"
            error={errText(fieldState.error?.message)}
            direction={direction}
          />
        )}
      />
      <Controller
        control={control}
        name="email"
        render={({ field, fieldState }) => (
          <TextField
            label={t('onboarding.addChild.labelEmail')}
            value={field.value}
            onChangeText={field.onChange}
            keyboardType="email-address"
            autoCapitalize="none"
            error={errText(fieldState.error?.message)}
            direction={direction}
          />
        )}
      />
      <Controller
        control={control}
        name="password"
        render={({ field, fieldState }) => (
          <TextField
            label={t('onboarding.addChild.labelPassword')}
            value={field.value}
            onChangeText={field.onChange}
            secureTextEntry
            error={errText(fieldState.error?.message)}
            direction={direction}
          />
        )}
      />
      <Controller
        control={control}
        name="grade"
        render={({ field, fieldState }) => (
          <GradePicker
            label={t('onboarding.addChild.labelGrade')}
            placeholder={t('onboarding.addChild.gradePlaceholder')}
            options={gradeOptions}
            value={field.value ?? null}
            onChange={(v) => field.onChange(Number(v))}
            error={errText(fieldState.error?.message)}
            direction={direction}
          />
        )}
      />
      <Controller
        control={control}
        name="language"
        render={({ field, fieldState }) => (
          <LanguageSelect
            label={t('onboarding.addChild.labelLanguage')}
            placeholder={t('onboarding.addChild.languagePlaceholder')}
            options={languageOptions}
            value={field.value ?? null}
            onChange={(v) => field.onChange(v as Locale)}
            error={errText(fieldState.error?.message)}
            direction={direction}
          />
        )}
      />
      <Controller
        control={control}
        name="country"
        render={({ field, fieldState }) => (
          <TextField
            label={t('onboarding.addChild.labelCountry')}
            value={field.value}
            onChangeText={field.onChange}
            autoCapitalize="words"
            error={errText(fieldState.error?.message)}
            direction={direction}
          />
        )}
      />

      <Button variant="secondary" size="full" accessibilityLabel={submitLabel} onPress={onSubmit}>
        {submitLabel}
      </Button>
    </Stack>
  );
}
