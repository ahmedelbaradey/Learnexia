/**
 * Marketing landing copy — single source of all user-facing strings on the
 * landing page (mirrors the admin app's `strings.ts` pattern: copy lives in one
 * typed module, never as free-text literals in JSX).
 *
 * The marketing surface ships English-first (per the P1-11-FE-12 task: a
 * marketing page may use English copy; RTL/AR are not required for the hero).
 * Keeping it `as const` makes every value type-checked and centralized.
 */

export const LANDING_COPY = {
  brand: 'Learnexia',
  brandLogoAlt: 'Learnexia',

  nav: {
    howItWorks: 'How it works',
    subjects: 'Subjects',
    forSchools: 'For schools',
    pricing: 'Pricing',
    logIn: 'Log in',
    startFree: 'Start free',
  },

  hero: {
    pill: 'POWERED BY AI',
    // The headline is split so "adventure game" can be accent-coloured.
    headlineLead: 'An ',
    headlineAccent: 'adventure game',
    headlineRest: ' your kids will love — that teaches.',
    // Paragraph is split so the subject list can be emphasised.
    paragraphLead:
      'Learnexia mixes a personal AI tutor with hearts, streaks, XP and badges. Kids learn ',
    paragraphSubjects: 'Math, Science, English and Arabic',
    paragraphRest: ' by playing — you watch them grow.',
    ctaPrimary: 'Create parent account →',
    ctaSecondaryPlay: '▶',
    ctaSecondary: 'Watch demo (2 min)',
    trustRating: '4.9 in App Store',
    trustCoppa: 'COPPA-compliant',
    trustFirstChild: 'Free for first child',
  },

  // In-phone decorative marketing art.
  phone: {
    childName: 'Sami',
    streak: '7',
    continueLearning: 'CONTINUE LEARNING',
    continueSubject: 'Fractions',
    subjectMath: 'Math',
    subjectScience: 'Science',
    subjectArabic: 'Arabic',
    subjectEnglish: 'GB',
  },

  // Floating reward chips.
  chips: {
    xp: '+50 XP',
    badge: 'New badge!',
  },

  // Below-the-fold section stubs (nav anchor targets — kept minimal).
  sections: {
    howItWorksTitle: 'How it works',
    howItWorksBody:
      'Parents create an account and add their children. Each child gets a personal AI tutor that adapts to how they learn — turning Math, Science, English and Arabic into a game of hearts, streaks, XP and badges.',
    subjectsTitle: 'Subjects',
    subjectsBody: 'Four core subjects, one adventure: Math, Science, English and Arabic.',
    forSchoolsTitle: 'For schools',
    forSchoolsBody:
      'Bring Learnexia to your classroom. Get in touch to learn about school plans and dashboards for teachers and administrators.',
    pricingTitle: 'Pricing',
    pricingBody: 'Free for your first child. Simple family pricing as you add more learners.',
  },

  footer: {
    rights: '© Learnexia. Learning, leveled up.',
  },
} as const;
