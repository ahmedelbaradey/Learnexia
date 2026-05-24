import Image from 'next/image';

import styles from './page.module.css';
import { LANDING_COPY as C } from '../lib/copy';
import { REGISTER_URL, LOGIN_URL } from '../lib/config';
import { PhoneMockup } from './_components/PhoneMockup';
import { FeaturesSection } from './_components/FeaturesSection';
import { SubjectsBand } from './_components/SubjectsBand';
import { CTABanner } from './_components/CTABanner';
import { SiteFooter } from './_components/SiteFooter';

/**
 * Landing page (P1-11-FE-12) — pixel-aligned to
 * `design-system/screenshots/web/01-landing.png` and the canonical preview
 * cards (web-nav / web-hero-phonemock / web-feature-card / web-subject-band /
 * web-cta-banner / web-footer). A frosted top nav, a two-column hero (copy +
 * CTAs left, decorative phone mockup right), then the below-the-fold sections:
 * "Why Learnexia" feature grid, the four-subject band, a gradient CTA banner
 * and the footer. All colour / type / radius / spacing values come from the
 * design-system `--lx-*` tokens (see `globals.css`); all copy from
 * `lib/copy.ts`.
 */
export default function LandingPage() {
  return (
    <div className={styles.root}>
      {/* ----------------------------- Top nav ----------------------------- */}
      <header className={styles.nav}>
        <div className={styles.navInner}>
          <a href="#top" className={styles.brand} aria-label={C.brand}>
            <Image
              src="/assets/logo.svg"
              alt={C.brandLogoAlt}
              width={170}
              height={45}
              priority
            />
          </a>

          <nav className={styles.navLinks} aria-label={C.brand}>
            <a href="#how-it-works">{C.nav.howItWorks}</a>
            <a href="#subjects">{C.nav.subjects}</a>
            <a href="#how-it-works">{C.nav.forSchools}</a>
            <a href="#pricing">{C.nav.pricing}</a>
          </nav>

          <div className={styles.navActions}>
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
            <a className={styles.ctaSecondary} href="#how-it-works">
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
          <PhoneMockup />
        </section>
      </main>

      {/* ----------------------- Below-the-fold sections ------------------- */}
      <FeaturesSection />
      <SubjectsBand />
      <CTABanner />
      <SiteFooter />
    </div>
  );
}
