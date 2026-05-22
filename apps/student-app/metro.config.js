/**
 * Metro config — Expo + monorepo (Turborepo/pnpm) aware.
 *
 * Watches the workspace `packages/*` so the universal Tamagui packages
 * (@learnexia/ui, design-system, api-client, shared) hot-reload, and resolves
 * their deps from both the app and the workspace root. The Tamagui Metro plugin
 * enables compile-time extraction / tree-shaking of the design-system config.
 */
const { getDefaultConfig } = require('expo/metro-config');
const { withTamagui } = require('@tamagui/metro-plugin');
const path = require('path');

const projectRoot = __dirname;
const workspaceRoot = path.resolve(projectRoot, '../..');

const config = getDefaultConfig(projectRoot);

// Watch the whole monorepo so workspace packages are bundled from source.
config.watchFolders = [workspaceRoot];
config.resolver.nodeModulesPaths = [
  path.resolve(projectRoot, 'node_modules'),
  path.resolve(workspaceRoot, 'node_modules'),
];
config.resolver.disableHierarchicalLookup = true;

module.exports = withTamagui(config, {
  components: ['tamagui', '@learnexia/ui'],
  config: '../../packages/design-system/src/tamagui.config.ts',
  outputCSS: './tamagui-web.css',
});
