/**
 * Component catalog — a typed, runnable gallery of every P1-08 component across
 * variants × en/ar locale × dark/light theme.
 *
 * This is NOT a test; it is a React component tree importable in any RN-Web /
 * Expo host: `import { Catalog } from '@learnexia/ui/catalog'`. Full
 * Expo-native + Next.js host verification is deferred to P1-09 (no Metro /
 * @tamagui/next-plugin host exists yet). See README.md.
 *
 * A "passing" entry renders without TS error, with all required props supplied,
 * no empty/undefined visuals, correct direction + font per locale, and the dark
 * surface ($bg) / light surface ($bgLight) visible per theme. See README.md §
 * "Passing render criteria".
 */
import { LearnexiaProvider, type ThemeName } from '@learnexia/design-system';
import { Stack } from '@tamagui/core';
import React, { useState } from 'react';

import {
  AITutorBubble,
  Badge,
  Button,
  Card,
  Hearts,
  RewardPopup,
  StreakFlame,
  XPBar,
} from '../src';
import { Text, XStack, YStack } from '../src/internal/primitives';

type Locale = 'en' | 'ar';

function Section({ name, children }: { name: string; children: React.ReactNode }) {
  return (
    <YStack gap="$3" padding="$4" borderColor="$border" borderWidth={1} borderRadius="$card">
      <Text fontSize={18} fontWeight="700" color="$fg1" fontFamily="$heading">
        {name}
      </Text>
      <XStack gap="$6" flexWrap="wrap">
        {children}
      </XStack>
    </YStack>
  );
}

/** All components for a single locale, inside one provider. */
export function LocaleColumn({ locale, theme }: { locale: Locale; theme: ThemeName }) {
  return (
    <LearnexiaProvider locale={locale} theme={theme}>
      <Stack backgroundColor="$background" padding="$4" gap="$6">
        <Text fontSize={24} fontWeight="800" color="$fg1" fontFamily="$heading">
          {locale === 'ar' ? 'العربية (RTL)' : 'English (LTR)'} · {theme}
        </Text>

        <Section name="Button">
          <Button accessibilityLabel="primary">Primary</Button>
          <Button variant="secondary" accessibilityLabel="secondary">Secondary</Button>
          <Button variant="success" accessibilityLabel="success">Success</Button>
          <Button variant="danger" accessibilityLabel="danger">Danger</Button>
          <Button variant="ghost" accessibilityLabel="ghost">Ghost</Button>
          <Button disabled accessibilityLabel="disabled">Disabled</Button>
          <Button loading accessibilityLabel="loading">Loading</Button>
          <Button size="sm" accessibilityLabel="small">Small</Button>
        </Section>

        <Section name="Card">
          <Card>
            <Text color="$fg1">Default card</Text>
          </Card>
          <Card variant="soft">
            <Text color="$fg1">Soft card</Text>
          </Card>
        </Section>

        <Section name="XPBar">
          <YStack width={280} gap="$4">
            <XPBar currentXp={820} totalXp={1000} level={7} locale={locale} accessibilityLabel="820 of 1000 XP, level 7" />
            <XPBar currentXp={1000} totalXp={1000} level={7} locale={locale} accessibilityLabel="Level up" />
            <XPBar currentXp={200} totalXp={1500} level={3} locale={locale} accessibilityLabel="200 of 1500 XP, level 3" />
          </YStack>
        </Section>

        <Section name="Hearts">
          <Hearts current={5} locale={locale} accessibilityLabel="5 of 5 hearts" />
          <Hearts current={3} locale={locale} accessibilityLabel="3 of 5 hearts" />
          <Hearts current={0} locale={locale} accessibilityLabel="0 of 5 hearts" />
          <Hearts current={2} recoveringIn={23} locale={locale} accessibilityLabel="2 of 5 hearts, recovering" />
        </Section>

        <Section name="StreakFlame">
          <StreakFlame days={7} size="md" locale={locale} accessibilityLabel="7 day streak" />
          <StreakFlame days={7} size="sm" locale={locale} accessibilityLabel="7 day streak" />
          <StreakFlame days={1} size="md" locale={locale} accessibilityLabel="1 day streak" />
        </Section>

        <Section name="Badge">
          <Badge variant="bronze" label="Bronze" sublabel="Starter" accessibilityLabel="Bronze badge, earned" />
          <Badge variant="silver" label="Silver" sublabel="Rising" accessibilityLabel="Silver badge, earned" />
          <Badge variant="gold" label="Gold" sublabel="Master" accessibilityLabel="Gold badge, earned" />
          <Badge variant="legendary" label="Legendary" sublabel="Elite" accessibilityLabel="Legendary badge, earned" />
          <Badge variant="locked" label="Locked" accessibilityLabel="Locked badge" />
          <Badge variant="gold" label="New!" isNewlyEarned accessibilityLabel="Gold badge, just earned" />
        </Section>

        <Section name="AITutorBubble">
          <AITutorBubble
            variant="ai"
            sender="Lexi · AI Tutor"
            message={locale === 'ar' ? 'أحسنت! لنجرب سؤالاً آخر.' : 'Great job! Let us try another one.'}
            chips={[
              { label: locale === 'ar' ? 'تلميح' : 'Hint' },
              { label: locale === 'ar' ? 'اشرح' : 'Explain' },
            ]}
            locale={locale}
            accessibilityLabel="AI tutor message"
          />
          <AITutorBubble variant="student" sender="Ahmed" message={locale === 'ar' ? 'فهمت الآن!' : 'I get it now!'} locale={locale} accessibilityLabel="Student message" />
          <AITutorBubble variant="hint" sender="Hint" message={locale === 'ar' ? 'فكّر في الأرقام الزوجية.' : 'Think about even numbers.'} locale={locale} accessibilityLabel="Hint message" />
          <AITutorBubble variant="ai" sender="Lexi · AI Tutor" message={null} locale={locale} accessibilityLabel="AI tutor is typing" />
        </Section>

        <Section name="RewardPopup">
          <Stack width={360} height={360} position="relative" backgroundColor="$bgElevated" borderRadius="$card" overflow="hidden">
            <RewardPopup
              variant="xp"
              xpAmount={120}
              title={locale === 'ar' ? 'اكتمل الدرس!' : 'Lesson Complete!'}
              subtitle={locale === 'ar' ? 'عمل رائع' : 'Awesome work'}
              visible
              onDismiss={() => undefined}
              locale={locale}
              accessibilityLabel="Lesson complete, plus 120 XP"
            />
          </Stack>
        </Section>
      </Stack>
    </LearnexiaProvider>
  );
}

/** Full catalog with locale + theme switchers. */
export function Catalog() {
  const [locale, setLocale] = useState<Locale>('en');
  const [theme, setTheme] = useState<ThemeName>('dark');

  return (
    <LearnexiaProvider locale={locale} theme={theme}>
      <Stack backgroundColor="$background" padding="$4" gap="$4">
        <XStack gap="$3">
          <Button size="sm" accessibilityLabel="toggle locale" onPress={() => setLocale((l) => (l === 'en' ? 'ar' : 'en'))}>
            {`Locale: ${locale}`}
          </Button>
          <Button size="sm" variant="secondary" accessibilityLabel="toggle theme" onPress={() => setTheme((t) => (t === 'dark' ? 'light' : 'dark'))}>
            {`Theme: ${theme}`}
          </Button>
        </XStack>
        <LocaleColumn locale={locale} theme={theme} />
      </Stack>
    </LearnexiaProvider>
  );
}

Catalog.displayName = 'Catalog';

export default Catalog;
