'use client';

import Image from 'next/image';
import { useEffect, useRef, useState } from 'react';

import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import styles from './ParentValueSection.module.css';

/**
 * ParentValueSection — "For Parents — value showcase" composed section.
 *
 * Design Spec: design-system/ui_kits/marketing/for-parents-section.md
 * Canonical EN source: PagesPublic.jsx lines 304–426
 * Canonical AR source: index-ar.html lines 135–195
 *
 * Layout: centered header (green eyebrow + h2) + 2-col grid (1fr 1.2fr):
 *   LEFT  — purple gradient Benefits panel (icon, h3, 4 bullets)
 *   RIGHT — stacked column: activity chart | tutor bubble | Sami child card
 *
 * RTL: logical CSS auto-flips grid direction; chart bars pinned direction:ltr
 * so Mon→Sun always reads left-to-right; tutor bubble tail flips via CSS.
 *
 * data-testid="parent-value-section"
 */

/** XP values Mon→Sun. Heights derive from (xp / 110) * 95 per spec. */
const XP_VALUES = [45, 80, 30, 95, 50, 70, 110] as const;
const HIGHLIGHT_INDEX = 6; // Sunday

interface ParentValueSectionProps {
  locale: Locale;
}

export function ParentValueSection({ locale }: ParentValueSectionProps) {
  const { parentValue } = getCopy(locale);

  // Section reveal on scroll — IntersectionObserver, gates the CSS transition.
  const sectionRef = useRef<HTMLElement>(null);
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const el = sectionRef.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const entry = entries[0];
        if (entry?.isIntersecting) {
          setVisible(true);
          observer.disconnect();
        }
      },
      { threshold: 0.08 },
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const isAr = locale === 'ar';

  return (
    <section
      ref={sectionRef}
      className={`${styles.section} ${visible ? styles.sectionVisible : ''}`}
      data-testid="parent-value-section"
      aria-labelledby="parent-value-heading"
    >
      <div className={styles.inner}>
        {/* ── Centered header ────────────────────────────── */}
        <div className={styles.headerBlock}>
          <p className={styles.eyebrow}>{parentValue.eyebrow}</p>
          <h2 id="parent-value-heading" className={styles.heading}>
            {parentValue.heading}
          </h2>
        </div>

        {/* ── 2-column grid ───────────────────────────────── */}
        <div className={styles.grid}>
          {/* ── LEFT: Benefits panel ──────────────────────── */}
          <div className={styles.panel} data-testid="parent-value-panel">
            <span
              className={styles.panelGlyph}
              role="img"
              aria-label={isAr ? 'تحكم' : 'controller'}
            >
              🎮
            </span>

            <h3 className={styles.panelHeading}>{parentValue.panel.heading}</h3>

            <ul className={styles.bulletList} role="list">
              {parentValue.panel.bullets.map((bullet) => (
                <li key={bullet.icon} className={styles.bulletRow}>
                  <span
                    className={styles.bulletTile}
                    role="img"
                    aria-hidden="true"
                  >
                    {bullet.icon}
                  </span>
                  <span>{bullet.text}</span>
                </li>
              ))}
            </ul>
          </div>

          {/* ── RIGHT: Live-demos column ───────────────────── */}
          <div className={`${styles.demosCol} ${visible ? styles.demosColVisible : ''}`}>
            {/* ── (1) Activity chart ────────────────────── */}
            <div
              className={styles.chartCard}
              data-testid="parent-value-chart"
              /*
               * Decorative demo content — not real user data.
               * Providing a concise aria-label per spec §Accessibility.
               */
              aria-label={
                isAr
                  ? 'مخطط نشاط أسبوعي نموذجي، الأحد الأعلى'
                  : 'Sample weekly activity chart, Sunday highest'
              }
              aria-hidden="false"
            >
              <div className={styles.chartHeader}>
                <span className={styles.chartTitle}>{parentValue.chart.title}</span>
                <span className={styles.chartDelta}>{parentValue.chart.delta}</span>
              </div>

              {/*
               * direction:ltr is pinned on the bars row so bars never reverse in RTL.
               * Spec §4.2 + AR source index-ar.html line 156: `direction:ltr`.
               */}
              <div
                className={`${styles.barsRow} ${visible ? styles.barsRowVisible : ''}`}
                data-testid="parent-value-chart-bars"
              >
                {XP_VALUES.map((xp, idx) => {
                  const isHi = idx === HIGHLIGHT_INDEX;
                  const heightPx = Math.round((xp / 110) * 95);
                  return (
                    <div key={parentValue.chart.days[idx]} className={styles.barCol}>
                      <div
                        className={`${styles.bar} ${isHi ? styles.barHi : ''}`}
                        style={{ height: `${heightPx}px` }}
                        aria-hidden="true"
                      />
                      <span
                        className={`${styles.dayLabel} ${isHi ? styles.dayLabelHi : ''}`}
                        aria-hidden="true"
                      >
                        {parentValue.chart.days[idx]}
                      </span>
                    </div>
                  );
                })}
              </div>
            </div>

            {/* ── (2) AI tutor bubble ───────────────────── */}
            <div className={styles.tutorRow} data-testid="parent-value-tutor">
              {/* Avatar */}
              <div className={styles.tutorAvatar} aria-hidden="true">
                <Image
                  src="/assets/mascot-owl.svg"
                  alt=""
                  width={46}
                  height={46}
                  aria-hidden="true"
                />
              </div>

              {/*
               * Bubble — tail corner flips via CSS [dir="rtl"] override.
               * EN: border-end-start-radius:4px (bottom-left in LTR)
               * AR: border-end-end-radius:4px (bottom-right in RTL)
               * No suggestion chips per spec §(2).
               */}
              <div className={styles.tutorBubble} data-testid="parent-value-tutor-bubble">
                <div className={styles.tutorLabel}>{parentValue.tutor.label}</div>
                <div className={styles.tutorMsg}>
                  {parentValue.tutor.msgLead}
                  <b className={styles.tutorHighlight}>{parentValue.tutor.msgHighlight}</b>
                  {parentValue.tutor.msgRest}
                </div>
              </div>
            </div>

            {/* ── (3) Sami child card ───────────────────── */}
            <div className={styles.childCard} data-testid="parent-value-child-card">
              {/* Row 1: monogram + name/grade/email + active dot */}
              <div className={styles.childRow1} data-testid="parent-value-child-row1">
                {/* Orange monogram */}
                <div
                  className={styles.childMonogram}
                  role="img"
                  aria-label={parentValue.card.name}
                >
                  {parentValue.card.monogram}
                </div>

                {/* Name + grade + email */}
                <div className={styles.childInfo}>
                  <div className={styles.childNameRow}>
                    <span className={styles.childName}>{parentValue.card.name}</span>
                    <span className={styles.childGrade}>{parentValue.card.grade}</span>
                  </div>
                  {/*
                   * Email: always LTR + monospace — technical string.
                   * AR source index-ar.html line 182: dir="ltr".
                   * direction:ltr set via .childEmail CSS class.
                   */}
                  <span
                    className={styles.childEmail}
                    data-testid="parent-value-child-email"
                  >
                    {parentValue.card.email}
                  </span>
                </div>

                {/* Active dot + label */}
                <div
                  className={styles.childActiveGroup}
                  data-testid="parent-value-child-status"
                >
                  <span className={styles.activeDot} aria-hidden="true" />
                  {parentValue.card.active}
                </div>
              </div>

              {/* Row 2: stats + "View progress →" */}
              <div className={styles.childRow2} data-testid="parent-value-child-stats">
                <span className={styles.statLv} aria-hidden="false">
                  {parentValue.card.lv}
                </span>
                <span className={styles.statXp} aria-hidden="false">
                  {parentValue.card.xp}
                </span>
                <span className={styles.statStreak} aria-hidden="false">
                  {parentValue.card.streak}
                </span>
                {/* Trailing edge — margin-inline-start:auto in CSS */}
                <a
                  href="#"
                  className={styles.viewProgress}
                  data-testid="parent-value-child-cta"
                  aria-label={
                    isAr
                      ? 'عرض تقدم سامي'
                      : 'View progress for Sami'
                  }
                >
                  {parentValue.card.cta}
                </a>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
