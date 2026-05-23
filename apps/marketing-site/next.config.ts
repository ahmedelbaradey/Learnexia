import type { NextConfig } from 'next';

/**
 * Next.js 15 config for the Learnexia marketing site.
 *
 * The marketing landing page is static, token-driven HTML/CSS (no Tamagui
 * runtime, no api-client, no auth) — so unlike `apps/admin-dashboard` it does
 * NOT need the `withTamagui` plugin or the native-peer null-aliases. It mirrors
 * the admin app's Next 15 + app-router + strict-TS baseline; styling comes from
 * the shared design-system tokens re-exported as `--lx-*` CSS vars in
 * `app/globals.css`.
 */
const nextConfig: NextConfig = {
  reactStrictMode: true,
};

export default nextConfig;
