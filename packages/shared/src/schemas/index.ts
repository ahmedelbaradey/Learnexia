/**
 * Zod schemas shared by the app (react-hook-form resolvers) and api-client.
 *
 * Auth-input schemas only for now — the minimal core needed by the sign-in /
 * refresh flows. Domain-entity schemas can be added as screens require them;
 * we deliberately don't over-model.
 */

import { z } from 'zod';

import { LOCALES } from '../constants';

/* ------------------------------------------------------------------ */
/* Auth inputs                                                         */
/* ------------------------------------------------------------------ */

/** Login form / SignInCommand body. */
export const signInSchema = z.object({
  userName: z.string().trim().min(1, 'auth.errors.userNameRequired'),
  password: z.string().min(1, 'auth.errors.passwordRequired'),
});

export type SignInFormValues = z.infer<typeof signInSchema>;

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
