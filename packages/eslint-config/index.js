// @learnexia/eslint-config — shared flat ESLint config (ESLint 9 / typescript-eslint 8).
// Consumed by packages/api-client and packages/shared via `eslint.config.js` re-export.
import js from "@eslint/js";
import tseslint from "typescript-eslint";

/** @type {import("eslint").Linter.Config[]} */
export default [
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    ignores: ["dist/**", ".turbo/**", "node_modules/**", "src/generated/**"],
  },
];
