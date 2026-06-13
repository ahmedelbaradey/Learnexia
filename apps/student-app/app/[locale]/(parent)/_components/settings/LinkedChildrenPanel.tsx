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
  useChangeLearningLanguage,
  type LinkedChildResponse,
  type UpdateChildCommand,
  type ChangeLearningLanguageCommand,
} from '@learnexia/api-client';
import { Button, ChildCard, Select, TextField } from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from '@/components/ServerErrorBanner';
import { useLocale } from '@/hooks/useLocale';
import { useServerError } from '@/hooks/useServerError';
import { COUNTRIES, type CountryCode } from '@learnexia/shared';
import { ChangeLearningLanguageModal } from './ChangeLearningLanguageModal';

export interface LinkedChildrenPanelProps {
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
  /** Called when the user taps "Add child". Opens the AddChildModal overlay. */
  onAddChild?: () => void;
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

// ────────────────────────────────────────────────────────────────────────────
// LearningLanguageRow — per-child inline row (P8-04-FE-2).
//
// Shows the child's current learning language (from LinkedChildResponse) and a
// ghost "Change" button. Tapping Change opens a picker strip (select + CTA).
// The CTA opens ChangeLearningLanguageModal. No-op guard: Confirm disabled if
// selected == current. Success shows an in-panel success strip (auto-clears 4s).
// ────────────────────────────────────────────────────────────────────────────

interface LearningLanguageRowProps {
  child: LinkedChildResponse;
  direction: 'ltr' | 'rtl';
  rowDir: 'row' | 'row-reverse';
}

function LearningLanguageRow({ child, direction, rowDir }: LearningLanguageRowProps) {
  const { t } = useTranslation();

  const changeLearningLanguage = useChangeLearningLanguage();

  // Local UI state — never in Zustand (server data lives in TanStack Query).
  const [isPickerOpen, setIsPickerOpen] = useState(false);
  const [selectedLang, setSelectedLang] = useState<string | null>(null);
  const [modalVisible, setModalVisible] = useState(false);
  const [ackChecked, setAckChecked] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const currentLang = child.learningLanguage ?? null;

  const languageOptions = [
    { value: 'ar', label: t('onboarding.language.ar') },
    { value: 'en', label: t('onboarding.language.en') },
  ];

  // No-op guard: disable "Change Language" CTA if selection matches current.
  const isNoOp = selectedLang !== null && selectedLang === currentLang;

  function handleOpenPicker() {
    setIsPickerOpen(true);
    setSelectedLang(null);
    changeLearningLanguage.reset();
    setSuccessMessage(null);
  }

  function handleClosePicker() {
    setIsPickerOpen(false);
    setSelectedLang(null);
  }

  function handleOpenModal() {
    if (!selectedLang || isNoOp) return;
    setAckChecked(false);
    changeLearningLanguage.reset();
    setModalVisible(true);
  }

  function handleModalCancel() {
    setModalVisible(false);
    setAckChecked(false);
  }

  function handleConfirm() {
    if (!selectedLang || !child.id || !ackChecked) return;
    const cmd: ChangeLearningLanguageCommand = {
      childId: child.id,
      newLearningLanguage: selectedLang,
      confirmFreshStart: true, // only true when ack is ticked and Confirm pressed
    };
    changeLearningLanguage.mutate(cmd, {
      onSuccess: () => {
        setModalVisible(false);
        setIsPickerOpen(false);
        setAckChecked(false);
        setSelectedLang(null);
        const langLabel = t(`onboarding.language.${selectedLang}` as const);
        setSuccessMessage(
          t('parent.settings.linkedChildren.learningLanguage.success', {
            name: child.fullName ?? '',
            language: langLabel,
          }),
        );
        setTimeout(() => setSuccessMessage(null), 4000);
      },
    });
  }

  // Display label for current language.
  const currentLangLabel = currentLang
    ? t(`onboarding.language.${currentLang}` as const)
    : null;

  return (
    <Stack flexDirection="column" gap={6}>
      {/* Idle row — low-prominence strip */}
      <Stack
        flexDirection={rowDir}
        alignItems="center"
        justifyContent="space-between"
        gap={12}
        padding={14}
        backgroundColor="$bgElevated"
        borderRadius={14}
        borderWidth={1}
        borderColor="rgba(255,255,255,0.06)"
        marginTop={-4}
      >
        {/* Leading label block */}
        <Stack flexDirection="column" gap={2} flex={1}>
          <Text
            color="$fg2"
            fontSize={13}
            fontWeight="600"
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.settings.linkedChildren.learningLanguage.rowLabel')}
          </Text>
          {currentLangLabel ? (
            <Text
              color="$fg1"
              fontSize={14}
              fontWeight="700"
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
            >
              {currentLangLabel}
            </Text>
          ) : null}
        </Stack>

        {/* Ghost Change button */}
        <Stack
          minWidth={44}
          minHeight={44}
          alignItems="center"
          justifyContent="center"
          paddingHorizontal={12}
          onPress={handleOpenPicker}
          cursor="pointer"
          pressStyle={{ opacity: 0.7 }}
          accessibilityRole="button"
          accessible
          accessibilityLabel={t('parent.settings.linkedChildren.learningLanguage.change')}
          aria-label={t('parent.settings.linkedChildren.learningLanguage.change')}
        >
          <Text color="$fg3" fontSize={13} fontWeight="600" fontFamily="$body">
            {t('parent.settings.linkedChildren.learningLanguage.change')}
          </Text>
        </Stack>
      </Stack>

      {/* Inline picker strip — revealed on "Change" tap */}
      {isPickerOpen ? (
        <Stack
          flexDirection="column"
          gap={12}
          padding={14}
          backgroundColor="$bgElevated"
          borderRadius={14}
          borderWidth={1}
          borderColor="rgba(255,255,255,0.06)"
          marginTop={-4}
        >
          <Select
            label={t('parent.settings.linkedChildren.learningLanguage.pickerLabel')}
            value={selectedLang}
            onChange={(v) => setSelectedLang(String(v))}
            options={languageOptions}
            placeholder={t('onboarding.addChild.learningLanguagePlaceholder')}
            direction={direction}
            disabled={changeLearningLanguage.isPending}
            accessibilityLabel={t('parent.settings.linkedChildren.learningLanguage.pickerLabel')}
          />

          {/* Helper sits UNDER the picker (Design Spec §3) — muted $fg3. */}
          <Text
            color="$fg3"
            fontSize={12}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {t('parent.settings.linkedChildren.learningLanguage.pickerHelper')}
          </Text>

          {/* No-op hint */}
          {isNoOp ? (
            <Text
              color="$fg3"
              fontSize={12}
              fontFamily="$body"
              writingDirection={direction}
              textAlign={direction === 'rtl' ? 'right' : 'left'}
              accessibilityLiveRegion="polite"
            >
              {t('parent.settings.linkedChildren.learningLanguage.noChange')}
            </Text>
          ) : null}

          {/* Action row: Cancel + Change Language CTA */}
          <Stack flexDirection={rowDir} justifyContent="flex-end" gap={10}>
            <Button
              variant="ghost"
              size="sm"
              onPress={handleClosePicker}
              disabled={changeLearningLanguage.isPending}
              accessibilityLabel={t('common.cancel')}
            >
              {t('common.cancel')}
            </Button>
            <Button
              variant="primary"
              size="sm"
              onPress={handleOpenModal}
              disabled={!selectedLang || isNoOp || changeLearningLanguage.isPending}
              accessibilityLabel={t('parent.settings.linkedChildren.learningLanguage.changeCta')}
            >
              {t('parent.settings.linkedChildren.learningLanguage.changeCta')}
            </Button>
          </Stack>
        </Stack>
      ) : null}

      {/* Success strip (auto-clears) */}
      {successMessage ? (
        <Stack
          backgroundColor="$successSoft"
          borderRadius="$sm"
          padding={12}
          accessibilityLiveRegion="polite"
          marginTop={-4}
        >
          <Text
            color="$success"
            fontSize={14}
            fontFamily="$body"
            writingDirection={direction}
            textAlign={direction === 'rtl' ? 'right' : 'left'}
          >
            {successMessage}
          </Text>
        </Stack>
      ) : null}

      {/* Confirmation overlay */}
      {selectedLang ? (
        <ChangeLearningLanguageModal
          visible={modalVisible}
          childId={child.id ?? 0}
          childName={child.fullName ?? ''}
          currentLanguage={currentLang}
          selectedLanguage={selectedLang}
          ackChecked={ackChecked}
          onAckChange={setAckChecked}
          isPending={changeLearningLanguage.isPending}
          isError={changeLearningLanguage.isError}
          error={changeLearningLanguage.error}
          direction={direction}
          rowDir={rowDir}
          onCancel={handleModalCancel}
          onConfirm={handleConfirm}
        />
      ) : null}
    </Stack>
  );
}

export function LinkedChildrenPanel({ direction, rowDir, onAddChild }: LinkedChildrenPanelProps) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  // router kept for any future navigation needs (link-child, etc.)
  const _router = useRouter();

  const childrenQuery = useMyChildren();

  // Track which child has an open edit form / unlink strip.
  const [editingId, setEditingId] = useState<number | null>(null);
  const [unlinkingId, setUnlinkingId] = useState<number | null>(null);
  // Per-child success strip (edit success).
  const [editSuccessId, setEditSuccessId] = useState<number | null>(null);
  const [unlinkSuccessName, setUnlinkSuccessName] = useState<string | null>(null);

  const children = childrenQuery.data ?? [];

  // Open the Add-Child modal (dashboard context). Falls back to onboarding route
  // if no modal callback is provided (e.g. in a standalone context).
  function handleAddChild() {
    if (onAddChild) {
      onAddChild();
    } else {
      _router.push('/(onboarding)/add-child' as never);
    }
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
                  borderRadius="$cardInner"
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

                {/* Learning language row (P8-04-FE) */}
                <LearningLanguageRow
                  child={child}
                  direction={direction}
                  rowDir={rowDir}
                />
              </Stack>
            );
          })}
        </Stack>
      )}
    </PanelSurface>
  );
}

LinkedChildrenPanel.displayName = 'LinkedChildrenPanel';
