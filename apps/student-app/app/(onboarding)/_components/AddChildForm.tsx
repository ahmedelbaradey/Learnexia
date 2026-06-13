/**
 * AddChildForm — the one-child detail form inside the Add-Child screen (Design
 * Spec Screen 5). Fields: name, login email, password, grade, [language group:
 * learning language + app language], country.
 *
 * P8-01-FE: adds a required `learningLanguage` field (axis B — medium of
 * instruction for Math & Science), distinct from the existing `language` field
 * (axis A — UI/account language). Both fields are grouped in a thin labelled
 * container for disambiguation. Selecting a learning language auto-fills the app
 * language to match while the parent has not manually touched the app-language
 * field (`appLanguageTouched` flag). Both fields stay independently editable.
 *
 * Validation (`addChildSchema`) fires on the "Add Child to List" press, not per
 * keystroke. On a valid submit it hands the values up via `onAdd` and resets.
 * Also serves as the Edit form (pre-filled via `initialValues` + `submitLabel`).
 */
import { addChildSchema, type AddChildFormValues, type Locale } from '@learnexia/shared';
import { Button, GradePicker, LanguageSelect, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { Controller, type DefaultValues, useForm } from 'react-hook-form';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { useGradeOptions, useLanguageOptions } from '../../../src/hooks/useChildOptions';

/**
 * EMPTY default values. `learningLanguage` is deliberately absent (undefined)
 * so the field starts with no selection (placeholder rendered) and zod enforces
 * a required pick. `language` keeps its `'ar'` default and auto-fills from
 * learningLanguage on first selection.
 *
 * Typed as `DefaultValues<AddChildFormValues>` (react-hook-form's own type that
 * accepts `undefined` for each field) so strictness is satisfied.
 */
const EMPTY: DefaultValues<AddChildFormValues> = {
  fullName: '',
  email: '',
  password: '',
  grade: 1,
  language: 'ar',
  country: '',
  // learningLanguage intentionally omitted → undefined → placeholder shown
};

export interface AddChildFormProps {
  initialValues?: AddChildFormValues;
  submitLabel: string;
  onAdd: (values: AddChildFormValues) => void;
}

export function AddChildForm({ initialValues, submitLabel, onAdd }: AddChildFormProps) {
  const { t } = useTranslation();
  const { direction, isRtl } = useLocale();
  const gradeOptions = useGradeOptions();
  const languageOptions = useLanguageOptions();

  /**
   * Tracks whether the parent has manually touched the app-language (axis A)
   * field. When false, selecting a learning language auto-fills the app language
   * to match. Once the parent edits app language directly, this becomes true and
   * subsequent learning-language changes no longer overwrite it.
   */
  const appLanguageTouched = useRef(false);

  const { control, handleSubmit, reset, setValue } = useForm<AddChildFormValues>({
    resolver: zodResolver(addChildSchema),
    defaultValues: initialValues ?? EMPTY,
    mode: 'onSubmit',
  });

  const onSubmit = handleSubmit((values) => {
    onAdd(values);
    appLanguageTouched.current = false;
    reset(EMPTY);
  });

  const errText = (msg?: string) => (msg ? t(msg) : undefined);

  const helperTextAlign = isRtl ? ('right' as const) : ('left' as const);

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
            testID="add-child-name"
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
            testID="add-child-email"
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
            showLabel={t('auth.login.showPassword')}
            hideLabel={t('auth.login.hidePassword')}
            error={errText(fieldState.error?.message)}
            direction={direction}
            testID="add-child-password"
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
            testID="add-child-grade"
          />
        )}
      />

      {/* Language group — thin labelled container fencing both language fields */}
      <Stack
        gap="$3"
        padding="$3"
        borderRadius="$sm"
        borderWidth={1}
        borderColor="$borderSubtle"
      >
        {/* Group eyebrow — matches the listLabel eyebrow style on the screen */}
        <Text
          color="$fg3"
          fontSize={12}
          fontWeight="600"
          fontFamily="$heading"
          textTransform="uppercase"
          letterSpacing={0.6}
          textAlign={helperTextAlign}
          writingDirection={direction}
        >
          {t('onboarding.addChild.languageGroupLabel')}
        </Text>

        {/* Learning language — axis B; required, no default */}
        <Controller
          control={control}
          name="learningLanguage"
          render={({ field, fieldState }) => (
            <>
              <LanguageSelect
                label={t('onboarding.addChild.labelLearningLanguage')}
                placeholder={t('onboarding.addChild.learningLanguagePlaceholder')}
                options={languageOptions}
                value={field.value ?? null}
                onChange={(v) => {
                  const chosen = v as Locale;
                  field.onChange(chosen);
                  // Auto-fill app language to match, guarded by the touched flag.
                  if (!appLanguageTouched.current) {
                    setValue('language', chosen, { shouldValidate: false });
                  }
                }}
                error={errText(fieldState.error?.message)}
                direction={direction}
                accessibilityLabel={t('onboarding.addChild.labelLearningLanguage')}
                testID="add-child-learning-language"
              />
              <Text
                color="$fg3"
                fontSize={12}
                fontFamily="$body"
                lineHeight={17}
                marginTop={2}
                textAlign={helperTextAlign}
                writingDirection={direction}
                accessibilityElementsHidden={false}
              >
                {t('onboarding.addChild.learningLanguageHelper')}
              </Text>
            </>
          )}
        />

        {/* App language — axis A; relabelled, auto-fills from learning language */}
        <Controller
          control={control}
          name="language"
          render={({ field, fieldState }) => (
            <>
              <LanguageSelect
                label={t('onboarding.addChild.labelAppLanguage')}
                placeholder={t('onboarding.addChild.languagePlaceholder')}
                options={languageOptions}
                value={field.value ?? null}
                onChange={(v) => {
                  // Mark as touched so the auto-fill stops overwriting.
                  appLanguageTouched.current = true;
                  field.onChange(v as Locale);
                }}
                error={errText(fieldState.error?.message)}
                direction={direction}
                accessibilityLabel={t('onboarding.addChild.labelAppLanguage')}
                testID="add-child-app-language"
              />
              <Text
                color="$fg3"
                fontSize={12}
                fontFamily="$body"
                lineHeight={17}
                marginTop={2}
                textAlign={helperTextAlign}
                writingDirection={direction}
                accessibilityElementsHidden={false}
              >
                {t('onboarding.addChild.appLanguageHelper')}
              </Text>
            </>
          )}
        />
      </Stack>

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
            testID="add-child-country"
          />
        )}
      />

      <Button variant="secondary" size="full" accessibilityLabel={submitLabel} onPress={onSubmit} testID="add-child-to-list">
        {submitLabel}
      </Button>
    </Stack>
  );
}
