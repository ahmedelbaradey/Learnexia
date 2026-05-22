/**
 * Map an api-client error to a localized message for display.
 *
 * Each form passes a `matchers` map keyed by a semantic case → i18n key. We
 * inspect the typed error (status code + backend message/errors) to pick the
 * best case, falling back to a generic server/network message. The backend
 * `errors` array and `message` are matched case-insensitively against hint
 * substrings (e.g. "duplicate", "exists", "not found", "weak").
 */
import { isApiError, ApiError } from '@learnexia/api-client';
import { useTranslation } from 'react-i18next';

export interface ServerErrorMatchers {
  /** Substring hints (lower-cased) → i18n key. First match wins. */
  hints?: Array<{ contains: string[]; key: string }>;
  /** Map by HTTP status (e.g. 401, 404, 409). */
  byStatus?: Record<number, string>;
}

function collectText(err: ApiError): string {
  const parts: string[] = [err.message ?? ''];
  for (const e of err.errors) {
    if (typeof e === 'string') parts.push(e);
    else if (e && typeof e === 'object') parts.push(JSON.stringify(e));
  }
  return parts.join(' ').toLowerCase();
}

export function useServerError() {
  const { t } = useTranslation();

  return function resolve(
    error: unknown,
    matchers: ServerErrorMatchers = {},
  ): string {
    // Network / unknown transport error (fetch rejected → not an ApiError, or status 0).
    if (!isApiError(error)) {
      return t('common.error.networkError');
    }
    const apiError = error;
    if (apiError.status === 0) {
      return t('common.error.networkError');
    }

    const byStatusKey = matchers.byStatus?.[apiError.status];
    const haystack = collectText(apiError);

    // Hint-substring matching takes precedence (more specific than status).
    if (matchers.hints) {
      for (const hint of matchers.hints) {
        if (hint.contains.some((c) => haystack.includes(c.toLowerCase()))) {
          return t(hint.key);
        }
      }
    }

    if (byStatusKey) return t(byStatusKey);

    return t('common.error.serverError');
  };
}
