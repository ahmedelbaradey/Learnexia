/**
 * Sign-in failure classification (P1-10-FE-6 / carryover item A1).
 *
 * The backend SignIn endpoint (`SignInCommandHandler`) returns BaseResponse
 * with `Successed=false`, HTTP 400, and a LOCALIZED `message` (en-US or ar-EG
 * depending on Accept-Language; server default is ar-EG). The envelope carries
 * NO machine-readable error code, so this classifier matches the exact backend
 * resource values from
 * `backend/src/Shared/Learnexia.Shared.Resources/SharedResources.{en-US,ar-EG}.resx`:
 *
 *   - `LoginTooManyFailedAttempts` (MSG-LOGIN-004) → AccountLocked
 *   - `LoginAccountDeactivated`    (MSG-LOGIN-003) → AccountDeactivated
 *     (the backend already collapses Suspended + Deleted + IsActive=false into
 *     this single message — no account-state leak)
 *   - any other 400/401/403       → InvalidCredentials. This branch is
 *     deliberately UNIFORM: the FE never distinguishes user-not-found from
 *     wrong-password (anti-enumeration); the backend returns the same message
 *     for both and we treat every unrecognised credential failure identically.
 *   - non-4xx / transport errors  → Unknown (generic retry message)
 *
 * If the backend copy changes, unmatched messages safely degrade to the
 * uniform InvalidCredentials branch — they never leak a more specific cause.
 */

import { isApiError } from '@learnexia/api-client';

export const SignInErrorKind = {
  InvalidCredentials: 'InvalidCredentials',
  AccountLocked: 'AccountLocked',
  AccountDeactivated: 'AccountDeactivated',
  Unknown: 'Unknown',
} as const;

export type SignInErrorKind = (typeof SignInErrorKind)[keyof typeof SignInErrorKind];

/**
 * Exact backend resource strings — backend-contract identifiers (never
 * rendered to the user; the UI shows the admin app's own localized copy from
 * `lib/strings.ts`).
 */
const LOCKED_BACKEND_MESSAGES = [
  // SharedResources.en-US.resx → LoginTooManyFailedAttempts
  'Account temporarily locked due to too many failed login attempts. Please try again later.',
  // SharedResources.ar-EG.resx → LoginTooManyFailedAttempts
  'تم قفل الحساب مؤقتاً بسبب محاولات تسجيل دخول فاشلة كثيرة. يرجى المحاولة لاحقاً.',
] as const;

const DEACTIVATED_BACKEND_MESSAGES = [
  // SharedResources.en-US.resx → LoginAccountDeactivated
  'Your account is inactive. Please contact support.',
  // SharedResources.ar-EG.resx → LoginAccountDeactivated
  'حسابك غير نشط. يرجى الاتصال بالدعم.',
] as const;

/** HTTP statuses the SignIn endpoint uses for credential-level rejections. */
const CREDENTIAL_FAILURE_STATUSES = [400, 401, 403] as const;

export function classifySignInError(err: unknown): SignInErrorKind {
  if (
    !isApiError(err) ||
    !(CREDENTIAL_FAILURE_STATUSES as readonly number[]).includes(err.status)
  ) {
    return SignInErrorKind.Unknown;
  }

  const message = (err.message ?? '').trim();

  if ((LOCKED_BACKEND_MESSAGES as readonly string[]).includes(message)) {
    return SignInErrorKind.AccountLocked;
  }
  if ((DEACTIVATED_BACKEND_MESSAGES as readonly string[]).includes(message)) {
    return SignInErrorKind.AccountDeactivated;
  }

  // Uniform fallback for every other credential failure (anti-enumeration).
  return SignInErrorKind.InvalidCredentials;
}
