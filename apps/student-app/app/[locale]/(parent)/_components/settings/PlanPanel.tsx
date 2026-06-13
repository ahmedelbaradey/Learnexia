/**
 * PlanPanel — Settings → Plan & billing tab (P2-12d).
 *
 * Renders the parent's current plan (read-only stub: Free / Active from BE)
 * with a status Badge, body copy, and a **disabled** Manage Subscription CTA.
 * No payment integration exists in W10 — see TODO(P2-12-PAYMENTS) below.
 *
 * Layout / tokens: mirrors the Profile panel's PanelSurface grammar.
 * RTL: `flexDirection={rowDir}` on all flex rows; Arabic fonts via $heading/$body.
 * Tokens only — no raw hex / px beyond the `rgba(255,255,255,0.06)` border that
 * already lives in the shared PanelSurface (sibling-consistent).
 */
import { useMyPlan } from '@learnexia/api-client';
import { Button } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React from 'react';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '@/components/ServerErrorBanner';
import { useServerError } from '@/hooks/useServerError';

interface PlanPanelProps {
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
}

/**
 * Map from BE plan-name string to the i18n key suffix for display.
 * The BE returns Latin strings ("Free", "Pro", …); we localize via
 * `parent.settings.billing.planName.*`. When the plan catalog grows,
 * add new keys to `packages/shared/src/i18n/resources.ts` (ar + en).
 * Tracked under DG-W10-04.
 */
const PLAN_NAME_KEY: Record<string, string> = {
  Free: 'parent.settings.billing.planName.free',
  Pro: 'parent.settings.billing.planName.pro',
};

function PanelSurface({ children }: { children: React.ReactNode }) {
  return (
    <Stack
      flexDirection="column"
      gap={18}
      borderRadius="$modal"
      backgroundColor="$card"
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      padding={22}
    >
      {children}
    </Stack>
  );
}

function PanelHeader({
  title,
  subtitle,
  direction,
}: {
  title: string;
  subtitle: string;
  direction: 'ltr' | 'rtl';
}) {
  return (
    <Stack flexDirection="column" gap="$1">
      <Text
        color="$fg1"
        fontSize={16}
        fontWeight="800"
        fontFamily="$heading"
        writingDirection={direction}
      >
        {title}
      </Text>
      <Text color="$fg3" fontSize={12} fontFamily="$body" writingDirection={direction}>
        {subtitle}
      </Text>
    </Stack>
  );
}

export function PlanPanel({ direction, rowDir }: PlanPanelProps) {
  const { t } = useTranslation();
  const planQuery = useMyPlan();
  const resolveError = useServerError();

  const serverMessage = planQuery.isError
    ? resolveError(planQuery.error, {})
    : null;

  const plan = planQuery.data;
  const isActive = plan?.status === 'Active';

  // Localized plan-name display: look up the i18n key; fall back to the raw
  // BE string if the plan name is not in the map (future-proofs new plans).
  const planNameKey = plan?.planName ? (PLAN_NAME_KEY[plan.planName] ?? null) : null;
  const displayPlanName = planNameKey ? t(planNameKey) : (plan?.planName ?? '');

  if (planQuery.isPending) {
    return (
      <PanelSurface>
        <PanelHeader
          title={t('parent.settings.billing.title')}
          subtitle={t('parent.settings.billing.subtitle')}
          direction={direction}
        />
        <Stack alignItems="center" paddingVertical="$8">
          <Text color="$fg3" fontSize={14} fontFamily="$body" writingDirection={direction}>
            {t('common.loading')}
          </Text>
        </Stack>
      </PanelSurface>
    );
  }

  return (
    <PanelSurface>
      <PanelHeader
        title={t('parent.settings.billing.title')}
        subtitle={t('parent.settings.billing.subtitle')}
        direction={direction}
      />

      <ServerErrorBanner message={serverMessage} direction={direction} />

      {plan ? (
        <Stack flexDirection="column" gap={18}>
          {/* Plan name + status pill */}
          <Stack
            flexDirection={rowDir}
            alignItems="center"
            gap={12}
            flexWrap="wrap"
          >
            <Text
              color="$fg3"
              fontSize={12}
              fontWeight="600"
              fontFamily="$body"
              // Eyebrow: uppercase for LTR/EN; full Arabic word (no uppercase) for RTL
              textTransform={direction === 'ltr' ? 'uppercase' : undefined}
              letterSpacing={direction === 'ltr' ? 0.04 : undefined}
              writingDirection={direction}
            >
              {t('parent.settings.billing.planLabel')}
            </Text>
            <Text
              color="$fg1"
              fontSize={22}
              fontWeight="800"
              fontFamily="$heading"
              writingDirection={direction}
            >
              {displayPlanName}
            </Text>
            {/* Status pill — inline, tokens only. Badge component only has
                achievement-disc variants; status pills are rendered directly
                per design spec tokens ($successSoft/$success or $cardSoft/$fg3). */}
            <Stack
              backgroundColor={isActive ? '$successSoft' : '$cardSoft'}
              borderRadius={9999}
              paddingHorizontal={10}
              paddingVertical={4}
              accessibilityLabel={t(
                isActive
                  ? 'parent.settings.billing.statusActive'
                  : 'parent.settings.billing.statusInactive',
              )}
            >
              <Text
                color={isActive ? '$success' : '$fg3'}
                fontSize={12}
                fontWeight="600"
                fontFamily="$body"
              >
                {t(
                  isActive
                    ? 'parent.settings.billing.statusActive'
                    : 'parent.settings.billing.statusInactive',
                )}
              </Text>
            </Stack>
          </Stack>

          {/* Body copy */}
          <Text
            color="$fg2"
            fontSize={14}
            fontFamily="$body"
            lineHeight={22}
            maxWidth={480}
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.settings.billing.upgradeComingSoon')}
          </Text>

          {/* CTA row */}
          <Stack flexDirection={rowDir} alignItems="center" gap={10} paddingTop={6}>
            {/* TODO(P2-12-PAYMENTS): wire to checkout once the payments BE ships. */}
            <Button
              variant="primary"
              size="md"
              disabled
              accessibilityLabel={t('parent.settings.billing.manage')}
              accessibilityState={{ disabled: true }}
            >
              {t('parent.settings.billing.manage')}
            </Button>
            <Text
              color="$fg4"
              fontSize={12}
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {t('parent.settings.billing.managePending')}
            </Text>
          </Stack>
        </Stack>
      ) : null}
    </PanelSurface>
  );
}

PlanPanel.displayName = 'PlanPanel';
