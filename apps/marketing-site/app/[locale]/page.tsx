import Image from 'next/image';

import type { Locale } from '../../lib/copy';
import { getCopy } from '../../lib/copy';
import { REGISTER_URL, LOGIN_URL } from '../../lib/config';
import { PhoneMockup } from '../_components/PhoneMockup';
import { FeaturesSection } from '../_components/FeaturesSection';
import { SubjectsBand } from '../_components/SubjectsBand';
import { CTABanner } from '../_components/CTABanner';
import { SiteFooter } from '../_components/SiteFooter';
import { LanguageSwitcher } from '../_components/LanguageSwitcher';
import { AITutorBubble } from '../_components/AITutorBubble';
import { ChildCardPhone } from '../_components/ChildCardPhone';
import { ActivityChart } from '../_components/ActivityChart';
import { BenefitsPanel } from '../_components/BenefitsPanel';
import styles from '../page.module.css';

/**
 * Locale-aware landing page.
 *
 * Routing: /en → lang="en" dir="ltr"; /ar → lang="ar" dir="rtl"
 * Copy: comes from getCopy(locale) in lib/copy.ts.
 * Locale parameter is read from the [locale] path segment.
 */
interface PageProps {
  params: Promise<{ locale: string }>;
}

export default async function LandingPage({ params }: PageProps) {
  const { locale: localeParam } = await params;
  const locale: Locale = localeParam === 'ar' ? 'ar' : 'en';
  const C = getCopy(locale);

  return (
    <div className={styles.root}>
      {/* ----------------------------- Top nav ----------------------------- */}
      <header className={styles.nav}>
        <div className={styles.navInner}>
          <a href={`/${locale}#top`} className={styles.brand} aria-label={C.brand}>
            {/* Brand wordmark is always LTR — image is direction-agnostic */}
            <Image
              src="/assets/logo.svg"
              alt={C.brandLogoAlt}
              width={170}
              height={45}
              priority
            />
          </a>

          <nav className={styles.navLinks} aria-label={C.brand}>
            <a href={`/${locale}#how-it-works`}>{C.nav.howItWorks}</a>
            <a href={`/${locale}#subjects`}>{C.nav.subjects}</a>
            <a href={`/${locale}#how-it-works`}>{C.nav.forSchools}</a>
            <a href={`/${locale}#pricing`}>{C.nav.pricing}</a>
          </nav>

          <div className={styles.navActions}>
            {/* Language switcher — two-segment pill, matches ar-web-nav.html */}
            <LanguageSwitcher currentLocale={locale} />
            <a className={styles.btnOutline} href={LOGIN_URL}>
              {C.nav.logIn}
            </a>
            <a className={styles.btnPrimary} href={REGISTER_URL}>
              {C.nav.startFree}
            </a>
          </div>
        </div>
      </header>

      {/* ------------------------------- Hero ------------------------------ */}
      <main id="top" className={styles.hero}>
        <div className={styles.heroGlow} aria-hidden="true" />

        <section className={styles.heroCopy}>
          <span className={styles.pill}>{C.hero.pill}</span>

          <h1 className={styles.headline}>
            {C.hero.headlineLead}
            <span className={styles.headlineAccent}>{C.hero.headlineAccent}</span>
            {C.hero.headlineRest}
          </h1>

          <p className={styles.paragraph}>
            {C.hero.paragraphLead}
            <strong className={styles.paragraphStrong}>{C.hero.paragraphSubjects}</strong>
            {C.hero.paragraphRest}
          </p>

          <div className={styles.ctaRow}>
            <a className={styles.ctaPrimary} href={REGISTER_URL}>
              {C.hero.ctaPrimary}
            </a>
            <a className={styles.ctaSecondary} href={`/${locale}#how-it-works`}>
              <span className={styles.playIcon} aria-hidden="true">
                {C.hero.ctaSecondaryPlay}
              </span>
              {C.hero.ctaSecondary}
            </a>
          </div>

          <ul className={styles.trustRow}>
            <li>
              <span aria-hidden="true">⭐</span>
              {C.hero.trustRating}
            </li>
            <li>
              <span aria-hidden="true">🛡</span>
              {C.hero.trustCoppa}
            </li>
            <li>
              <span aria-hidden="true">👨‍👩‍👧</span>
              {C.hero.trustFirstChild}
            </li>
          </ul>
        </section>

        <section className={styles.heroArt} aria-hidden="true">
          <PhoneMockup locale={locale} />
        </section>
      </main>

      {/* ----------------------- Below-the-fold sections ------------------- */}
      <FeaturesSection locale={locale} />
      <SubjectsBand locale={locale} />

      {/* ── Batch 2: product-proof + parent-proof sections ─────────── */}
      {/* "See it in action" — AI tutor screenshot */}
      <AITutorBubble locale={locale} />
      {/* In-phone child card screenshot */}
      <ChildCardPhone locale={locale} />
      {/* Weekly activity strip */}
      <ActivityChart locale={locale} />
      {/* Purple benefits panel — visual break before CTA */}
      <BenefitsPanel locale={locale} />
      {/* ─────────────────────────────────────────────────────────────── */}

      <CTABanner locale={locale} />
      <SiteFooter locale={locale} />
    </div>
  );
}
