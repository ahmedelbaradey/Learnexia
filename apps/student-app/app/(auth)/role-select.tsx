/**
 * Role Select screen — the signed-out entry point (Batch A, Design Spec §3).
 *
 * Presents two `RoleCard`s (Parent / Student) on a dark `$bg` canvas. Picking
 * a role pushes `/(auth)/login?role=<parent|student>`.
 *
 * Layout (native): full-screen scroll column, $bg background, paddingTop 70 (safe-area),
 * padding 0 16 24, gap 24 between header / cards / footnote.
 * Layout (web): single centered 460px column with a purple top-glow radial decal.
 *
 * No tab bar — this screen has `tabBar={false}` (enforced by no nav-bar in the
 * `(auth)/_layout.tsx` Stack with `headerShown: false`).
 *
 * RTL (AR):
 *   - Screen container `writingDirection={direction}` so all text flows RTL.
 *   - RoleCard receives `direction` — chevron flips to ‹, source order unchanged.
 *   - "Learnexia" brand word stays Latin/LTR via an inline LTR island in the title.
 *
 * Token gaps resolved:
 *   DS-A-06: purple radial glow uses raw rgba (background decal, not a border/shadow).
 */
import { LOGIN_PERSONAS } from '@learnexia/shared';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import { ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { useLocale } from '../../src/hooks/useLocale';
import { RoleCard } from './_components/RoleCard';

export default function RoleSelectScreen() {
  const { t } = useTranslation();
  const { direction } = useLocale();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  const handleRoleSelect = (role: 'parent' | 'student') => {
    router.push({ pathname: '/(auth)/login', params: { role } });
  };

  return (
    <Stack flex={1} backgroundColor="$bg">
      {/* Purple top-glow radial decal (web-only appearance — DS-A-06 raw rgba) */}
      <Stack
        position="absolute"
        // top: -160 per spec §3.1; left: 50% simulated with alignSelf on web via
        // the full-width parent. We use a centered approach below.
        top={-160}
        alignSelf="center"
        width={620}
        height={420}
        borderRadius={9999}
        // DS-A-06: rgba(168,85,247,0.28) — web role-select glow; raw rgba per spec gap note
        // backgroundColor fallback for native; gradient applied via style for web.
        backgroundColor="rgba(168,85,247,0)"
        // CSS radial gradient — web only; native falls back to the backgroundColor above.
        style={{
          background:
            'radial-gradient(circle, rgba(168,85,247,0.28) 0%, rgba(168,85,247,0) 65%)',
        } as object}
        pointerEvents="none"
        aria-hidden
        accessibilityElementsHidden
      />

      <ScrollView
        contentContainerStyle={{
          paddingTop: Math.max(70, insets.top + 24),
          paddingBottom: 24,
          paddingHorizontal: 16,
        }}
        showsVerticalScrollIndicator={false}
      >
        <Stack
          // Outer flex column, gap: 24 between header / cards / footnote (spec §3.1)
          gap="$6"
          // Web: centered column 460px wide (spec §3.1)
          $tablet={{
            width: 460,
            alignSelf: 'center',
            paddingTop: 24,
          }}
        >
          {/* ── Header block ── */}
          <Stack gap="$2" alignItems="center">
            {/* 🎮 emoji header */}
            <Text
              fontSize={40}
              marginBottom={4}
              accessibilityElementsHidden
              aria-hidden
            >
              🎮
            </Text>

            {/* "Welcome to Learnexia" — "Learnexia" stays Latin/LTR in the AR string */}
            <Text
              color="$fg1"
              fontSize={26}
              fontWeight="900"
              fontFamily="$heading"
              letterSpacing={-0.52}
              textAlign="center"
              writingDirection={direction}
              accessibilityRole="header"
              $tablet={{ fontSize: 30 }}
            >
              {direction === 'rtl' ? (
                // AR: title contains Latin brand word — wrap in LTR island
                <>
                  {t('auth.roleSelect.title').replace('Learnexia', '')}{' '}
                  {/* LTR island for the Latin brand name */}
                  {/* LTR island: "Learnexia" brand name stays Latin even in AR context */}
                  <Text
                    writingDirection="ltr"
                    style={{ unicodeBidi: 'embed' } as object}
                  >
                    Learnexia
                  </Text>
                </>
              ) : (
                t('auth.roleSelect.title')
              )}
            </Text>

            {/* "Who's signing in?" subtitle */}
            <Text
              color="$fg3"
              fontSize={14}
              fontFamily="$body"
              textAlign="center"
              writingDirection={direction}
              marginTop={6}
              $tablet={{ fontSize: 15 }}
            >
              {t('auth.roleSelect.subtitle')}
            </Text>
          </Stack>

          {/* ── Role cards column — gap 10 native / 12 web ── */}
          <Stack gap={10} $tablet={{ gap: 12 }}>
            <RoleCard
              id={LOGIN_PERSONAS.Parent}
              emoji="👨‍👩‍👦"
              label={t('auth.roleSelect.parentLabel')}
              sub={t('auth.roleSelect.parentSub')}
              onPress={() => handleRoleSelect(LOGIN_PERSONAS.Parent)}
              direction={direction}
              testID="role-card-parent"
              accessibilityLabel={t('auth.roleSelect.parentA11y')}
            />
            <RoleCard
              id={LOGIN_PERSONAS.Student}
              emoji="🎓"
              label={t('auth.roleSelect.studentLabel')}
              sub={t('auth.roleSelect.studentSub')}
              onPress={() => handleRoleSelect(LOGIN_PERSONAS.Student)}
              direction={direction}
              testID="role-card-student"
              accessibilityLabel={t('auth.roleSelect.studentA11y')}
            />
          </Stack>

          {/* ── Footnote — no-self-registration notice ── */}
          <Text
            color="$fg4"
            fontSize={12}
            fontFamily="$body"
            textAlign="center"
            writingDirection={direction}
            lineHeight={18}
            marginTop={6}
            $tablet={{ marginTop: 0 }}
          >
            {t('auth.roleSelect.footnote')}
          </Text>
        </Stack>
      </ScrollView>
    </Stack>
  );
}
