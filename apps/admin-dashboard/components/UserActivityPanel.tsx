'use client';

/**
 * UserActivityPanel — gamification activity summary for a single user (P7-06-FE-4).
 *
 * Each section (XP/Level, Streak, Badges, Missions, League) loads from
 * `AdminActivitySummaryDto`. Any null field → "No data" for that section.
 * Errors show a WARNING banner (not error — activity is best-effort) and
 * must NOT propagate to blank the profile card.
 *
 * XP/streak/badge numbers are always Latin numerals, `dir="ltr"`, `font-variant-numeric: tabular-nums`.
 * Design Spec Part C §C.5.
 * Acceptance criteria: P7-06 AC 6, 7.
 */

import { Stack, Text } from '@tamagui/core';

import { useUserActivity } from '@learnexia/api-client';
import { AdminErrorBanner } from './AdminErrorBanner';
import { getStrings, ADMIN_LOCALE } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

// ── Flame-icon substitute for streak (no emoji per design spec) ───────────────

function FlameIcon() {
  return (
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true" style={{ color: '#F59E0B' }}>
      <path
        d="M12 2C7 9 9 12 7 14c-1 1.5-1 3 .5 4.5a5 5 0 007 0C16 17 16 15 14 13c-1-1 0-4-2-11z"
        stroke="currentColor"
        strokeWidth="1.5"
        strokeLinejoin="round"
      />
    </svg>
  );
}

// ── Skeleton ───────────────────────────────────────────────────────────────────

function ActivitySkeleton() {
  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
        gap: 16,
      }}
    >
      {[1, 2, 3, 4, 5].map((i) => (
        <div
          key={i}
          role="presentation"
          style={{
            height: 80,
            borderRadius: 'var(--lx-radius-sm)',
            background:
              'linear-gradient(90deg, var(--lx-card-soft) 25%, var(--lx-card) 50%, var(--lx-card-soft) 75%)',
            backgroundSize: '400px 100%',
            animation: 'lx-shimmer 1400ms linear infinite',
          }}
        />
      ))}
    </div>
  );
}

// ── "No data" chip ─────────────────────────────────────────────────────────────

function NoDataChip() {
  return (
    <Text
      fontFamily="$body"
      fontSize={13}
      color="$fg3"
      style={{ fontStyle: 'italic' }}
    >
      {strings.userActivityNoData}
    </Text>
  );
}

// ── Stat card ─────────────────────────────────────────────────────────────────

interface StatCardProps {
  label: string;
  children: React.ReactNode;
}

function StatCard({ label, children }: StatCardProps) {
  return (
    <Stack
      flexDirection="column"
      gap="$2"
      padding="$4"
      borderRadius="$sm"
      style={{
        backgroundColor: 'var(--lx-bg)',
        border: '1px solid var(--lx-border)',
      }}
    >
      <Text
        fontFamily="$body"
        fontSize={11}
        fontWeight="600"
        color="$fg3"
        style={{ textTransform: 'uppercase', letterSpacing: '0.04em' }}
      >
        {label}
      </Text>
      {children}
    </Stack>
  );
}

// ── Number display (always Latin, dir ltr) ────────────────────────────────────

interface StatNumberProps {
  value: number | string;
  unit?: string;
}

function StatNumber({ value, unit }: StatNumberProps) {
  return (
    <Stack flexDirection="row" alignItems="baseline" gap="$2">
      <span
        dir="ltr"
        style={{
          fontSize: 20,
          fontWeight: 800,
          color: 'var(--lx-fg1)',
          fontVariantNumeric: 'tabular-nums',
          fontFamily: 'inherit',
        }}
      >
        {value}
      </span>
      {unit ? (
        <Text fontFamily="$body" fontSize={14} fontWeight="600" color="$primary">
          {unit}
        </Text>
      ) : null}
    </Stack>
  );
}

// ── Panel ──────────────────────────────────────────────────────────────────────

export interface UserActivityPanelProps {
  userId: number;
}

export function UserActivityPanel({ userId }: UserActivityPanelProps) {
  const { data, isLoading, isError, refetch } = useUserActivity(userId);

  return (
    <Stack
      flexDirection="column"
      gap="$4"
      padding="$6"
      borderRadius="$card"
      style={{
        backgroundColor: 'var(--lx-card)',
        border: '1px solid var(--lx-border)',
      }}
    >
      {/* Panel heading */}
      <Text
        fontFamily="$body"
        fontSize={14}
        fontWeight="600"
        color="$fg3"
        style={{ textTransform: 'uppercase', letterSpacing: '0.06em' }}
      >
        {strings.userActivityPanelHeading}
      </Text>

      {/* Sign-in note (always shown — lastSignInAtUtc is always null) */}
      <Stack
        flexDirection="row"
        alignItems="center"
        gap="$2"
        padding="$3"
        borderRadius="$sm"
        style={{
          backgroundColor: 'var(--lx-bg)',
          border: '1px solid var(--lx-border)',
        }}
      >
        <span aria-hidden="true" style={{ color: 'var(--lx-fg3)', display: 'flex' }}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
            <circle cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="1.5" />
            <path d="M12 8v4m0 4h.01" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" />
          </svg>
        </span>
        <Text fontFamily="$body" fontSize={14} color="$fg3">
          {strings.userActivitySignInNote}
        </Text>
      </Stack>

      {isLoading && <ActivitySkeleton />}

      {isError && (
        <Stack flexDirection="column" gap="$3">
          <AdminErrorBanner
            variant="warning"
            message={strings.userActivityNoData}
          />
          <button
            type="button"
            onClick={() => void refetch()}
            style={{
              alignSelf: 'flex-start',
              height: 32,
              paddingInline: 12,
              borderRadius: 'var(--lx-radius-button)',
              border: '1px solid var(--lx-border)',
              backgroundColor: 'transparent',
              color: 'var(--lx-fg2)',
              fontSize: 12,
              cursor: 'pointer',
              fontFamily: 'inherit',
            }}
          >
            {strings.usersListRetry}
          </button>
        </Stack>
      )}

      {!isLoading && !isError && data && (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
            gap: 16,
          }}
        >
          {/* XP / Level */}
          <StatCard label={strings.userActivityXp}>
            {data.xp ? (
              <StatNumber
                value={data.xp.totalXp}
                unit={`${strings.userActivityLevel} ${data.xp.currentLevel}`}
              />
            ) : (
              <NoDataChip />
            )}
          </StatCard>

          {/* Streak */}
          <StatCard label={strings.userActivityStreak}>
            {data.streak ? (
              <Stack flexDirection="column" gap="$1">
                <Stack flexDirection="row" alignItems="center" gap="$1">
                  <FlameIcon />
                  <span
                    dir="ltr"
                    style={{
                      fontSize: 20,
                      fontWeight: 800,
                      color: 'var(--lx-fg1)',
                      fontVariantNumeric: 'tabular-nums',
                      fontFamily: 'inherit',
                    }}
                  >
                    {data.streak.currentStreak}
                  </span>
                </Stack>
                <Text fontFamily="$body" fontSize={12} color="$fg3">
                  {strings.userActivityBestStreak}:{' '}
                  <span dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {data.streak.longestStreak}
                  </span>
                </Text>
              </Stack>
            ) : (
              <NoDataChip />
            )}
          </StatCard>

          {/* Badges */}
          <StatCard label={strings.userActivityBadges}>
            {data.badges ? (
              <StatNumber value={data.badges.totalCount} />
            ) : (
              <NoDataChip />
            )}
          </StatCard>

          {/* Missions */}
          <StatCard label={strings.userActivityMissions}>
            {data.missions ? (
              <span
                dir="ltr"
                style={{
                  fontSize: 20,
                  fontWeight: 800,
                  color: 'var(--lx-fg1)',
                  fontVariantNumeric: 'tabular-nums',
                  fontFamily: 'inherit',
                }}
              >
                {data.missions.completedDailyMissions} / {data.missions.dailyMissionCount}
              </span>
            ) : (
              <NoDataChip />
            )}
          </StatCard>

          {/* League */}
          <StatCard label={strings.userActivityLeague}>
            {data.league ? (
              <Stack flexDirection="column" gap="$1">
                <Text fontFamily="$body" fontSize={14} fontWeight="700" color="$fg1">
                  {data.league.tier}
                </Text>
                <Text fontFamily="$body" fontSize={12} color="$fg3">
                  {strings.userActivityLeagueRankOf
                    .replace('{N}', String(data.league.currentRank))
                    .replace('{M}', String(data.league.groupSize))}
                </Text>
                <Text fontFamily="$body" fontSize={12} color="$fg3">
                  {strings.userActivityWeeklyXp}:{' '}
                  <span dir="ltr" style={{ fontVariantNumeric: 'tabular-nums' }}>
                    {data.league.weeklyXp}
                  </span>
                </Text>
              </Stack>
            ) : (
              <NoDataChip />
            )}
          </StatCard>
        </div>
      )}
    </Stack>
  );
}
