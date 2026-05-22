'use client';

/**
 * Dashboard landing body (Design Spec §4d) — static placeholder.
 * Uses the web-safe `Card` from `@learnexia/ui` (subpath import) + `@tamagui/core`
 * Text/YStack. No data fetched.
 */

import { Card } from '@learnexia/ui/components/Card';
import { Stack, Text } from '@tamagui/core';

import { ADMIN_LOCALE, getStrings } from '../../../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export function DashboardLanding() {
  return (
    <Stack flexDirection="column" gap="$6">
      <Text fontFamily="$heading" fontSize={24} fontWeight="700" color="$fg1">
        {strings.dashboardHeading}
      </Text>
      <Text fontFamily="$body" fontSize={14} color="$fg3">
        {strings.dashboardSubtext}
      </Text>
      <Card variant="soft" padding="$8" alignItems="center">
        <Text fontFamily="$body" fontSize={14} color="$fg3">
          {strings.dashboardPlaceholder}
        </Text>
      </Card>
    </Stack>
  );
}
