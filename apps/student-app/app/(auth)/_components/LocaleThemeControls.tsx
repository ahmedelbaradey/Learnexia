/**
 * LocaleThemeControls — the language (en/ar) + dark/light switches shown on the
 * Login screen (P1-11). Language toggle flips the locale store; on native a
 * direction change requires a reload (handled by the locale-switch path) — here
 * it flips instantly on web and updates the store on native. Theme toggle flips
 * the theme store (instant on both). Token-driven, RTL-aware, i18n labels.
 */
import {
  LOCALES,
  directionForLocale,
  useRestartPromptStore,
  type Direction,
  type Locale,
} from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { Platform } from 'react-native';
import { useTranslation } from 'react-i18next';

import { useLocaleStore } from '../../../src/providers/localeStore';
import { useThemeStore } from '../../../src/providers/themeStore';

export interface LocaleThemeControlsProps {
  direction?: Direction;
}

const LOCALE_LABEL_KEY: Record<Locale, string> = {
  en: 'common.prefs.switchToEnglish',
  ar: 'common.prefs.switchToArabic',
};

export function LocaleThemeControls({ direction = 'ltr' }: LocaleThemeControlsProps) {
  const { t } = useTranslation();
  const locale = useLocaleStore((s) => s.locale);
  const setLocale = useLocaleStore((s) => s.setLocale);
  const showRestartPrompt = useRestartPromptStore((s) => s.show);
  const theme = useThemeStore((s) => s.theme);
  const toggleTheme = useThemeStore((s) => s.toggleTheme);

  const rowDir = direction === 'rtl' ? 'row-reverse' : 'row';
  const isDark = theme === 'dark';

  const handleLocaleChange = (nextLocale: Locale) => {
    if (nextLocale === locale) return;
    const nextDirection = directionForLocale(nextLocale);
    const currentDirection = directionForLocale(locale);
    if (Platform.OS !== 'web' && nextDirection !== currentDirection) {
      // NATIVE: direction change requires a restart for RTL to fully apply.
      showRestartPrompt(nextLocale);
    } else {
      setLocale(nextLocale);
    }
  };

  return (
    <Stack flexDirection={rowDir} alignItems="center" gap="$2">
      {/* Language segmented switch */}
      <Stack
        flexDirection={rowDir}
        backgroundColor="$card"
        borderRadius="$pill"
        borderWidth={1}
        borderColor="$border"
        padding={3}
        gap={3}
        accessibilityRole="radiogroup"
        accessible
        accessibilityLabel={t('common.prefs.language')}
        aria-label={t('common.prefs.language')}
      >
        {LOCALES.map((loc) => {
          const active = loc === locale;
          return (
            <Stack
              key={loc}
              height={32}
              paddingHorizontal="$3"
              alignItems="center"
              justifyContent="center"
              borderRadius="$pill"
              cursor="pointer"
              backgroundColor={active ? '$primary' : 'transparent'}
              onPress={() => handleLocaleChange(loc)}
              accessibilityRole="radio"
              accessible
              accessibilityState={{ selected: active }}
              accessibilityLabel={t(LOCALE_LABEL_KEY[loc])}
              aria-label={t(LOCALE_LABEL_KEY[loc])}
            >
              <Text
                color={active ? '$fg1' : '$fg3'}
                fontSize={13}
                fontWeight="700"
                fontFamily="$heading"
              >
                {t(LOCALE_LABEL_KEY[loc])}
              </Text>
            </Stack>
          );
        })}
      </Stack>

      {/* Theme toggle */}
      <Stack
        width={36}
        height={36}
        alignItems="center"
        justifyContent="center"
        borderRadius="$pill"
        borderWidth={1}
        borderColor="$border"
        backgroundColor="$card"
        cursor="pointer"
        hoverStyle={{ backgroundColor: '$cardSoft' }}
        onPress={toggleTheme}
        accessibilityRole="button"
        accessible
        accessibilityLabel={t(isDark ? 'common.prefs.switchToLight' : 'common.prefs.switchToDark')}
        aria-label={t(isDark ? 'common.prefs.switchToLight' : 'common.prefs.switchToDark')}
      >
        <Text fontSize={16} accessibilityElementsHidden>
          {isDark ? '☀️' : '🌙'}
        </Text>
      </Stack>
    </Stack>
  );
}
