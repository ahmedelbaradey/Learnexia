import nextPlugin from '@next/eslint-plugin-next';

import sharedConfig from '@learnexia/eslint-config';

/** @type {import("eslint").Linter.Config[]} */
export default [
  {
    ignores: ['.next/**', 'next-env.d.ts', 'public/**'],
  },
  ...sharedConfig,
  {
    // Wire the Next.js plugin (core-web-vitals rule set) so `eslint .` detects
    // it — replaces the deprecated `next lint` integration.
    plugins: { '@next/next': nextPlugin },
    rules: {
      ...nextPlugin.configs.recommended.rules,
      ...nextPlugin.configs['core-web-vitals'].rules,
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
];
