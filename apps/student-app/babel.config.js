/**
 * Babel config — Expo preset + Tamagui optimizing compiler + Reanimated.
 *
 * The Tamagui plugin extracts styles at build time and tree-shakes the
 * design-system config. The Reanimated plugin MUST be last. Module-resolver
 * keeps the `@learnexia/*` workspace aliases resolvable in app source.
 */
module.exports = function babel(api) {
  api.cache(true);
  return {
    presets: ['babel-preset-expo'],
    plugins: [
      [
        '@tamagui/babel-plugin',
        {
          components: ['tamagui', '@learnexia/ui'],
          config: '../../packages/design-system/src/tamagui.config.ts',
          logTimings: true,
          disableExtraction: process.env.NODE_ENV === 'development',
        },
      ],
      [
        'module-resolver',
        {
          root: ['./'],
          alias: {
            '@': './src',
          },
        },
      ],
      // Reanimated plugin must be listed last.
      'react-native-reanimated/plugin',
    ],
  };
};
