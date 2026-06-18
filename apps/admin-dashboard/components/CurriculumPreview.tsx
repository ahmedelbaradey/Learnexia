'use client';

/**
 * CurriculumPreview — structured, read-only snapshot field viewer.
 *
 * Renders the PreviewDto.snapshot (raw JSON string) as a semantic field list
 * per entity type (Subject / Unit / Lesson / QuizQuestion). The student Expo/RN
 * lesson player is NOT imported — this is a structured preview of snapshot fields.
 *
 * Direction follows PreviewDto.language (Ar=0 → dir="rtl", En=1 → dir="ltr"),
 * INDEPENDENT of the admin UI locale. This is a per-card direction, not page-level.
 *
 * SECURITY (DG-6): The Lesson snapshot's Visual field is rendered as <img> only
 * for URLs that:
 *   1. Start with "https://" (http:// and relative paths are blocked)
 *   2. End with a safe image extension (.jpg/.jpeg/.png/.webp/.gif/.svg)
 *   3. Are NOT javascript:, data:, blob:, or other non-https schemes
 * A clickable link is always rendered alongside the image. See security-auditor note.
 *
 * JSON parse safety: Options and CorrectAnswer from QuizQuestion are raw JSON
 * strings. All parse operations are wrapped in try/catch. On failure the raw
 * string is shown in a monospace block (no crash).
 *
 * Design Spec P7-05 §Component 5.
 */

import { Stack, Text } from '@tamagui/core';
import { usePreviewContent, type PreviewDto } from '@learnexia/api-client';
import {
  LIFECYCLE_STATE,
  VERSIONED_ENTITY_TYPE,
  CONTENT_LANGUAGE,
  SUBJECT_CODE,
  type VersionedEntityTypeValue,
} from '@learnexia/shared/constants';
import { LifecycleBadge } from './LifecycleBadge';
import { AdminErrorBanner } from './AdminErrorBanner';
import type { AdminStrings } from '../lib/strings';

// ── Security helpers ──────────────────────────────────────────────────────────

/**
 * isSecureImageUrl — returns true only for https:// URLs with a safe image extension.
 * Blocks javascript:, data:, blob:, http://, and any unknown scheme.
 *
 * SECURITY NOTE (flagged for security-auditor):
 *   - Only https:// allowed (TLS required; no plaintext image loading)
 *   - Extension allowlist (blocks php/aspx/etc. rendered as images)
 *   - javascript: and data: schemes explicitly blocked
 *   - blob: scheme blocked (could hold arbitrary content)
 */
function isSecureImageUrl(url: string): boolean {
  if (!url || typeof url !== 'string') return false;
  // Parse the URL first — rejects malformed strings
  let parsed: URL;
  try {
    parsed = new URL(url);
  } catch {
    return false;
  }
  // Must be https (TLS required; block http, javascript, data, blob, etc.)
  if (parsed.protocol !== 'https:') return false;
  // Block private/loopback/link-local hosts
  const host = parsed.hostname;
  if (/^(localhost|127\.|10\.|172\.(1[6-9]|2\d|3[01])\.|192\.168\.|169\.254\.|0\.0\.0\.0)/.test(host)) return false;
  // IPv6 loopback [::1], link-local [fe80::…], and ULA [fc00:: / fd00::]
  if (/^\[?(::1|fe[89ab][0-9a-f]:|fc[0-9a-f]{2}:|fd[0-9a-f]{2}:)/i.test(host)) return false;
  // Extension must appear at the END of the pathname (not query/fragment)
  const pathname = parsed.pathname.toLowerCase();
  const hasSafeExt = /\.(jpe?g|png|webp|gif|svg)$/.test(pathname);
  return hasSafeExt;
}

/**
 * safeParseJson — wraps JSON.parse in try/catch.
 * Returns [parsed, null] on success, [null, rawString] on failure.
 */
function safeParseJson(raw: string): [unknown, string | null] {
  try {
    return [JSON.parse(raw), null];
  } catch {
    return [null, raw];
  }
}

// ── Skeleton ──────────────────────────────────────────────────────────────────

function PreviewSkeleton() {
  return (
    <Stack flexDirection="column" gap="$4">
      <div style={{
        height: 80, borderRadius: 20, width: '100%',
        background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
        backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
      }} />
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} style={{
          height: 36, borderRadius: 8,
          background: 'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
          backgroundSize: '400px 100%', animation: 'lx-shimmer 1400ms linear infinite',
        }} />
      ))}
    </Stack>
  );
}

// ── Field row ─────────────────────────────────────────────────────────────────

function FieldRow({ label, children, isLast = false }: {
  label: string;
  children: React.ReactNode;
  isLast?: boolean;
}) {
  return (
    <Stack
      flexDirection="column"
      gap="$1"
      style={{
        paddingBlock: 8,
        borderBottom: isLast ? 'none' : '1px solid var(--lx-border)',
      }}
    >
      <Text
        fontFamily="$body"
        fontSize={11}
        fontWeight="600"
        style={{ color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.04em' }}
      >
        {label}
      </Text>
      {children}
    </Stack>
  );
}

// ── Subject snapshot fields ───────────────────────────────────────────────────

interface SubjectSnapshot {
  Name?: string;
  Country?: string;
  SubjectCode?: number;
  Language?: number;
  SequenceOrder?: number;
  IsActive?: boolean;
}

function SubjectSnapshotView({ data, strings }: { data: SubjectSnapshot; strings: AdminStrings }) {
  const subjectLabel = (() => {
    switch (data.SubjectCode) {
      case SUBJECT_CODE.MATH:    return 'MATH';
      case SUBJECT_CODE.SCIENCE: return 'SCIENCE';
      case SUBJECT_CODE.ARABIC:  return 'ARABIC';
      case SUBJECT_CODE.ENGLISH: return 'ENGLISH';
      default: return String(data.SubjectCode ?? '—');
    }
  })();
  const langLabel = data.Language === CONTENT_LANGUAGE.Ar ? 'Arabic / عربي' : 'English / إنجليزي';

  return (
    <>
      <FieldRow label={strings.clPreviewFieldName}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">{data.Name ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldCountry}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">{data.Country ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldSubject}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">{subjectLabel}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldLanguage}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">{langLabel}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldOrder}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.SequenceOrder ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldActive} isLast>
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {data.IsActive === true ? strings.clPreviewYes : data.IsActive === false ? strings.clPreviewNo : '—'}
        </Text>
      </FieldRow>
    </>
  );
}

// ── Unit snapshot fields ──────────────────────────────────────────────────────

interface UnitSnapshot {
  Name?: string;
  SequenceOrder?: number;
  IsActive?: boolean;
}

function UnitSnapshotView({ data, strings }: { data: UnitSnapshot; strings: AdminStrings }) {
  return (
    <>
      <FieldRow label={strings.clPreviewFieldName}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">{data.Name ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldOrder}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.SequenceOrder ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldActive} isLast>
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {data.IsActive === true ? strings.clPreviewYes : data.IsActive === false ? strings.clPreviewNo : '—'}
        </Text>
      </FieldRow>
    </>
  );
}

// ── Lesson snapshot fields ────────────────────────────────────────────────────

interface LessonSnapshot {
  Name?: string;
  Difficulty?: number;
  SequenceOrder?: number;
  IsLocked?: boolean;
  IsActive?: boolean;
  EstimatedMinutes?: number;
  Explanation?: string;
  Visual?: string | null;
  IsBoss?: boolean;
}

function LessonSnapshotView({ data, strings }: { data: LessonSnapshot; strings: AdminStrings }) {
  const visual = data.Visual ?? null;
  const isSecureImg = visual ? isSecureImageUrl(visual) : false;

  return (
    <>
      <FieldRow label={strings.clPreviewFieldName}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">{data.Name ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldDifficulty}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.Difficulty ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldOrder}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.SequenceOrder ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldLocked}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {data.IsLocked === true ? strings.clPreviewYes : data.IsLocked === false ? strings.clPreviewNo : '—'}
        </Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldActive}>
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {data.IsActive === true ? strings.clPreviewYes : data.IsActive === false ? strings.clPreviewNo : '—'}
        </Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldDuration}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">
          {data.EstimatedMinutes != null ? `${data.EstimatedMinutes} min` : '—'}
        </Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldExplanation}>
        {data.Explanation ? (
          <pre style={{
            margin: 0, fontFamily: 'inherit', fontSize: 14, color: '#CBD5E1',
            whiteSpace: 'pre-wrap', wordBreak: 'break-word',
            backgroundColor: 'var(--lx-bg)', borderRadius: 8,
            padding: 12, border: '1px solid var(--lx-border)',
          }}>
            {data.Explanation}
          </pre>
        ) : (
          <Text fontFamily="$body" fontSize={14} color="$fg2">—</Text>
        )}
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldVisual}>
        {visual ? (
          <Stack flexDirection="column" gap="$2">
            {/* Always render as a link */}
            <a
              href={isSecureImg ? visual : '#'}
              target={isSecureImg ? '_blank' : undefined}
              rel="noopener noreferrer"
              onClick={!isSecureImg ? (e) => e.preventDefault() : undefined}
              style={{ color: '#4F46E5', textDecoration: 'underline', fontSize: 13, wordBreak: 'break-all' }}
              data-testid="preview-visual-link"
            >
              {visual}
            </a>
            {/* Inline image — only for validated https:// + image extension URLs.
                next/image requires a static domain list; content images can be
                any CDN URL, so we use <img> with explicit security validation above.
                eslint-disable-next-line @next/next/no-img-element */}
            {isSecureImg && (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={visual}
                alt={strings.clPreviewImageAlt}
                data-testid="preview-visual-img"
                style={{
                  maxWidth: '100%',
                  borderRadius: 8,
                  marginTop: 8,
                  border: '1px solid var(--lx-border)',
                }}
                // On error, hide the img element gracefully (broken link prevention)
                onError={(e) => { (e.currentTarget as HTMLImageElement).style.display = 'none'; }}
              />
            )}
          </Stack>
        ) : (
          <Text fontFamily="$body" fontSize={14} color="$fg2">—</Text>
        )}
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldBoss} isLast>
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {data.IsBoss === true ? strings.clPreviewYes : data.IsBoss === false ? strings.clPreviewNo : '—'}
        </Text>
      </FieldRow>
    </>
  );
}

// ── QuizQuestion snapshot fields ──────────────────────────────────────────────

interface QuizQuestionSnapshot {
  QuestionText?: string;
  QuestionType?: number;
  Options?: string | null;
  CorrectAnswer?: string | null;
  Difficulty?: number;
  GeneratedBy?: number;
  SequenceOrder?: number;
  IsActive?: boolean;
}

function QuizQuestionSnapshotView({ data, strings }: { data: QuizQuestionSnapshot; strings: AdminStrings }) {
  // Options: raw JSON — best-effort parse (DG-5)
  const renderOptions = () => {
    if (!data.Options) return <Text fontFamily="$body" fontSize={14} color="$fg2">—</Text>;
    const [parsed, rawFallback] = safeParseJson(data.Options);
    if (rawFallback !== null) {
      return (
        <pre style={{
          margin: 0, fontFamily: 'ui-monospace, "SF Mono", Menlo, monospace',
          fontSize: 13, color: '#94A3B8', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
        }}>
          {rawFallback}
        </pre>
      );
    }
    // Render as JSON (type-specific rendering deferred to P7-04 — DG-5)
    return (
      <pre style={{
        margin: 0, fontFamily: 'ui-monospace, "SF Mono", Menlo, monospace',
        fontSize: 13, color: '#94A3B8', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
        backgroundColor: 'var(--lx-bg)', borderRadius: 8,
        padding: 12, border: '1px solid var(--lx-border)',
      }}>
        {JSON.stringify(parsed, null, 2)}
      </pre>
    );
  };

  const renderCorrectAnswer = () => {
    if (!data.CorrectAnswer) return <Text fontFamily="$body" fontSize={14} color="$fg2">—</Text>;
    const [parsed, rawFallback] = safeParseJson(data.CorrectAnswer);
    const displayValue = rawFallback !== null ? rawFallback : JSON.stringify(parsed, null, 2);
    return (
      <pre style={{
        margin: 0, fontFamily: 'ui-monospace, "SF Mono", Menlo, monospace',
        fontSize: 13, color: '#CBD5E1', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
        backgroundColor: 'var(--lx-bg)', borderRadius: 8,
        padding: 12, border: '1px solid var(--lx-border)',
      }}>
        {displayValue}
      </pre>
    );
  };

  return (
    <>
      <FieldRow label={strings.clPreviewFieldQuestion}>
        <pre style={{
          margin: 0, fontFamily: 'inherit', fontSize: 14, color: '#CBD5E1',
          whiteSpace: 'pre-wrap', wordBreak: 'break-word',
        }}>
          {data.QuestionText ?? '—'}
        </pre>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldType}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.QuestionType ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldOptions}>
        {renderOptions()}
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldCorrectAnswer}>
        {renderCorrectAnswer()}
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldDifficulty}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.Difficulty ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldGeneratedBy}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.GeneratedBy ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldOrder}>
        <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr">{data.SequenceOrder ?? '—'}</Text>
      </FieldRow>
      <FieldRow label={strings.clPreviewFieldActive} isLast>
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {data.IsActive === true ? strings.clPreviewYes : data.IsActive === false ? strings.clPreviewNo : '—'}
        </Text>
      </FieldRow>
    </>
  );
}

// ── Entity type labels ────────────────────────────────────────────────────────

function getEntityTypeLabel(entityType: VersionedEntityTypeValue, strings: AdminStrings): string {
  switch (entityType) {
    case VERSIONED_ENTITY_TYPE.Subject:      return strings.clEntityTypeSubject;
    case VERSIONED_ENTITY_TYPE.Unit:         return strings.clEntityTypeUnit;
    case VERSIONED_ENTITY_TYPE.Lesson:       return strings.clEntityTypeLesson;
    case VERSIONED_ENTITY_TYPE.QuizQuestion: return strings.clEntityTypeQuestion;
    default: return 'Entity';
  }
}

// ── Snapshot renderer ─────────────────────────────────────────────────────────

function SnapshotRenderer({
  preview,
  strings,
}: {
  preview: PreviewDto;
  strings: AdminStrings;
}) {
  const [parsed, rawFallback] = safeParseJson(preview.snapshot);
  const dir = preview.language === CONTENT_LANGUAGE.Ar ? 'rtl' : 'ltr';

  if (rawFallback !== null) {
    return (
      <div dir={dir}>
        <AdminErrorBanner variant="warning" message={strings.clPreviewInvalidJson} />
        <pre style={{
          marginTop: 12, fontFamily: 'ui-monospace, "SF Mono", Menlo, monospace',
          fontSize: 13, color: '#94A3B8', whiteSpace: 'pre-wrap', wordBreak: 'break-word',
          backgroundColor: 'var(--lx-bg)', borderRadius: 8,
          padding: 12, border: '1px solid var(--lx-border)',
        }}>
          {rawFallback}
        </pre>
      </div>
    );
  }

  return (
    <div dir={dir}>
      {preview.entityType === VERSIONED_ENTITY_TYPE.Subject && (
        <SubjectSnapshotView data={parsed as SubjectSnapshot} strings={strings} />
      )}
      {preview.entityType === VERSIONED_ENTITY_TYPE.Unit && (
        <UnitSnapshotView data={parsed as UnitSnapshot} strings={strings} />
      )}
      {preview.entityType === VERSIONED_ENTITY_TYPE.Lesson && (
        <LessonSnapshotView data={parsed as LessonSnapshot} strings={strings} />
      )}
      {preview.entityType === VERSIONED_ENTITY_TYPE.QuizQuestion && (
        <QuizQuestionSnapshotView data={parsed as QuizQuestionSnapshot} strings={strings} />
      )}
    </div>
  );
}

// ── Props ─────────────────────────────────────────────────────────────────────

export interface CurriculumPreviewProps {
  entityType: VersionedEntityTypeValue;
  entityId: number;
  strings: AdminStrings;
  onBack?: () => void;
}

// ── Component ─────────────────────────────────────────────────────────────────

export function CurriculumPreview({ entityType, entityId, strings, onBack }: CurriculumPreviewProps) {
  const { data, isLoading, isError } = usePreviewContent(entityType, entityId);

  const entityTypeLabel = getEntityTypeLabel(entityType, strings);
  const langChip = data
    ? data.language === CONTENT_LANGUAGE.Ar ? 'AR — عربي' : 'EN — English'
    : null;

  return (
    <Stack flexDirection="column" gap="$6" data-testid="curriculum-preview">
      {isLoading && <PreviewSkeleton />}

      {isError && !isLoading && (
        <Stack flexDirection="column" gap="$3">
          <AdminErrorBanner variant="error" message={strings.clPreviewError} />
          {onBack && (
            <button
              type="button"
              onClick={onBack}
              style={{
                alignSelf: 'flex-start', display: 'flex', alignItems: 'center', gap: 6,
                height: 36, paddingInline: 14, borderRadius: 'var(--lx-radius-button)',
                border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                color: '#CBD5E1', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
              }}
            >
              {strings.clPreviewBack}
            </button>
          )}
        </Stack>
      )}

      {!isLoading && !isError && data && (
        <>
          {/* Preview header card */}
          <Stack
            flexDirection="column"
            gap="$4"
            padding="$6"
            borderRadius="$card"
            style={{
              backgroundColor: 'var(--lx-card)',
              border: '1px solid var(--lx-border)',
            }}
            data-testid="preview-header-card"
          >
            <Stack flexDirection="row" alignItems="center" gap="$4" style={{ flexWrap: 'wrap' }}>
              {/* Title block */}
              <Stack flex={1} flexDirection="column" gap="$1">
                <Text
                  fontFamily="$body"
                  fontSize={11}
                  fontWeight="600"
                  style={{ color: '#94A3B8', textTransform: 'uppercase', letterSpacing: '0.06em' }}
                >
                  {strings.clLifecyclePreviewTitle}
                </Text>
                <Text fontFamily="$heading" fontSize={18} fontWeight="700" color="$fg1">
                  {entityTypeLabel} #{entityId}
                </Text>
                <Stack flexDirection="row" gap="$2" alignItems="center" style={{ marginTop: 4 }}>
                  <LifecycleBadge state={data.currentState} strings={strings} testId="preview-lifecycle-badge" />
                  {langChip && (
                    <span style={{
                      fontSize: 11, color: '#94A3B8', backgroundColor: 'var(--lx-card-soft)',
                      padding: '3px 8px', borderRadius: 9999,
                    }}>
                      {langChip}
                    </span>
                  )}
                </Stack>
              </Stack>

              {/* Back button */}
              {onBack && (
                <button
                  type="button"
                  onClick={onBack}
                  data-testid="preview-back-btn"
                  style={{
                    display: 'flex', alignItems: 'center', gap: 6,
                    height: 36, paddingInline: 14, borderRadius: 'var(--lx-radius-button)',
                    border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                    color: '#CBD5E1', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
                    flexShrink: 0,
                  }}
                >
                  {strings.clPreviewBack}
                </button>
              )}
            </Stack>
          </Stack>

          {/* Draft warning banner (role="alert" — immediate announcement) */}
          {data.currentState === LIFECYCLE_STATE.Draft && (
            <div role="alert" data-testid="preview-draft-banner">
              <AdminErrorBanner variant="warning" message={strings.clPreviewDraftBanner} />
            </div>
          )}

          {/* Snapshot field view */}
          <Stack
            flexDirection="column"
            padding="$6"
            borderRadius="$card"
            style={{
              backgroundColor: 'var(--lx-card)',
              border: '1px solid var(--lx-border)',
            }}
            data-testid="preview-snapshot-card"
          >
            <SnapshotRenderer preview={data} strings={strings} />
          </Stack>
        </>
      )}
    </Stack>
  );
}
