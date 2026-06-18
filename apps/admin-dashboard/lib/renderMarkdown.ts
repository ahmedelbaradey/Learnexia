/**
 * renderMarkdown — sanitized Markdown-to-HTML pipeline for BlockPreview.
 *
 * SECURITY: Text and Callout payloads are author-supplied Markdown stored
 * verbatim. This helper is the ONLY place Markdown is converted to HTML and
 * must be used EVERY time before dangerouslySetInnerHTML.
 *
 * Pipeline:
 *   1. marked.parse(md)    — parse Markdown to raw HTML string
 *   2. DOMPurify.sanitize  — strip all dangerous tags/attrs/URLs
 *
 * DOMPurify config allows a safe subset:
 *   Tags:   p, br, strong, em, b, i, ul, ol, li, h1-h4, blockquote,
 *           code, pre, hr, a
 *   Disallows: script, style, iframe, object, embed + all event handlers
 *   URL allowlist (href/src): https:// only; strips javascript:, data:, vbscript:
 *
 * Called ONLY through this function — never use dangerouslySetInnerHTML with
 * unsanitized content anywhere in the admin dashboard.
 *
 * NOTE: DOMPurify is a browser-only API. This module should only be imported
 * in 'use client' components (BlockPreview is a client component).
 */

import { marked } from 'marked';
import DOMPurify from 'dompurify';

/** Allowed HTML tags after Markdown parsing. */
const ALLOWED_TAGS = [
  'p', 'br', 'strong', 'em', 'b', 'i',
  'ul', 'ol', 'li',
  'h1', 'h2', 'h3', 'h4',
  'blockquote', 'code', 'pre', 'hr',
  'a',
];

/** Allowed attributes (no event handlers). */
const ALLOWED_ATTR = ['href', 'title', 'class'];

/**
 * Converts author-supplied Markdown to sanitized HTML.
 *
 * @param md - Raw Markdown string (from TextPayload.markdown or CalloutPayload.markdown).
 * @returns Sanitized HTML string safe for dangerouslySetInnerHTML injection.
 */
export function renderMarkdown(md: string): string {
  if (!md || typeof md !== 'string') return '';

  // SECURITY: DOMPurify requires a real DOM. On SSR/server there is no window,
  // so sanitize() would return the RAW input unchanged and dangerouslySetInnerHTML
  // would receive unsanitized HTML. Fail-closed: return empty string instead.
  if (typeof window === 'undefined') return '';

  // Step 1: Parse Markdown → raw HTML.
  // marked.parse returns string | Promise<string>; the sync overload is used.
  const rawHtml = marked.parse(md, { async: false }) as string;

  // Step 2: Sanitize — strip dangerous tags, event handlers, and URL schemes.
  const safeHtml = DOMPurify.sanitize(rawHtml, {
    ALLOWED_TAGS,
    ALLOWED_ATTR,
    // Force-allow only https: URLs on href/src. Strip javascript:, data:, vbscript:
    ALLOWED_URI_REGEXP: /^https:/i,
    // Prevent DOM clobbering
    SANITIZE_DOM: true,
    // Do not return DOM node — keep as string
    RETURN_DOM: false,
    RETURN_DOM_FRAGMENT: false,
  });

  return safeHtml;
}
