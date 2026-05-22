// Flat ESLint config for @learnexia/student-app, layered on the shared config.
//
// The shared config targets browser/TS source. This app additionally has:
//   - Node CommonJS config files (babel/metro) — lint with Node globals + CJS;
//   - Expo asset modules loaded via `require()` (the RN/Metro idiom);
//   - inline `react-hooks/*` disable directives (that plugin isn't registered
//     in the shared config) — don't error on the unknown directive.
import config from "@learnexia/eslint-config";

export default [
  ...config,
  {
    // Don't fail on inline disable directives for rules not registered here
    // (e.g. react-hooks/exhaustive-deps documented at effect call sites).
    linterOptions: { reportUnusedDisableDirectives: "off" },
  },
  {
    // App source: static assets are loaded with require() per the Metro idiom.
    files: ["src/**/*.{ts,tsx}", "app/**/*.{ts,tsx}"],
    rules: { "@typescript-eslint/no-require-imports": "off" },
  },
  {
    // Node CommonJS tooling/config files.
    files: ["*.js", "babel.config.js", "metro.config.js"],
    languageOptions: {
      sourceType: "commonjs",
      globals: {
        module: "writable",
        require: "readonly",
        process: "readonly",
        __dirname: "readonly",
      },
    },
    rules: { "@typescript-eslint/no-require-imports": "off" },
  },
  { ignores: [".expo/**", "dist/**", "expo-env.d.ts"] },
];
