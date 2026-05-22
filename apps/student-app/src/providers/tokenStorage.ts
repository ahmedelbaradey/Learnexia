/**
 * Platform token storage selector.
 *
 * Native (iOS/Android): `expo-secure-store` (Keychain/Keystore-backed) via the
 * shared `createNativeTokenStorage` DI factory. Web: `sessionStorage`-backed
 * `createWebTokenStorage`. Selection is by `Platform.OS` so one codebase serves
 * both. The shared package never imports Expo — the SecureStore module is
 * injected here.
 */
import {
  createNativeTokenStorage,
  createWebTokenStorage,
  type TokenStorage,
} from '@learnexia/shared';
import * as SecureStore from 'expo-secure-store';
import { Platform } from 'react-native';

export function createPlatformTokenStorage(): TokenStorage {
  if (Platform.OS === 'web') {
    return createWebTokenStorage();
  }
  return createNativeTokenStorage(SecureStore);
}
