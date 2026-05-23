/**
 * Marketing-site runtime config.
 *
 * The landing-page CTAs deep-link into the Expo student app (parent register /
 * login). The base URL is configurable via `NEXT_PUBLIC_APP_URL` so the same
 * build points at local dev (`http://localhost:8081`), a preview, or prod
 * without hardcoding any environment. Never hardcode a prod URL here.
 */

const DEFAULT_APP_URL = 'http://localhost:8081';

/** Student-app base URL (Expo web). Override with NEXT_PUBLIC_APP_URL. */
export const APP_URL: string = process.env.NEXT_PUBLIC_APP_URL ?? DEFAULT_APP_URL;

const trimTrailingSlash = (u: string): string => u.replace(/\/+$/, '');

/** Parent register / "Start free" / "Create parent account" destination. */
export const REGISTER_URL = `${trimTrailingSlash(APP_URL)}/register`;

/** "Log in" destination. */
export const LOGIN_URL = `${trimTrailingSlash(APP_URL)}/login`;
