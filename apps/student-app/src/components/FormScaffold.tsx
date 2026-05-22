/**
 * FormScaffold — the responsive auth/onboarding container (Design Spec §1).
 *
 * Phone (sm): full-width column on `$bg`, scrollable, horizontal `$6` padding.
 * Tablet/laptop: a centered `Card` (max-width 480px) on `$bg`. Tamagui media
 * props (`$tablet`) switch between the two without JS branching. Wraps content
 * in `KeyboardAvoidingView` + `ScrollView` for keyboard-aware native forms.
 */
import { Card } from '@learnexia/ui';
import { Stack } from '@tamagui/core';
import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

export interface FormScaffoldProps {
  children: ReactNode;
  /** Optional header rendered above the scroll area (e.g. ScreenHeader). */
  header?: ReactNode;
}

export function FormScaffold({ children, header }: FormScaffoldProps) {
  const insets = useSafeAreaInsets();
  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      {header}
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={{ flexGrow: 1 }}
          keyboardShouldPersistTaps="handled"
        >
          <Stack flex={1} alignItems="center" justifyContent="center" paddingVertical="$6">
            {/* Phone: full width. Tablet+: centered card, max 480. */}
            <Stack
              width="100%"
              paddingHorizontal="$6"
              $tablet={{ maxWidth: 480, paddingHorizontal: 0 }}
            >
              <Stack
                $tablet={{
                  backgroundColor: '$card',
                  borderRadius: '$card',
                  padding: '$8',
                  borderWidth: 1,
                  borderColor: '$border',
                }}
              >
                {children}
              </Stack>
            </Stack>
          </Stack>
        </ScrollView>
      </KeyboardAvoidingView>
    </Stack>
  );
}

// Re-export Card so screens needing the explicit card primitive can grab it here.
export { Card };
