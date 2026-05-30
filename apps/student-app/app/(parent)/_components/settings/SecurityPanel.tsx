/**
 * SecurityPanel — Settings → Security tab (P2-12c).
 *
 * Two sub-sections inside one PanelSurface, separated by a 1 px divider:
 *  1. Change-password form (current / new / confirm + PasswordStrengthMeter).
 *  2. Active-sessions list + "Sign out other sessions" CTA.
 *
 * AC: S1 — 3 TextField (secureTextEntry + show/hide + forceLtr) + PasswordStrengthMeter.
 *     S2 — POST /api/Users/Account/ChangePassword; success clears form + strip.
 *     S3 — GET sessions list (truncated id, expiresAt, Active/Expired pill).
 *     S4 — Sign-out-others POST; re-fetches list on success.
 *     S5 — forceLtr on all password fields.
 *
 * Security: passwords are NEVER logged (no console.log). Session IDs are rendered
 * truncated to 8 chars only. Password values live only in controlled useState
 * and are not included in any query key or TanStack cache entry.
 *
 * RTL: `flexDirection={rowDir}` on action rows; text `writingDirection={direction}`;
 *      password fields `forceLtr` regardless of locale.
 * Tokens only — no raw hex (rgba border is sibling-consistent).
 */
import {
  useChangePassword,
  useMySessions,
  useSignOutOtherSessions,
  queryKeys,
  type SessionInfo,
} from '@learnexia/api-client';
import { useQueryClient } from '@tanstack/react-query';
import { Button, TextField, PasswordStrengthMeter, PASSWORD_STRENGTH, type PasswordStrength } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../../src/hooks/useLocale';
import { useServerError } from '../../../../src/hooks/useServerError';

interface SecurityPanelProps {
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
}

/** Simple password scorer (0-4). Mirrors the score used in Register. */
function scorePassword(pwd: string): PasswordStrength {
  if (!pwd) return PASSWORD_STRENGTH.Empty;
  let score = 0;
  if (pwd.length >= 6) score++;
  if (pwd.length >= 10) score++;
  if (/[A-Z]/.test(pwd) && /[a-z]/.test(pwd)) score++;
  if (/[0-9]/.test(pwd) && /[^A-Za-z0-9]/.test(pwd)) score++;
  return (Math.min(score, 4) as PasswordStrength);
}

const STRENGTH_KEY: Record<PasswordStrength, string> = {
  0: 'auth.register.strength.weak',
  1: 'auth.register.strength.weak',
  2: 'auth.register.strength.fair',
  3: 'auth.register.strength.good',
  4: 'auth.register.strength.strong',
};

function PanelSurface({ children }: { children: React.ReactNode }) {
  return (
    <Stack
      flexDirection="column"
      gap={18}
      borderRadius="$modal"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      padding={22}
    >
      {children}
    </Stack>
  );
}

function PanelHeader({
  title,
  subtitle,
  direction,
}: {
  title: string;
  subtitle: string;
  direction: 'ltr' | 'rtl';
}) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text
        color="$fg1"
        fontSize={16}
        fontWeight="800"
        fontFamily="$heading"
        writingDirection={direction}
      >
        {title}
      </Text>
      <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
        {subtitle}
      </Text>
    </Stack>
  );
}

/** Inline status pill for session Active / Expired. */
function SessionStatusPill({
  isActive,
  activeLabel,
  expiredLabel,
}: {
  isActive: boolean;
  activeLabel: string;
  expiredLabel: string;
}) {
  return (
    <Stack
      backgroundColor={isActive ? '$successSoft' : '$dangerSoft'}
      borderRadius={9999}
      paddingHorizontal={10}
      paddingVertical={4}
    >
      <Text
        color={isActive ? '$success' : '$danger'}
        fontSize={12}
        fontWeight="600"
        fontFamily="$body"
      >
        {isActive ? activeLabel : expiredLabel}
      </Text>
    </Stack>
  );
}

function SessionRow({
  session,
  rowDir,
  locale,
  activeLabel,
  expiredLabel,
  expiresAtLabel,
}: {
  session: SessionInfo;
  rowDir: 'row' | 'row-reverse';
  locale: string;
  activeLabel: string;
  expiredLabel: string;
  expiresAtLabel: (date: string) => string;
}) {
  // Truncate session id to 8 chars — rendered forceLtr (technical string).
  const truncatedId = session.sessionId ? `${session.sessionId.slice(0, 8)}…` : '—';
  // Format expiry — locale-aware for the date; Eastern-Arabic numerals will appear
  // in 'ar' locale via toLocaleString (correct per SKILL.md Skill 4).
  const expiryFormatted = session.expiresAt
    ? new Date(session.expiresAt).toLocaleString(locale)
    : '—';

  return (
    <Stack
      flexDirection={rowDir}
      alignItems="center"
      gap={12}
      paddingVertical={12}
      paddingHorizontal={14}
      backgroundColor="$bgElevated"
      borderRadius={14}
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
    >
      <Stack flex={1} gap={2}>
        {/* Session ID — always LTR (technical string per SKILL.md) */}
        <Stack direction="ltr">
          <Text
            color="$fg1"
            fontSize={13}
            fontWeight="700"
            fontFamily="$body"
            // fontVariant tabular-nums for consistent id spacing (web)
          >
            {truncatedId}
          </Text>
        </Stack>
        {/* Expiry date — locale-aware */}
        <Stack direction="ltr">
          <Text color="$fg3" fontSize={12} fontFamily="$body">
            {expiresAtLabel(expiryFormatted)}
          </Text>
        </Stack>
      </Stack>
      <SessionStatusPill
        isActive={session.isActive ?? false}
        activeLabel={activeLabel}
        expiredLabel={expiredLabel}
      />
    </Stack>
  );
}

export function SecurityPanel({ direction, rowDir }: SecurityPanelProps) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const resolveError = useServerError();

  // --- Change-password form state ---
  // Passwords live ONLY in these controlled states. Never logged, never in query keys.
  const [currentPwd, setCurrentPwd] = useState('');
  const [newPwd, setNewPwd] = useState('');
  const [confirmPwd, setConfirmPwd] = useState('');
  const [pwdSuccess, setPwdSuccess] = useState(false);

  const changePassword = useChangePassword();
  const sessionsQuery = useMySessions();
  const signOutOthersMutation = useSignOutOtherSessions();
  const [signOutSuccess, setSignOutSuccess] = useState(false);
  const [otherSessionsClosed, setOtherSessionsClosed] = useState<number | null>(null);
  const queryClient = useQueryClient();

  // Client-side validation guards.
  const mismatch = confirmPwd.length > 0 && newPwd !== confirmPwd;
  const sameAsCurrent = newPwd.length > 0 && newPwd === currentPwd;
  const isFormValid =
    currentPwd.trim().length > 0 &&
    newPwd.trim().length > 0 &&
    confirmPwd.trim().length > 0 &&
    !mismatch &&
    !sameAsCurrent;

  const pwdServerMessage = changePassword.isError
    ? resolveError(changePassword.error, {
        byStatus: {
          400: 'parent.settings.security.saveErrorCurrent',
          422: 'parent.settings.security.saveErrorPolicy',
        },
        hints: [
          { contains: ['policy', 'requirements', 'weak'], key: 'parent.settings.security.saveErrorPolicy' },
          { contains: ['current', 'wrong', 'incorrect'], key: 'parent.settings.security.saveErrorCurrent' },
        ],
      })
    : null;

  function handleChangePassword() {
    changePassword.mutate(
      { currentPassword: currentPwd, newPassword: newPwd, confirmPassword: confirmPwd },
      {
        onSuccess: () => {
          // Clear form — passwords gone from state immediately.
          setCurrentPwd('');
          setNewPwd('');
          setConfirmPwd('');
          setPwdSuccess(true);
          setTimeout(() => setPwdSuccess(false), 4000);
          // The BE killed other sessions — invalidate to drop stale cache + refetch.
          queryClient.invalidateQueries({ queryKey: queryKeys.account.sessions() });
        },
      },
    );
  }

  function handleSignOutOthers() {
    const closedCount = Math.max(0, (sessionsQuery.data ?? []).length - 1);
    signOutOthersMutation.mutate(undefined, {
      onSuccess: () => {
        setOtherSessionsClosed(closedCount);
        setSignOutSuccess(true);
        setTimeout(() => setSignOutSuccess(false), 3000);
      },
    });
  }

  const sessions = sessionsQuery.data ?? [];

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.security.title')}
        subtitle={t('parent.settings.security.subtitle')}
        direction={direction}
      />

      {/* ── Change-password sub-section ── */}
      <Stack flexDirection="column" gap={14}>
        <TextField
          label={t('parent.settings.security.currentPassword')}
          value={currentPwd}
          onChangeText={setCurrentPwd}
          secureTextEntry
          autoComplete="current-password"
          direction={direction}
          forceLtr
          disabled={changePassword.isPending}
          accessibilityLabel={t('parent.settings.security.currentPassword')}
        />

        <TextField
          label={t('parent.settings.security.newPassword')}
          value={newPwd}
          onChangeText={setNewPwd}
          secureTextEntry
          autoComplete="new-password"
          direction={direction}
          forceLtr
          disabled={changePassword.isPending}
          accessibilityLabel={t('parent.settings.security.newPassword')}
        />

        <PasswordStrengthMeter
          score={scorePassword(newPwd)}
          label={t(STRENGTH_KEY[scorePassword(newPwd)])}
          direction={direction}
          accessibilityLabel={t('auth.register.strength.a11y', {
            label: t(STRENGTH_KEY[scorePassword(newPwd)]),
          })}
        />

        <TextField
          label={t('parent.settings.security.confirmPassword')}
          value={confirmPwd}
          onChangeText={setConfirmPwd}
          secureTextEntry
          autoComplete="new-password"
          direction={direction}
          forceLtr
          disabled={changePassword.isPending}
          accessibilityLabel={t('parent.settings.security.confirmPassword')}
        />

        {/* Client-side validation hints */}
        {mismatch ? (
          <Text
            color="$danger"
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            accessibilityLiveRegion="polite"
          >
            {t('parent.settings.security.mismatchError')}
          </Text>
        ) : null}
        {sameAsCurrent ? (
          <Text
            color="$danger"
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
            accessibilityLiveRegion="polite"
          >
            {t('parent.settings.security.sameAsCurrent')}
          </Text>
        ) : null}

        {/* Server error banner */}
        <ServerErrorBanner message={pwdServerMessage} direction={direction} />

        {/* Password change success strip */}
        {pwdSuccess ? (
          <Stack
            backgroundColor="$successSoft"
            borderRadius="$sm"
            padding={12}
            accessibilityLiveRegion="polite"
          >
            <Text
              color="$success"
              fontSize={14}
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {t('parent.settings.security.saveSuccess')}
            </Text>
          </Stack>
        ) : null}

        {/* Save button */}
        <Stack flexDirection={rowDir} justifyContent="flex-end" gap={10} paddingTop={6}>
          <Button
            variant="primary"
            size="md"
            loading={changePassword.isPending}
            disabled={!isFormValid || changePassword.isPending}
            accessibilityLabel={t('parent.settings.security.save')}
            accessibilityState={{ disabled: !isFormValid || changePassword.isPending }}
            onPress={handleChangePassword}
          >
            {t('parent.settings.security.save')}
          </Button>
        </Stack>
      </Stack>

      {/* ── Divider ── */}
      <Stack
        height={1}
        marginVertical={8}
        backgroundColor="rgba(255,255,255,0.06)"
      />

      {/* ── Active sessions sub-section ── */}
      <Stack flexDirection="column" gap={12}>
        {/* Section header + sign-out CTA */}
        <Stack
          flexDirection={rowDir}
          justifyContent="space-between"
          alignItems="flex-start"
          gap={12}
        >
          <Stack flexDirection="column" gap={2} flex={1}>
            <Text
              color="$fg1"
              fontSize={14}
              fontWeight="800"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {t('parent.settings.security.sessionsTitle')}
            </Text>
            <Text
              color="$fg3"
              fontSize={12}
              fontFamily="$body"
              writingDirection={direction}
            >
              {t('parent.settings.security.sessionsSubtitle')}
            </Text>
          </Stack>
          <Button
            variant="ghost"
            size="sm"
            loading={signOutOthersMutation.isPending}
            accessibilityLabel={t('parent.settings.security.signOutOthers')}
            onPress={handleSignOutOthers}
          >
            {t('parent.settings.security.signOutOthers')}
          </Button>
        </Stack>

        {/* Sign-out success strip */}
        {signOutSuccess ? (
          <Stack
            backgroundColor="$successSoft"
            borderRadius="$sm"
            padding={12}
            accessibilityLiveRegion="polite"
          >
            <Text
              color="$success"
              fontSize={14}
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {t('parent.settings.security.signOutOthersSuccess', { count: otherSessionsClosed ?? 0 })}
            </Text>
          </Stack>
        ) : null}

        {/* Sessions list */}
        {sessionsQuery.isPending ? (
          <Stack alignItems="center" paddingVertical="$4">
            <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
              {t('common.loading')}
            </Text>
          </Stack>
        ) : sessions.length === 0 ? (
          <Stack alignItems="center" paddingVertical="$4">
            <Text
              color="$fg3"
              fontSize={13}
              fontFamily="$body"
              writingDirection={direction}
              textAlign="center"
            >
              {t('parent.settings.security.noActiveSessions')}
            </Text>
          </Stack>
        ) : (
          <Stack
            flexDirection="column"
            gap={8}
          >
            {sessions.map((session, idx) => (
              <SessionRow
                key={session.sessionId ?? idx}
                session={session}
                rowDir={rowDir}
                locale={locale}
                activeLabel={t('parent.settings.security.sessionActive')}
                expiredLabel={t('parent.settings.security.sessionExpired')}
                expiresAtLabel={(date) => t('parent.settings.security.expiresAt', { date })}
              />
            ))}
          </Stack>
        )}
      </Stack>
    </PanelSurface>
  );
}

SecurityPanel.displayName = 'SecurityPanel';
