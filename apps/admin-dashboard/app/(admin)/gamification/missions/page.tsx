'use client';

export const dynamic = 'force-dynamic';

/**
 * Mission Catalog — `/gamification/missions` (P7-13-FE).
 *
 * Four-state catalog table: loading skeleton / empty + Create CTA / error + retry / results.
 * CRUD: Create + Edit (MissionForm modal). Activate / Deactivate via PATCH + AdminConfirmDialog.
 * Design Spec Part D.
 */

import { useState } from 'react';
import { Stack, Text } from '@tamagui/core';
import Link from 'next/link';

import {
  useAdminMissions,
  useSetMissionActive,
  type MissionDefinitionDto,
  type MissionTargetType,
} from '@learnexia/api-client';
import { isApiError } from '@learnexia/api-client/client';

import { AdminShell } from '../../../../components/AdminShell';
import { AdminErrorBanner } from '../../../../components/AdminErrorBanner';
import { AdminConfirmDialog } from '../../../../components/AdminConfirmDialog';
import { MissionForm } from '../../../../components/gamification/MissionForm';
import { getStrings, ADMIN_LOCALE } from '../../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);
const isAr = ADMIN_LOCALE === 'ar';

// ── Label maps ────────────────────────────────────────────────────────────────

const CADENCE_LABELS: Record<number, { en: string; ar: string }> = {
  1: { en: 'Daily', ar: 'يومية' },
  2: { en: 'Weekly', ar: 'أسبوعية' },
};

const TARGET_TYPE_LABELS: Record<MissionTargetType, { en: string; ar: string }> = {
  1: { en: 'Complete Lessons', ar: 'إكمال دروس' },
  2: { en: 'Correct Answers', ar: 'إجابات صحيحة' },
  3: { en: 'Maintain Streak', ar: 'الحفاظ على السلسلة' },
};

function cadenceLabel(c: number): string {
  return isAr ? (CADENCE_LABELS[c]?.ar ?? String(c)) : (CADENCE_LABELS[c]?.en ?? String(c));
}
function targetTypeLabel(t: MissionTargetType): string {
  return (isAr ? TARGET_TYPE_LABELS[t]?.ar : TARGET_TYPE_LABELS[t]?.en) ?? String(t);
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

function TargetIcon() {
  return (
    <svg width="32" height="32" viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" /><circle cx="12" cy="12" r="6" /><circle cx="12" cy="12" r="2" />
    </svg>
  );
}

// ── Active badge ──────────────────────────────────────────────────────────────

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
      {isActive ? strings.gamMissionActive : strings.gamMissionInactive}
    </span>
  );
}

// ── Row action btn ────────────────────────────────────────────────────────────

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

// ── Skeleton row ──────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <tr>
      {Array.from({ length: 7 }).map((_, i) => (
        <td key={i} style={{ padding: '14px 16px' }}>
          <div style={{ height: 14, borderRadius: 6, backgroundColor: 'var(--lx-card-soft)', width: i === 0 ? 140 : 80 }} />
        </td>
      ))}
    </tr>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export default function MissionCatalogPage() {
  const { data: missions, isLoading, isError, error, refetch, isFetching } = useAdminMissions();

  const [createOpen, setCreateOpen] = useState(false);
  const [editMission, setEditMission] = useState<MissionDefinitionDto | null>(null);
  const [deactivateTarget, setDeactivateTarget] = useState<MissionDefinitionDto | null>(null);
  const [activateTarget, setActivateTarget] = useState<MissionDefinitionDto | null>(null);
  const [successBanner, setSuccessBanner] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const setActiveMutation = useSetMissionActive();

  const handleSetActive = async (mission: MissionDefinitionDto, isActive: boolean) => {
    setActionError(null);
    try {
      await setActiveMutation.mutateAsync({ id: mission.id, isActive });
      setSuccessBanner(isActive ? strings.gamMissionActivatedBanner : strings.gamMissionDeactivatedBanner);
      setDeactivateTarget(null);
      setActivateTarget(null);
    } catch (err) {
      if (isApiError(err) && err.status === 404) {
        setActionError(strings.gamMissionNotFoundError);
      } else {
        setActionError(strings.gamMissionActionError);
      }
      setDeactivateTarget(null);
      setActivateTarget(null);
    }
  };

  return (
    <AdminShell title={strings.gamMissionPageTitle}>
      <Stack flexDirection="column" gap="$5" data-testid="mission-catalog-page">
        {/* Header row */}
        <Stack flexDirection="row" alignItems="center" justifyContent="space-between" flexWrap="wrap" gap="$3">
          <Stack flexDirection="column" gap="$1">
            <Stack flexDirection="row" alignItems="center" gap="$2">
              <Link href="/gamification" style={{ fontSize: 13, color: 'var(--lx-fg3)', textDecoration: 'none' }}>
                {strings.gamification}
              </Link>
              <span style={{ color: 'var(--lx-fg3)', fontSize: 13 }}>/</span>
              <span style={{ fontSize: 13, color: 'var(--lx-fg2)' }}>{strings.gamMissionPageTitle}</span>
            </Stack>
            <Text fontFamily="$heading" fontSize={22} fontWeight="700" color="$fg1">
              {strings.gamMissionPageTitle}
            </Text>
          </Stack>
          <button
            type="button" data-testid="mission-create-btn" onClick={() => setCreateOpen(true)}
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
            <PlusIcon />{strings.gamMissionCreateBtn}
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

        {/* Results region */}
        <div
          aria-live="polite" aria-busy={isFetching}
          data-testid="mission-results-region"
          style={{
            backgroundColor: 'var(--lx-card)', borderRadius: 20,
            border: '1px solid var(--lx-border)', overflow: 'hidden',
          }}
        >
          {isLoading && (
            <div role="status" aria-label={strings.gamMissionLoading}>
              <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <tbody>{Array.from({ length: 5 }).map((_, i) => <SkeletonRow key={i} />)}</tbody>
              </table>
            </div>
          )}
          {isError && !isLoading && (
            <div style={{ padding: 32, display: 'flex', flexDirection: 'column', gap: 12, alignItems: 'center' }}>
              <AdminErrorBanner variant="error" message={String((error as Error)?.message ?? strings.gamMissionFetchError)} />
              <button type="button" onClick={() => refetch()} data-testid="mission-retry-btn"
                style={{
                  height: 36, paddingInline: 16, borderRadius: 16,
                  border: '1px solid var(--lx-border)', backgroundColor: 'transparent',
                  color: 'var(--lx-fg2)', fontSize: 13, cursor: 'pointer', fontFamily: 'inherit',
                }}>
                {strings.gamRetry}
              </button>
            </div>
          )}
          {!isLoading && !isError && missions && missions.length === 0 && (
            <div style={{ padding: 48, display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 16 }}>
              <span style={{ color: 'var(--lx-fg3)' }}><TargetIcon /></span>
              <Text fontFamily="$body" fontSize={15} color="$fg3" style={{ textAlign: 'center' }}>
                {strings.gamMissionEmpty}
              </Text>
              <button type="button" onClick={() => setCreateOpen(true)} data-testid="mission-empty-create-btn"
                style={{
                  height: 36, paddingInline: 16, borderRadius: 16,
                  backgroundColor: '#4F46E5', border: 'none',
                  color: '#F8FAFC', fontSize: 13, fontWeight: 600, cursor: 'pointer', fontFamily: 'inherit',
                }}>
                {strings.gamMissionCreateFirst}
              </button>
            </div>
          )}
          {!isLoading && !isError && missions && missions.length > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }} data-testid="mission-table">
                <caption className="sr-only">{strings.gamMissionTableCaption}</caption>
                <thead>
                  <tr style={{ borderBottom: '1px solid var(--lx-border)' }}>
                    {[
                      strings.gamMissionColTitle,
                      strings.gamMissionColType,
                      strings.gamMissionColTargetType,
                      strings.gamMissionColTargetCount,
                      strings.gamMissionColXp,
                      strings.gamMissionColStatus,
                      strings.gamMissionColActions,
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
                  {missions.map((mission, idx) => (
                    <tr
                      key={mission.id}
                      data-testid={`mission-row-${mission.id}`}
                      style={{ borderBottom: idx < missions.length - 1 ? '1px solid var(--lx-border)' : 'none' }}
                      onMouseEnter={(e) => { (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'var(--lx-card-soft)'; }}
                      onMouseLeave={(e) => { (e.currentTarget as HTMLTableRowElement).style.backgroundColor = 'transparent'; }}
                    >
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg1)', fontWeight: 500, maxWidth: 200 }}>
                        <span style={{ display: '-webkit-box', WebkitLineClamp: 2, WebkitBoxOrient: 'vertical', overflow: 'hidden' }}>
                          {mission.titleKey}
                        </span>
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)' }}>
                        {cadenceLabel(mission.cadence)}
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)' }}>
                        {targetTypeLabel(mission.targetType)}
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)', fontVariantNumeric: 'tabular-nums' }} dir="ltr">
                        {mission.target}
                      </td>
                      <td style={{ padding: '14px 16px', color: 'var(--lx-fg2)', fontVariantNumeric: 'tabular-nums' }} dir="ltr">
                        {mission.rewardXp}
                      </td>
                      <td style={{ padding: '14px 16px' }}>
                        <ActiveBadge isActive={mission.isActive} />
                      </td>
                      <td style={{ padding: '14px 16px' }}>
                        <div style={{ display: 'flex', gap: 6 }}>
                          <ActionBtn testId={`mission-edit-${mission.id}`} onClick={() => setEditMission(mission)}>
                            <PencilIcon />{strings.gamMissionEditBtn}
                          </ActionBtn>
                          {mission.isActive ? (
                            <ActionBtn testId={`mission-deactivate-${mission.id}`} onClick={() => setDeactivateTarget(mission)} danger>
                              {strings.gamMissionDeactivateBtn}
                            </ActionBtn>
                          ) : (
                            <ActionBtn testId={`mission-activate-${mission.id}`} onClick={() => setActivateTarget(mission)}>
                              {strings.gamMissionActivateBtn}
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
      <MissionForm
        open={createOpen || !!editMission}
        onClose={() => { setCreateOpen(false); setEditMission(null); }}
        editMission={editMission ?? undefined}
      />

      {/* Deactivate confirm */}
      <AdminConfirmDialog
        open={!!deactivateTarget}
        variant="retire"
        title={strings.gamMissionDeactivateTitle}
        subtitle={strings.gamMissionDeactivateSubtitle.replace('{name}', deactivateTarget?.titleKey ?? '')}
        cancelLabel={strings.gamCancelBtn}
        dialogTestId="mission-deactivate-dialog"
        onClose={() => setDeactivateTarget(null)}
        confirmButton={
          <button
            type="button" data-testid="mission-deactivate-confirm-btn"
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
            {setActiveMutation.isPending ? '…' : strings.gamMissionDeactivateConfirmBtn}
          </button>
        }
      >
        <Text fontFamily="$body" fontSize={14} color="$fg2">{strings.gamMissionDeactivateNote}</Text>
      </AdminConfirmDialog>

      {/* Activate confirm */}
      <AdminConfirmDialog
        open={!!activateTarget}
        variant="reactivate"
        title={strings.gamMissionActivateTitle}
        subtitle={strings.gamMissionActivateSubtitle.replace('{name}', activateTarget?.titleKey ?? '')}
        cancelLabel={strings.gamCancelBtn}
        dialogTestId="mission-activate-dialog"
        onClose={() => setActivateTarget(null)}
        confirmButton={
          <button
            type="button" data-testid="mission-activate-confirm-btn"
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
            {setActiveMutation.isPending ? '…' : strings.gamMissionActivateConfirmBtn}
          </button>
        }
      >
        <Text fontFamily="$body" fontSize={14} color="$fg2">{strings.gamMissionActivateNote}</Text>
      </AdminConfirmDialog>
    </AdminShell>
  );
}
