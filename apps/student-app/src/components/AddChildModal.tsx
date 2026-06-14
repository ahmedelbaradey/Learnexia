/**
 * AddChildModal — centered modal overlay for adding or editing a child from
 * the parent dashboard context (workstream E, parent-dashboard-uiux spec).
 *
 * Reuses the in-repo RN `Modal transparent` + `$overlay` scrim convention
 * (same as `ChangeLearningLanguageModal` / `EditChildSheet`) — NOT a new pattern.
 *
 * Two modes driven by whether `childId` is provided:
 *   - ADD mode  (childId absent): photo upload, all fields including email +
 *     password, submits via `useAddChild`.
 *   - EDIT mode (childId present): no photo upload, no email/password (those
 *     are not accepted by `updateChild` — security requirement); pre-fills from
 *     `initialValues`; submits via `useUpdateChild`.
 *
 * Anatomy (top → bottom):
 *   1. Header — mark + title/subtitle + ✕ close
 *   2. Body (scrolls): [add-only: photo upload] child name, [add-only: login
 *      email, password], grade tiles (6 plant-emoji, NEVER a <select>),
 *      app-language flag tiles (2 flags), learning-language Select
 *   3. Footer — Cancel (ghost) + personalized primary CTA
 *
 * RTL: card `direction` from locale; CTA arrow flips; email LTR-pinned;
 * grade numerals Eastern-Arabic; 📷 badge on logical-end corner.
 *
 * Tokens only (no raw hex except the few sanctioned modal-surface values per spec).
 * No new design pattern — mirrors the existing overlay convention.
 */
import {
  useAddChild,
  useUpdateChild,
  type AddChildCommand,
  type UpdateChildCommand,
} from '@learnexia/api-client';
import { LOCALES } from '@learnexia/shared';
import {
  Avatar,
  Button,
  PasswordStrengthMeter,
  PASSWORD_STRENGTH,
  TextField,
  LanguageSelect,
  type PasswordStrength,
} from '@learnexia/ui';
import { Stack, Text } from '@tamagui/core';
import React, { useEffect, useRef, useState } from 'react';
import { Animated, Modal, Platform, ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useTranslation } from 'react-i18next';

import { ServerErrorBanner } from './ServerErrorBanner';
import { useLocale } from '../hooks/useLocale';
import { useServerError } from '../hooks/useServerError';

/** Initial field values when opening the modal in EDIT mode. */
export interface EditChildInitialValues {
  fullName: string;
  grade: number;
  /** App-language code ('ar' | 'en'). */
  language: string;
  /** Learning-language code — shown but not editable in edit mode (update endpoint omits it). */
  learningLanguage?: string;
}

export interface AddChildModalProps {
  visible: boolean;
  onClose: () => void;
  /**
   * When provided the modal opens in EDIT mode: pre-fills fields from
   * `initialValues`, hides email/password, uses `useUpdateChild` on submit.
   * Absent = ADD mode (default).
   */
  childId?: number;
  initialValues?: EditChildInitialValues;
}

/** Grade options with plant-emoji per spec (order: 🌱🌿🌳🌲🍃🌴). */
const GRADE_PLANT: readonly string[] = ['🌱', '🌿', '🌳', '🌲', '🍃', '🌴'];

/** Fixed grade set (1–6). */
const GRADES = [1, 2, 3, 4, 5, 6] as const;

/** Fixed accepted avatar MIME types (client-side guard only). */
const AVATAR_ACCEPT = 'image/png,image/jpeg';
const AVATAR_MAX_BYTES = 5 * 1024 * 1024;

/** Derive a simple password strength from the password string. */
function derivePasswordStrength(password: string): PasswordStrength {
  if (!password) return PASSWORD_STRENGTH.Empty;
  let score = 0;
  if (password.length >= 8) score += 1;
  if (/[A-Z]/.test(password)) score += 1;
  if (/[0-9]/.test(password)) score += 1;
  if (/[^A-Za-z0-9]/.test(password)) score += 1;
  if (score <= 1) return PASSWORD_STRENGTH.Weak;
  if (score === 2) return PASSWORD_STRENGTH.Fair;
  if (score === 3) return PASSWORD_STRENGTH.Good;
  return PASSWORD_STRENGTH.Strong;
}

/** Maps the numeric strength score to its i18n label key (mirrors RegisterForm). */
const STRENGTH_LABEL_KEY: Record<Exclude<PasswordStrength, 0>, string> = {
  1: 'auth.register.strength.weak',
  2: 'auth.register.strength.fair',
  3: 'auth.register.strength.good',
  4: 'auth.register.strength.strong',
};

function formatGradeNumber(grade: number, locale: string): string {
  return new Intl.NumberFormat(locale === 'ar' ? 'ar-EG' : 'en-US').format(grade);
}

export function AddChildModal({ visible, onClose, childId, initialValues }: AddChildModalProps) {
  const { t } = useTranslation();
  const { direction, isRtl, locale } = useLocale();
  const insets = useSafeAreaInsets();
  const addChild = useAddChild();
  const updateChild = useUpdateChild();
  const resolveError = useServerError();

  /** true when `childId` is provided (edit mode); false = add mode. */
  const isEditMode = childId !== undefined;

  const rowDir = isRtl ? 'row-reverse' : 'row';

  // Card pop-in animation (scale 0.92→1, ~280ms, spring overshoot).
  // Mirrors AttemptSummaryCard entrance — RN Animated, useNativeDriver.
  // The Modal animationType="fade" handles the scrim; this adds the card scale.
  const cardScale = useRef(new Animated.Value(0.92)).current;
  useEffect(() => {
    if (visible) {
      cardScale.setValue(0.92);
      Animated.spring(cardScale, {
        toValue: 1,
        damping: 12,
        stiffness: 180,
        useNativeDriver: true,
      }).start();
    }
  }, [visible, cardScale]);

  // Form state — seeded from initialValues when in edit mode.
  const [fullName, setFullName] = useState(initialValues?.fullName ?? '');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [grade, setGrade] = useState<number | null>(initialValues?.grade ?? null);
  const [appLanguage, setAppLanguage] = useState<string | null>(initialValues?.language ?? null);
  const [learningLanguage, setLearningLanguage] = useState<string | null>(
    initialValues?.learningLanguage ?? null,
  );

  // Re-seed form when initialValues change (modal re-opens for a different child).
  useEffect(() => {
    if (visible && isEditMode && initialValues) {
      setFullName(initialValues.fullName);
      setGrade(initialValues.grade);
      setAppLanguage(initialValues.language);
      setLearningLanguage(initialValues.learningLanguage ?? null);
    }
  }, [visible, isEditMode, initialValues]);

  // Photo state (add mode only — update endpoint does not accept avatar).
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [photoPreviewUri, setPhotoPreviewUri] = useState<string | null>(null);
  // photoFile is set on pick/reset and will be included in AddChildCommand once BE supports avatar upload.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [photoError, setPhotoError] = useState<string | null>(null);

  // Validation errors
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const passwordStrength = derivePasswordStrength(password);
  const strengthLabel = passwordStrength > 0
    ? t(STRENGTH_LABEL_KEY[passwordStrength as Exclude<PasswordStrength, 0>])
    : '';

  const languageOptions = LOCALES.map((loc) => ({
    value: loc,
    label: loc === 'ar' ? t('common.prefs.switchToArabic') : t('common.prefs.switchToEnglish'),
  }));

  const activeHook = isEditMode ? updateChild : addChild;

  const serverMessage = activeHook.isError
    ? resolveError(activeHook.error, {
        byStatus: { 400: 'parent.addChildModal.errors.generic' },
      })
    : null;

  function handlePhotoPress() {
    if (Platform.OS === 'web' && fileInputRef.current) {
      fileInputRef.current.value = '';
      fileInputRef.current.click();
    }
  }

  function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    if (!AVATAR_ACCEPT.split(',').includes(file.type)) {
      setPhotoError(t('parent.settings.profile.avatar.wrongType'));
      return;
    }
    if (file.size > AVATAR_MAX_BYTES) {
      setPhotoError(t('parent.settings.profile.avatar.tooLarge'));
      return;
    }
    setPhotoError(null);
    setPhotoFile(file);
    if (typeof URL !== 'undefined' && URL.createObjectURL) {
      setPhotoPreviewUri(URL.createObjectURL(file));
    }
  }

  function validate(): boolean {
    const errors: Record<string, string> = {};
    if (!fullName.trim()) errors.fullName = t('parent.addChildModal.errors.nameRequired');
    if (!isEditMode) {
      if (!email.trim()) errors.email = t('parent.addChildModal.errors.emailRequired');
      if (!password.trim()) errors.password = t('parent.addChildModal.errors.passwordRequired');
    }
    if (grade === null) errors.grade = t('parent.addChildModal.errors.gradeRequired');
    if (!appLanguage) errors.appLanguage = t('parent.addChildModal.errors.languageRequired');
    if (!isEditMode && !learningLanguage) {
      errors.learningLanguage = t('parent.addChildModal.errors.learningLanguageRequired');
    }
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  }

  function resetForm() {
    setFullName('');
    setEmail('');
    setPassword('');
    setGrade(null);
    setAppLanguage(null);
    setLearningLanguage(null);
    setPhotoPreviewUri(null);
    setPhotoFile(null);
    setPhotoError(null);
    setFieldErrors({});
    addChild.reset();
    updateChild.reset();
  }

  function handleClose() {
    resetForm();
    onClose();
  }

  async function handleSubmit() {
    if (!validate()) return;
    try {
      if (isEditMode) {
        const cmd: UpdateChildCommand = {
          childId: childId!,
          fullName: fullName.trim(),
          grade: grade!,
          language: appLanguage!,
        };
        await updateChild.mutateAsync(cmd);
      } else {
        const cmd: AddChildCommand = {
          fullName: fullName.trim(),
          email: email.trim(),
          password: password.trim(),
          grade: grade!,
          language: appLanguage!,
          learningLanguage: learningLanguage!,
        };
        await addChild.mutateAsync(cmd);
      }
      resetForm();
      onClose();
    } catch {
      // Error shown via serverMessage.
    }
  }

  // CTA label — edit mode reuses `onboarding.saveChanges` (already translated
  // in both locales); add mode is personalized with the child's name.
  const ctaLabel = isEditMode
    ? t('onboarding.saveChanges')
    : fullName.trim()
      ? t('parent.addChildModal.ctaWithName', { name: fullName.trim() })
      : t('parent.addChildModal.ctaDefault');

  // Title and subtitle differ by mode.
  // Edit: reuses `onboarding.editChild` (both locales) + settings subtitle.
  // Add: uses addChildModal.title / .subtitle.
  const modalTitle = isEditMode
    ? t('onboarding.editChild')
    : t('parent.addChildModal.title');
  const modalSubtitle = isEditMode
    ? t('parent.settings.linkedChildren.subtitle')
    : t('parent.addChildModal.subtitle');

  const isPending = isEditMode ? updateChild.isPending : addChild.isPending;

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={handleClose}
      statusBarTranslucent
    >
      {/* Scrim */}
      <Stack
        flex={1}
        alignItems="center"
        justifyContent="center"
        style={{ backgroundColor: 'rgba(5,8,22,0.7)', backdropFilter: 'blur(4px)' }}
        onPress={handleClose}
        accessibilityViewIsModal
        paddingHorizontal={16}
        paddingVertical={insets.top + 16}
      >
        {/* Modal card — stop propagation of press to scrim; pop-in scale animation */}
        <Animated.View style={{ transform: [{ scale: cardScale }] }}>
        <Stack
          testID="add-child-modal"
          width={480}
          style={{
            backgroundColor: '#15161D',
            borderRadius: 24,
            borderWidth: 1,
            borderColor: 'rgba(255,255,255,0.08)',
            boxShadow: '0 24px 64px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.06)',
            overflow: 'hidden',
            // Web: constrain to 92vw so modal doesn't overflow on narrow viewports.
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            ...(Platform.OS === 'web' ? { maxWidth: '92vw' as any } : { maxWidth: '100%' }),
          }}
          onPress={(e) => e.stopPropagation?.()}
          accessible
        >
          {/* Header */}
          <Stack
            flexDirection={rowDir}
            alignItems="center"
            gap="$3"
            padding={22}
            paddingBottom={16}
            borderBottomWidth={1}
            borderBottomColor="rgba(255,255,255,0.05)"
          >
            {/* Mark */}
            <Stack
              width={44}
              height={44}
              borderRadius="$cardInner"
              alignItems="center"
              justifyContent="center"
              style={{
                background: 'linear-gradient(135deg,#A855F7,#6366F1)',
                boxShadow: '0 6px 16px rgba(99,102,241,0.4)',
              }}
            >
              <Text fontSize={20} accessibilityElementsHidden>{isEditMode ? '✏️' : '👶'}</Text>
            </Stack>

            {/* Title block */}
            <Stack flexDirection="column" flex={1} gap="$0">
              <Text
                color="$fg1"
                fontSize={18}
                fontWeight="800"
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {modalTitle}
              </Text>
              <Text
                color="$fg3"
                fontSize={12}
                fontFamily="$body"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {modalSubtitle}
              </Text>
            </Stack>

            {/* ✕ close */}
            <Stack
              width={32}
              height={32}
              borderRadius="$sm"
              backgroundColor="rgba(255,255,255,0.05)"
              alignItems="center"
              justifyContent="center"
              cursor="pointer"
              pressStyle={{ scale: 0.95 }}
              onPress={handleClose}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('onboarding.close')}
              aria-label={t('onboarding.close')}
            >
              <Text color="$fg3" fontSize={14} fontWeight="700">{'✕'}</Text>
            </Stack>
          </Stack>

          {/* Body — scrollable. Web: 60vh for responsive short viewports; native: 540px. */}
          <ScrollView
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            style={{ maxHeight: Platform.OS === 'web' ? ('60vh' as any) : 540 }}
            contentContainerStyle={{ padding: 20, gap: 16, flexDirection: 'column' }}
            showsVerticalScrollIndicator
          >
            {/* Web hidden file input (add mode only) */}
            {Platform.OS === 'web' && !isEditMode && (
              <input
                ref={fileInputRef}
                type="file"
                accept={AVATAR_ACCEPT}
                style={{ display: 'none' }}
                onChange={handleFileChange}
                aria-hidden
              />
            )}

            {/* Photo upload row — add mode only */}
            {!isEditMode && (
              <Stack flexDirection={rowDir} alignItems="flex-start" gap={14}>
                {/* Avatar circle 64×64 + 📷 badge */}
                <Stack position="relative" width={64} height={64}>
                  <Avatar
                    name={fullName || 'A'}
                    uri={photoPreviewUri ?? undefined}
                    size="lg"
                    accessibilityLabel={t('parent.addChildModal.labelName')}
                  />
                  {/* 📷 badge — on logical-end corner (right in LTR, left in RTL) */}
                  <Stack
                    position="absolute"
                    bottom={-2}
                    {...(isRtl ? { left: -2 } : { right: -2 })}
                    width={24}
                    height={24}
                    borderRadius={9999}
                    backgroundColor="$primary"
                    style={{ border: '2px solid #15161D' }}
                    alignItems="center"
                    justifyContent="center"
                    cursor="pointer"
                    onPress={handlePhotoPress}
                    accessibilityElementsHidden
                  >
                    <Text fontSize={11}>{'📷'}</Text>
                  </Stack>
                </Stack>

                {/* Dashed drop-zone */}
                <Stack
                  flex={1}
                  style={{
                    borderStyle: 'dashed',
                    borderColor: 'rgba(99,102,241,0.45)',
                    backgroundColor: 'rgba(79,70,229,0.05)',
                    borderWidth: 1.5,
                    borderRadius: 14,
                    padding: '12px 14px',
                    cursor: 'pointer',
                    flexDirection: 'column',
                    gap: 4,
                  }}
                  onPress={handlePhotoPress}
                  cursor="pointer"
                  accessible
                  accessibilityRole="button"
                  accessibilityLabel={t('parent.addChildModal.photoUpload')}
                >
                  <Stack flexDirection={rowDir} alignItems="center" gap="$1">
                    <Text fontSize={14} accessibilityElementsHidden>{'⬆️'}</Text>
                    <Text color="$primaryLight" fontSize={13} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
                      {t('parent.addChildModal.photoUpload')}
                    </Text>
                  </Stack>
                  <Text color="$fg3" fontSize={11} fontFamily="$body" writingDirection={direction}>
                    {t('parent.addChildModal.photoSub')}
                  </Text>
                </Stack>
              </Stack>
            )}
            {photoError ? (
              <Text color="$danger" fontSize={12} fontFamily="$body" writingDirection={direction}>
                {photoError}
              </Text>
            ) : null}

            {/* Child name */}
            <TextField
              label={t('parent.addChildModal.labelName')}
              value={fullName}
              onChangeText={setFullName}
              autoCapitalize="words"
              direction={direction}
              error={fieldErrors.fullName}
              testID="add-child-name"
            />

            {/* Login email — add mode only (LTR pinned). `autoComplete="off"` plus
                the password's `new-password` below stops the browser autofilling
                the parent's saved credentials into the child-account form. */}
            {!isEditMode && (
              <TextField
                label={t('parent.addChildModal.labelEmail')}
                value={email}
                onChangeText={setEmail}
                keyboardType="email-address"
                autoComplete="off"
                autoCorrect={false}
                direction={direction}
                forceLtr
                error={fieldErrors.email}
                testID="add-child-email"
              />
            )}

            {/* Password + strength meter — add mode only. `new-password` marks this
                as account-creation so the browser does NOT autofill the parent's
                saved password (and suppresses login autofill for the form). */}
            {!isEditMode && (
              <Stack flexDirection="column" gap="$1">
                <TextField
                  label={t('parent.addChildModal.labelPassword')}
                  value={password}
                  onChangeText={setPassword}
                  secureTextEntry
                  showLabel={t('auth.login.showPassword')}
                  hideLabel={t('auth.login.hidePassword')}
                  autoComplete="new-password"
                  autoCorrect={false}
                  direction={direction}
                  error={fieldErrors.password}
                  testID="add-child-password"
                />
                {password.length > 0 ? (
                  <PasswordStrengthMeter
                    score={passwordStrength}
                    label={strengthLabel}
                    direction={direction}
                    accessibilityLabel={t('auth.register.strength.a11y', { label: strengthLabel })}
                  />
                ) : null}
              </Stack>
            )}

            {/* Grade — 6 plant-emoji tiles (NEVER a select) */}
            <Stack flexDirection="column" gap="$1">
              <Text
                color="$fg3"
                fontSize={12}
                fontWeight="700"
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {t('parent.addChildModal.labelGrade')}
              </Text>
              <Stack flexDirection={rowDir} gap={6} flexWrap="wrap">
                {GRADES.map((g) => {
                  const isSelected = grade === g;
                  return (
                    <Stack
                      key={g}
                      testID={`grade-tile-${g}`}
                      height={46}
                      minWidth={46}
                      flex={1}
                      borderRadius="$nav"
                      alignItems="center"
                      justifyContent="center"
                      gap="$0"
                      cursor="pointer"
                      pressStyle={{ scale: 0.95 }}
                      onPress={() => {
                        setGrade(g);
                        setFieldErrors((prev) => ({ ...prev, grade: '' }));
                      }}
                      accessibilityRole="radio"
                      accessible
                      accessibilityState={{ selected: isSelected }}
                      accessibilityLabel={`${t('parent.addChildModal.labelGrade')} ${formatGradeNumber(g, locale)}`}
                      style={isSelected
                        ? { background: 'linear-gradient(135deg,#A855F7,#6366F1)', boxShadow: '0 4px 12px rgba(99,102,241,0.4)', border: 'none' }
                        : { backgroundColor: '#0B0C12', border: '1px solid rgba(255,255,255,0.1)' }}
                    >
                      <Text fontSize={15} accessibilityElementsHidden>{GRADE_PLANT[g - 1]}</Text>
                      <Text
                        color={isSelected ? '#fff' : '$fg3'}
                        fontSize={11}
                        fontWeight="800"
                        fontFamily="$heading"
                        style={{ fontVariant: ['tabular-nums'] }}
                      >
                        {formatGradeNumber(g, locale)}
                      </Text>
                    </Stack>
                  );
                })}
              </Stack>
              {fieldErrors.grade ? (
                <Text color="$danger" fontSize={12} fontFamily="$body" writingDirection={direction}>
                  {fieldErrors.grade}
                </Text>
              ) : null}
            </Stack>

            {/* App language — 2 flag tiles */}
            <Stack flexDirection="column" gap="$1">
              <Text
                color="$fg3"
                fontSize={12}
                fontWeight="700"
                fontFamily="$heading"
                writingDirection={direction}
                textAlign={isRtl ? 'right' : 'left'}
              >
                {t('parent.addChildModal.labelAppLanguage')}
              </Text>
              <Stack flexDirection={rowDir} gap={8}>
                {(['ar', 'en'] as const).map((lang) => {
                  const isSelected = appLanguage === lang;
                  const flag = lang === 'ar' ? '🇪🇬' : '🇺🇸';
                  const label = lang === 'ar' ? t('parent.addChildModal.flagAr') : t('parent.addChildModal.flagEn');
                  return (
                    <Stack
                      key={lang}
                      testID={`app-lang-tile-${lang}`}
                      flex={1}
                      height={48}
                      borderRadius="$nav"
                      flexDirection={rowDir}
                      alignItems="center"
                      justifyContent="center"
                      gap="$1"
                      cursor="pointer"
                      pressStyle={{ scale: 0.95 }}
                      onPress={() => {
                        setAppLanguage(lang);
                        setFieldErrors((prev) => ({ ...prev, appLanguage: '' }));
                      }}
                      accessibilityRole="radio"
                      accessible
                      accessibilityState={{ selected: isSelected }}
                      accessibilityLabel={label}
                      style={isSelected
                        ? { backgroundColor: 'rgba(79,70,229,0.18)', border: '1.5px solid #4F46E5' }
                        : { backgroundColor: '#0B0C12', border: '1px solid rgba(255,255,255,0.1)' }}
                    >
                      <Text fontSize={18} accessibilityElementsHidden>{flag}</Text>
                      <Text
                        color={isSelected ? '$fg1' : '$fg3'}
                        fontSize={14}
                        fontWeight="700"
                        fontFamily="$heading"
                      >
                        {label}
                      </Text>
                    </Stack>
                  );
                })}
              </Stack>
              {fieldErrors.appLanguage ? (
                <Text color="$danger" fontSize={12} fontFamily="$body" writingDirection={direction}>
                  {fieldErrors.appLanguage}
                </Text>
              ) : null}
            </Stack>

            {/* Learning language select — add mode only (update endpoint omits it) */}
            {!isEditMode && (
              <Stack flexDirection="column" gap="$1">
                <LanguageSelect
                  label={t('parent.addChildModal.labelLearningLanguage')}
                  value={learningLanguage}
                  onChange={(v) => {
                    setLearningLanguage(String(v));
                    setFieldErrors((prev) => ({ ...prev, learningLanguage: '' }));
                  }}
                  options={languageOptions}
                  direction={direction}
                  accessibilityLabel={t('parent.addChildModal.labelLearningLanguage')}
                  error={fieldErrors.learningLanguage}
                  testID="add-child-learning-language"
                />
                <Text
                  color="$fg3"
                  fontSize={11}
                  fontFamily="$body"
                  writingDirection={direction}
                >
                  {t('parent.addChildModal.learningLanguageHelper')}
                </Text>
              </Stack>
            )}

            {/* Server error */}
            <ServerErrorBanner message={serverMessage} direction={direction} />
          </ScrollView>

          {/* Footer */}
          <Stack
            flexDirection={rowDir}
            gap={10}
            padding={22}
            paddingTop={16}
            borderTopWidth={1}
            borderTopColor="rgba(255,255,255,0.05)"
          >
            {/* Cancel */}
            <Stack
              flex={1}
              height={48}
              borderRadius="$button"
              alignItems="center"
              justifyContent="center"
              cursor="pointer"
              pressStyle={{ scale: 0.95 }}
              onPress={handleClose}
              accessibilityRole="button"
              accessible
              accessibilityLabel={t('parent.addChildModal.cancel')}
              style={{ border: '1px solid rgba(255,255,255,0.12)', backgroundColor: 'transparent' }}
            >
              <Text color="$fg2" fontSize={15} fontWeight="700" fontFamily="$heading" writingDirection={direction}>
                {t('parent.addChildModal.cancel')}
              </Text>
            </Stack>

            {/* Primary CTA */}
            <Button
              variant="primary"
              size="md"
              loading={isPending}
              disabled={isPending}
              accessibilityLabel={ctaLabel}
              onPress={handleSubmit}
              testID="add-child-submit"
              style={{ flex: 2 }}
            >
              {ctaLabel}
            </Button>
          </Stack>
        </Stack>
        </Animated.View>
      </Stack>
    </Modal>
  );
}

AddChildModal.displayName = 'AddChildModal';
