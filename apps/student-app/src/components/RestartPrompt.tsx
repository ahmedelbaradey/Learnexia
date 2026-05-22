/**
 * RestartPrompt — native "restart to change language" dialog (Design Spec
 * Screen 10).
 *
 * Visible only when `restartPromptStore.isVisible` is true (set when a native
 * locale change requires an RTL flip). "Restart" applies the direction via
 * `applyNativeRtl` + `react-native-restart`; "Later" defers. On web this is a
 * no-op (web flips direction instantly, never shows the prompt).
 */
import { applyNativeRtl } from '@learnexia/design-system';
import { useRestartPromptStore } from '@learnexia/shared';
import { Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { MotiView } from 'moti';
import { I18nManager, Platform } from 'react-native';
import RNRestart from 'react-native-restart';
import { useTranslation } from 'react-i18next';

export function RestartPrompt() {
  const { t } = useTranslation();
  const isVisible = useRestartPromptStore((s) => s.isVisible);
  const pendingLocale = useRestartPromptStore((s) => s.pendingLocale);
  const dismiss = useRestartPromptStore((s) => s.dismiss);

  if (Platform.OS === 'web' || !isVisible || !pendingLocale) return null;

  const onConfirm = () => {
    applyNativeRtl(pendingLocale, I18nManager, RNRestart);
  };

  return (
    <Stack
      position="absolute"
      top={0}
      bottom={0}
      start={0}
      end={0}
      backgroundColor="$overlay"
      alignItems="center"
      justifyContent="center"
      zIndex={2000}
    >
      <MotiView
        from={{ scale: 0.9, opacity: 0 }}
        animate={{ scale: 1, opacity: 1 }}
        transition={{ type: 'spring', damping: 12, stiffness: 180 }}
      >
        <Stack
          maxWidth={320}
          padding="$6"
          gap="$4"
          backgroundColor="$card"
          borderRadius="$modal"
          borderWidth={1}
          borderColor="$border"
        >
          <Text color="$fg1" fontSize={18} fontWeight="700" fontFamily="$heading" textAlign="center">
            {t('common.restartPrompt.title')}
          </Text>
          <Text color="$fg2" fontSize={14} fontFamily="$body" textAlign="center" lineHeight={22}>
            {t('common.restartPrompt.body')}
          </Text>
          <Stack flexDirection="row" gap="$3" justifyContent="center">
            <Button variant="ghost" accessibilityLabel={t('common.restartPrompt.cancel')} onPress={dismiss}>
              {t('common.restartPrompt.cancel')}
            </Button>
            <Button variant="primary" accessibilityLabel={t('common.restartPrompt.confirm')} onPress={onConfirm}>
              {t('common.restartPrompt.confirm')}
            </Button>
          </Stack>
        </Stack>
      </MotiView>
    </Stack>
  );
}
