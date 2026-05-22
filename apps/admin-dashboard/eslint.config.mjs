import nextPlugin from '@next/eslint-plugin-next';

import sharedConfig from '@learnexia/eslint-config';

/** @type {import("eslint").Linter.Config[]} */
export default [
  // Ignore build artifacts + generated Tamagui output (flat-config ignores must
  // be in their own object to apply globally).
  {
    ignores: ['.next/**', '.tamagui/**', 'next-env.d.ts', 'public/**'],
  },
  ...sharedConfig,
  {
    // Wire the Next.js plugin (core-web-vitals rule set) so `eslint .` detects
    // it — replaces the deprecated `next lint` integration.
    plugins: { '@next/next': nextPlugin },
    rules: {
      ...nextPlugin.configs.recommended.rules,
      ...nextPlugin.configs['core-web-vitals'].rules,
      // Allow intentionally-unused args/vars when prefixed with `_`.
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
];
