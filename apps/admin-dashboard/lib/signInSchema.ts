/**
 * Admin sign-in form schema (FE-2). No registration fields — username +
 * password only (admins are seeded/invited; there is no self-registration).
 */

import { z } from 'zod';

export const signInSchema = z.object({
  userName: z.string().min(1, 'Username is required'),
  password: z.string().min(1, 'Password is required'),
});

export type SignInFormValues = z.infer<typeof signInSchema>;
