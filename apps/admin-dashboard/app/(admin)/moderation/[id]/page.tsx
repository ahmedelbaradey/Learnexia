'use client';

// Force dynamic rendering — same reason as users/[id]/page.tsx.
export const dynamic = 'force-dynamic';

/**
 * Moderation item detail page — `/moderation/[id]` (P7-09-FE-3, FE-4, FE-5, FE-6).
 *
 * Read-only detail view for a single moderation item plus the review action panel.
 * Renders:
 *   - Detail header (status + source badges, content reference, detected-at, item ID)
 *   - Facets card (all item fields)
 *   - SafetyVerdictView (defensively parsed safetyVerdict JSON — no raw content)
 *   - Review history card (conditional — when item has been reviewed)
 *   - ReviewActionsPanel (Approve / Reject / Flag — gated by status)
 *
 * PII-light invariant: renders only reason codes, check names, opaque content refs,
 * and loose ids — never raw AI prompt/response content.
 *
 * Design Spec Part C, D, E. Acceptance criteria: P7-09 AC 4, 5, 6, 7, 8.
 */

import { use, useState, useEffect } from 'react';
import Link from 'next/link';
import { Stack, Text } from '@tamagui/core';

import {
  useModerationItem,
  type ModerationItemDetailDto,
  type ModerationStatus,
  type SafetyVerdict,
} from '@learnexia/api-client';

import { AdminShell } from '../../../../components/AdminShell';
import { StatusBadge } from '../../../../components/StatusBadge';
import { SourceBadge } from '../../../../components/SourceBadge';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { ReviewItemDialog } from '../../../../components/ReviewItemDialog';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

type ReviewDecision = Exclude<ModerationStatus, 'Pending'>;

// ── Date format helper ────────────────────────────────────────────────────────

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  try {
    const d = new Date(iso);
    return d.toLocaleString(ADMIN_LOCALE === 'ar' ? 'ar-EG' : 'en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

// ── Skeleton ──────────────────────────────────────────────────────────────────

function SkeletonBlock({ w, h }: { w: number | string; h: number }) {
  return (
    <div
      role="presentation"
      style={{
        height: h,
        width: w,
        maxWidth: '100%',
        borderRadius: 6,
        background:
          'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
        backgroundSize: '400px 100%',
        animation: 'lx-shimmer 1400ms linear infinite',
      }}
    />
  );
}

function DetailSkeleton() {
  return (
    <Stack
      role="status"
      aria-label={strings.modDetailLoadingLabel}
      data-testid="mod-detail-loading"
      flexDirection="column"
      gap="$6"
    >
      {/* Header card skeleton */}
      <Stack
        padding="$6"
        borderRadius="$card"
        gap="$3"
        style={{ backgroundColor: 'var(--lx-card)', border: '1px solid var(--lx-border)' }}
      >
        <Stack flexDirection="row" gap="$2">
          <SkeletonBlock w={80} h={22} />
          <SkeletonBlock w={100} h={22} />
        </Stack>
        <SkeletonBlock w={220} h={20} />
        <SkeletonBlock w={160} h={14} />
      </Stack>
      {/* Facets card skeleton */}
      <Stack
        padding="$6"
        borderRadius="$card"
        gap="$4"
        style={{ backgroundColor: 'var(--lx-card)', border: '1px solid var(--lx-border)' }}
      >
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
          {Array.from({ length: 6 }).map((_, i) => (
            <Stack key={i} gap="$1">
              <SkeletonBlock w={80} h={12} />
              <SkeletonBlock w={140} h={16} />
            </Stack>
          ))}
        </div>
      </Stack>
      {/* Verdict card skeleton */}
      <Stack
        padding="$6"
        borderRadius="$card"
        gap="$4"
        style={{ backgroundColor: 'var(--lx-card)', border: '1px solid var(--lx-border)' }}
      >
        <SkeletonBlock w={120} h={12} />
        <SkeletonBlock w="100%" h={48} />
        <Stack flexDirection="row" gap="$2" flexWrap="wrap">
          {Array.from({ length: 3 }).map((_, i) => (
            <SkeletonBlock key={i} w={80} h={24} />
          ))}
        </Stack>
      </Stack>
    </Stack>
  );
}

// ── SafetyVerdictView ─────────────────────────────────────────────────────────

function SafetyVerdictView({
  safetyVerdictJson,
}: {
  safetyVerdictJson: string | null | undefined;
}) {
  let parsed: SafetyVerdict | null = null;
  let hasContent = false;

  if (safetyVerdictJson) {
    try {
      parsed = JSON.parse(safetyVerdictJson) as SafetyVerdict;
      hasContent =
        (parsed.failedChecks?.length ?? 0) > 0 ||
        (parsed.reasonCodes?.length ?? 0) > 0 ||
        Boolean(parsed.actionTaken) ||
        Boolean(parsed.modelId);
    } catch {
      // Parse failure — render fallback
      parsed = null;
      hasContent = false;
    }
  }

  return (
    <Stack
      data-testid="mod-verdict-section"
      flexDirection="column"
      gap="$4"
      padding="$4"
      borderRadius="$sm"
      style={{
        backgroundColor: 'var(--lx-bg)',
        border: '1px solid var(--lx-border)',
      }}
    >
      {/* Eyebrow */}
      <Text
        fontFamily="$body"
        fontSize={11}
        fontWeight="600"
        style={{
          color: 'var(--lx-fg3)',
          textTransform: 'uppercase',
          letterSpacing: '0.06em',
        }}
      >
        {strings.modVerdictSection}
      </Text>

      {/* Privacy framing notice — always shown */}
      <Stack
        flexDirection="row"
        gap="$2"
        alignItems="flex-start"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'rgba(79,70,229,0.08)',
          border: '1px solid rgba(79,70,229,0.15)',
        }}
      >
        <span
          aria-hidden="true"
          style={{
            color: 'var(--lx-fg3)',
            flexShrink: 0,
            marginTop: 1,
            display: 'inline-flex',
          }}
        >
          {/* Info icon */}
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
            <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
            <line x1="12" y1="8" x2="12" y2="8" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            <line x1="12" y1="12" x2="12" y2="16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          </svg>
        </span>
        <Text
          fontFamily="$body"
          fontSize={12}
          style={{ color: 'var(--lx-fg3)', lineHeight: 1.5 }}
        >
          {strings.modVerdictPrivacyNote}
        </Text>
      </Stack>

      {/* Fallback when verdict unavailable or unparseable */}
      {(!hasContent) && (
        <Text
          fontFamily="$body"
          fontSize={14}
          style={{ color: 'var(--lx-fg3)', fontStyle: 'italic' }}
        >
          {strings.modVerdictUnavailable}
        </Text>
      )}

      {/* Failed checks */}
      {parsed && (parsed.failedChecks?.length ?? 0) > 0 && (
        <Stack flexDirection="column" gap="$2">
          <Text
            fontFamily="$body"
            fontSize={12}
            fontWeight="600"
            style={{
              color: 'var(--lx-fg3)',
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
            }}
          >
            {strings.modVerdictFailedChecks}
          </Text>
          <Stack flexDirection="row" flexWrap="wrap" gap="$2">
            {parsed.failedChecks!.map((check) => (
              <span
                key={check}
                dir="ltr"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  height: 24,
                  paddingInline: 10,
                  borderRadius: 9999,
                  backgroundColor: 'rgba(239,68,68,0.12)',
                  color: '#EF4444',
                  fontSize: 12,
                  fontWeight: 600,
                  fontFamily: 'inherit',
                }}
              >
                {check}
              </span>
            ))}
          </Stack>
        </Stack>
      )}

      {/* Reason codes */}
      {parsed && (parsed.reasonCodes?.length ?? 0) > 0 && (
        <Stack flexDirection="column" gap="$2">
          <Text
            fontFamily="$body"
            fontSize={12}
            fontWeight="600"
            style={{
              color: 'var(--lx-fg3)',
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
            }}
          >
            {strings.modVerdictReasonCodes}
          </Text>
          <Stack flexDirection="row" flexWrap="wrap" gap="$2">
            {parsed.reasonCodes!.map((code) => (
              <span
                key={code}
                dir="ltr"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  height: 24,
                  paddingInline: 10,
                  borderRadius: 9999,
                  backgroundColor: 'rgba(245,158,11,0.12)',
                  color: '#F59E0B',
                  fontSize: 12,
                  fontWeight: 600,
                  fontFamily: 'inherit',
                }}
              >
                {code}
              </span>
            ))}
          </Stack>
        </Stack>
      )}

      {/* Action taken */}
      {parsed?.actionTaken && (
        <Stack flexDirection="column" gap="$1">
          <Text
            fontFamily="$body"
            fontSize={12}
            fontWeight="600"
            style={{
              color: 'var(--lx-fg3)',
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
            }}
          >
            {strings.modVerdictActionTaken}
          </Text>
          <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$fg1">
            {parsed.actionTaken}
          </Text>
        </Stack>
      )}

      {/* Model ID */}
      {parsed?.modelId && (
        <Stack flexDirection="column" gap="$1">
          <Text
            fontFamily="$body"
            fontSize={12}
            fontWeight="600"
            style={{
              color: 'var(--lx-fg3)',
              textTransform: 'uppercase',
              letterSpacing: '0.04em',
            }}
          >
            {strings.modVerdictModelId}
          </Text>
          <Text
            fontFamily="$body" style={{ fontFamily: 'var(--lx-font-mono, monospace)' }}
            fontSize={13}
            color="$fg2"
            dir="ltr"
          >
            {parsed.modelId}
          </Text>
        </Stack>
      )}
    </Stack>
  );
}

// ── ReviewActionsPanel ────────────────────────────────────────────────────────

interface ReviewActionsPanelProps {
  item: ModerationItemDetailDto;
  onReview: (decision: ReviewDecision) => void;
}

function ReviewActionsPanel({ item, onReview }: ReviewActionsPanelProps) {
  const isTerminal =
    item.status === 'Approved' || item.status === 'Rejected';
  const isFlagged = item.status === 'Flagged';
  const isReviewable = item.status === 'Pending' || item.status === 'Flagged';

  return (
    <Stack
      data-testid="mod-review-actions-panel"
      padding="$6"
      borderRadius="$card"
      flexDirection="column"
      gap="$4"
      style={{
        backgroundColor: 'var(--lx-card)',
        border: '1px solid var(--lx-border)',
      }}
    >
      <Text
        fontFamily="$body"
        fontSize={14}
        fontWeight="600"
        style={{
          color: 'var(--lx-fg3)',
          textTransform: 'uppercase',
          letterSpacing: '0.06em',
        }}
      >
        {strings.modReviewActionsHeading}
      </Text>

      {/* Terminal notice — replaces buttons when item is resolved */}
      {isTerminal && (
        <Stack
          data-testid="mod-terminal-notice"
          flexDirection="row"
          gap="$2"
          alignItems="center"
          padding="$3"
          borderRadius="$sm"
          style={{
            backgroundColor: 'rgba(148,163,184,0.08)',
            border: '1px solid rgba(148,163,184,0.15)',
          }}
        >
          <span aria-hidden="true" style={{ color: 'var(--lx-fg3)', display: 'inline-flex' }}>
            {/* Lock icon */}
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none">
              <rect x="4" y="10" width="16" height="11" rx="2" stroke="currentColor" strokeWidth="2" />
              <path d="M8 10V7a4 4 0 0 1 8 0v3" stroke="currentColor" strokeWidth="2" />
            </svg>
          </span>
          <Text
            fontFamily="$body"
            fontSize={13}
            style={{ color: 'var(--lx-fg3)', lineHeight: 1.5 }}
            role="status"
          >
            {strings.modTerminalNotice}
          </Text>
        </Stack>
      )}

      {/* Reviewable action buttons */}
      {isReviewable && (
        <Stack flexDirection="column" gap="$3">
          {/* Approve */}
          <button
            type="button"
            data-testid="mod-review-approve-btn"
            aria-label={`${strings.modReviewApprove}: ${item.contentReference}`}
            onClick={() => onReview('Approved')}
            style={{
              height: 44,
              width: '100%',
              borderRadius: 'var(--lx-radius-button)',
              backgroundColor: 'transparent',
              border: '1px solid rgba(34,197,94,0.35)',
              color: '#22C55E',
              fontSize: 14,
              fontWeight: 600,
              fontFamily: 'inherit',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              cursor: 'pointer',
              transition: 'background-color 120ms var(--lx-ease-out), border-color 120ms, transform 80ms',
            }}
            onMouseEnter={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(34,197,94,0.10)';
              (e.currentTarget as HTMLButtonElement).style.borderColor = '#22C55E';
            }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
              (e.currentTarget as HTMLButtonElement).style.borderColor = 'rgba(34,197,94,0.35)';
            }}
            onMouseDown={(e) => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)'; }}
            onMouseUp={(e) => { (e.currentTarget as HTMLButtonElement).style.transform = ''; }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
              <polyline points="9,12 11,14 15,10" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            </svg>
            {strings.modReviewApprove}
          </button>

          {/* Reject */}
          <button
            type="button"
            data-testid="mod-review-reject-btn"
            aria-label={`${strings.modReviewReject}: ${item.contentReference}`}
            onClick={() => onReview('Rejected')}
            style={{
              height: 44,
              width: '100%',
              borderRadius: 'var(--lx-radius-button)',
              backgroundColor: 'transparent',
              border: '1px solid rgba(239,68,68,0.35)',
              color: '#EF4444',
              fontSize: 14,
              fontWeight: 600,
              fontFamily: 'inherit',
              display: 'inline-flex',
              alignItems: 'center',
              justifyContent: 'center',
              gap: 8,
              cursor: 'pointer',
              transition: 'background-color 120ms var(--lx-ease-out), border-color 120ms, transform 80ms',
            }}
            onMouseEnter={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(239,68,68,0.08)';
              (e.currentTarget as HTMLButtonElement).style.borderColor = '#EF4444';
            }}
            onMouseLeave={(e) => {
              (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
              (e.currentTarget as HTMLButtonElement).style.borderColor = 'rgba(239,68,68,0.35)';
            }}
            onMouseDown={(e) => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)'; }}
            onMouseUp={(e) => { (e.currentTarget as HTMLButtonElement).style.transform = ''; }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
              <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="2" />
              <line x1="15" y1="9" x2="9" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              <line x1="9" y1="9" x2="15" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
            {strings.modReviewReject}
          </button>

          {/* Flag — shown only when status is Pending; hidden when already Flagged */}
          {item.status === 'Pending' && (
            <button
              type="button"
              data-testid="mod-review-flag-btn"
              aria-label={`${strings.modReviewFlag}: ${item.contentReference}`}
              onClick={() => onReview('Flagged')}
              style={{
                height: 44,
                width: '100%',
                borderRadius: 'var(--lx-radius-button)',
                backgroundColor: 'transparent',
                border: '1px solid rgba(168,85,247,0.35)',
                color: '#A855F7',
                fontSize: 14,
                fontWeight: 600,
                fontFamily: 'inherit',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                gap: 8,
                cursor: 'pointer',
                transition: 'background-color 120ms var(--lx-ease-out), border-color 120ms, transform 80ms',
              }}
              onMouseEnter={(e) => {
                (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'rgba(168,85,247,0.08)';
                (e.currentTarget as HTMLButtonElement).style.borderColor = '#A855F7';
              }}
              onMouseLeave={(e) => {
                (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
                (e.currentTarget as HTMLButtonElement).style.borderColor = 'rgba(168,85,247,0.35)';
              }}
              onMouseDown={(e) => { (e.currentTarget as HTMLButtonElement).style.transform = 'scale(0.95)'; }}
              onMouseUp={(e) => { (e.currentTarget as HTMLButtonElement).style.transform = ''; }}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                <line x1="4" y1="22" x2="4" y2="15" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
              {strings.modReviewFlag}
            </button>
          )}

          {/* Already-flagged notice when Flagged state */}
          {isFlagged && (
            <Text
              fontFamily="$body"
              fontSize={12}
              style={{ color: 'var(--lx-fg3)', fontStyle: 'italic', paddingTop: 8 }}
            >
              {strings.modAlreadyFlagged}
            </Text>
          )}
        </Stack>
      )}
    </Stack>
  );
}

// ── Main page ──────────────────────────────────────────────────────────────────

interface PageProps {
  params: Promise<{ id: string }>;
}

export default function ModerationDetailPage({ params }: PageProps) {
  const { id: idParam } = use(params);
  const itemId = parseInt(idParam, 10);

  const [dialogDecision, setDialogDecision] = useState<ReviewDecision | null>(
    null,
  );
  const [successBanner, setSuccessBanner] = useState(false);

  const { data: item, isLoading, isError, refetch, error } = useModerationItem(
    isNaN(itemId) ? 0 : itemId,
  );

  // Auto-dismiss success banner after 5s
  useEffect(() => {
    if (!successBanner) return;
    const id = setTimeout(() => setSuccessBanner(false), 5000);
    return () => clearTimeout(id);
  }, [successBanner]);

  // Detect 404 — the api-client throws ApiError with status 404
  const is404 =
    !isLoading &&
    isError &&
    (error as { status?: number })?.status === 404;

  const title = item
    ? item.contentReference
    : strings.modPageTitleDetail;

  // ── Render ──────────────────────────────────────────────────────────────────

  return (
    <AdminShell title={title}>
      <Stack flexDirection="column" gap="$6">
        {/* Back link */}
        <Link
          href="/moderation"
          style={{ textDecoration: 'none', display: 'inline-flex', alignItems: 'center', gap: 8 }}
          data-testid="mod-detail-back-btn"
        >
          <span style={{ color: 'var(--lx-fg3)', display: 'inline-flex' }}>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
              <path
                d={ADMIN_LOCALE === 'ar' ? 'M9 18l6-6-6-6' : 'M15 18l-6-6 6-6'}
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          </span>
          <Text
            fontFamily="$body"
            fontSize={14}
            style={{ color: 'var(--lx-fg3)' }}
          >
            {strings.modBackToQueue}
          </Text>
        </Link>

        {/* Success banner (auto-dismiss 5s) */}
        {successBanner && (
          <AdminErrorBanner variant="success" message={strings.modReviewSuccess} />
        )}

        {/* Loading skeleton */}
        {isLoading && <DetailSkeleton />}

        {/* Not found state */}
        {is404 && (
          <Stack
            alignItems="center"
            padding={40}
            gap="$4"
            style={{
              backgroundColor: 'var(--lx-card)',
              borderRadius: 'var(--lx-radius-card)',
              border: '1px solid var(--lx-border)',
            }}
          >
            <span style={{ color: 'var(--lx-fg3)' }}>
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                <circle cx="11" cy="11" r="8" stroke="currentColor" strokeWidth="2" />
                <path d="m21 21-4.35-4.35" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                <path d="m8 8 6 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                <path d="m14 8-6 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
            </span>
            <Text fontFamily="$heading" fontSize={18} fontWeight="600" color="$fg1" style={{ textAlign: 'center' }}>
              {strings.modNotFoundHeading}
            </Text>
            <Text fontFamily="$body" fontSize={14} style={{ color: 'var(--lx-fg3)', textAlign: 'center', maxWidth: 320 }}>
              {strings.modNotFoundBody}
            </Text>
          </Stack>
        )}

        {/* General error state */}
        {!isLoading && isError && !is404 && (
          <Stack flexDirection="column" gap="$4" data-testid="mod-detail-error">
            <AdminErrorBanner variant="error" message={strings.modDetailError} />
            <button
              type="button"
              onClick={() => void refetch()}
              style={{
                height: 36,
                paddingInline: 16,
                borderRadius: 'var(--lx-radius-button)',
                border: '1px solid var(--lx-border)',
                backgroundColor: 'transparent',
                color: 'var(--lx-fg2)',
                fontSize: 13,
                cursor: 'pointer',
                fontFamily: 'inherit',
              }}
            >
              {strings.modListRetry}
            </button>
          </Stack>
        )}

        {/* Detail content */}
        {!isLoading && !isError && item && (
          <>
            {/* Header card */}
            <Stack
              data-testid="mod-detail-card"
              padding="$6"
              borderRadius="$card"
              flexDirection="row"
              alignItems="flex-start"
              gap="$6"
              style={{
                backgroundColor: 'var(--lx-card)',
                border: '1px solid var(--lx-border)',
              }}
            >
              <Stack flex={1} flexDirection="column" gap="$3">
                {/* Badges row */}
                <Stack flexDirection="row" gap="$2" flexWrap="wrap">
                  <StatusBadge
                    variant="moderation"
                    value={item.status}
                    strings={strings}
                  />
                  <SourceBadge value={item.source} strings={strings} />
                </Stack>

                {/* Content reference */}
                <Text
                  fontFamily="$body"
                  fontSize={18}
                  fontWeight="700"
                  color="$fg1"
                  dir="ltr"
                  style={{ fontFamily: 'var(--lx-font-mono, monospace)', lineHeight: 1.3 }}
                >
                  {item.contentReference}
                </Text>

                {/* Meta row */}
                <Stack flexDirection="row" alignItems="center" gap="$4" flexWrap="wrap">
                  <Text
                    fontFamily="$body"
                    fontSize={13}
                    style={{ color: 'var(--lx-fg3)', fontVariantNumeric: 'tabular-nums' }}
                    dir="ltr"
                  >
                    {strings.modFieldDetectedAt}: {formatDate(item.detectedAt)}
                  </Text>
                  <Text
                    fontFamily="$body"
                    fontSize={12}
                    dir="ltr"
                    style={{ fontFamily: 'var(--lx-font-mono, monospace)', color: 'var(--lx-fg3)' }}
                  >
                    {strings.modFieldItemId} {item.id}
                  </Text>
                </Stack>
              </Stack>
            </Stack>

            {/* Two-column layout */}
            <Stack
              flexDirection="row"
              gap="$6"
              alignItems="flex-start"
              style={{ flexWrap: 'wrap' }}
            >
              {/* Primary column */}
              <Stack flex={2} style={{ minWidth: 0 }} flexDirection="column" gap="$6">

                {/* Facets card */}
                <Stack
                  data-testid="mod-facets-card"
                  padding="$6"
                  borderRadius="$card"
                  flexDirection="column"
                  gap="$4"
                  style={{
                    backgroundColor: 'var(--lx-card)',
                    border: '1px solid var(--lx-border)',
                  }}
                >
                  <Text
                    fontFamily="$body"
                    fontSize={14}
                    fontWeight="600"
                    style={{
                      color: 'var(--lx-fg3)',
                      textTransform: 'uppercase',
                      letterSpacing: '0.06em',
                    }}
                  >
                    {strings.modSectionDetails}
                  </Text>

                  <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                    {/* Source */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {strings.modColSource}
                      </Text>
                      <SourceBadge value={item.source} strings={strings} />
                    </Stack>

                    {/* Status */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {strings.modColStatus}
                      </Text>
                      <StatusBadge variant="moderation" value={item.status} strings={strings} />
                    </Stack>

                    {/* Content reference */}
                    <Stack flexDirection="column" gap="$1" style={{ gridColumn: '1 / -1' }}>
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {strings.modColContentRef}
                      </Text>
                      <Text fontFamily="$body" style={{ fontFamily: 'var(--lx-font-mono, monospace)' }} fontSize={14} fontWeight="500" color="$fg2" dir="ltr">
                        {item.contentReference}
                      </Text>
                    </Stack>

                    {/* Subject */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {strings.modColSubjectGrade.split(' / ')[0] ?? strings.modColSubjectGrade}
                      </Text>
                      <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                        {item.subjectCode ?? '—'}
                      </Text>
                    </Stack>

                    {/* Grade */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {ADMIN_LOCALE === 'ar' ? 'الصف' : 'Grade'}
                      </Text>
                      <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                        {item.grade !== null
                          ? ADMIN_LOCALE === 'ar'
                            ? `الصف ${item.grade}`
                            : `Grade ${item.grade}`
                          : '—'}
                      </Text>
                    </Stack>

                    {/* Task kind */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {strings.modColTaskKind}
                      </Text>
                      <Text fontFamily="$body" fontSize={14} fontWeight="500" color="$fg1">
                        {item.taskKind ?? '—'}
                      </Text>
                    </Stack>

                    {/* Source Event ID */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {ADMIN_LOCALE === 'ar' ? 'معرّف حدث المصدر' : 'Source Event ID'}
                      </Text>
                      <Text fontFamily="$body" fontSize={12} dir="ltr" style={{ fontFamily: 'var(--lx-font-mono, monospace)', color: 'var(--lx-fg3)' }}>
                        {item.sourceEventId}
                      </Text>
                    </Stack>

                    {/* Student ID (only when non-null) */}
                    {item.studentId !== null && (
                      <Stack flexDirection="column" gap="$1">
                        <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                          {strings.modFieldStudentId}
                        </Text>
                        <Text fontFamily="$body" style={{ fontFamily: 'var(--lx-font-mono, monospace)' }} fontSize={14} color="$fg2" dir="ltr">
                          {item.studentId}
                        </Text>
                      </Stack>
                    )}

                    {/* Detected at */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {strings.modFieldDetectedAt}
                      </Text>
                      <Text fontFamily="$body" fontSize={14} color="$fg2" dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>
                        {formatDate(item.detectedAt)}
                      </Text>
                    </Stack>

                    {/* Created at */}
                    <Stack flexDirection="column" gap="$1">
                      <Text fontFamily="$body" fontSize={12} fontWeight="500" style={{ color: 'var(--lx-fg3)' }}>
                        {ADMIN_LOCALE === 'ar' ? 'أُنشئ في' : 'Created At'}
                      </Text>
                      <Text fontFamily="$body" fontSize={14} dir="ltr" style={{ color: 'var(--lx-fg3)', fontVariantNumeric: 'tabular-nums' }}>
                        {formatDate(item.createdAt)}
                      </Text>
                    </Stack>
                  </div>
                </Stack>

                {/* SafetyVerdictView */}
                <Stack
                  padding="$6"
                  borderRadius="$card"
                  style={{
                    backgroundColor: 'var(--lx-card)',
                    border: '1px solid var(--lx-border)',
                  }}
                >
                  <SafetyVerdictView safetyVerdictJson={item.safetyVerdict} />
                </Stack>

                {/* Review history (conditional on reviewed) */}
                {item.reviewedByUserId !== null && (
                  <Stack
                    data-testid="mod-review-history"
                    padding="$6"
                    borderRadius="$card"
                    flexDirection="column"
                    gap="$4"
                    style={{
                      backgroundColor: 'var(--lx-card)',
                      border: '1px solid var(--lx-border)',
                    }}
                  >
                    <Text
                      fontFamily="$body"
                      fontSize={14}
                      fontWeight="600"
                      style={{
                        color: 'var(--lx-fg3)',
                        textTransform: 'uppercase',
                        letterSpacing: '0.06em',
                      }}
                    >
                      {strings.modSectionReviewHistory}
                    </Text>

                    <Stack
                      flexDirection="column"
                      gap="$3"
                      padding="$4"
                      borderRadius="$sm"
                      style={{
                        backgroundColor: 'var(--lx-bg)',
                        border: '1px solid var(--lx-border)',
                      }}
                    >
                      {/* Reviewed by */}
                      <Stack flexDirection="row" alignItems="center" gap="$2">
                        <Text fontFamily="$body" fontSize={12} style={{ color: 'var(--lx-fg3)' }}>
                          {strings.modReviewedBy}
                        </Text>
                        <Text fontFamily="$body" style={{ fontFamily: 'var(--lx-font-mono, monospace)' }} fontSize={14} fontWeight="600" color="$fg1" dir="ltr">
                          {item.reviewedByUserId}
                        </Text>
                      </Stack>

                      {/* Reviewed at */}
                      {item.reviewedAtUtc && (
                        <Stack flexDirection="row" alignItems="center" gap="$2">
                          <Text fontFamily="$body" fontSize={12} style={{ color: 'var(--lx-fg3)' }}>
                            {strings.modReviewedAt}
                          </Text>
                          <Text fontFamily="$body" fontSize={14} color="$fg1" dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>
                            {formatDate(item.reviewedAtUtc)}
                          </Text>
                        </Stack>
                      )}

                      {/* Decision reason */}
                      {item.reviewDecisionReason && (
                        <Stack flexDirection="column" gap="$1">
                          <Text fontFamily="$body" fontSize={12} style={{ color: 'var(--lx-fg3)' }}>
                            {strings.modReviewReason}
                          </Text>
                          <Text
                            fontFamily="$body"
                            fontSize={14}
                            color="$fg2"
                            style={{ lineHeight: 1.5, fontStyle: 'italic' }}
                          >
                            {item.reviewDecisionReason}
                          </Text>
                        </Stack>
                      )}
                    </Stack>
                  </Stack>
                )}
              </Stack>

              {/* Secondary column — Review actions panel */}
              <Stack
                style={{ flex: 1, minWidth: 220, maxWidth: 280 }}
              >
                <ReviewActionsPanel
                  item={item}
                  onReview={(decision) => setDialogDecision(decision)}
                />
              </Stack>
            </Stack>
          </>
        )}
      </Stack>

      {/* Review dialog */}
      {item && dialogDecision && (
        <ReviewItemDialog
          open={dialogDecision !== null}
          itemId={item.id}
          contentReference={item.contentReference}
          decision={dialogDecision}
          onClose={() => setDialogDecision(null)}
          onSuccess={() => {
            setDialogDecision(null);
            setSuccessBanner(true);
          }}
        />
      )}
    </AdminShell>
  );
}
