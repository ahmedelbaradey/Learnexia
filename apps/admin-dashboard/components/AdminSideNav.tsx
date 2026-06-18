'use client';

/**
 * AdminSideNav — persistent side navigation (Design Spec §4b).
 *
 * Built with Tamagui (`@tamagui/core` Stack/Text) + design-system tokens — no
 * CSS module. Icons are emoji placeholders (Design Spec Gap 1 — no admin icon
 * set yet).
 *
 * Nav items:
 *   - "Users"      → /users  — REAL link, active-aware (P7-06-FE-5, Batch A).
 *   - "Curriculum" → placeholder (no route yet), aria-disabled.
 *   - "Content"    → placeholder (no route yet), aria-disabled.
 *
 * Active detection: `usePathname()` → `aria-current="page"` on the active item
 * + visual highlight via `$active` pseudo token styles.
 *
 * Responsive (Design Spec §4a + Gap 2), mobile-first:
 *   - base (< laptop): fixed drawer, slid off-screen via `transform`, revealed
 *     when `isOpen`; a backdrop covers the content. Logical inset/transform are
 *     locale-aware (drawer enters from the start edge — flips under `dir="rtl"`).
 *   - `$laptop` (≥ 1024px): static 240px column, no drawer, no backdrop.
 */

import { Stack, Text } from '@tamagui/core';
import Image from 'next/image';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

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

/**
 * A real nav entry that renders an anchor via Next.js `<Link>`.
 * `href` is the route prefix; active = pathname starts with href.
 */
interface RealNavItem {
  kind: 'real';
  key: string;
  label: string;
  icon: string;
  href: string;
}

/**
 * A placeholder nav entry: renders a non-interactive button with aria-disabled.
 * No route exists for this feature yet.
 */
interface PlaceholderNavItem {
  kind: 'placeholder';
  key: string;
  label: string;
  icon: string;
}

type NavItem = RealNavItem | PlaceholderNavItem;

const NAV_ITEMS: readonly NavItem[] = [
  {
    kind: 'real',
    key: 'users',
    label: strings.navUsers,
    icon: '👤',
    href: '/users',
  },
  {
    kind: 'placeholder',
    key: 'curriculum',
    label: strings.navCurriculum,
    icon: '📚',
  },
  {
    kind: 'placeholder',
    key: 'content',
    label: strings.navContent,
    icon: '📄',
  },
] as const;

export function AdminSideNav({ isOpen = false, onClose }: AdminSideNavProps) {
  const pathname = usePathname();

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
          {NAV_ITEMS.map((item) => {
            if (item.kind === 'real') {
              const isActive =
                pathname === item.href ||
                pathname.startsWith(`${item.href}/`);

              return (
                <Stack tag="li" key={item.key}>
                  <Link
                    href={item.href}
                    aria-current={isActive ? 'page' : undefined}
                    style={{ textDecoration: 'none', display: 'block' }}
                  >
                    <Stack
                      flexDirection="row"
                      alignItems="center"
                      gap="$3"
                      width="100%"
                      height={44}
                      paddingHorizontal="$3"
                      borderRadius="$button"
                      backgroundColor={isActive ? '$primarySoft' : 'transparent'}
                      cursor="pointer"
                      hoverStyle={{ backgroundColor: isActive ? '$primarySoft' : '$card' }}
                      style={{
                        transition:
                          'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                      }}
                    >
                      <Text
                        fontSize={18}
                        width={20}
                        textAlign="center"
                        aria-hidden
                      >
                        {item.icon}
                      </Text>
                      <Text
                        fontFamily="$body"
                        fontSize={14}
                        fontWeight={isActive ? '600' : '400'}
                        color={isActive ? '$primary' : '$fg2'}
                      >
                        {item.label}
                      </Text>
                    </Stack>
                  </Link>
                </Stack>
              );
            }

            // Placeholder: non-functional, aria-disabled
            return (
              <Stack tag="li" key={item.key}>
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
                  style={{
                    transition:
                      'background-color var(--lx-dur-fast) var(--lx-ease-out)',
                  }}
                  onPress={() => {
                    /* no-op placeholder */
                  }}
                >
                  <Text fontSize={18} width={20} textAlign="center" aria-hidden>
                    {item.icon}
                  </Text>
                  <Text
                    fontFamily="$body"
                    fontSize={14}
                    fontWeight="400"
                    color="$fg3"
                  >
                    {item.label}
                  </Text>
                </Stack>
              </Stack>
            );
          })}
        </Stack>
      </Stack>
    </>
  );
}
