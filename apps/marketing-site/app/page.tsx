import Image from 'next/image';

import styles from './page.module.css';
import { LANDING_COPY as C } from '../lib/copy';
import { REGISTER_URL, LOGIN_URL } from '../lib/config';
import { PhoneMockup } from './_components/PhoneMockup';

/**
 * Landing page (P1-11-FE-12) — pixel-perfect to
 * `design-system/screenshots/web/01-landing.png`.
 *
 * Layout: a fixed dark top nav, then a two-column hero (copy + CTAs on the
 * left, decorative phone mockup with floating reward chips on the right), then
 * minimal below-the-fold section stubs that the nav anchors target. All colour
 * / type / radius / spacing values come from the design-system `--lx-*` tokens
 * (see `globals.css`); all copy comes from `lib/copy.ts`.
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
            <a href="#for-schools">{C.nav.forSchools}</a>
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
          <span className={styles.pill}>
            <span className={styles.pillSpark} aria-hidden="true">
              ✦
            </span>
            {C.hero.pill}
          </span>

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
              <span className={styles.trustIcon} aria-hidden="true">
                ⭐
              </span>
              {C.hero.trustRating}
            </li>
            <li>
              <span className={styles.trustIcon} aria-hidden="true">
                🛡
              </span>
              {C.hero.trustCoppa}
            </li>
            <li>
              <span className={styles.trustIcon} aria-hidden="true">
                👨‍👩‍👧
              </span>
              {C.hero.trustFirstChild}
            </li>
          </ul>
        </section>

        <section className={styles.heroArt} aria-hidden="true">
          <PhoneMockup />
        </section>
      </main>

      {/* ----------------- Below-the-fold section stubs ------------------- */}
      <section id="how-it-works" className={styles.section}>
        <h2 className={styles.sectionTitle}>{C.sections.howItWorksTitle}</h2>
        <p className={styles.sectionBody}>{C.sections.howItWorksBody}</p>
      </section>

      <section id="subjects" className={styles.section}>
        <h2 className={styles.sectionTitle}>{C.sections.subjectsTitle}</h2>
        <p className={styles.sectionBody}>{C.sections.subjectsBody}</p>
      </section>

      <section id="for-schools" className={styles.section}>
        <h2 className={styles.sectionTitle}>{C.sections.forSchoolsTitle}</h2>
        <p className={styles.sectionBody}>{C.sections.forSchoolsBody}</p>
      </section>

      <section id="pricing" className={styles.section}>
        <h2 className={styles.sectionTitle}>{C.sections.pricingTitle}</h2>
        <p className={styles.sectionBody}>{C.sections.pricingBody}</p>
        <a className={styles.ctaPrimary} href={REGISTER_URL}>
          {C.nav.startFree}
        </a>
      </section>

      <footer className={styles.footer}>{C.footer.rights}</footer>
    </div>
  );
}
