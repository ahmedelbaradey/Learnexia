/**
 * LinkChildForm (Design Spec Screen 9) — link an existing child by email via
 * `useLinkChild`. On success: invalidates the My-Children query and shows the
 * linked-child success card. Maps not-found / already-linked errors to localized
 * banner copy. The form field `email` is sent as `childEmail`.
 */
import { useLinkChild, type LinkedChildResponse } from '@learnexia/api-client';
import { linkChildSchema, type LinkChildFormValues } from '@learnexia/shared';
import { Button, ChildCard, TextField } from '@learnexia/ui';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQueryClient } from '@tanstack/react-query';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../src/hooks/useLocale';
import { useServerError } from '../../../src/hooks/useServerError';

export function LinkChildForm() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  const linkChild = useLinkChild();
  const resolveError = useServerError();
  const [linked, setLinked] = useState<LinkedChildResponse | null>(null);

  const { control, handleSubmit, formState } = useForm<LinkChildFormValues>({
    resolver: zodResolver(linkChildSchema),
    defaultValues: { email: '' },
    mode: 'onTouched',
  });

  const serverMessage = linkChild.isError
    ? resolveError(linkChild.error, {
        hints: [
          { contains: ['not found', 'no child', 'does not exist'], key: 'parent.linkChild.errors.notFound' },
          { contains: ['already', 'linked'], key: 'parent.linkChild.errors.alreadyLinked' },
        ],
        byStatus: { 404: 'parent.linkChild.errors.notFound', 409: 'parent.linkChild.errors.alreadyLinked' },
      })
    : null;

  const onSubmit = handleSubmit(async (values) => {
    try {
      const res = await linkChild.mutateAsync({ childEmail: values.email.trim() });
      await queryClient.invalidateQueries({ queryKey: ['family', 'my-children'] });
      setLinked(res);
    } catch {
      // Failure is surfaced inline via linkChild.error; swallow the rejection so
      // it doesn't bubble as an uncaught promise error.
    }
  });

  if (linked) {
    return (
      <Stack gap="$4">
        <ChildCard
          variant="linking"
          child={{ fullName: linked.fullName ?? '', meta: linked.email }}
          direction={direction}
          accessibilityLabel={linked.fullName ?? ''}
          testID="link-child-success"
        />
        <Text color="$success" fontSize={18} fontWeight="700" fontFamily="$heading" textAlign="center" writingDirection={direction}>
          {t('parent.linkChild.successTitle')}
        </Text>
        <Button variant="ghost" size="full" accessibilityLabel={t('parent.linkChild.doneButton')} onPress={() => router.back()}>
          {t('parent.linkChild.doneButton')}
        </Button>
      </Stack>
    );
  }

  return (
    <Stack gap="$6">
      <Text color="$fg2" fontSize={15} fontFamily="$body" lineHeight={24} writingDirection={direction}>
        {t('parent.linkChild.explanation')}
      </Text>
      <Controller
        control={control}
        name="email"
        render={({ field, fieldState }) => (
          <TextField
            label={t('parent.linkChild.labelEmail')}
            value={field.value}
            onChangeText={field.onChange}
            keyboardType="email-address"
            autoCapitalize="none"
            error={fieldState.error ? t(fieldState.error.message ?? '') : undefined}
            direction={direction}
            disabled={linkChild.isPending}
            testID="link-child-email"
          />
        )}
      />
      <ServerErrorBanner message={serverMessage} direction={direction} testID="link-child-error" />
      <Button
        variant="primary"
        size="full"
        accessibilityLabel={t('parent.linkChild.submitButton')}
        loading={linkChild.isPending}
        disabled={linkChild.isPending || formState.isSubmitting}
        onPress={onSubmit}
        testID="link-child-submit"
      >
        {t('parent.linkChild.submitButton')}
      </Button>
    </Stack>
  );
}
