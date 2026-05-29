/**
 * EditLinkedChildSheet — edit a linked child's details (My Children grid).
 *
 * Opens as a bottom sheet (Modal overlay) from the ChildDashboardCard "Edit"
 * affordance. Pre-fills fullName from the `LinkedChildResponse`; grade,
 * language, and country are editable but can't be pre-filled from the minimal
 * `LinkedChildResponse` shape (only id/fullName/email). Submits via
 * `useUpdateChild`. Shows inline API validation errors from `BaseResponse.errors`.
 *
 * On success: closes the sheet; the parent component should refetch via
 * `useMyChildren` (invalidated automatically by useUpdateChild.onSuccess). RTL +
 * ar/en. Tokens only, no new design pattern — mirrors EditChildSheet structure.
 */
import { useUpdateChild } from '@learnexia/api-client';
import { COUNTRIES, LOCALES, type Locale } from '@learnexia/shared';
import { Button, Select, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { Stack, Text } from '@tamagui/core';
import { Modal } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';

const GRADE_OPTIONS = [1, 2, 3, 4, 5, 6] as const;

const editChildSchema = z.object({
  fullName: z.string().trim().min(1, 'onboarding.addChild.errors.nameRequired'),
  grade: z.number().int().min(1).max(6),
  language: z.enum(LOCALES),
  country: z.string().min(1, 'onboarding.addChild.errors.countryRequired'),
});

type EditChildValues = z.infer<typeof editChildSchema>;

export interface EditLinkedChildSheetProps {
  visible: boolean;
  childId: number;
  initialFullName: string;
  onClose: () => void;
  /** Called after a successful save (the sheet has already closed). */
  onSaved?: () => void;
}

export function EditLinkedChildSheet({
  visible,
  childId,
  initialFullName,
  onClose,
  onSaved,
}: EditLinkedChildSheetProps) {
  const { t, i18n } = useTranslation();
  const { isRtl, direction } = useLocale();
  const insets = useSafeAreaInsets();
  const updateChild = useUpdateChild();
  const resolveError = useServerError();

  const locale = (i18n.language?.startsWith('ar') ? 'ar' : 'en') as Locale;

  const { control, handleSubmit, formState, reset } = useForm<EditChildValues>({
    resolver: zodResolver(editChildSchema),
    defaultValues: {
      fullName: initialFullName,
      grade: 1,
      language: 'ar',
      country: '',
    },
    mode: 'onTouched',
  });

  const gradeOptions = GRADE_OPTIONS.map((g) => ({ value: g, label: t(`onboarding.grade.${g}`) }));
  const languageOptions = LOCALES.map((loc) => ({ value: loc, label: t(`onboarding.language.${loc}`) }));
  const countryOptions = COUNTRIES.map((c) => ({ value: c.code, label: locale === 'ar' ? c.ar : c.en }));

  const serverMessage = updateChild.isError
    ? resolveError(updateChild.error, {
        byStatus: {
          400: 'parent.editChild.saveError',
          403: 'parent.editChild.saveError',
          404: 'parent.editChild.saveError',
        },
      })
    : null;

  const onSave = handleSubmit(async (values) => {
    try {
      await updateChild.mutateAsync({
        childId,
        fullName: values.fullName.trim(),
        grade: values.grade,
        language: values.language,
        country: values.country,
      });
      reset({ fullName: values.fullName.trim(), grade: values.grade, language: values.language, country: values.country });
      updateChild.reset();
      onClose();
      onSaved?.();
    } catch {
      // Error surfaced inline via serverMessage.
    }
  });

  const handleClose = () => {
    updateChild.reset();
    onClose();
  };

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={handleClose}>
      <Stack flex={1} backgroundColor="$overlay" justifyContent="flex-end">
        <Stack
          maxHeight="90%"
          backgroundColor="$card"
          borderTopStartRadius="$modal"
          borderTopEndRadius="$modal"
          paddingBottom={insets.bottom + 16}
        >
          {/* Drag handle */}
          <Stack alignItems="center" paddingTop="$3">
            <Stack width={40} height={4} borderRadius="$pill" backgroundColor="$cardSoft" />
          </Stack>

          {/* Title row */}
          <Stack
            flexDirection={isRtl ? 'row-reverse' : 'row'}
            justifyContent="space-between"
            alignItems="center"
            paddingHorizontal="$6"
            paddingVertical="$4"
          >
            <Text color="$fg1" fontSize={18} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
              {t('parent.editChild.title')}
            </Text>
            <Stack
              minWidth={48}
              minHeight={48}
              alignItems="center"
              justifyContent="center"
              cursor="pointer"
              onPress={handleClose}
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

          {/* Form fields */}
          <Stack paddingHorizontal="$6" gap="$4">
            <Controller
              control={control}
              name="fullName"
              render={({ field, fieldState }) => (
                <TextField
                  label={t('onboarding.addChild.labelName')}
                  value={field.value}
                  onChangeText={field.onChange}
                  autoCapitalize="words"
                  direction={direction}
                  error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                  disabled={updateChild.isPending}
                />
              )}
            />

            <Controller
              control={control}
              name="grade"
              render={({ field, fieldState }) => (
                <Select
                  label={t('onboarding.addChild.labelGrade')}
                  value={field.value}
                  onChange={(v) => field.onChange(Number(v))}
                  options={gradeOptions}
                  placeholder={t('onboarding.addChild.gradePlaceholder')}
                  direction={direction}
                  error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                  accessibilityLabel={t('onboarding.addChild.labelGrade')}
                />
              )}
            />

            <Controller
              control={control}
              name="language"
              render={({ field, fieldState }) => (
                <Select
                  label={t('onboarding.addChild.labelLanguage')}
                  value={field.value}
                  onChange={(v) => field.onChange(String(v) as Locale)}
                  options={languageOptions}
                  placeholder={t('onboarding.addChild.languagePlaceholder')}
                  direction={direction}
                  error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                  accessibilityLabel={t('onboarding.addChild.labelLanguage')}
                />
              )}
            />

            <Controller
              control={control}
              name="country"
              render={({ field, fieldState }) => (
                <Select
                  label={t('onboarding.addChild.labelCountry')}
                  value={field.value}
                  onChange={(v) => field.onChange(String(v))}
                  options={countryOptions}
                  placeholder={t('auth.register.countryPlaceholder')}
                  direction={direction}
                  error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
                  accessibilityLabel={t('onboarding.addChild.labelCountry')}
                />
              )}
            />

            <ServerErrorBanner message={serverMessage} direction={direction} />

            <Button
              variant="primary"
              size="full"
              loading={updateChild.isPending}
              disabled={updateChild.isPending || formState.isSubmitting}
              accessibilityLabel={t('parent.editChild.save')}
              onPress={onSave}
            >
              {t('parent.editChild.save')}
            </Button>
          </Stack>
        </Stack>
      </Stack>
    </Modal>
  );
}

EditLinkedChildSheet.displayName = 'EditLinkedChildSheet';
