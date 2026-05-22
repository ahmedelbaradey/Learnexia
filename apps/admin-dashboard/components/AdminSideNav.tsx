'use client';

/**
 * AdminSideNav — persistent side navigation (Design Spec §4b).
 *
 * Built with Tamagui (`@tamagui/core` Stack/Text) + design-system tokens — no
 * CSS module. Placeholder, NON-FUNCTIONAL nav items (Curriculum, Content) per
 * the story: they render in the inactive state and do nothing on click (no
 * `/curriculum` or `/content` routes are created). Icons are emoji placeholders
 * (Design Spec Gap 1 — no admin icon set yet).
 *
 * Responsive (Design Spec §4a + Gap 2), mobile-first:
 *   - base (< laptop): fixed drawer, slid off-screen via `transform`, revealed
 *     when `isOpen`; a backdrop covers the content. Logical inset/transform are
 *     locale-aware (drawer enters from the start edge — flips under `dir="rtl"`).
 *   - `$laptop` (≥ 1024px): static 240px column, no drawer, no backdrop.
 */

import { Stack, Text } from '@tamagui/core';
import Image from 'next/image';

import { getStrings, ADMIN_LOCALE } from '../lib/strings';
import { directionForLocale } from '@learnexia/shared/i18n';

export interface AdminSideNavProps {
  /** Whether the drawer is open (only relevant below the laptop breakpoint). */
  isOpen?: boolean;
  /** Close handler for the mobile/tablet drawer backdrop. */
  onClose?: () => void;
}

const strings = getStrings(ADMIN_LOCALE);
const isRtl = directionForLocale(ADMIN_LOCALE) === 'rtl';

const NAV_ITEMS = [
  { key: 'curriculum', label: strings.navCurriculum, icon: '📚' },
  { key: 'content', label: strings.navContent, icon: '📄' },
] as const;

export function AdminSideNav({ isOpen = false, onClose }: AdminSideNavProps) {
  return (
    <>
      {/* Backdrop (drawer only, below laptop) — `lx-nav-backdrop` shows the
          fixed overlay below the laptop breakpoint (rules in globals.css). */}
      {isOpen ? (
        <Stack
          aria-hidden
          className="lx-nav-backdrop"
          zIndex={150}
          backgroundColor="$overlay"
          onPress={onClose}
        />
      ) : null}

      {/*
        Layout/colors come from Tamagui tokens; the breakpoint-switched drawer
        mechanics (fixed positioning + off-screen transform below laptop, static
        column at laptop+) live in globals.css under `.lx-side-nav` — web-only
        behavior that Tamagui's RN-style position types cannot express. The
        `is-open` class + `data-rtl` drive the open state and locale-aware slide.
      */}
      <Stack
        tag="nav"
        aria-label="Admin navigation"
        className={`lx-side-nav${isOpen ? ' is-open' : ''}`}
        data-rtl={isRtl ? 'true' : undefined}
        flexDirection="column"
        flexShrink={0}
        width={240}
        backgroundColor="$bgElevated"
        zIndex={200}
      >
        <Stack
          padding="$6"
          borderBottomWidth={1}
          borderBottomColor="$border"
        >
          <Image
            src="/assets/logo.svg"
            alt="Learnexia"
            width={140}
            height={36}
            priority
            style={{ height: 36, width: 'auto' }}
          />
        </Stack>

        <Stack tag="ul" flex={1} flexDirection="column" gap={4} padding="$4" margin={0}>
          {NAV_ITEMS.map((item) => (
            <Stack tag="li" key={item.key}>
              {/* Non-functional placeholder: inactive state, no-op press, disabled
                  semantics for screen readers (feature not yet available). */}
              <Stack
                tag="button"
                aria-disabled
                flexDirection="row"
                alignItems="center"
                gap="$3"
                width="100%"
                height={44}
                paddingHorizontal="$3"
                borderWidth={0}
                borderRadius="$button"
                backgroundColor="transparent"
                cursor="pointer"
                hoverStyle={{ backgroundColor: '$card' }}
                style={{ transition: 'background-color var(--lx-dur-fast) var(--lx-ease-out)' }}
                onPress={() => {
                  /* no-op placeholder */
                }}
              >
                <Text fontSize={18} width={20} textAlign="center" aria-hidden>
                  {item.icon}
                </Text>
                <Text fontFamily="$body" fontSize={14} fontWeight="400" color="$fg3">
                  {item.label}
                </Text>
              </Stack>
            </Stack>
          ))}
        </Stack>
      </Stack>
    </>
  );
}
