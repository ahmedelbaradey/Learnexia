'use client';

export const dynamic = 'force-dynamic';

/**
 * Badge Catalog — `/gamification/badges` (P7-13-FE).
 *
 * Four-state catalog table: loading skeleton / empty + Create CTA / error + retry / results.
 * CRUD: Create + Edit (BadgeForm modal). Activate / Deactivate via PATCH + AdminConfirmDialog
 *       variant="retire" (deactivate) — activate uses a direct confirm action.
 * Design Spec Part C.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import Link from 'next/link';

import {
  useAdminBadges,
  useSetBadgeActive,
  type BadgeDefinitionDto,
  type BadgeTriggerType,
} from '@learnexia/api-client';
// Local numeric union types compatible with admin gamification DTOs
type BadgeRarity = 1 | 2 | 3 | 4;
import { isApiError } from '@learnexia/api-client/client';

import { AdminShell } from '../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { AdminConfirmDialog } from '../../../../components/AdminConfirmDialog';
import { BadgeForm } from '../../../../components/gamification/BadgeForm';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Label maps ────────────────────────────────────────────────────────────────

const isAr = ADMIN_LOCALE === 'ar';

const RARITY_LABELS: Record<BadgeRarity, { en: string; ar: string }> = {
  1: { en: 'Common', ar: 'شائع' },
  2: { en: 'Rare', ar: 'نادر' },
  3: { en: 'Epic', ar: 'نادر جدًا' },
  4: { en: 'Legendary', ar: 'أسطوري' },
};

const TRIGGER_LABELS: Record<BadgeTriggerType, { en: string; ar: string }> = {
  1: { en: 'First Lesson', ar: 'أول درس' },
  2: { en: 'Streak Threshold', ar: 'عتبة السلسلة' },
  3: { en: 'Level Threshold', ar: 'عتبة المستوى' },
};

function rarityLabel(r: BadgeRarity): string {
  return (isAr ? RARITY_LABELS[r]?.ar : RARITY_LABELS[r]?.en) ?? String(r);
}
function triggerLabel(t: BadgeTriggerType): string {
  return (isAr ? TRIGGER_LABELS[t]?.ar : TRIGGER_LABELS[t]?.en) ?? String(t);
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

function AwardIcon() {
  return (
    <svg width="32" height="32" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="6" />
      <path d="M15.477 12.89 17 22l-5-3-5 3 1.523-9.11" />
    </svg>
  );
}

// ── Status badge ──────────────────────────────────────────────────────────────

function ActiveBadge({ isActive }: { isActive: boolean }) {
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 4,
      paddingInline: 8, paddingBlock: 3, borderRadius: 9999,
      fontSize: 11, fontWeight: 600, letterSpacing: '0.04em',
      backgroundColor: isActive ? 'rgba(34,197,94,0.12)' : 'rgba(148,163,184,0.12)',
      color: isActive ? '#22C55E' : '#94A3B8',
      border: `1px solid ${isActive ? 'rgba(34,197,94,0.2)' : 'rgba(148,163,184,0.2)'}`,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: 9999, backgroundColor: 'currentColor', flexShrink: 0 }} />
      {isActive ? strings.gamBadgeActive : strings.gamBadgeInactive}
    </span>
  );
}

// ── Row action button ─────────────────────────────────────────────────────────

function ActionBtn({
  onClick, danger = false, disabled = false, testId, children,
}: {
  onClick: () => void; danger?: boolean; disabled?: boolean; testId?: string; children: React.ReactNode;
}) {
  return (
    <button
      type="button" onClick={onClick} disabled={disabled}
      data-testid={testId}
      style={{
        height: 28, paddingInline: 10, borderRadius: 8,
        border: '1px solid var(--lx-border)',
        backgroundColor: 'transparent',
        color: danger ? '#EF4444' : 'var(--lx-fg2)',
        fontSize: 12, fontWeight: 500,
        cursor: disabled ? 'not-allowed' : 'pointer',
        fontFamily: 'inherit',
        opacity: disabled ? 0.4 : 1,
        display: 'inline-flex', alignItems: 'center', gap: 4,
        transition: 'background-color 80ms, color 80ms',
      }}
      onMouseEnter={(e) => {
        if (!disabled) {
          (e.currentTarget as HTMLButtonElement).style.backgroundColor = danger
            ? 'rgba(239,68,68,0.08)' : 'var(--lx-card-soft)';
        }
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
          <div style={{ height: 14, borderRadius: 6, backgroundColor: 'var(--lx-card-soft)', width: i === 0 ? 80 : i === 1 ? 140 : 60 }} />
        </td>
      ))}
    </tr>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function BadgeCatalogPage() {
  const { data: badges, isLoading, isError, error, refetch, isFetching } = useAdminBadges();

  const [createOpen, setCreateOpen] = useState(false);
  const [editBadge, setEditBadge] = useState<BadgeDefinitionDto | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<BadgeDefinitionDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<BadgeDefinitionDto | null>(null);
  const [successBanner, setSuccessBanner] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const setActiveMutation = useSetBadgeActive();

  const handleSetActive = async (badge: BadgeDefinitionDto, isActive: boolean) => {
    setActionError(null);
    try {
      await setActiveMutation.mutateAsync({ id: badge.id, isActive });
      setSuccessBanner(isActive ? strings.gamBadgeActivatedBanner : strings.gamBadgeDeactivatedBanner);
      setDeactivateTarget(null);
      setActivateTarget(null);
    } catch (err) {
      if (isApiError(err) && err.status === 404) {
        setActionError(strings.gamBadgeNotFoundError);
      } else {
        setActionError(strings.gamBadgeActionError);
      }
      setDeactivateTarget(null);
      setActivateTarget(null);
    }
  };

  return (
    <AdminShell title={strings.gamBadgePageTitle}>
      <Stack flexDirection="column" gap="$5" data-testid="badge-catalog-page">
        {/* Header row */}
        <Stack flexDirection="row" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap="$3">
          <Stack flexDirection="column" gap="$1">
            <Stack flexDirection="row" alignItems="center" gap="$2">
              <Link
                href="/gamification"
                style={{ fontSize: 13, color: 'var(--lx-fg3)', textDecoration: 'none' }}
                data-testid="badge-breadcrumb-back"
              >
                {strings.gamification}
              </Link>
              <span style={{ color: 'var(--lx-fg3)', fontSize: 13 }}>/</span>
              <span style={{ fontSize: 13, color: 'var(--lx-fg2)' }}>{strings.gamBadgePageTitle}</span>
            </Stack>
            <Text fontFamily="$heading" fontSize={22} fontWeight="700" color="$fg1">
              {strings.gamBadgePageTitle}
            </Text>
          </Stack>

          <button
            type="button"
            data-testid="badge-create-btn"
            onClick={() => setCreateOpen(true)}
            style={{
              height: 40, paddingInline: 16, borderRadius: 16,
              backgroundColor: '#4F46E5', border: 'none',
              color: '#F8FAFC', fontSize: 14, fontWeight: 600,
              cursor: 'pointer', fontFamily: 'inherit',
              display: 'inline-flex', alignItems: 'center', gap: 8,
            }}
            onMouseEnter={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#6366F1'; }}
            onMouseLeave={(e) => { (e.currentTarget as HTMLButtonElement).style.backgroundColor = '#4F46E5'; }}
          >
            <PlusIcon />{strings.gamBadgeCreateBtn}
          </button>
        </Stack>

        {/* Success banner */}
        {successBanner && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
            <div style={{ flex: 1 }}>
              <AdminErrorBanner variant="success" message={successBanner} />
            </div>
            <button
              type="button" onClick={() => setSuccessBanner(null)}
              aria-label="Dismiss"
              style={{ flexShrink: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--lx-fg3)', padding: 4 }}
            >✕</button>
          </div>
        )}
        {actionError && (
          <div style={{ display: 'flex', gap: 8, alignItems: 'flex-start' }}>
            <div style={{ flex: 1 }}>
              <AdminErrorBanner variant="error" message={actionError} />
            </div>
            <button
              type="button" onClick={() => setActionError(null)}
              aria-label="Dismiss"
              style={{ flexShrink: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--lx-fg3)', padding: 4 }}
            >✕</button>
          </div>
        )}

        {/* Results region */}
        <div
          aria-live="polite"
          aria-busy={isFetching}
          data-testid="badge-results-region"
          style={{
            backgroundColor: 'var(--lx-card)',
            borderRadius: 20,
            border: '1px solid var(--lx-border)',
            overflow: 'hidden',
          }}
        >
          {/* Loading */}
          {isLoading && (
            <div role="status" aria-label={strings.gamBadgeLoading}>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <tbody>{Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)}</tbody>
              </table>
            </div>
          )}

          {/* Error */}
          {isError && !isLoading && (
            <div style={{ padding: 32, display: 'flex', flexDirection: 'column', gap: 12, alignItems: 'center' }}>
              <AdminErrorBanner variant="error" message={String((error as Error)?.message ?? strings.gamBadgeFetchError)} />
              <button
                type="button" onClick={() => refetch()}
                data-testid="badge-retry-btn"
                style={{
                  height: 36, paddingInline: 16, borderRadius: 16,
                  border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                  color: 'var(--lx-fg2)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
                }}
              >
                {strings.gamRetry}
              </button>
            </div>
          )}

          {/* Empty */}
          {!isLoading && !isError && badges && badges.length === 0 && (
            <div style={{ padding: 48, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
              <span style={{ color: 'var(--lx-fg3)' }}><AwardIcon /></span>
              <Text fontFamily="$body" fontSize={15} color="$fg3" style={{ textAlign: 'center' }}>
                {strings.gamBadgeEmpty}
              </Text>
              <button
                type="button" onClick={() => setCreateOpen(true)}
                data-testid="badge-empty-create-btn"
                style={{
                  height: 36, paddingInline: 16, borderRadius: 16,
                  backgroundColor: '#4F46E5', border: 'none',
                  color: '#F8FAFC', fontSize: 13, fontWeight: 600,
                  cursor: 'pointer', fontFamily: 'inherit',
                }}
              >
                {strings.gamBadgeCreateFirst}
              </button>
            </div>
          )}

          {/* Table */}
          {!isLoading && !isError && badges && badges.length > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table
                style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}
                data-testid="badge-table"
              >
                <caption className="sr-only">{strings.gamBadgeTableCaption}</caption>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
                    {[
                      strings.gamBadgeColCode,
                      strings.gamBadgeColName,
                      strings.gamBadgeColRarity,
                      strings.gamBadgeColTrigger,
                      strings.gamBadgeColXp,
                      strings.gamBadgeColStatus,
                      strings.gamBadgeColActions,
                    ].map((col) => (
                      <th
                        key={col} scope="col"
                        style={{
                          padding: '12px 16px', textAlign: 'start',
                          fontSize: 11, fontWeight: 600, color: 'var(--lx-fg3)',
                          textTransform: 'uppercase', letterSpacing: '0.06em',
                          whiteSpace: 'nowrap',
                        }}
                      >
                        {col}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {badges.map((badge, idx) => (
                    <tr
                      key={badge.id}
                      data-testid={`badge-row-${badge.id}`}
                      style={{
                        borderBottom: idx < badges.length - 1 ? '1px solid var(--lx-border)' : 'none',
                        transition: 'background-color 80ms',
                      }}
                      onMouseEnter={(e) => { (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'var(--lx-card-soft)'; }}
                      onMouseLeave={(e) => { (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'transparent'; }}
                    >
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg3)', fontFamily: 'monospace', fontSize: 12 }}>
                        {badge.code}
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg1)', fontWeight: 500, maxWidth: 200 }}>
                        <span style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
                          {badge.name}
                        </span>
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)' }}>
                        {rarityLabel(badge.rarity)}
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)' }}>
                        {triggerLabel(badge.triggerType)}
                        {badge.threshold ? (
                          <span style={{ color: 'var(--lx-fg3)', marginInlineStart: 4 }} dir="ltr">
                            ({badge.threshold})
                          </span>
                        ) : null}
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)', fontVariantNumeric: 'tabular-nums' }} dir="ltr">
                        {badge.rewardXp}
                      </td>
                      <td style={{ padding: '14px 16px' }}>
                        <ActiveBadge isActive={badge.isActive} />
                      </td>
                      <td style={{ padding: '14px 16px' }}>
                        <div style={{ display: 'flex', gap: 6, flexWrap: 'nowrap' }}>
                          <ActionBtn
                            testId={`badge-edit-${badge.id}`}
                            onClick={() => setEditBadge(badge)}
                          >
                            <PencilIcon />{strings.gamBadgeEditBtn}
                          </ActionBtn>
                          {badge.isActive ? (
                            <ActionBtn
                              testId={`badge-deactivate-${badge.id}`}
                              onClick={() => setDeactivateTarget(badge)}
                              danger
                            >
                              {strings.gamBadgeDeactivateBtn}
                            </ActionBtn>
                          ) : (
                            <ActionBtn
                              testId={`badge-activate-${badge.id}`}
                              onClick={() => setActivateTarget(badge)}
                            >
                              {strings.gamBadgeActivateBtn}
                            </ActionBtn>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </Stack>

      {/* Create/Edit modal */}
      <BadgeForm
        open={createOpen || !!editBadge}
        onClose={() => { setCreateOpen(false); setEditBadge(null); }}
        editBadge={editBadge ?? undefined}
      />

      {/* Deactivate confirm */}
      <AdminConfirmDialog
        open={!!deactivateTarget}
        variant="retire"
        title={strings.gamBadgeDeactivateTitle}
        subtitle={strings.gamBadgeDeactivateSubtitle.replace('{name}', deactivateTarget?.name ?? '')}
        cancelLabel={strings.gamCancelBtn}
        dialogTestId="badge-deactivate-dialog"
        onClose={() => setDeactivateTarget(null)}
        confirmButton={
          <button
            type="button"
            data-testid="badge-deactivate-confirm-btn"
            aria-disabled={setActiveMutation.isPending}
            onClick={() => deactivateTarget && handleSetActive(deactivateTarget, false)}
            style={{
              height: 40, paddingInline: 20, borderRadius: 'var(--lx-radius-button)',
              backgroundColor: '#F59E0B', border: 'none',
              color: '#0F172A', fontSize: 14, fontWeight: 600,
              cursor: setActiveMutation.isPending ? 'not-allowed' : 'pointer',
              fontFamily: 'inherit', opacity: setActiveMutation.isPending ? 0.6 : 1,
            }}
          >
            {setActiveMutation.isPending ? '…' : strings.gamBadgeDeactivateConfirmBtn}
          </button>
        }
      >
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {strings.gamBadgeDeactivateNote}
        </Text>
      </AdminConfirmDialog>

      {/* Activate confirm */}
      <AdminConfirmDialog
        open={!!activateTarget}
        variant="reactivate"
        title={strings.gamBadgeActivateTitle}
        subtitle={strings.gamBadgeActivateSubtitle.replace('{name}', activateTarget?.name ?? '')}
        cancelLabel={strings.gamCancelBtn}
        dialogTestId="badge-activate-dialog"
        onClose={() => setActivateTarget(null)}
        confirmButton={
          <button
            type="button"
            data-testid="badge-activate-confirm-btn"
            aria-disabled={setActiveMutation.isPending}
            onClick={() => activateTarget && handleSetActive(activateTarget, true)}
            style={{
              height: 40, paddingInline: 20, borderRadius: 'var(--lx-radius-button)',
              backgroundColor: '#22C55E', border: 'none',
              color: '#0F172A', fontSize: 14, fontWeight: 600,
              cursor: setActiveMutation.isPending ? 'not-allowed' : 'pointer',
              fontFamily: 'inherit', opacity: setActiveMutation.isPending ? 0.6 : 1,
            }}
          >
            {setActiveMutation.isPending ? '…' : strings.gamBadgeActivateConfirmBtn}
          </button>
        }
      >
        <Text fontFamily="$body" fontSize={14} color="$fg2">
          {strings.gamBadgeActivateNote}
        </Text>
      </AdminConfirmDialog>
    </AdminShell>
  );
}
