/**
 * BadgeUnlockOverlay — P4-08 FE-2 (approved OD-1: lives in packages/ui for
 * reuse from both Home 3c and the Badges screen).
 *
 * Wraps `RewardPopup variant="badge-unlock"` with the correct badge disc,
 * multicolor confetti, and the pop-in motion that fires when `visible` becomes
 * true. The Badge `isNewlyEarned` prop triggers the internal spring pop-in.
 *
 * Design Spec P4-08 §2.1:
 *   - Overlay fill: $overlay (rgba(15,23,42,0.72))
 *   - Card spring: springs.popup = { damping: 12, stiffness: 180 }
 *   - Confetti: paletteVariant="multicolor", count=18
 *   - xpAmount=0 (badge-only unlock hides the XP row)
 *   - Reduce-motion: inherited from RewardPopup (spring skipped, renders static)
 *   - A11y: accessibilityRole="alert", accessibilityLiveRegion="assertive" — from RewardPopup
 *
 * Props:
 *   badgeVariant   — disc gradient / glow variant (bronze/silver/gold/legendary)
 *   badgeName      — already-resolved display name in the current locale
 *   visible        — mounts / unmounts the overlay
 *   onDismiss      — called when the CTA is pressed
 *   title          — i18n-resolved title (e.g. "New Badge!" / "شارة جديدة!")
 *   ctaLabel       — i18n-resolved CTA (e.g. "Awesome!" / "رائع!")
 *   accessibilityLabel — full a11y announcement e.g. "New Badge! First Steps"
 *   locale         — forwarded to RewardPopup (not used internally)
 */
import React from 'react';

import { RewardPopup } from '../RewardPopup';
import type { BadgeVariant } from '../Badge';

export interface BadgeUnlockOverlayProps {
  badgeVariant: BadgeVariant;
  badgeName: string;
  visible: boolean;
  onDismiss: () => void;
  /** i18n-resolved overlay title, e.g. "New Badge!" */
  title: string;
  /** i18n-resolved CTA label, e.g. "Awesome!" */
  ctaLabel?: string;
  /** Full screen-reader announcement including badge name and rarity. */
  accessibilityLabel: string;
  locale?: string;
  /**
   * Stable test identifier forwarded to the underlying RewardPopup container.
   * @default 'badge-unlock-overlay'
   */
  testID?: string;
}

/**
 * Badge unlock celebration overlay. Composes `RewardPopup variant="badge-unlock"`
 * with `xpAmount=0` (XP row hidden) and `isNewlyEarned=true` on the badge disc
 * (the pop-in spring comes from the Badge component itself via `springs.pop`).
 *
 * Reduce-motion: fully inherited from RewardPopup + ConfettiLayer (no extra gates
 * needed here).
 */
export function BadgeUnlockOverlay({
  badgeVariant,
  badgeName,
  visible,
  onDismiss,
  title,
  ctaLabel = 'Awesome!',
  accessibilityLabel,
  locale,
  testID = 'badge-unlock-overlay',
}: BadgeUnlockOverlayProps) {
  if (!visible) return null;

  return (
    <RewardPopup
      variant="badge-unlock"
      xpAmount={0}
      title={title}
      subtitle={badgeName}
      badgeVariant={badgeVariant}
      visible={visible}
      ctaLabel={ctaLabel}
      onDismiss={onDismiss}
      locale={locale}
      accessibilityLabel={accessibilityLabel}
      testID={testID}
    />
  );
}

BadgeUnlockOverlay.displayName = 'BadgeUnlockOverlay';
