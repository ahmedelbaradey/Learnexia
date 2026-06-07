/**
 * Add-Child screen (Design Spec Screen 5): add-child form + multi-child list
 * editor + submit loop with per-child success/failure feedback.
 *
 * Submit flow: for each draft (still idle/error), call `useAddChild` one at a
 * time. Success → ChildCard status badge turns green (stays in list). Failure →
 * red badge + per-child error. All succeeded → route to `/(onboarding)/complete`.
 * Partial failure → banner + failed cards remain editable for retry.
 */
import { useAddChild, isApiError } from '@learnexia/api-client';
import { type AddChildFormValues } from '@learnexia/shared';
import { ChildCard } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { useState } from 'react';
import { ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';

import { Button } from '@learnexia/ui';
import { ServerErrorBanner } from '../../src/components/ServerErrorBanner';
import { useLocale } from '../../src/hooks/useLocale';
import { AddChildForm } from './_components/AddChildForm';
import { EditChildSheet } from './_components/EditChildSheet';
import { nextLocalId, type ChildDraft } from './_components/childListTypes';

function perChildErrorKey(error: unknown): string {
  if (isApiError(error)) {
    const text = [error.message, ...error.errors.map((e) => (typeof e === 'string' ? e : JSON.stringify(e)))]
      .join(' ')
      .toLowerCase();
    if (text.includes('exists') || text.includes('duplicate') || error.status === 409) {
      return 'onboarding.child.errors.duplicateEmail';
    }
    if (text.includes('grade')) return 'onboarding.child.errors.invalidGrade';
    if (text.includes('password') || text.includes('weak')) return 'onboarding.child.errors.weakPassword';
  }
  return 'onboarding.child.errors.generic';
}

export default function AddChildScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const addChild = useAddChild();

  const [drafts, setDrafts] = useState<ChildDraft[]>([]);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [partialFailure, setPartialFailure] = useState(false);

  const editing = drafts.find((d) => d.localId === editingId) ?? null;

  const childMeta = (values: AddChildFormValues) =>
    `${t(`onboarding.grade.${values.grade}`)} · ${t(`onboarding.language.${values.language}`)}`;

  const addToList = (values: AddChildFormValues) => {
    setDrafts((prev) => [...prev, { localId: nextLocalId(), values, status: 'idle' }]);
  };

  const removeDraft = (localId: string) => {
    setDrafts((prev) => prev.filter((d) => d.localId !== localId));
  };

  const saveEdit = (values: AddChildFormValues) => {
    setDrafts((prev) =>
      prev.map((d) => (d.localId === editingId ? { ...d, values, status: 'idle', errorKey: undefined } : d)),
    );
  };

  const submitAll = async () => {
    setSubmitting(true);
    setPartialFailure(false);
    let anyFailed = false;

    for (const draft of drafts) {
      if (draft.status === 'success') continue; // never re-submit a succeeded child
      setDrafts((prev) => prev.map((d) => (d.localId === draft.localId ? { ...d, status: 'pending' } : d)));
      try {
        await addChild.mutateAsync({
          fullName: draft.values.fullName.trim(),
          email: draft.values.email.trim(),
          password: draft.values.password,
          grade: draft.values.grade,
          language: draft.values.language,
          learningLanguage: draft.values.learningLanguage,
          country: draft.values.country.trim(),
        });
        setDrafts((prev) => prev.map((d) => (d.localId === draft.localId ? { ...d, status: 'success', errorKey: undefined } : d)));
      } catch (error) {
        anyFailed = true;
        const errorKey = perChildErrorKey(error);
        setDrafts((prev) => prev.map((d) => (d.localId === draft.localId ? { ...d, status: 'error', errorKey } : d)));
      }
    }

    setSubmitting(false);
    if (anyFailed) {
      setPartialFailure(true);
    } else {
      router.replace('/(onboarding)/complete');
    }
  };

  const canSubmit = drafts.length > 0 && !submitting;

  return (
    <Stack flex={1} backgroundColor="$bg">
      <ScrollView contentContainerStyle={{ flexGrow: 1 }} keyboardShouldPersistTaps="handled">
        <Stack paddingHorizontal="$6" paddingTop="$6" paddingBottom="$16" gap="$6">
          <Stack gap="$2">
            <Text color="$fg1" fontSize={22} fontWeight="700" fontFamily="$heading" accessibilityRole="header" writingDirection={direction}>
              {t('onboarding.addChild.title')}
            </Text>
            <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
              {t('onboarding.addChild.subtitle')}
            </Text>
          </Stack>

          {/* Add-child form card */}
          <Stack testID="add-child-form-card" backgroundColor="$cardSoft" borderRadius="$card" padding="$4">
            <AddChildForm submitLabel={t('onboarding.addChild.addToListButton')} onAdd={addToList} />
          </Stack>

          {/* Children list */}
          {drafts.length > 0 ? (
            <Stack gap="$3">
              <Text color="$fg3" fontSize={12} fontWeight="600" fontFamily="$heading" textTransform="uppercase" letterSpacing={0.6} writingDirection={direction}>
                {t('onboarding.addChild.listLabel', { count: drafts.length })}
              </Text>
              {drafts.map((draft) => (
                <ChildCard
                  key={draft.localId}
                  testID={`child-card-${draft.localId}`}
                  variant={draft.status === 'idle' || draft.status === 'pending' ? 'editable' : 'status'}
                  child={{ fullName: draft.values.fullName, meta: childMeta(draft.values) }}
                  status={draft.status === 'success' ? 'success' : draft.status === 'error' ? 'error' : 'idle'}
                  errorMessage={draft.errorKey ? t(draft.errorKey) : undefined}
                  onEdit={() => setEditingId(draft.localId)}
                  onRemove={() => removeDraft(draft.localId)}
                  direction={direction}
                  accessibilityLabel={draft.values.fullName}
                />
              ))}
            </Stack>
          ) : null}

          {partialFailure ? (
            <ServerErrorBanner message={t('onboarding.addChild.partialFailureBanner')} direction={direction} />
          ) : null}

          <Button
            variant="primary"
            size="full"
            accessibilityLabel={t('onboarding.addChild.submitButton', { count: drafts.length })}
            loading={submitting}
            disabled={!canSubmit}
            onPress={submitAll}
            testID="add-child-submit"
          >
            {t('onboarding.addChild.submitButton', { count: drafts.length })}
          </Button>
        </Stack>
      </ScrollView>

      <EditChildSheet
        visible={editingId !== null}
        initialValues={editing?.values ?? null}
        onSave={saveEdit}
        onClose={() => setEditingId(null)}
      />
    </Stack>
  );
}
