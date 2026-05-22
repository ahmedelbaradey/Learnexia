'use client';

/**
 * Injects Tamagui's generated CSS into the server-rendered HTML so styled
 * components (`Button`, `Card`) paint correctly on first load (no flash of
 * unstyled content). Uses Next.js App Router's `useServerInsertedHTML`.
 *
 * `config.getCSS()` returns the full token + variant CSS for the design-system
 * config; we emit it once during SSR.
 */

import { useServerInsertedHTML } from 'next/navigation';
import { useRef, type ReactNode } from 'react';

import tamaguiConfig from '../tamagui.config';

export function TamaguiCssProvider({ children }: { children: ReactNode }) {
  const inserted = useRef(false);

  useServerInsertedHTML(() => {
    if (inserted.current) return null;
    inserted.current = true;
    return (
      <style
        // Tamagui-generated CSS is trusted, build-time output (no user input).
        dangerouslySetInnerHTML={{
          __html: tamaguiConfig.getCSS(),
        }}
      />
    );
  });

  return <>{children}</>;
}
