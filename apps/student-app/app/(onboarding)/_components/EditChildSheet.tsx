/**
 * EditChildSheet — edit a child's details.
 *
 * Two modes:
 *
 * 1. **Onboarding / in-memory mode** (when `childId` is NOT provided):
 *    Reuses `AddChildForm` pre-filled with the draft's values; calls `onSave`
 *    with the updated values. No backend call. Unchanged behaviour for the
 *    onboarding `add-child` flow.
 *
 * 2. **Web edit-child mode** (when `childId` IS provided — P1-12-FE-5):
 *    Renders a slim edit-only field set (`EditChildFields`) with only the four
 *    editable fields (`fullName`, `grade`, `language`, `country`). Calls
 *    `useUpdateChild` via the mutation. On success, the hook invalidates
 *    `queryKeys.family.myChildren()` so the card list refreshes; the sheet closes.
 *    Shows `ServerErrorBanner` inside the sheet on error.
 *    Does NOT show email, password, or learningLanguage (those are not accepted
 *    by `updateChild` and must not be re-entered — security requirement).
 *
 * Implemented as a bottom RN `Modal` overlay (universal; avoids a native-only
 * Sheet dep). RTL-aware throughout.
 */
import { useUpdateChild } from '@learnexia/api-client';
import {
  COUNTRIES,
  LOCALES,
  type AddChildFormValues,
  type Locale,
} from '@learnexia/shared';
import { Button, GradePicker, LanguageSelect, Select, TextField, type SelectOption } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { Modal } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';
import { useGradeOptions, useLanguageOptions } from '../../../src/hooks/useChildOptions';
import { AddChildForm } from './AddChildForm';

/* ------------------------------------------------------------------ */
/* Slim edit schema (fullName + grade + language + country only)        */
/* ------------------------------------------------------------------ */

const editChildSchema = z.object({
  fullName: z.string().trim().min(1, 'onboarding.addChild.errors.nameRequired'),
  grade: z
    .number({ invalid_type_error: 'onboarding.child.errors.invalidGrade' })
    .int('onboarding.child.errors.invalidGrade')
    .min(1, 'onboarding.child.errors.invalidGrade')
    .max(6, 'onboarding.child.errors.invalidGrade'),
  language: z.enum(LOCALES, {
    errorMap: () => ({ message: 'onboarding.addChild.errors.languageRequired' }),
  }),
  country: z.string().trim().min(1, 'onboarding.addChild.errors.countryRequired'),
});

type EditChildFormValues = z.infer<typeof editChildSchema>;

/* ------------------------------------------------------------------ */
/* Sheet props                                                           */
/* ------------------------------------------------------------------ */

/** Initial values seeded from the child when opening the edit sheet. */
export interface EditChildInitialValues {
  fullName: string;
  grade: number;
  language: string;
  country: string;
  /** Optional email — display-only, never editable here. */
  email?: string;
}

export interface EditChildSheetProps {
  visible: boolean;
  initialValues: AddChildFormValues | EditChildInitialValues | null;
  onSave: (values: AddChildFormValues) => void;
  onClose: () => void;
  /**
   * When provided, the sheet enters backend-wired edit mode: it renders the slim
   * field set and calls `useUpdateChild` instead of `onSave`. The onboarding
   * in-memory path (`onSave`) is only used when `childId` is absent.
   */
  childId?: number;
}

/* ------------------------------------------------------------------ */
/* Sheet                                                                 */
/* ------------------------------------------------------------------ */

export function EditChildSheet({
  visible,
  initialValues,
  onSave,
  onClose,
  childId,
}: EditChildSheetProps) {
  const { t } = useTranslation();
  const { isRtl } = useLocale();
  const insets = useSafeAreaInsets();

  const isBackendMode = childId !== undefined;

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Stack flex={1} backgroundColor="$overlay" justifyContent="flex-end">
        <Stack
          testID="edit-child-sheet"
          maxHeight="85%"
          backgroundColor="$card"
          borderTopStartRadius="$modal"
          borderTopEndRadius="$modal"
          paddingBottom={insets.bottom + 16}
        >
          {/* Drag handle */}
          <Stack alignItems="center" paddingTop="$3">
            <Stack width={40} height={4} borderRadius="$pill" backgroundColor="$cardSoft" />
          </Stack>

          {/* Header row */}
          <Stack
            flexDirection={isRtl ? 'row-reverse' : 'row'}
            justifyContent="space-between"
            alignItems="center"
            paddingHorizontal="$6"
            paddingVertical="$4"
          >
            <Text color="$fg1" fontSize={18} fontWeight="700" fontFamily="$heading">
              {t('onboarding.editChild')}
            </Text>
            <Stack
              minWidth={48}
              minHeight={48}
              alignItems="center"
              justifyContent="center"
              cursor="pointer"
              onPress={onClose}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('onboarding.close')}
              aria-label={t('onboarding.close')}
            >
              <Text fontSize={20} color="$fg3" accessibilityElementsHidden>
                {'✕'}
              </Text>
            </Stack>
          </Stack>

          {/* Content */}
          <Stack paddingHorizontal="$6">
            {visible && initialValues ? (
              isBackendMode ? (
                <EditChildFields
                  childId={childId}
                  initialValues={initialValues as EditChildInitialValues}
                  onClose={onClose}
                />
              ) : (
                <AddChildForm
                  initialValues={initialValues as AddChildFormValues}
                  submitLabel={t('onboarding.saveChanges')}
                  onAdd={(values) => {
                    onSave(values);
                    onClose();
                  }}
                />
              )
            ) : null}
          </Stack>
        </Stack>
      </Stack>
    </Modal>
  );
}

/* ------------------------------------------------------------------ */
/* Slim edit fields (backend-wired mode)                               */
/* ------------------------------------------------------------------ */

interface EditChildFieldsProps {
  childId: number;
  initialValues: EditChildInitialValues;
  onClose: () => void;
}

function EditChildFields({ childId, initialValues, onClose }: EditChildFieldsProps) {
  const { t, i18n } = useTranslation();
  const { direction } = useLocale();
  const updateChild = useUpdateChild();
  const resolveError = useServerError();
  const gradeOptions = useGradeOptions();
  const languageOptions = useLanguageOptions();

  const locale = (i18n.language?.startsWith('ar') ? 'ar' : 'en') as Locale;
  const countryOptions: SelectOption[] = useMemo(
    () => COUNTRIES.map((c) => ({ value: c.code, label: c[locale] })),
    [locale],
  );

  const { control, handleSubmit, formState } = useForm<EditChildFormValues>({
    resolver: zodResolver(editChildSchema),
    defaultValues: {
      fullName: initialValues.fullName,
      grade: typeof initialValues.grade === 'number' ? initialValues.grade : 1,
      language: LOCALES.includes(initialValues.language as Locale)
        ? (initialValues.language as Locale)
        : 'ar',
      country: initialValues.country,
    },
    mode: 'onTouched',
  });

  const serverMessage = updateChild.isError
    ? resolveError(updateChild.error, {
        hints: [
          { contains: ['duplicate', 'exists'], key: 'parent.myChildren.editError' },
          { contains: ['grade', 'invalid'], key: 'onboarding.child.errors.invalidGrade' },
        ],
        byStatus: { 422: 'parent.myChildren.editError', 400: 'parent.myChildren.editError' },
      })
    : null;

  const disabled = updateChild.isPending;

  const onSubmit = handleSubmit(async (values) => {
    try {
      await updateChild.mutateAsync({
        childId,
        fullName: values.fullName.trim(),
        grade: values.grade,
        language: values.language,
        country: values.country,
      });
      // Hook invalidates myChildren query; close the sheet on success.
      onClose();
    } catch {
      // Error surfaced inline via updateChild.error → serverMessage.
    }
  });

  const errText = (msg?: string) => (msg ? t(msg) : undefined);

  return (
    <Stack gap="$4">
      {/* Email display (read-only, identifies the child) — shown if available */}
      {initialValues.email ? (
        <TextField
          label={t('onboarding.addChild.labelEmail')}
          value={initialValues.email}
          onChangeText={() => undefined}
          keyboardType="email-address"
          autoCapitalize="none"
          forceLtr
          disabled
          direction={direction}
        />
      ) : null}

      {/* Full name */}
      <Controller
        control={control}
        name="fullName"
        render={({ field, fieldState }) => (
          <TextField
            label={t('parent.settings.linkedChildren.fullName')}
            value={field.value}
            onChangeText={field.onChange}
            autoCapitalize="words"
            error={errText(fieldState.error?.message)}
            direction={direction}
            disabled={disabled}
          />
        )}
      />

      {/* Grade */}
      <Controller
        control={control}
        name="grade"
        render={({ field, fieldState }) => (
          <GradePicker
            label={t('parent.settings.linkedChildren.grade')}
            placeholder={t('onboarding.addChild.gradePlaceholder')}
            options={gradeOptions}
            value={field.value ?? null}
            onChange={(v) => field.onChange(Number(v))}
            error={errText(fieldState.error?.message)}
            direction={direction}
            disabled={disabled}
          />
        )}
      />

      {/* Language */}
      <Controller
        control={control}
        name="language"
        render={({ field, fieldState }) => (
          <LanguageSelect
            label={t('parent.settings.linkedChildren.language')}
            placeholder={t('onboarding.addChild.languagePlaceholder')}
            options={languageOptions}
            value={field.value ?? null}
            onChange={(v) => field.onChange(v as Locale)}
            error={errText(fieldState.error?.message)}
            direction={direction}
            disabled={disabled}
            accessibilityLabel={t('parent.settings.linkedChildren.language')}
          />
        )}
      />

      {/* Country */}
      <Controller
        control={control}
        name="country"
        render={({ field, fieldState }) => (
          <Select
            label={t('parent.settings.linkedChildren.country')}
            placeholder={t('parent.settings.linkedChildren.countryPlaceholder')}
            value={field.value || null}
            onChange={(v) => field.onChange(String(v))}
            options={countryOptions}
            error={errText(fieldState.error?.message)}
            direction={direction}
            disabled={disabled}
          />
        )}
      />

      <ServerErrorBanner message={serverMessage} direction={direction} testID="edit-child-error" />

      <Button
        variant="secondary"
        size="full"
        accessibilityLabel={t('onboarding.saveChanges')}
        loading={updateChild.isPending}
        disabled={disabled || formState.isSubmitting}
        onPress={onSubmit}
        testID="edit-child-save"
      >
        {t('onboarding.saveChanges')}
      </Button>
    </Stack>
  );
}
