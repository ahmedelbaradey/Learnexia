/**
 * FormScaffold — the responsive auth/onboarding container (Design Spec §1).
 *
 * Phone (sm): full-width column on `$bg`, scrollable, horizontal `$6` padding.
 * Tablet/laptop: a centered `Card` (max-width 480px) on `$bg`. Tamagui media
 * props (`$tablet`) switch between the two without JS branching. Wraps content
 * in `KeyboardAvoidingView` + `ScrollView` for keyboard-aware native forms.
 *
 * `variant="split"` (web login/register, P1-11): a two-column split-panel — a
 * `$primary` brand/feature panel + a `$bg` form column at the `$tablet`
 * breakpoint (≥768). `brandSide` chooses which visual side the panel sits on:
 * `'start'` = login (panel left), `'end'` = register (panel right, the Login
 * mirror). At ≤768 the brand panel is hidden via the media prop and it collapses
 * to the same single-column form as the default scaffold. The prop/style shape
 * mirrors the default scaffold (no new pattern).
 */
import { gradients } from '@learnexia/design-system';
import { Card } from '@learnexia/ui';
import { Stack } from '@tamagui/core';
import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

/**
 * Decorative glowing particle dots scattered across the web brand panel
 * (align-login m-16). Web-only: positions/sizes are fixed design values, each a
 * soft white glow. Hidden from a11y. Native renders the flat `$primary` panel.
 */
const BRAND_PANEL_DOTS = [
  { top: '12%', left: '18%', size: 4 },
  { top: '20%', left: '72%', size: 3 },
  { top: '30%', left: '40%', size: 5 },
  { top: '38%', left: '85%', size: 3 },
  { top: '46%', left: '10%', size: 4 },
  { top: '54%', left: '60%', size: 3 },
  { top: '62%', left: '28%', size: 5 },
  { top: '70%', left: '78%', size: 4 },
  { top: '78%', left: '15%', size: 3 },
  { top: '84%', left: '50%', size: 4 },
  { top: '88%', left: '88%', size: 3 },
  { top: '24%', left: '5%', size: 4 },
] as const;

export type FormScaffoldVariant = 'default' | 'split';

/** Which visual side the `$primary` brand panel sits on (split variant). */
export type BrandSide = 'start' | 'end';

export interface FormScaffoldProps {
  children: ReactNode;
  /** Optional header rendered above the scroll area (e.g. ScreenHeader). */
  header?: ReactNode;
  /** Layout variant. `split` adds the web brand panel (requires `brandPanel`). */
  variant?: FormScaffoldVariant;
  /** Brand/feature panel content, rendered at `$tablet`+ when `variant="split"`. */
  brandPanel?: ReactNode;
  /** Side the brand panel sits on: `start` (login, left) or `end` (register, right). */
  brandSide?: BrandSide;
}

export function FormScaffold({
  children,
  header,
  variant = 'default',
  brandPanel,
  brandSide = 'start',
}: FormScaffoldProps) {
  const insets = useSafeAreaInsets();

  if (variant === 'split') {
    return (
      <SplitFormScaffold header={header} brandPanel={brandPanel} brandSide={brandSide}>
        {children}
      </SplitFormScaffold>
    );
  }

  return (
    <Stack flex={1} backgroundColor="$bg" paddingTop={insets.top}>
      {header}
      <KeyboardAvoidingView
        style={{ flex: 1 }}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={{ flexGrow: 1 }}
          keyboardShouldPersistTaps="handled"
        >
          <Stack flex={1} alignItems="center" justifyContent="center" paddingVertical="$6">
            {/* Phone: full width. Tablet+: centered card, max 480. */}
            <Stack
              width="100%"
              paddingHorizontal="$6"
              $tablet={{ maxWidth: 480, paddingHorizontal: 0 }}
            >
              <Stack
                $tablet={{
                  backgroundColor: '$card',
                  borderRadius: '$card',
                  padding: '$8',
                  borderWidth: 1,
                  borderColor: '$border',
                }}
              >
                {children}
              </Stack>
            </Stack>
          </Stack>
        </ScrollView>
      </KeyboardAvoidingView>
    </Stack>
  );
}

/**
 * SplitFormScaffold — the two-column web variant. A `$primary` brand panel +
 * `$bg` form column at `$tablet`+; single-column form (no brand panel) at `$sm`.
 * `brandSide` flips which visual side the panel sits on (`row` vs `row-reverse`)
 * without reordering the DOM (form stays first for tab/reading order). Same
 * scroll/keyboard wiring as the default scaffold.
 */
export function SplitFormScaffold({
  children,
  header,
  brandPanel,
  brandSide = 'start',
}: Omit<FormScaffoldProps, 'variant'>) {
  const insets = useSafeAreaInsets();
  const isWeb = Platform.OS === 'web';
  // `start` → brand left (panel rendered first, plain `row`).
  // `end` → brand right (panel still rendered first, flipped via `row-reverse`).
  const tabletRowDirection = brandSide === 'end' ? 'row-reverse' : 'row';
  return (
    <Stack
      flex={1}
      backgroundColor="$bg"
      paddingTop={insets.top}
      flexDirection="column"
      $tablet={{ flexDirection: tabletRowDirection, paddingTop: 0 }}
    >
      {/* Left brand panel — hidden on phones, shown side-by-side on tablet+. */}
      {brandPanel ? (
        <Stack
          display="none"
          // Native: flat `$primary`. Web: the 3-stop brand gradient (align-login m-15).
          backgroundColor="$primary"
          style={isWeb ? ({ backgroundImage: gradients.gradBrandPanel } as object) : undefined}
          $tablet={{
            display: 'flex',
            flex: 1,
            paddingHorizontal: '$8',
            paddingVertical: '$10',
            justifyContent: 'space-between',
            overflow: 'hidden',
          }}
        >
          {/* Scattered glowing particle dots (web-only decoration, align-login m-16). */}
          {isWeb ? (
            <Stack
              position="absolute"
              top={0}
              left={0}
              right={0}
              bottom={0}
              pointerEvents="none"
              accessibilityElementsHidden
              aria-hidden
            >
              {BRAND_PANEL_DOTS.map((dot, i) => (
                <Stack
                  key={i}
                  position="absolute"
                  top={dot.top as unknown as number}
                  left={dot.left as unknown as number}
                  width={dot.size}
                  height={dot.size}
                  borderRadius={9999}
                  backgroundColor="rgba(255,255,255,0.25)"
                  style={{ boxShadow: '0 0 8px rgba(255,255,255,0.5)' } as object}
                />
              ))}
            </Stack>
          ) : null}
          {brandPanel}
        </Stack>
      ) : null}

      {/* Right form column. */}
      <Stack flex={1} $tablet={{ flex: 1 }}>
        {header}
        <KeyboardAvoidingView
          style={{ flex: 1 }}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          <ScrollView
            contentContainerStyle={{ flexGrow: 1 }}
            keyboardShouldPersistTaps="handled"
          >
            <Stack flex={1} alignItems="center" justifyContent="center" paddingVertical="$6">
              <Stack
                width="100%"
                paddingHorizontal="$6"
                $tablet={{ maxWidth: 500, paddingHorizontal: 0 }}
              >
                {children}
              </Stack>
            </Stack>
          </ScrollView>
        </KeyboardAvoidingView>
      </Stack>
    </Stack>
  );
}

// Re-export Card so screens needing the explicit card primitive can grab it here.
export { Card };
