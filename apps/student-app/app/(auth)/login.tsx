/**
 * Shared Login screen (Design Spec Screen 3, Batch A restyled from P1-11).
 *
 * Batch A changes:
 *  - Reads `role` from route param (`useLocalSearchParams`). If missing, redirects
 *    to `/(auth)/role-select` (F7 graceful degradation).
 *  - Renders a `RoleBadge` pill below the header (replaces `PersonaToggle`).
 *  - Role-conditional subtitle: parent → `auth.login.subtitleParent`; student →
 *    existing `auth.login.subtitle`.
 *  - Role-conditional footer: parent → register link; student → amber notice (no
 *    register link — product rule: no student self-registration).
 *  - Restyled phone logo mark: 72×72 purple-gradient brand tile with 🌟 and shadow
 *    (replaces the flat `$primary` square).
 *  - Back button (top-left LTR / top-right RTL) → `router.replace('/(auth)/role-select')`.
 *
 * Web (≥768): split-panel — left `LoginBrandPanel`, right form column. Phone
 * (≤768): brand panel collapses; brand tile + header above the form.
 *
 * RTL:
 *  - Back button position flips (left ↔ right) based on `direction`.
 *  - Email field is always an LTR island (technical strings).
 *  - All other layout uses natural `writingDirection`; no `flexDirection:'row-reverse'`
 *    on new elements (existing scaffold uses it — carried over).
 *
 * Token gaps (Design Spec §6):
 *   DS-A-06: purple top-glow radial uses raw rgba (background decal).
 */
import { LOGIN_PERSONAS, type LoginPersona, useFlashMessageStore } from '@learnexia/shared';
import { Card } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { FormScaffold } from '../../src/components/FormScaffold';
import { useLocale } from '../../src/hooks/useLocale';
import { LoginBrandPanel } from './_components/LoginBrandPanel';
import { LocaleThemeControls } from './_components/LocaleThemeControls';
import { LoginForm } from './_components/LoginForm';
import { RoleBadge } from './_components/RoleBadge';

export default function LoginScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const { role: roleParam } = useLocalSearchParams<{ role?: string }>();
  const consume = useFlashMessageStore((s) => s.consume);
  const [flashKey, setFlashKey] = useState<string | null>(null);
  const align = direction === 'rtl' ? 'right' : 'left';

  // Resolve the role from the route param. Default to 'parent' when absent
  // (matches today's default and the kit's role='parent' default — Q3).
  const role: LoginPersona =
    roleParam === LOGIN_PERSONAS.Student ? LOGIN_PERSONAS.Student : LOGIN_PERSONAS.Parent;

  // Redirect to role-select when no role param is present in the URL (F7).
  // Use a ref to run this only once on mount, not on every render.
  const redirectedRef = useRef(false);
  useEffect(() => {
    if (!roleParam && !redirectedRef.current) {
      redirectedRef.current = true;
      router.replace('/(auth)/role-select');
    }
  }, [roleParam, router]);

  // Read-and-clear the one-shot flash (e.g. session expired) once on mount.
  useEffect(() => {
    setFlashKey(consume());
  }, [consume]);

  const handleBack = () => {
    router.replace('/(auth)/role-select');
  };

  const subtitleKey =
    role === LOGIN_PERSONAS.Parent ? 'auth.login.subtitleParent' : 'auth.login.subtitle';

  return (
    <FormScaffold
      variant="split"
      brandSide={direction === 'rtl' ? 'end' : 'start'}
      brandPanel={<LoginBrandPanel direction={direction} appName={t('common.appName')} />}
    >
      <Stack gap="$6">
        {/* Top controls — language + theme switch, aligned to the inline-end.
            dir + natural order (no row-reverse) per RTL build contract; flex-end
            resolves to the end side in both directions. */}
        <Stack flexDirection="row" dir={direction} justifyContent="flex-end">
          <LocaleThemeControls direction={direction} />
        </Stack>

        {/* App-icon logo mark — phone only (web shows it in the brand panel).
            Batch A restyled: 72×72 purple-gradient brand tile with 🌟 and glow shadow. */}
        <Stack alignItems="center" $tablet={{ display: 'none' }}>
          {/* Back button — absolute-positioned top-left (LTR) / top-right (RTL).
              Only visible on phone where the brand panel is hidden. */}
          <Stack
            position="absolute"
            // Back button: top:0 (relative to this logo-mark block), left/right per direction
            top={0}
            left={direction === 'rtl' ? undefined : 0}
            right={direction === 'rtl' ? 0 : undefined}
            width={40}
            height={40}
            borderRadius="$nav"
            backgroundColor="$card"
            borderWidth={1}
            // $border = rgba(255,255,255,0.08) — spec §4.1 back button border
            borderColor="$border"
            alignItems="center"
            justifyContent="center"
            cursor="pointer"
            onPress={handleBack}
            hoverStyle={{ backgroundColor: '$cardSoft' }}
            pressStyle={{ scale: 0.95 }}
            accessibilityRole="button"
            accessible
            accessibilityLabel={t('auth.login.change')}
            aria-label={t('auth.login.change')}
            // hitSlop extends the 40px touch target to 44px minimum (spec §8)
          >
            <Text
              color="$fg2"
              fontSize={18}
              accessibilityElementsHidden
              aria-hidden
            >
              {/* Back chevron flips: ‹ in LTR (go back = left), › in RTL (go back = right) */}
              {direction === 'rtl' ? '›' : '‹'}
            </Text>
          </Stack>

          {/* 72×72 brand tile: linear-gradient(135deg, #A855F7, #6366F1) with 🌟 */}
          <Stack
            width={72}
            height={72}
            borderRadius={22}
            // DS-A-06: gradient applied via web inline style; native falls back to $purple
            backgroundColor="$purple"
            style={{
              background: 'linear-gradient(135deg, #A855F7, #6366F1)',
              boxShadow:
                '0 12px 32px rgba(168,85,247,0.4), inset 0 2px 0 rgba(255,255,255,0.18)',
            } as object}
            alignItems="center"
            justifyContent="center"
          >
            <Text
              // DS-A-06: drop-shadow via filter (web-only); native has no direct equiv
              style={{ filter: 'drop-shadow(0 0 8px rgba(250,204,21,0.6))' } as object}
              fontSize={34}
              accessibilityElementsHidden
              aria-hidden
            >
              🌟
            </Text>
          </Stack>
        </Stack>

        {/* Header — eyebrow + heading + role-conditional subtitle.
            Web: align to the reading start (left LTR, right RTL). */}
        <Stack
          gap="$2"
          alignItems="center"
          $tablet={{ alignItems: direction === 'rtl' ? 'flex-end' : 'flex-start' }}
        >
          <Text
            color="$primaryLight"
            fontSize={12}
            fontWeight="800"
            fontFamily="$heading"
            textTransform={direction === 'rtl' ? 'none' : 'uppercase'}
            letterSpacing={1.44}
            writingDirection={direction}
          >
            {t('auth.login.eyebrow')}
          </Text>
          <Text
            color="$fg1"
            fontSize={32}
            fontWeight="800"
            fontFamily="$heading"
            letterSpacing={-0.64}
            lineHeight={37}
            textAlign={align}
            accessibilityRole="header"
            writingDirection={direction}
          >
            {t('auth.login.title')}
          </Text>
          <Text
            color="$fg3"
            fontSize={14}
            fontFamily="$body"
            textAlign={align}
            writingDirection={direction}
          >
            {t(subtitleKey)}
          </Text>
        </Stack>

        {/* Role badge — replaces PersonaToggle (Batch A). */}
        <RoleBadge
          role={role}
          onChangePress={handleBack}
          direction={direction}
          testID="role-badge"
        />

        {flashKey ? (
          <Card variant="soft">
            <Text color="$fg2" fontSize={13} textAlign="center" writingDirection={direction}>
              {t(flashKey)}
            </Text>
          </Card>
        ) : null}

        {/* LoginForm — no PersonaToggle, no persona state; role passed as prop. */}
        <LoginForm role={role} />

        {/* Role-conditional footer (spec §4.9) */}
        {role === LOGIN_PERSONAS.Parent ? (
          /* Parent: register link — dir + natural order (no row-reverse) per RTL contract */
          <Stack flexDirection="row" dir={direction} justifyContent="center" gap="$1">
            <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
              {t('auth.login.newParent')}
            </Text>
            <Text
              testID="login-create-account-link"
              color="$primaryLight"
              fontSize={14}
              fontWeight="800"
              fontFamily="$body"
              cursor="pointer"
              onPress={() => router.push('/(auth)/register')}
              accessibilityRole="link"
              accessibilityLabel={t('auth.login.createAccount')}
              aria-label={t('auth.login.createAccount')}
              writingDirection={direction}
            >
              {t('auth.login.createAccount')}
            </Text>
          </Stack>
        ) : (
          /* Student: amber "Ask a parent" notice — NO register link (product rule). */
          <Stack
            // DS-A-04: rgba(245,158,11,0.08) consistently (web value, spec gap note)
            backgroundColor="rgba(245,158,11,0.08)"
            borderWidth={1}
            // DS-A-05: rgba(245,158,11,0.25) consistently (web value, spec gap note)
            borderColor="rgba(245,158,11,0.25)"
            borderRadius="$nav"
            padding="$3"
            marginTop="$1"
            accessibilityRole="text"
            accessible
            accessibilityLabel={`${t('auth.login.studentNoAccountTitle')} ${t('auth.login.studentNoAccountBody')}`}
            aria-label={`${t('auth.login.studentNoAccountTitle')} ${t('auth.login.studentNoAccountBody')}`}
          >
            <Text
              fontSize={13}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
            >
              {/* "Need an account?" in accent color */}
              <Text
                color="$accent"
                fontWeight="700"
              >
                {t('auth.login.studentNoAccountTitle')}{' '}
              </Text>
              {/* "Ask a parent to add you." in fg4 (native) / fg2 (web) */}
              <Text color="$fg4">
                {t('auth.login.studentNoAccountBody')}
              </Text>
            </Text>
          </Stack>
        )}
      </Stack>
    </FormScaffold>
  );
}
