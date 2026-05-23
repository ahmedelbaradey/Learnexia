/**
 * PersonaToggle — segmented pill selecting the login persona (parent | student)
 * on the shared login screen (P1-11 / B-02). UI-only: it does NOT enable student
 * self-registration (parent-driven onboarding stands) — it is a hint for the
 * login form. Active tab = `$primary` fill / `$fg1`; inactive = `$card` / `$fg3`.
 * RTL-aware (row flips). Each tab is a ≥48px touch target.
 */
import {
  ALL_LOGIN_PERSONAS,
  LOGIN_PERSONAS,
  type Direction,
  type LoginPersona,
} from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';

const PERSONA_LABEL_KEYS: Record<LoginPersona, string> = {
  [LOGIN_PERSONAS.Parent]: 'auth.login.personaParent',
  [LOGIN_PERSONAS.Student]: 'auth.login.personaStudent',
};

export interface PersonaToggleProps {
  value: LoginPersona;
  onChange: (persona: LoginPersona) => void;
  /** Localized label per persona (resolved by the caller via i18n). */
  labelFor: (persona: LoginPersona) => string;
  /** Group accessibility label (localized). */
  accessibilityLabel: string;
  direction?: Direction;
  disabled?: boolean;
}

/** i18n key for a persona's tab label — caller resolves with `t(...)`. */
export function personaLabelKey(persona: LoginPersona): string {
  return PERSONA_LABEL_KEYS[persona];
}

export function PersonaToggle({
  value,
  onChange,
  labelFor,
  accessibilityLabel,
  direction = 'ltr',
  disabled = false,
}: PersonaToggleProps) {
  return (
    <Stack
      flexDirection={direction === 'rtl' ? 'row-reverse' : 'row'}
      backgroundColor="$card"
      borderRadius="$pill"
      borderWidth={1}
      borderColor="$border"
      padding={4}
      gap={4}
      accessibilityRole="radiogroup"
      accessible
      accessibilityLabel={accessibilityLabel}
      aria-label={accessibilityLabel}
      opacity={disabled ? 0.5 : 1}
      pointerEvents={disabled ? 'none' : 'auto'}
    >
      {ALL_LOGIN_PERSONAS.map((persona) => {
        const active = persona === value;
        return (
          <Stack
            key={persona}
            flex={1}
            height={52}
            alignItems="center"
            justifyContent="center"
            borderRadius="$pill"
            cursor="pointer"
            backgroundColor={active ? '$primary' : 'transparent'}
            hoverStyle={active ? undefined : { backgroundColor: '$cardSoft' }}
            onPress={() => onChange(persona)}
            accessibilityRole="radio"
            accessible
            accessibilityState={{ selected: active }}
            accessibilityLabel={labelFor(persona)}
            aria-label={labelFor(persona)}
          >
            <Text
              color={active ? '$fg1' : '$fg3'}
              fontSize={15}
              fontWeight="700"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {labelFor(persona)}
            </Text>
          </Stack>
        );
      })}
    </Stack>
  );
}
