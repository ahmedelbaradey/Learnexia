'use client';

/**
 * Admin Sign-In page (FE-2, Design Spec §3).
 *
 * Flow:
 *   1. react-hook-form + zod validate { userName, password }.
 *   2. `useSignIn` → POST /api/Users/Authentication/Sign-In (skipAuth).
 *   3. On success: persist tokens via `authStore.setTokens` (writes through to
 *      sessionStorage AND in-memory, flipping status → 'signed-in').
 *   4. Admin role GATE via JWT-claim decode: decode the access token, read role
 *      claims, check Admin/SuperAdmin case-insensitively (lib/jwt). NO call to
 *      GetUserProfile / Me.
 *      - Non-admin → `authStore.signOut()` + show forbidden banner; DO NOT enter
 *        the shell.
 *      - Admin → set `authStore.user` (id + normalized roles) + redirect to
 *        /dashboard.
 *
 * There is NO registration / forgot-password link anywhere (by design).
 *
 * UI is built entirely from the shared design system: the labelled inputs use the
 * universal `TextField` primitive from `@learnexia/ui` (built on RN TextInput →
 * RN Web DOM input), the container is the shared `Card`, and the submit is the
 * shared `Button`. No CSS modules / plain `<input>` on this surface.
 */

import { useSignIn } from '@learnexia/api-client';
import { useAuthStore } from '@learnexia/shared/stores';
import { zodResolver } from '@hookform/resolvers/zod';
import Image from 'next/image';
import { useRouter } from 'next/navigation';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';

import { Button } from '@learnexia/ui/components/Button';
import { Card } from '@learnexia/ui/components/Card';
import { TextField } from '@learnexia/ui/components/TextField';
import { Stack, Text } from '@tamagui/core';

import { AdminErrorBanner, type AdminBannerVariant } from '../../components/AdminErrorBanner';
import { getRolesFromToken, isAdminRoleList, normalizeRoles } from '../../lib/jwt';
import { SignInErrorKind, classifySignInError } from '../../lib/signInErrors';
import { ADMIN_LOCALE, getStrings } from '../../lib/strings';
import { signInSchema, type SignInFormValues } from '../../lib/signInSchema';

const strings = getStrings(ADMIN_LOCALE);

interface BannerState {
  variant: AdminBannerVariant;
  message: string;
}

export default function LoginPage() {
  const router = useRouter();
  const signIn = useSignIn();
  const setTokens = useAuthStore((s) => s.setTokens);
  const setUser = useAuthStore((s) => s.setUser);
  const storeSignOut = useAuthStore((s) => s.signOut);

  const [banner, setBanner] = useState<BannerState | null>(null);
  const [gating, setGating] = useState(false);

  const {
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { userName: '', password: '' },
  });

  const submitting = signIn.isPending || gating;

  const onSubmit = handleSubmit(async (values) => {
    setBanner(null);
    setGating(false);
    try {
      const auth = await signIn.mutateAsync({
        userName: values.userName,
        password: values.password,
      });

      const accessToken = auth.accessToken ?? '';
      const refreshToken = auth.refreshToken?.tokenString ?? '';
      if (!accessToken || !refreshToken) {
        setBanner({ variant: 'warning', message: strings.errNetwork });
        return;
      }

      // Persist first so the JWT is readable from storage/memory.
      setGating(true);
      await setTokens({ accessToken, refreshToken });

      // Admin role gate via JWT-claim decode (case-insensitive).
      const roles = getRolesFromToken(accessToken);
      if (!isAdminRoleList(roles)) {
        // Valid credentials but NOT an admin → reject; do not enter the shell.
        await storeSignOut();
        setGating(false);
        setBanner({ variant: 'forbidden', message: strings.errForbidden });
        return;
      }

      // Admin: set identity + enter the shell.
      setUser({
        id: auth.userId ?? 0,
        userName: values.userName,
        roles: normalizeRoles(roles),
      });
      router.replace('/dashboard');
    } catch (err) {
      setGating(false);
      // Distinct localized messages for locked / deactivated (carryover A1).
      // Every OTHER credential failure shows the SAME uniform invalid-credentials
      // message — never user-not-found vs wrong-password (anti-enumeration).
      switch (classifySignInError(err)) {
        case SignInErrorKind.AccountLocked:
          setBanner({ variant: 'warning', message: strings.errAccountLocked });
          break;
        case SignInErrorKind.AccountDeactivated:
          setBanner({ variant: 'error', message: strings.errAccountDeactivated });
          break;
        case SignInErrorKind.InvalidCredentials:
          setBanner({ variant: 'error', message: strings.errInvalidCredentials });
          break;
        default:
          // Network / 5xx → generic retry message.
          setBanner({ variant: 'warning', message: strings.errNetwork });
      }
    }
  });

  return (
    <Stack
      flexDirection="column"
      alignItems="center"
      backgroundColor="$bg"
      paddingTop="$16"
      paddingHorizontal="$4"
      style={{ minHeight: '100vh' }}
      $sm={{ paddingTop: '$6', paddingHorizontal: 0 }}
      $tablet={{ paddingTop: 40 }}
    >
      <Stack
        flexDirection="column"
        alignItems="center"
        width="100%"
        maxWidth={440}
        $tablet={{ maxWidth: 400 }}
      >
        <Image
          src="/assets/logo.svg"
          alt="Learnexia"
          width={180}
          height={48}
          priority
          style={{ height: 36, width: 'auto', marginBottom: 32 }}
        />

        <Card
          variant="default"
          borderRadius="$modal"
          padding="$8"
          width="100%"
          $sm={{ borderRadius: 0 }}
        >
          <Stack flexDirection="column" gap="$2">
            <Text fontFamily="$heading" fontSize={24} fontWeight="700" color="$fg1">
              {strings.loginHeading}
            </Text>
            <Text fontFamily="$body" fontSize={14} color="$fg3">
              {strings.loginSubheading}
            </Text>
          </Stack>

          <Stack
            tag="form"
            flexDirection="column"
            gap="$6"
            marginTop="$6"
            // @ts-expect-error — DOM form submit handler on a Tamagui web element.
            onSubmit={onSubmit}
            noValidate
          >
            <Controller
              control={control}
              name="userName"
              render={({ field }) => (
                <TextField
                  label={strings.usernameLabel}
                  placeholder={strings.usernamePlaceholder}
                  value={field.value}
                  onChangeText={field.onChange}
                  disabled={submitting}
                  autoComplete="username"
                  locale={ADMIN_LOCALE}
                  error={errors.userName?.message}
                  testID="login-username"
                />
              )}
            />

            <Controller
              control={control}
              name="password"
              render={({ field }) => (
                <TextField
                  label={strings.passwordLabel}
                  placeholder={strings.passwordPlaceholder}
                  value={field.value}
                  onChangeText={field.onChange}
                  disabled={submitting}
                  autoComplete="current-password"
                  secureTextEntry
                  locale={ADMIN_LOCALE}
                  error={errors.password?.message}
                  testID="login-password"
                />
              )}
            />

            {banner ? (
              <AdminErrorBanner variant={banner.variant} message={banner.message} />
            ) : null}

            <Button
              variant="primary"
              size="full"
              accessibilityLabel={strings.signInButton}
              loading={submitting}
              testID="login-submit"
              onPress={() => {
                void onSubmit();
              }}
            >
              {submitting ? strings.signingInButton : strings.signInButton}
            </Button>
          </Stack>
        </Card>

        <Text fontFamily="$body" fontSize={14} color="$fg3" textAlign="center" marginTop="$4">
          {strings.finePrint}
        </Text>
      </Stack>
    </Stack>
  );
}
