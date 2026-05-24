/**
 * Brand font face assets — the actual `.ttf` files, imported so Metro resolves
 * each to a URL (web) or numeric module id (native).
 *
 * Tamagui registers ONE family name per font ('Poppins' / 'Cairo' / 'Tajawal')
 * with a numeric weight scale (400–900). For the right weight to render we must
 * make every weight resolve under that single family name:
 *   - WEB: `@font-face` rules that all share `font-family` but each declare their
 *     own `font-weight` descriptor (see `webFontFace.ts`).
 *   - NATIVE: expo-font registers each face under a distinct family key; Tamagui
 *     maps weight → key via the `face` attached in `faces.native.ts`.
 *
 * One descriptor list is the single source of truth for both platforms.
 *
 * Weight coverage shipped in `../../assets/fonts`:
 *   Poppins  400 500 600 700 800 900 (full family)
 *   Cairo    400 500 600 700 800 900 (full family)
 *   Tajawal  400 500     700 800 900 — no SemiBold (600) face ships, so weight
 *            600 is mapped onto the Bold (700) file as the nearest available.
 */

// Poppins
import PoppinsBlack from '../../assets/fonts/Poppins-Black.ttf';
import PoppinsBold from '../../assets/fonts/Poppins-Bold.ttf';
import PoppinsExtraBold from '../../assets/fonts/Poppins-ExtraBold.ttf';
import PoppinsMedium from '../../assets/fonts/Poppins-Medium.ttf';
import PoppinsRegular from '../../assets/fonts/Poppins-Regular.ttf';
import PoppinsSemiBold from '../../assets/fonts/Poppins-SemiBold.ttf';
// Cairo
import CairoBlack from '../../assets/fonts/Cairo-Black.ttf';
import CairoBold from '../../assets/fonts/Cairo-Bold.ttf';
import CairoExtraBold from '../../assets/fonts/Cairo-ExtraBold.ttf';
import CairoMedium from '../../assets/fonts/Cairo-Medium.ttf';
import CairoRegular from '../../assets/fonts/Cairo-Regular.ttf';
import CairoSemiBold from '../../assets/fonts/Cairo-SemiBold.ttf';
// Tajawal
import TajawalBlack from '../../assets/fonts/Tajawal-Black.ttf';
import TajawalBold from '../../assets/fonts/Tajawal-Bold.ttf';
import TajawalExtraBold from '../../assets/fonts/Tajawal-ExtraBold.ttf';
import TajawalMedium from '../../assets/fonts/Tajawal-Medium.ttf';
import TajawalRegular from '../../assets/fonts/Tajawal-Regular.ttf';

/** A loadable brand font face. */
export interface FontFaceDescriptor {
  /** Tamagui family name this face renders under. */
  family: 'Poppins' | 'Cairo' | 'Tajawal';
  /** CSS numeric weight. */
  weight: 400 | 500 | 600 | 700 | 800 | 900;
  /** Distinct expo-font key (native) — `${family}-${weight}`. */
  key: string;
  /** Metro-resolved asset: URL string on web, module id (number) on native. */
  asset: string | number;
}

const face = (
  family: FontFaceDescriptor['family'],
  weight: FontFaceDescriptor['weight'],
  asset: string | number,
): FontFaceDescriptor => ({ family, weight, key: `${family}-${weight}`, asset });

export const fontFaces: readonly FontFaceDescriptor[] = [
  // Poppins (English display + body)
  face('Poppins', 400, PoppinsRegular),
  face('Poppins', 500, PoppinsMedium),
  face('Poppins', 600, PoppinsSemiBold),
  face('Poppins', 700, PoppinsBold),
  face('Poppins', 800, PoppinsExtraBold),
  face('Poppins', 900, PoppinsBlack),
  // Cairo (Arabic display / headings)
  face('Cairo', 400, CairoRegular),
  face('Cairo', 500, CairoMedium),
  face('Cairo', 600, CairoSemiBold),
  face('Cairo', 700, CairoBold),
  face('Cairo', 800, CairoExtraBold),
  face('Cairo', 900, CairoBlack),
  // Tajawal (Arabic body) — no SemiBold ships; 600 maps onto Bold.
  face('Tajawal', 400, TajawalRegular),
  face('Tajawal', 500, TajawalMedium),
  face('Tajawal', 600, TajawalBold),
  face('Tajawal', 700, TajawalBold),
  face('Tajawal', 800, TajawalExtraBold),
  face('Tajawal', 900, TajawalBlack),
] as const;
