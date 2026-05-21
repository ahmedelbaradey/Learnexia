// Re-export the shared flat ESLint config for @learnexia/api-client.
// Note: the shared config already ignores `src/generated/**` (the type-gen output).
// Locally also ignore `scripts/**` — Node dev tooling (refresh-swagger) that
// runs outside the TS/browser lint scope.
import config from "@learnexia/eslint-config";

export default [...config, { ignores: ["scripts/**"] }];
