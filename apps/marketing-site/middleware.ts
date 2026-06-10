import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

/**
 * Locale middleware for the marketing site.
 *
 * Rules:
 *  - Bare /  → redirect to /en (default locale, marketing is English-first).
 *  - /en/**  → allow through; attach x-locale: en header so RootLayout can
 *              read the locale for <html lang dir> without re-parsing the URL.
 *  - /ar/**  → allow through; attach x-locale: ar.
 *  - Next.js internals, static files, etc. → pass through unchanged.
 */

const VALID_LOCALES = new Set(['en', 'ar'] as const);
type Locale = 'en' | 'ar';

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Skip Next.js internals and static files.
  if (
    pathname.startsWith('/_next') ||
    pathname.startsWith('/api') ||
    pathname.startsWith('/assets') ||
    pathname.startsWith('/fonts') ||
    pathname.includes('.')
  ) {
    return NextResponse.next();
  }

  // Bare / → redirect to /en.
  if (pathname === '/') {
    const url = request.nextUrl.clone();
    url.pathname = '/en';
    return NextResponse.redirect(url);
  }

  // Extract the first path segment to detect locale.
  const firstSegment = pathname.split('/')[1] as Locale;

  // Redirect unknown locale segments to /en.
  if (firstSegment && !VALID_LOCALES.has(firstSegment)) {
    const url = request.nextUrl.clone();
    url.pathname = `/en${pathname}`;
    return NextResponse.redirect(url);
  }

  // Valid locale — pass through and inject x-locale header so RootLayout
  // can set <html lang dir> without re-parsing the URL.
  const locale: Locale = VALID_LOCALES.has(firstSegment) ? firstSegment : 'en';
  const response = NextResponse.next();
  response.headers.set('x-locale', locale);
  return response;
}

export const config = {
  matcher: [
    /*
     * Match all paths except:
     * - _next/static  (static files)
     * - _next/image   (image optimization)
     * - favicon.ico
     */
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};
