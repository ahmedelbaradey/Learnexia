'use client';

export const dynamic = 'force-dynamic';

/**
 * Gamification Hub — `/gamification` (P7-13-FE).
 *
 * Section landing: 3 catalog cards (Badges / Missions / Events) + student-overrides
 * notice directing admins to the Users surface.
 *
 * Guard: useAdminGuard → loading skeleton / redirect to /login / content.
 * Design Spec Part B.
 */

import Link from 'next/link';
import { Stack, Text } from '@tamagui/core';

import { AdminShell } from '../../../components/AdminShell';
import { getStrings, ADMIN_LOCALE } from '../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Lucide inline SVGs ────────────────────────────────────────────────────────

function AwardIcon({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="6" />
      <path d="M15.477 12.89 17 22l-5-3-5 3 1.523-9.11" />
    </svg>
  );
}

function TargetIcon({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <circle cx="12" cy="12" r="6" />
      <circle cx="12" cy="12" r="2" />
    </svg>
  );
}

function CalendarClockIcon({ size = 20 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M21 7.5V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3.5" />
      <path d="M16 2v4M8 2v4M3 10h5" />
      <circle cx="17" cy="17" r="5" />
      <path d="M17 14v3l2 2" />
    </svg>
  );
}

function InfoIcon({ size = 16 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true"
      stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="12" y1="16" x2="12" y2="12" />
      <line x1="12" y1="8" x2="12.01" y2="8" />
    </svg>
  );
}

// ── Hub card definition ───────────────────────────────────────────────────────

interface HubCardDef {
  icon: React.ReactNode;
  iconBg: string;
  iconColor: string;
  titleEn: string;
  titleAr: string;
  descEn: string;
  descAr: string;
  href: string;
  testId: string;
}

const HUB_CARDS: HubCardDef[] = [
  {
    icon: <AwardIcon />,
    iconBg: 'rgba(168,85,247,0.15)',
    iconColor: '#A855F7',
    titleEn: 'Badge Catalog',
    titleAr: 'كتالوج الشارات',
    descEn: 'Manage badge definitions, rarity, triggers, and XP rewards.',
    descAr: 'إدارة تعريفات الشارات والندرة والمُشغِّلات ومكافآت النقاط.',
    href: '/gamification/badges',
    testId: 'hub-card-badges',
  },
  {
    icon: <TargetIcon />,
    iconBg: 'rgba(79,70,229,0.15)',
    iconColor: '#4F46E5',
    titleEn: 'Mission Catalog',
    titleAr: 'كتالوج المهام',
    descEn: 'Manage daily and weekly mission definitions.',
    descAr: 'إدارة تعريفات المهام اليومية والأسبوعية.',
    href: '/gamification/missions',
    testId: 'hub-card-missions',
  },
  {
    icon: <CalendarClockIcon />,
    iconBg: 'rgba(245,158,11,0.15)',
    iconColor: '#F59E0B',
    titleEn: 'Timed Events',
    titleAr: 'الأحداث الزمنية',
    descEn: 'Schedule XP multiplier events, activate and expire.',
    descAr: 'جدولة أحداث مضاعفة النقاط وتفعيلها وإنهائها.',
    href: '/gamification/events',
    testId: 'hub-card-events',
  },
];

// ── Component ──────────────────────────────────────────────────────────────────

export default function GamificationHubPage() {
  const isAr = ADMIN_LOCALE === 'ar';

  return (
    <AdminShell title={strings.gamificationHubTitle}>
      <Stack flexDirection="column" gap="$6" data-testid="gamification-hub">
        {/* Page header */}
        <Stack flexDirection="row" alignItems="center" justifyContent="space-between">
          <Stack flexDirection="column" gap="$1">
            <Text
              fontFamily="$heading"
              fontSize={24}
              fontWeight="700"
              color="$fg1"
              data-testid="gam-hub-title"
            >
              {strings.gamificationHubTitle}
            </Text>
            <Text fontFamily="$body" fontSize={14} color="$fg3" style={{ marginTop: 4 }}>
              {strings.gamificationHubSubtitle}
            </Text>
          </Stack>
        </Stack>

        {/* Catalog cards grid */}
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(3, 1fr)',
            gap: 16,
          }}
        >
          {HUB_CARDS.map((card) => (
            <div
              key={card.href}
              data-testid={card.testId}
              style={{
                backgroundColor: 'var(--lx-card)',
                borderRadius: 20,
                border: '1px solid var(--lx-border)',
                padding: 20,
                display: 'flex',
                flexDirection: 'column',
                gap: 16,
                transition: 'border-color 120ms var(--lx-ease-out)',
              }}
              onMouseEnter={(e) => {
                (e.currentTarget as HTMLDivElement).style.borderColor = 'rgba(79,70,229,0.3)';
              }}
              onMouseLeave={(e) => {
                (e.currentTarget as HTMLDivElement).style.borderColor = 'var(--lx-border)';
              }}
            >
              {/* Icon circle */}
              <div style={{
                width: 40,
                height: 40,
                borderRadius: 9999,
                backgroundColor: card.iconBg,
                color: card.iconColor,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexShrink: 0,
              }}>
                {card.icon}
              </div>

              {/* Content */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
                <span style={{ fontSize: 16, fontWeight: 600, color: 'var(--lx-fg1)', fontFamily: 'inherit' }}>
                  {isAr ? card.titleAr : card.titleEn}
                </span>
                <span style={{ fontSize: 13, color: 'var(--lx-fg3)', lineHeight: 1.5 }}>
                  {isAr ? card.descAr : card.descEn}
                </span>
              </div>

              {/* Manage link */}
              <Link
                href={card.href}
                data-testid={`${card.testId}-manage`}
                style={{
                  height: 36,
                  paddingInline: 16,
                  borderRadius: 'var(--lx-radius-button)',
                  border: '1px solid var(--lx-border)',
                  backgroundColor: 'transparent',
                  color: 'var(--lx-fg2)',
                  fontSize: 13,
                  fontWeight: 500,
                  alignSelf: 'flex-start',
                  textDecoration: 'none',
                  display: 'inline-flex',
                  alignItems: 'center',
                  transition: 'background-color 120ms var(--lx-ease-out), color 120ms var(--lx-ease-out)',
                }}
                onMouseEnter={(e) => {
                  const el = e.currentTarget as HTMLAnchorElement;
                  el.style.backgroundColor = 'var(--lx-card-soft)';
                  el.style.color = 'var(--lx-fg1)';
                }}
                onMouseLeave={(e) => {
                  const el = e.currentTarget as HTMLAnchorElement;
                  el.style.backgroundColor = 'transparent';
                  el.style.color = 'var(--lx-fg2)';
                }}
              >
                {strings.gamificationHubManage}
              </Link>
            </div>
          ))}
        </div>

        {/* Student overrides section */}
        <Stack flexDirection="column" gap="$3">
          <div style={{ borderTop: '1px solid var(--lx-border)', marginTop: 8 }} />

          <Text
            fontFamily="$body"
            fontSize={14}
            fontWeight="600"
            color="$fg3"
            style={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}
          >
            {strings.gamificationStudentOverridesHeading}
          </Text>

          {/* Notice strip */}
          <div
            style={{
              padding: 12,
              backgroundColor: 'rgba(79,70,229,0.08)',
              borderRadius: 8,
              border: '1px solid rgba(79,70,229,0.15)',
              display: 'flex',
              flexDirection: 'row',
              gap: 8,
              alignItems: 'flex-start',
            }}
            data-testid="student-overrides-notice"
          >
            <span style={{ color: 'var(--lx-fg3)', display: 'inline-flex', flexShrink: 0, marginTop: 2 }}>
              <InfoIcon />
            </span>
            <Text fontFamily="$body" fontSize={13} color="$fg2" style={{ lineHeight: 1.5 }}>
              {strings.gamificationStudentOverridesNotice}
            </Text>
          </div>
        </Stack>
      </Stack>
    </AdminShell>
  );
}
