/**
 * WEB font loading — inject `@font-face` rules for the brand families.
 *
 * Why not `expo-font` `useFonts` on web? It registers each `.ttf` under a
 * distinct family name with NO weight descriptor, so a single Tamagui family
 * ('Cairo') with a numeric weight scale can't pick the right file. Instead we
 * emit real `@font-face` rules that all share one `font-family` but each declare
 * their own `font-weight` descriptor pointing at the matching `.ttf` URL — the
 * browser then selects the correct file per requested weight.
 *
 * The `.ttf` URLs come from Metro (it serves the bundled assets on web). This
 * runs as a one-time side effect at app start (guarded for SSR / double-inject).
 */
import { fontFaces } from './assets';

const STYLE_ELEMENT_ID = 'learnexia-brand-fonts';

function buildFontFaceCss(): string {
  return fontFaces
    .map((f) => {
      const url = typeof f.asset === 'string' ? f.asset : String(f.asset);
      return [
        '@font-face {',
        `  font-family: '${f.family}';`,
        '  font-style: normal;',
        `  font-weight: ${f.weight};`,
        `  src: url('${url}') format('truetype');`,
        '  font-display: swap;',
        '}',
      ].join('\n');
    })
    .join('\n');
}

/**
 * Inject the brand `@font-face` rules into the document head (web only, once).
 * No-op when there is no DOM (native / SSR without `document`).
 */
export function loadWebFonts(): void {
  if (typeof document === 'undefined') return;
  if (document.getElementById(STYLE_ELEMENT_ID)) return;

  const style = document.createElement('style');
  style.id = STYLE_ELEMENT_ID;
  style.textContent = buildFontFaceCss();
  document.head.appendChild(style);
}
