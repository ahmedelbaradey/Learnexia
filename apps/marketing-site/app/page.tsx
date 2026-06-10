import { redirect } from 'next/navigation';

/**
 * Root page — middleware handles the / → /en redirect for full requests,
 * but this fallback ensures server-side redirect for any edge case where
 * the middleware does not intercept (e.g. static pre-rendering).
 */
export default function RootPage() {
  redirect('/en');
}
