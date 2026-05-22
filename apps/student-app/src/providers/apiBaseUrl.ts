/**
 * Resolve the API base URL. Reads `EXPO_PUBLIC_API_BASE_URL` (Expo exposes
 * `EXPO_PUBLIC_*` env vars to the client bundle) and falls back to the local
 * dev API. No secrets here — only the public base URL.
 */
export function resolveApiBaseUrl(): string {
  const fromEnv = process.env.EXPO_PUBLIC_API_BASE_URL;
  return fromEnv && fromEnv.length > 0 ? fromEnv : 'https://localhost:7080';
}
