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
    pill: '✨ POWERED BY AI',
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
    subjectEnglishLabel: 'English',
    subjectEnglish: '🇬🇧',
  },

  // Floating reward chips.
  chips: {
    xp: '+50 XP ⭐',
    badge: 'New badge!',
  },

  // "Why Learnexia" feature grid (web-feature-card.html).
  features: {
    eyebrow: 'Why Learnexia',
    title: 'Learning that feels like play',
    cards: [
      {
        icon: '🤖',
        tone: 'purple',
        title: 'AI tutor that explains',
        body: "Stuck on a problem? Lexi explains it with pictures, examples and patient follow-ups — adapted to your child's grade.",
      },
      {
        icon: '🎮',
        tone: 'orange',
        title: 'Gamified, not gimmicky',
        body: 'Streaks, XP, badges and weekly leagues turn practice into a game your child wants to come back to.',
      },
      {
        icon: '📊',
        tone: 'green',
        title: 'Parents stay in the loop',
        body: 'A simple dashboard shows what each child is learning, where they shine and where they need a hand.',
      },
      {
        icon: '🌍',
        tone: 'purple',
        title: 'Arabic and English, side by side',
        body: 'Switch language any time. Lessons, hints and feedback all speak your child’s language.',
      },
      {
        icon: '🛡',
        tone: 'green',
        title: 'Safe by design',
        body: 'No ads, no chat with strangers, COPPA-aligned. Parents create accounts and add their own children.',
      },
      {
        icon: '⏱',
        tone: 'orange',
        title: 'Five minutes a day',
        body: 'Short, focused sessions build a daily habit — no marathon homework, just steady progress.',
      },
    ],
  },

  // Subjects band (web-subject-band.html). Math/Science/Arabic/English only.
  subjects: {
    title: 'Four subjects. One adventure.',
    grade: 'Grade 1–6 →',
    items: [
      { icon: '🧮', tone: 'indigo', name: 'Math', topics: 'Numbers · Fractions · Geometry' },
      { icon: '🧪', tone: 'green', name: 'Science', topics: 'Plants · States · Space' },
      { icon: '📖', tone: 'orange', name: 'Arabic', topics: 'Reading · Grammar · Poetry' },
      { icon: '🇬🇧', tone: 'purple', name: 'English', topics: 'Phonics · Verbs · Stories' },
    ],
  },

  // Gradient CTA banner (web-cta-banner.html).
  cta: {
    title: 'Ready to start the adventure?',
    subtitle: 'Free for your first child · No credit card required',
    button: 'Create parent account →',
    star: '🌟',
  },

  footer: {
    rights: '© 2026 Learnexia · Made for curious kids',
    links: {
      privacy: 'Privacy',
      terms: 'Terms',
      support: 'Support',
      arabic: 'العربية',
    },
  },
} as const;
