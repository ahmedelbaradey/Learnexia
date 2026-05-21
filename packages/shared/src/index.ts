/**
 * @learnexia/shared — domain types, constants, zod schemas, Zustand stores,
 * token-storage abstraction, and i18n/RTL helpers.
 *
 * Framework-agnostic: no Expo/Next imports. Platform-specific token storage is
 * injected by the consuming app. No server data lives in the Zustand stores —
 * that is TanStack Query's job in @learnexia/api-client.
 */

export * from './envelope';
export * from './types';
export * from './constants';
export * from './schemas';
export * from './stores';
export * from './storage';
export * from './i18n';
