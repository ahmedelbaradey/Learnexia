/**
 * Zod schemas shared by the app (react-hook-form resolvers) and api-client.
 *
 * Auth-input schemas only for now — the minimal core needed by the sign-in /
 * refresh flows. Domain-entity schemas can be added as screens require them;
 * we deliberately don't over-model.
 */

import { z } from 'zod';

import { LOCALES, MIN_GRADE, MAX_GRADE } from '../constants';

/* ------------------------------------------------------------------ */
/* Shared field rules                                                  */
/* ------------------------------------------------------------------ */

/**
 * Password policy mirroring the backend `RegisterParentCommandValidator` /
 * `AddChildCommandValidator`: ≥6 chars with at least one lowercase, one
 * uppercase, one digit, and one special character. The error message is an
 * i18n KEY (resolved by the form via `t()`), not a literal string.
 */
export const PASSWORD_REGEX =
  /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$/;

const passwordField = z
  .string()
  .regex(PASSWORD_REGEX, 'auth.register.errors.weakPassword');

/** Minimum password length surfaced to parents (Register capture helper). */
export const PASSWORD_MIN_LENGTH = 6;

/**
 * Register-screen password rule (P1-11 capture): a single password field with a
 * minimal ≥6-char gate and the "At least 6 characters" helper. The backend
 * still enforces the stronger `PASSWORD_REGEX`; that surfaces as the
 * `weakPassword` server error if the chosen password is too simple.
 */
const registerPasswordField = z
  .string()
  .min(PASSWORD_MIN_LENGTH, 'auth.register.errors.weakPassword');

const emailField = z
  .string()
  .trim()
  .email('auth.register.errors.invalidEmail');

/* ------------------------------------------------------------------ */
/* Auth inputs                                                         */
/* ------------------------------------------------------------------ */

/** Login form / SignInCommand body. */
export const signInSchema = z.object({
  userName: z.string().trim().min(1, 'auth.errors.userNameRequired'),
  password: z.string().min(1, 'auth.errors.passwordRequired'),
});

export type SignInFormValues = z.infer<typeof signInSchema>;

/**
 * Parent registration form. Maps to `RegisterParentCommand`
 * (`{ email, password, fullName?, country?, acceptedTerms? }`). As of P1-12-FE,
 * `country` and `acceptedTerms` ARE posted to the backend (the merged P1-12 BE
 * accepts both):
 *  - `country` — controlled-vocabulary code, sent on register.
 *  - `acceptedTerms` — required consent gate; defaults to `false` and only
 *    becomes `true` by explicit user action, then is transmitted to the BE.
 * (CAPTCHA token is a separate task, P1-11-FE-16, and is intentionally not here.)
 * `fullName` is optional (the backend defaults it to the email local-part).
 */
export const registerParentSchema = z.object({
  fullName: z.string().trim().optional(),
  country: z.string().trim().min(1, 'auth.register.errors.countryRequired'),
  email: emailField,
  password: registerPasswordField,
  acceptedTerms: z
    .boolean()
    .refine((v) => v === true, { message: 'auth.register.errors.termsRequired' }),
});

export type RegisterParentFormValues = z.infer<typeof registerParentSchema>;

/**
 * Add-child form. Maps to `AddChildCommand`
 * (`{ fullName, email, password, grade, language, learningLanguage, country }`).
 * The acting parent is resolved server-side from the JWT — never sent by the
 * client.
 *
 * `learningLanguage` — the medium of instruction for Math & Science (axis B).
 *   Required; no default so the parent must choose explicitly.
 * `language` — the child's UI/account language (axis A); defaults to `'ar'`
 *   and auto-fills from `learningLanguage` while the parent hasn't manually
 *   touched it (handled in the form component).
 */
export const addChildSchema = z.object({
  fullName: z.string().trim().min(1, 'onboarding.addChild.errors.nameRequired'),
  email: emailField,
  password: passwordField,
  grade: z
    .number({ invalid_type_error: 'onboarding.child.errors.invalidGrade' })
    .int('onboarding.child.errors.invalidGrade')
    .min(MIN_GRADE, 'onboarding.child.errors.invalidGrade')
    .max(MAX_GRADE, 'onboarding.child.errors.invalidGrade'),
  learningLanguage: z.enum(LOCALES, {
    errorMap: () => ({
      message: 'onboarding.addChild.errors.learningLanguageRequired',
    }),
  }),
  language: z.enum(LOCALES, {
    errorMap: () => ({ message: 'onboarding.addChild.errors.languageRequired' }),
  }),
  country: z
    .string()
    .trim()
    .min(1, 'onboarding.addChild.errors.countryRequired'),
});

export type AddChildFormValues = z.infer<typeof addChildSchema>;

/**
 * Link-existing-child form. Maps to `LinkChildCommand` (`{ childEmail }`) — the
 * form field is `email` and is renamed to `childEmail` at the call site.
 */
export const linkChildSchema = z.object({
  email: emailField,
});

export type LinkChildFormValues = z.infer<typeof linkChildSchema>;

/** Refresh-Token command body — both tokens required, travel in the body. */
export const refreshTokenSchema = z.object({
  accessToken: z.string().min(1),
  refreshToken: z.string().min(1),
});

export type RefreshTokenFormValues = z.infer<typeof refreshTokenSchema>;

/* ------------------------------------------------------------------ */
/* Envelope (runtime guards, optional)                                 */
/* ------------------------------------------------------------------ */

/**
 * Runtime guard for the BaseResponse envelope. Note the `successed` spelling
 * (sic) to match the backend. `data` is left as `unknown` here; callers narrow.
 */
export const baseResponseSchema = z.object({
  statusCode: z.number(),
  successed: z.boolean(),
  message: z.string().nullish(),
  data: z.unknown(),
  errors: z.array(z.unknown()).nullish(),
});

/* ------------------------------------------------------------------ */
/* Locale                                                              */
/* ------------------------------------------------------------------ */

export const localeSchema = z.enum(LOCALES);
