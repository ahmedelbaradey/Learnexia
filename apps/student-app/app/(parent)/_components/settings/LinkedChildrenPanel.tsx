/**
 * LinkedChildrenPanel — Settings → Linked children tab (P2-12b).
 *
 * Renders a ChildCard per linked child. Per-child:
 *  - Edit CTA → inline expandable form (fullName, grade, language, country).
 *    Grade/language/country default to empty — LinkedChildResponse lacks these
 *    fields (Q L-1 carry-forward; no GET /api/Parent/Child/{id} exists yet).
 *  - Unlink CTA → inline confirm strip (NOT a dialog per design spec §2.2).
 *
 * Add-child CTA at top-right navigates to /(onboarding)/add-child (existing route).
 * Empty state shown when no children are linked.
 *
 * AC: L1 — useMyChildren hook.
 *     L2 — fullName + email meta line.
 *     L3 — inline Edit form → useUpdateChild (PUT).
 *     L4 — inline Unlink strip → useUnlinkChild (DELETE); 400 = last-parent guard.
 *     L5 — Add child CTA routes to add-child flow.
 *     L6 — empty state.
 *
 * RTL: flexDirection={rowDir} on all rows; logical borderStartWidth on the unlink
 *      strip; ChildCard already handles its own RTL.
 * Tokens only — no raw hex (rgba border is sibling-consistent).
 */
import {
  useMyChildren,
  useUpdateChild,
  useUnlinkChild,
  type LinkedChildResponse,
  type UpdateChildCommand,
} from '@learnexia/api-client';
import { Button, ChildCard, Select, TextField } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '../../../../src/components/ServerErrorBanner';
import { useLocale } from '../../../../src/hooks/useLocale';
import { useServerError } from '../../../../src/hooks/useServerError';
import { COUNTRIES, type CountryCode } from '@learnexia/shared';

interface LinkedChildrenPanelProps {
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
}

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

// Grade options 1..6 — labels resolved via t() in the component
const GRADE_VALUES = [1, 2, 3, 4, 5, 6] as const;

interface EditFormProps {
  child: LinkedChildResponse;
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
  locale: string;
  onCancel: () => void;
  onSuccess: () => void;
}

function InlineEditForm({ child, direction, rowDir, locale, onCancel, onSuccess }: EditFormProps) {
  const { t } = useTranslation();
  const resolveError = useServerError();
  const updateChild = useUpdateChild();

  const [fullName, setFullName] = useState(child.fullName ?? '');
  const [grade, setGrade] = useState<number | null>(null);
  const [language, setLanguage] = useState<string | null>(null);
  const [country, setCountry] = useState<CountryCode | null>(null);

  const countryOptions = COUNTRIES.map((c) => ({
    value: c.code,
    label: locale === 'ar' ? c.ar : c.en,
  }));

  const gradeOptions = GRADE_VALUES.map((g) => ({
    value: g,
    label: t(`parent.settings.linkedChildren.gradeOption.${g}`),
  }));

  const languageOptions = [
    { value: 'ar', label: t('onboarding.language.ar') },
    { value: 'en', label: t('onboarding.language.en') },
  ];

  const isValid = fullName.trim().length > 0 && grade !== null && language !== null && country !== null;

  const serverMessage = updateChild.isError
    ? resolveError(updateChild.error, {
        byStatus: { 400: 'parent.settings.linkedChildren.updateError' },
      })
    : null;

  function handleSubmit() {
    if (!isValid || !child.id) return;
    const cmd: UpdateChildCommand = {
      childId: child.id,
      fullName: fullName.trim(),
      grade: grade!,
      language: language!,
      country: country!,
    };
    updateChild.mutate(cmd, {
      onSuccess: () => {
        onSuccess();
      },
    });
  }

  return (
    <Stack
      flexDirection="column"
      gap={14}
      padding={18}
      backgroundColor="$bgElevated"
      borderRadius={14}
      borderWidth={1}
      borderColor="rgba(255,255,255,0.06)"
      marginTop={-4}
    >
      {/* Row 1: full name + grade */}
      <Stack flexDirection={rowDir} gap={14} flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <TextField
            label={t('parent.settings.linkedChildren.fullName')}
            value={fullName}
            onChangeText={setFullName}
            direction={direction}
            disabled={updateChild.isPending}
            accessibilityLabel={t('parent.settings.linkedChildren.fullName')}
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          <Select
            label={t('parent.settings.linkedChildren.grade')}
            value={grade}
            onChange={(v) => setGrade(Number(v))}
            options={gradeOptions}
            placeholder={t('onboarding.addChild.gradePlaceholder')}
            direction={direction}
            disabled={updateChild.isPending}
            accessibilityLabel={t('parent.settings.linkedChildren.grade')}
          />
        </Stack>
      </Stack>

      {/* Row 2: language + country */}
      <Stack flexDirection={rowDir} gap={14} flexWrap="wrap">
        <Stack flex={1} minWidth={240}>
          <Select
            label={t('parent.settings.linkedChildren.language')}
            value={language}
            onChange={(v) => setLanguage(String(v))}
            options={languageOptions}
            placeholder={t('onboarding.addChild.languagePlaceholder')}
            direction={direction}
            disabled={updateChild.isPending}
            accessibilityLabel={t('parent.settings.linkedChildren.language')}
          />
        </Stack>
        <Stack flex={1} minWidth={240}>
          <Select
            label={t('parent.settings.linkedChildren.country')}
            value={country}
            onChange={(v) => setCountry(v as CountryCode)}
            options={countryOptions}
            placeholder={t('parent.settings.linkedChildren.countryPlaceholder')}
            direction={direction}
            disabled={updateChild.isPending}
            accessibilityLabel={t('parent.settings.linkedChildren.country')}
          />
        </Stack>
      </Stack>

      <ServerErrorBanner message={serverMessage} direction={direction} />

      {/* Action row */}
      <Stack flexDirection={rowDir} justifyContent="flex-end" gap={10} paddingTop={6}>
        <Button
          variant="ghost"
          size="md"
          onPress={onCancel}
          disabled={updateChild.isPending}
          accessibilityLabel={t('common.cancel')}
        >
          {t('common.cancel')}
        </Button>
        <Button
          variant="primary"
          size="md"
          loading={updateChild.isPending}
          disabled={!isValid || updateChild.isPending}
          accessibilityLabel={t('common.done')}
          onPress={handleSubmit}
        >
          {t('common.done')}
        </Button>
      </Stack>
    </Stack>
  );
}

interface UnlinkStripProps {
  child: LinkedChildResponse;
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
  onCancel: () => void;
  onSuccess: () => void;
}

function InlineUnlinkStrip({ child, direction, rowDir, onCancel, onSuccess }: UnlinkStripProps) {
  const { t } = useTranslation();
  const resolveError = useServerError();
  const unlinkChild = useUnlinkChild();

  const serverMessage = unlinkChild.isError
    ? resolveError(unlinkChild.error, {
        byStatus: { 400: 'parent.settings.linkedChildren.unlinkLastParentError' },
      })
    : null;

  function handleConfirm() {
    if (!child.id) return;
    unlinkChild.mutate(
      { childId: child.id },
      {
        onSuccess: () => {
          onSuccess();
        },
      },
    );
  }

  return (
    <Stack
      flexDirection="column"
      gap={10}
      padding={14}
      backgroundColor="$dangerSoft"
      borderRadius={14}
      borderStartWidth={3}
      borderStartColor="$danger"
      marginTop={-4}
    >
      <Stack flexDirection={rowDir} alignItems="center" gap={12}>
        <Text
          flex={1}
          color="$fg1"
          fontSize={13}
          fontFamily="$body"
          writingDirection={direction}
          textAlign={direction === 'rtl' ? 'right' : 'left'}
        >
          {t('parent.settings.linkedChildren.unlinkConfirmBody')}
        </Text>
        <Stack flexDirection={rowDir} gap={8}>
          <Button
            variant="ghost"
            size="sm"
            onPress={onCancel}
            disabled={unlinkChild.isPending}
            accessibilityLabel={t('common.cancel')}
          >
            {t('common.cancel')}
          </Button>
          {/* Destructive unlink: ghost variant with $danger text color in label.
              No new Button variant — per design spec §2.2 / CLAUDE.md rule #8. */}
          <Stack
            minHeight={44}
            justifyContent="center"
            onPress={unlinkChild.isPending ? undefined : handleConfirm}
            cursor={unlinkChild.isPending ? 'not-allowed' : 'pointer'}
            opacity={unlinkChild.isPending ? 0.4 : 1}
            pressStyle={{ scale: 0.95 }}
            accessibilityRole="button"
            accessible
            accessibilityLabel={t('parent.settings.linkedChildren.unlinkConfirmTitle', {
              name: child.fullName,
            })}
          >
            <Text
              color="$danger"
              fontSize={14}
              fontWeight="600"
              fontFamily="$body"
            >
              {t('parent.settings.linkedChildren.unlink')}
            </Text>
          </Stack>
        </Stack>
      </Stack>

      {/* Last-parent guard error */}
      {serverMessage ? (
        <Text
          color="$danger"
          fontSize={12}
          fontFamily="$body"
          writingDirection={direction}
          textAlign={direction === 'rtl' ? 'right' : 'left'}
          accessibilityLiveRegion="polite"
        >
          {serverMessage}
        </Text>
      ) : null}
    </Stack>
  );
}

export function LinkedChildrenPanel({ direction, rowDir }: LinkedChildrenPanelProps) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const router = useRouter();

  const childrenQuery = useMyChildren();

  // Track which child has an open edit form / unlink strip.
  const [editingId, setEditingId] = useState<number | null>(null);
  const [unlinkingId, setUnlinkingId] = useState<number | null>(null);
  // Per-child success strip (edit success).
  const [editSuccessId, setEditSuccessId] = useState<number | null>(null);
  const [unlinkSuccessName, setUnlinkSuccessName] = useState<string | null>(null);

  const children = childrenQuery.data ?? [];

  function handleAddChild() {
    router.push('/(onboarding)/add-child' as never);
  }

  const AddChildCTA = (
    <Button
      variant="primary"
      size="sm"
      onPress={handleAddChild}
      accessibilityLabel={t('parent.settings.linkedChildren.addChild')}
    >
      {t('parent.settings.linkedChildren.addChild')}
    </Button>
  );

  if (childrenQuery.isPending) {
    return (
      <PanelSurface>
        <Stack flexDirection={rowDir} justifyContent="space-between" alignItems="center" gap={12}>
          <PanelHeader
            title={t('parent.settings.linkedChildren.title')}
            subtitle={t('parent.settings.linkedChildren.subtitle')}
            direction={direction}
          />
          {AddChildCTA}
        </Stack>
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
      {/* Header row with Add-child CTA */}
      <Stack flexDirection={rowDir} justifyContent="space-between" alignItems="center" gap={12}>
        <PanelHeader
          title={t('parent.settings.linkedChildren.title')}
          subtitle={t('parent.settings.linkedChildren.subtitle')}
          direction={direction}
        />
        {AddChildCTA}
      </Stack>

      {/* Unlink success notification */}
      {unlinkSuccessName ? (
        <Stack
          backgroundColor="$successSoft"
          borderRadius="$sm"
          padding={12}
          accessibilityLiveRegion="polite"
        >
          <Text
            color="$success"
            fontSize={14}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.settings.linkedChildren.unlinkSuccess', { name: unlinkSuccessName })}
          </Text>
        </Stack>
      ) : null}

      {/* Empty state */}
      {children.length === 0 ? (
        <Stack alignItems="center" gap={12} paddingVertical={32}>
          <Text fontSize={32} accessibilityElementsHidden>
            {'👨‍👩‍👦'}
          </Text>
          <Text
            color="$fg1"
            fontSize={16}
            fontWeight="800"
            fontFamily="$heading"
            textAlign="center"
            writingDirection={direction}
          >
            {t('parent.settings.linkedChildren.emptyTitle')}
          </Text>
          <Text
            color="$fg3"
            fontSize={13}
            fontFamily="$body"
            textAlign="center"
            maxWidth={320}
            writingDirection={direction}
          >
            {t('parent.settings.linkedChildren.emptyBody')}
          </Text>
          <Button
            variant="primary"
            size="md"
            onPress={handleAddChild}
            accessibilityLabel={t('parent.settings.linkedChildren.addChild')}
          >
            {t('parent.settings.linkedChildren.addChild')}
          </Button>
        </Stack>
      ) : (
        /* Child list */
        <Stack flexDirection="column" gap={12}>
          {children.map((child) => {
            const childId = child.id ?? 0;
            const isEditing = editingId === childId;
            const isUnlinking = unlinkingId === childId;
            const showEditSuccess = editSuccessId === childId;

            return (
              <Stack key={childId} flexDirection="column" gap={8}>
                <ChildCard
                  variant="editable"
                  child={{ fullName: child.fullName ?? '', meta: child.email }}
                  direction={direction}
                  accessibilityLabel={child.fullName ?? ''}
                  onEdit={() => {
                    setEditingId(isEditing ? null : childId);
                    setUnlinkingId(null);
                  }}
                  onRemove={() => {
                    setUnlinkingId(isUnlinking ? null : childId);
                    setEditingId(null);
                  }}
                />

                {/* Inline edit form */}
                {isEditing ? (
                  <InlineEditForm
                    child={child}
                    direction={direction}
                    rowDir={rowDir}
                    locale={locale}
                    onCancel={() => setEditingId(null)}
                    onSuccess={() => {
                      setEditingId(null);
                      setEditSuccessId(childId);
                      setTimeout(() => setEditSuccessId(null), 3000);
                    }}
                  />
                ) : null}

                {/* Edit success strip */}
                {showEditSuccess ? (
                  <Stack
                    backgroundColor="$successSoft"
                    borderRadius="$sm"
                    padding={12}
                    accessibilityLiveRegion="polite"
                  >
                    <Text
                      color="$success"
                      fontSize={14}
                      fontFamily="$body"
                      writingDirection={direction}
                      textAlign={direction === 'rtl' ? 'right' : 'left'}
                    >
                      {t('parent.settings.linkedChildren.updateSuccess')}
                    </Text>
                  </Stack>
                ) : null}

                {/* Inline unlink confirm strip */}
                {isUnlinking ? (
                  <InlineUnlinkStrip
                    child={child}
                    direction={direction}
                    rowDir={rowDir}
                    onCancel={() => setUnlinkingId(null)}
                    onSuccess={() => {
                      setUnlinkingId(null);
                      setUnlinkSuccessName(child.fullName ?? '');
                      setTimeout(() => setUnlinkSuccessName(null), 3000);
                    }}
                  />
                ) : null}
              </Stack>
            );
          })}
        </Stack>
      )}
    </PanelSurface>
  );
}

LinkedChildrenPanel.displayName = 'LinkedChildrenPanel';
