'use client';

export const dynamic = 'force-dynamic';

/**
 * Timed Events — `/gamification/events` (P7-13-FE).
 *
 * Four-state catalog: loading skeleton / empty + CTA / error + retry / results table.
 * Status derived client-side from now/isActive/startUtc/endUtc:
 *   now > endUtc                    → EXPIRED
 *   isActive && startUtc <= now     → ACTIVE
 *   else                            → SCHEDULED
 *
 * Activate = POST .../activate (no body); Expire = POST .../expire (no body).
 * Edit gap: TimedEventListItemDto lacks descriptionEn/descriptionAr;
 *           form shows empty descriptions with a notice.
 * Design Spec Part E.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import Link from 'next/link';

import {
  useTimedEvents,
  useActivateTimedEvent,
  useExpireTimedEvent,
  type TimedEventListItemDto,
} from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminShell } from '../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { AdminConfirmDialog } from '../../../../components/AdminConfirmDialog';
import { TimedEventForm } from '../../../../components/gamification/TimedEventForm';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);
const isAr = ADMIN_LOCALE === 'ar';

// ── Status derivation ─────────────────────────────────────────────────────────

type EventStatus = 'ACTIVE' | 'SCHEDULED' | 'EXPIRED';

function deriveStatus(event: TimedEventListItemDto, now: Date): EventStatus {
  const end = new Date(event.endUtc);
  const start = new Date(event.startUtc);
  if (now > end) return 'EXPIRED';
  if (event.isActive && start <= now) return 'ACTIVE';
  return 'SCHEDULED';
}

// ── Scope labels (scope comes from BE as string name e.g. "AllXp") ───────────

const SCOPE_LABELS: Record<string, { en: string; ar: string }> = {
  AllXp:    { en: 'All XP',     ar: 'كل النقاط' },
  MissionXp:{ en: 'Mission XP', ar: 'نقاط المهام' },
  LeagueXp: { en: 'League XP',  ar: 'نقاط الدوري' },
};

function scopeLabel(s: string): string {
  const l = SCOPE_LABELS[s];
  return l ? (isAr ? l.ar : l.en) : s;
}

// ── Date formatting ───────────────────────────────────────────────────────────

function fmtDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString(isAr ? 'ar-SA' : 'en-GB', {
      dateStyle: 'medium', timeStyle: 'short',
    });
  } catch {
    return iso;
  }
}

// ── Lucide icons ──────────────────────────────────────────────────────────────

function PlusIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <line x1="12" y1="5" x2="12" y2="19" /><line x1="5" y1="12" x2="19" y2="12" />
    </svg>
  );
}

function PencilIcon() {
  return (
    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
      <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
    </svg>
  );
}

function CalendarIcon() {
  return (
    <svg width="32" height="32" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="4" width="18" height="18" rx="2" ry="2" />
      <line x1="16" y1="2" x2="16" y2="6" /><line x1="8" y1="2" x2="8" y2="6" />
      <line x1="3" y1="10" x2="21" y2="10" />
    </svg>
  );
}

// ── Status badge ──────────────────────────────────────────────────────────────

const STATUS_STYLES: Record<EventStatus, { bg: string; color: string; border: string }> = {
  ACTIVE:    { bg: 'rgba(34,197,94,0.12)',   color: '#22C55E', border: 'rgba(34,197,94,0.2)' },
  SCHEDULED: { bg: 'rgba(79,70,229,0.12)',   color: '#6366F1', border: 'rgba(79,70,229,0.2)' },
  EXPIRED:   { bg: 'rgba(148,163,184,0.12)', color: '#94A3B8', border: 'rgba(148,163,184,0.2)' },
};

const STATUS_LABELS: Record<EventStatus, { en: string; ar: string }> = {
  ACTIVE:    { en: 'Active',    ar: 'نشط' },
  SCHEDULED: { en: 'Scheduled', ar: 'مجدول' },
  EXPIRED:   { en: 'Expired',   ar: 'منتهي' },
};

function StatusBadge({ status }: { status: EventStatus }) {
  const s = STATUS_STYLES[status];
  const label = isAr ? STATUS_LABELS[status].ar : STATUS_LABELS[status].en;
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      paddingInline: 8, paddingBlock: 3, borderRadius: 9999,
      fontSize: 11, fontWeight: 600, letterSpacing: '0.04em',
      backgroundColor: s.bg, color: s.color, border: `1px solid ${s.border}`,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: 9999, backgroundColor: 'currentColor', flexShrink: 0 }} />
      {label}
    </span>
  );
}

// ── Action btn ────────────────────────────────────────────────────────────────

function ActionBtn({
  onClick, danger = false, disabled = false, testId, children,
}: {
  onClick: () => void; danger?: boolean; disabled?: boolean; testId?: string; children: React.ReactNode;
}) {
  return (
    <button
      type="button" onClick={onClick} disabled={disabled} data-testid={testId}
      style={{
        height: 28, paddingInline: 10, borderRadius: 8,
        border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
        color: danger ? '#EF4444' : 'var(--lx-fg2)',
        fontSize: 12, fontWeight: 500,
        cursor: disabled ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit', opacity: disabled ? 0.4 : 1,
        display: 'inline-flex', alignItems: 'center', gap: 4,
      }}
      onMouseEnter={(e) => {
        if (!disabled) (e.currentTarget as HTMLButtonElement).style.backgroundColor =
          danger ? 'rgba(239,68,68,0.08)' : 'var(--lx-card-soft)';
      }}
      onMouseLeave={(e) => {
        if (!disabled) (e.currentTarget as HTMLButtonElement).style.backgroundColor = 'transparent';
      }}
    >
      {children}
    </button>
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr>
      {Array.from({ length: 7 }).map((_, i) => (
        <td key={i} style={{ padding: '14px 16px' }}>
          <div style={{ height: 14, borderRadius: 6, backgroundColor: 'var(--lx-card-soft)', width: i === 0 ? 160 : 80 }} />
        </td>
      ))}
    </tr>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function TimedEventsPage() {
  const { data: events, isLoading, isError, error, refetch, isFetching } = useTimedEvents();

  const [createOpen, setCreateOpen] = useState(false);
  const [editEvent, setEditEvent] = useState<TimedEventListItemDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<TimedEventListItemDto | null>(null);
  const [expireTarget, setExpireTarget] = useState<TimedEventListItemDto | null>(null);
  const [successBanner, setSuccessBanner] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const activateMutation = useActivateTimedEvent();
  const expireMutation = useExpireTimedEvent();

  const now = new Date();

  const handleActivate = async (event: TimedEventListItemDto) => {
    setActionError(null);
    try {
      await activateMutation.mutateAsync({ id: event.id });
      setSuccessBanner(strings.gamEventActivatedBanner);
      setActivateTarget(null);
    } catch (err) {
      if (isApiError(err) && err.status === 404) setActionError(strings.gamEventNotFoundError);
      else setActionError(strings.gamEventActionError);
      setActivateTarget(null);
    }
  };

  const handleExpire = async (event: TimedEventListItemDto) => {
    setActionError(null);
    try {
      await expireMutation.mutateAsync({ id: event.id });
      setSuccessBanner(strings.gamEventExpiredBanner);
      setExpireTarget(null);
    } catch (err) {
      if (isApiError(err) && err.status === 404) setActionError(strings.gamEventNotFoundError);
      else setActionError(strings.gamEventActionError);
      setExpireTarget(null);
    }
  };

  return (
    <AdminShell title={strings.gamEventPageTitle}>
      <Stack flexDirection="column" gap="$5" data-testid="timed-events-page">
        {/* Header */}
        <Stack flexDirection="row" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap="$3">
          <Stack flexDirection="column" gap="$1">
            <Stack flexDirection="row" alignItems="center" gap="$2">
              <Link href="/gamification" style={{ fontSize: 13, color: 'var(--lx-fg3)', textDecoration: 'none' }}>
                {strings.gamification}
              </Link>
              <span style={{ color: 'var(--lx-fg3)', fontSize: 13 }}>/</span>
              <span style={{ fontSize: 13, color: 'var(--lx-fg2)' }}>{strings.gamEventPageTitle}</span>
            </Stack>
            <Text fontFamily="$heading" fontSize={22} fontWeight="700" color="$fg1">
              {strings.gamEventPageTitle}
            </Text>
          </Stack>
          <button
            type="button" data-testid="event-create-btn" onClick={() => setCreateOpen(true)}
            style={{
              height: 40, paddingInline: 16, borderRadius: 16,
              backgroundColor: '#F59E0B', border: 'none',
              color: '#0F172A', fontSize: 14, fontWeight: 600,
              cursor: 'pointer', fontFamily: 'inherit',
              display: 'inline-flex', alignItems: 'center', gap: 8,
            }}
            onMouseEnter={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#FBBF24'; }}
            onMouseLeave={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#F59E0B'; }}
          >
            <PlusIcon />{strings.gamEventCreateBtn}
          </button>
        </Stack>

        {/* Banners */}
        {successBanner && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
            <div style={{ flex: 1 }}><AdminErrorBanner variant="success" message={successBanner} /></div>
            <button type="button" onClick={() => setSuccessBanner(null)} aria-label="Dismiss"
              style={{ flexShrink: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--lx-fg3)', padding: 4 }}>
              ✕
            </button>
          </div>
        )}
        {actionError && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
            <div style={{ flex: 1 }}><AdminErrorBanner variant="error" message={actionError} /></div>
            <button type="button" onClick={() => setActionError(null)} aria-label="Dismiss"
              style={{ flexShrink: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--lx-fg3)', padding: 4 }}>
              ✕
            </button>
          </div>
        )}

        {/* Results */}
        <div
          aria-live="polite" aria-busy={isFetching}
          data-testid="event-results-region"
          style={{
            backgroundColor: 'var(--lx-card)', borderRadius: 20,
            border: '1px solid var(--lx-border)', overflow: 'hidden',
          }}
        >
          {isLoading && (
            <div role="status" aria-label={strings.gamEventLoading}>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <tbody>{Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)}</tbody>
              </table>
            </div>
          )}
          {isError && !isLoading && (
            <div style={{ padding: 32, display: 'flex', flexDirection: 'column', gap: 12, alignItems: 'center' }}>
              <AdminErrorBanner variant="error" message={String((error as Error)?.message ?? strings.gamEventFetchError)} />
              <button type="button" onClick={() => refetch()} data-testid="event-retry-btn"
                style={{
                  height: 36, paddingInline: 16, borderRadius: 16,
                  border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                  color: 'var(--lx-fg2)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
                }}>
                {strings.gamRetry}
              </button>
            </div>
          )}
          {!isLoading && !isError && events && events.length === 0 && (
            <div style={{ padding: 48, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
              <span style={{ color: 'var(--lx-fg3)' }}><CalendarIcon /></span>
              <Text fontFamily="$body" fontSize={15} color="$fg3" style={{ textAlign: 'center' }}>
                {strings.gamEventEmpty}
              </Text>
              <button type="button" onClick={() => setCreateOpen(true)} data-testid="event-empty-create-btn"
                style={{
                  height: 36, paddingInline: 16, borderRadius: 16,
                  backgroundColor: '#F59E0B', border: 'none',
                  color: '#0F172A', fontSize: 13, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
                }}>
                {strings.gamEventCreateFirst}
              </button>
            </div>
          )}
          {!isLoading && !isError && events && events.length > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }} data-testid="event-table">
                <caption className="sr-only">{strings.gamEventTableCaption}</caption>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
                    {[
                      strings.gamEventColName,
                      strings.gamEventColScope,
                      strings.gamEventColMultiplier,
                      strings.gamEventColStart,
                      strings.gamEventColEnd,
                      strings.gamEventColStatus,
                      strings.gamEventColActions,
                    ].map((col) => (
                      <th key={col} scope="col" style={{
                        padding: '12px 16px', textAlign: 'start',
                        fontSize: 11, fontWeight: 600, color: 'var(--lx-fg3)',
                        textTransform: 'uppercase', letterSpacing: '0.06em', whiteSpace: 'nowrap',
                      }}>
                        {col}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {events.map((event, idx) => {
                    const status = deriveStatus(event, now);
                    const isExpired = status === 'EXPIRED';
                    const eventName = isAr ? event.nameAr : event.nameEn;
                    return (
                      <tr
                        key={event.id}
                        data-testid={`event-row-${event.id}`}
                        style={{ borderBottom: idx < events.length - 1 ? '1px solid var(--lx-border)' : 'none' }}
                        onMouseEnter={(e) => { (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'var(--lx-card-soft)'; }}
                        onMouseLeave={(e) => { (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'transparent'; }}
                      >
                        <td style={{ padding: '14px 16px', color: 'var(--lx-fg1)', fontWeight: 500, maxWidth: 200 }}>
                          <span style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
                            {eventName}
                          </span>
                        </td>
                        <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)' }}>
                          {scopeLabel(event.scope)}
                        </td>
                        <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)', fontVariantNumeric: 'tabular-nums' }} dir="ltr">
                          ×{event.multiplier}
                        </td>
                        <td style={{ padding: '14px 16px', color: 'var(--lx-fg3)', fontSize: 12, whiteSpace: 'nowrap' }} dir="ltr">
                          {fmtDate(event.startUtc)}
                        </td>
                        <td style={{ padding: '14px 16px', color: 'var(--lx-fg3)', fontSize: 12, whiteSpace: 'nowrap' }} dir="ltr">
                          {fmtDate(event.endUtc)}
                        </td>
                        <td style={{ padding: '14px 16px' }}>
                          <StatusBadge status={status} />
                        </td>
                        <td style={{ padding: '14px 16px' }}>
                          <div style={{ display: 'flex', gap: 6, flexWrap: 'nowrap' }}>
                            <ActionBtn
                              testId={`event-edit-${event.id}`}
                              onClick={() => setEditEvent(event)}
                              disabled={isExpired}
                            >
                              <PencilIcon />{strings.gamEventEditBtn}
                            </ActionBtn>
                            {status === 'SCHEDULED' && (
                              <ActionBtn testId={`event-activate-${event.id}`} onClick={() => setActivateTarget(event)}>
                                {strings.gamEventActivateBtn}
                              </ActionBtn>
                            )}
                            {status === 'ACTIVE' && (
                              <ActionBtn testId={`event-expire-${event.id}`} onClick={() => setExpireTarget(event)} danger>
                                {strings.gamEventExpireBtn}
                              </ActionBtn>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </Stack>

      {/* Create/Edit modal */}
      <TimedEventForm
        open={createOpen || !!editEvent}
        onClose={() => { setCreateOpen(false); setEditEvent(null); }}
        editEvent={editEvent ?? undefined}
      />

      {/* Activate confirm */}
      <AdminConfirmDialog
        open={!!activateTarget}
        variant="publish"
        title={strings.gamEventActivateTitle}
        subtitle={strings.gamEventActivateSubtitle.replace('{name}', isAr ? (activateTarget?.nameAr ?? '') : (activateTarget?.nameEn ?? ''))}
        cancelLabel={strings.gamCancelBtn}
        dialogTestId="event-activate-dialog"
        onClose={() => setActivateTarget(null)}
        confirmButton={
          <button
            type="button" data-testid="event-activate-confirm-btn"
            aria-disabled={activateMutation.isPending}
            onClick={() => activateTarget && handleActivate(activateTarget)}
            style={{
              height: 40, paddingInline: 20, borderRadius: 'var(--lx-radius-button)',
              backgroundColor: '#4F46E5', border: 'none',
              color: '#F8FAFC', fontSize: 14, fontWeight: 600,
              cursor: activateMutation.isPending ? 'not-allowed' : 'pointer',
              fontFamily: 'inherit', opacity: activateMutation.isPending ? 0.6 : 1,
            }}
          >
            {activateMutation.isPending ? '…' : strings.gamEventActivateConfirmBtn}
          </button>
        }
      >
        <Text fontFamily="$body" fontSize={14} color="$fg2">{strings.gamEventActivateNote}</Text>
      </AdminConfirmDialog>

      {/* Expire confirm */}
      <AdminConfirmDialog
        open={!!expireTarget}
        variant="expire"
        title={strings.gamEventExpireTitle}
        subtitle={strings.gamEventExpireSubtitle.replace('{name}', isAr ? (expireTarget?.nameAr ?? '') : (expireTarget?.nameEn ?? ''))}
        cancelLabel={strings.gamCancelBtn}
        dialogTestId="event-expire-dialog"
        onClose={() => setExpireTarget(null)}
        confirmButton={
          <button
            type="button" data-testid="event-expire-confirm-btn"
            aria-disabled={expireMutation.isPending}
            onClick={() => expireTarget && handleExpire(expireTarget)}
            style={{
              height: 40, paddingInline: 20, borderRadius: 'var(--lx-radius-button)',
              backgroundColor: '#F59E0B', border: 'none',
              color: '#0F172A', fontSize: 14, fontWeight: 600,
              cursor: expireMutation.isPending ? 'not-allowed' : 'pointer',
              fontFamily: 'inherit', opacity: expireMutation.isPending ? 0.6 : 1,
            }}
          >
            {expireMutation.isPending ? '…' : strings.gamEventExpireConfirmBtn}
          </button>
        }
      >
        <Text fontFamily="$body" fontSize={14} color="$fg2">{strings.gamEventExpireNote}</Text>
      </AdminConfirmDialog>
    </AdminShell>
  );
}
