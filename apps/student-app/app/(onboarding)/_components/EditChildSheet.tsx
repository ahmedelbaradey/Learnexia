/**
 * EditChildSheet — edit an in-memory child draft (Design Spec Screen 6).
 *
 * No backend call — only updates the in-memory list. Implemented as a bottom
 * RN `Modal` overlay (universal; avoids a native-only Sheet dep). Reuses
 * `AddChildForm` pre-filled with the draft's values.
 */
import type { AddChildFormValues } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { Modal } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../../src/hooks/useLocale';
import { AddChildForm } from './AddChildForm';

export interface EditChildSheetProps {
  visible: boolean;
  initialValues: AddChildFormValues | null;
  onSave: (values: AddChildFormValues) => void;
  onClose: () => void;
}

export function EditChildSheet({ visible, initialValues, onSave, onClose }: EditChildSheetProps) {
  const { t } = useTranslation();
  const { isRtl } = useLocale();
  const insets = useSafeAreaInsets();

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Stack flex={1} backgroundColor="$overlay" justifyContent="flex-end">
        <Stack
          maxHeight="85%"
          backgroundColor="$card"
          borderTopStartRadius="$modal"
          borderTopEndRadius="$modal"
          paddingBottom={insets.bottom + 16}
        >
          <Stack alignItems="center" paddingTop="$3">
            <Stack width={40} height={4} borderRadius="$pill" backgroundColor="$cardSoft" />
          </Stack>
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

          <Stack paddingHorizontal="$6">
            {visible && initialValues ? (
              <AddChildForm
                initialValues={initialValues}
                submitLabel={t('onboarding.saveChanges')}
                onAdd={(values) => {
                  onSave(values);
                  onClose();
                }}
              />
            ) : null}
          </Stack>
        </Stack>
      </Stack>
    </Modal>
  );
}
