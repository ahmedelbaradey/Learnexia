import { NextResponse, type NextRequest } from 'next/server';

/**
 * Middleware STUB — intentionally a pass-through for P1-10.
 *
 * TODO (FOLLOW-UP DEBT — HttpOnly-cookie auth): real server-side route
 * protection is DEFERRED. The admin app stores tokens in `sessionStorage`
 * (per `@learnexia/shared` web storage), which the Next.js edge runtime cannot
 * read — so this middleware cannot enforce auth here. Route protection is
 * therefore CLIENT-SIDE only (see `lib/hooks/useAdminGuard.ts`), which means a
 * determined user could momentarily receive the shell HTML before the client
 * guard redirects.
 *
 * True server-side enforcement requires HttpOnly + Secure + SameSite cookie
 * auth set by the backend (a backend cookie path not in this story's scope).
 * When that lands, decode/validate the cookie here and `NextResponse.redirect`
 * unauthenticated requests to `/login` before any protected HTML is sent.
 */
export function middleware(_request: NextRequest) {
  return NextResponse.next();
}

export const config = {
  // No matcher active while the guard is client-only. Kept here as the seam for
  // future cookie-based enforcement.
  matcher: [],
};
