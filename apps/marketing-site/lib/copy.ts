/**
 * Marketing landing copy — locale-keyed source of all user-facing strings on
 * the landing page (mirrors the admin app's `strings.ts` pattern: copy lives
 * in one typed module, never as free-text literals in JSX).
 *
 * COPY.en  — English (LTR)
 * COPY.ar  — Arabic (RTL) ported from the design-system AR kit:
 *            ar-web-nav.html, ar-web-features.html, ar-web-cta.html,
 *            ar-child-card.html, ar-tutor.html and related AR preview cards.
 *            Arabic-Indic numerals (٠–٩) used in COPY.ar where the previews do.
 *
 * getCopy(locale) — pick the right block at render time.
 */

export type Locale = 'en' | 'ar';

const en = {
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
    // Headline is split so "adventure game" can be accent-coloured.
    headlineLead: 'An ',
    headlineAccent: 'adventure game',
    headlineRest: ' your kids will love — that teaches.',
    // Paragraph split so the subject list can be emphasised.
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
        tone: 'purple' as const,
        title: 'AI tutor that explains',
        body: "Stuck on a problem? Lexi explains it with pictures, examples and patient follow-ups — adapted to your child's grade.",
      },
      {
        icon: '🎮',
        tone: 'orange' as const,
        title: 'Gamified, not gimmicky',
        body: 'Streaks, XP, badges and weekly leagues turn practice into a game your child wants to come back to.',
      },
      {
        icon: '📊',
        tone: 'green' as const,
        title: 'Parents stay in the loop',
        body: 'A simple dashboard shows what each child is learning, where they shine and where they need a hand.',
      },
      {
        icon: '🌍',
        tone: 'purple' as const,
        title: 'Arabic and English, side by side',
        body: "Switch language any time. Lessons, hints and feedback all speak your child's language.",
      },
      {
        icon: '🛡',
        tone: 'green' as const,
        title: 'Safe by design',
        body: 'No ads, no chat with strangers, COPPA-aligned. Parents create accounts and add their own children.',
      },
      {
        icon: '⏱',
        tone: 'orange' as const,
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
      { icon: '🧮', tone: 'indigo' as const, name: 'Math', topics: 'Numbers · Fractions · Geometry' },
      { icon: '🧪', tone: 'green' as const, name: 'Science', topics: 'Plants · States · Space' },
      { icon: '📖', tone: 'orange' as const, name: 'Arabic', topics: 'Reading · Grammar · Poetry' },
      { icon: '🇬🇧', tone: 'purple' as const, name: 'English', topics: 'Phonics · Verbs · Stories' },
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
    },
  },

  // Benefits panel (web-benefits-panel.html).
  benefits: {
    glyph: '🎮',
    heading: 'Set up once. Watch them learn forever.',
    items: [
      { icon: '✨', text: "AI-powered explanations tailored to each child's grade" },
      { icon: '📊', text: "Weekly reports show exactly what they've mastered" },
      { icon: '🛡️', text: 'COPPA-compliant — no ads, no DMs, no data resold' },
    ],
  },

  // Activity chart (web-activity-chart.html).
  activityChart: {
    title: 'Daily activity',
    subtitle: 'XP earned per day',
    exportBtn: 'Export CSV',
    days: ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'],
    // Western digits — bar heights come from CHART_VALUES in the component.
    values: ['45', '80', '30', '95', '50', '70', '110'],
  },

  // AI Tutor Bubble (components-tutor.html).
  tutorBubble: {
    name: 'Lexi · AI Tutor',
    msgLead: 'When we compare two numbers, the one with more ',
    msgHighlight: 'tens',
    msgRest: ' is bigger. Want me to show you with blocks?',
    chips: ['Yes, show me', 'Give a hint', 'Skip'],
  },

  // Child Card (mobile-child-card.html).
  childCard: {
    monogram: 'S',
    name: 'Sami',
    grade: 'Grade 3',
    email: 'sami@learnexia.com',
    chevron: '›',
    statLevel: '🧠 Lv 12',
    statXp: '⭐ 1,240',
    statStreak: '🔥 7d',
    statusActive: 'Active today',
    langLabel: 'Language:',
    langValue: '🇬🇧 English',
    viewProgress: 'View progress →',
  },
} as const;

const ar = {
  brand: 'Learnexia',
  brandLogoAlt: 'Learnexia',

  nav: {
    // From ar-web-nav.html
    howItWorks: 'كيف يعمل',
    subjects: 'المواد',
    forSchools: 'للمدارس',
    pricing: 'الأسعار',
    logIn: 'تسجيل الدخول',
    startFree: 'ابدأ مجاناً',
  },

  hero: {
    pill: '✨ مدعوم بالذكاء الاصطناعي',
    headlineLead: 'لعبة ',
    headlineAccent: 'مغامرة',
    headlineRest: ' يحبها أطفالك — وتُعلّمهم.',
    paragraphLead: 'تجمع Learnexia بين معلم ذكاء اصطناعي شخصي وقلوب وسلاسل ونقاط خبرة وشارات. يتعلّم الأطفال ',
    paragraphSubjects: 'الرياضيات والعلوم والإنجليزية والعربية',
    paragraphRest: ' من خلال اللعب — وأنت تراقبهم يكبرون.',
    ctaPrimary: 'أنشئ حساب ولي الأمر ←',
    ctaSecondaryPlay: '▶',
    ctaSecondary: 'شاهد العرض (٢ دقيقة)',
    trustRating: '٤.٩ في App Store',
    trustCoppa: 'متوافق مع COPPA',
    trustFirstChild: 'مجاني للطفل الأول',
  },

  phone: {
    childName: 'سامي',
    streak: '٧',
    continueLearning: 'تابع التعلّم',
    continueSubject: 'الكسور',
    subjectMath: 'الرياضيات',
    subjectScience: 'العلوم',
    subjectArabic: 'العربية',
    subjectEnglishLabel: 'الإنجليزية',
    subjectEnglish: '🇬🇧',
  },

  chips: {
    xp: '+٥٠ نقطة ⭐',
    badge: 'شارة جديدة!',
  },

  // From ar-web-features.html (partial — other cards from pattern)
  features: {
    eyebrow: 'لماذا Learnexia',
    title: 'تعلّم يبدو كاللعب',
    cards: [
      {
        icon: '🤖',
        tone: 'purple' as const,
        title: 'معلم ذكاء اصطناعي يشرح',
        body: 'عَلِق طفلك في مسألة؟ ليكسي يشرحها بالصور والأمثلة وبصبر — مكيّفة لصف طفلك.',
      },
      {
        icon: '🎮',
        tone: 'orange' as const,
        title: 'مُلعَّب بصدق، لا بحيلة',
        body: 'السلاسل ونقاط الخبرة والشارات والدوريات الأسبوعية تحوّل التدريب إلى لعبة يعود لها طفلك.',
      },
      {
        icon: '📊',
        tone: 'green' as const,
        title: 'أولياء الأمور على اطلاع دائم',
        body: 'لوحة تحكم بسيطة تُظهر ما يتعلّمه كل طفل، أين يتألق وأين يحتاج مساعدة.',
      },
      {
        icon: '🌍',
        tone: 'purple' as const,
        title: 'العربية والإنجليزية جنباً إلى جنب',
        body: 'غيّر اللغة في أي وقت. الدروس والتلميحات والتغذية الراجعة كلها تتحدث بلغة طفلك.',
      },
      {
        icon: '🛡',
        tone: 'green' as const,
        title: 'آمن بالتصميم',
        body: 'لا إعلانات، لا دردشة مع غرباء، متوافق مع COPPA. يُنشئ أولياء الأمور الحسابات ويضيفون أطفالهم.',
      },
      {
        icon: '⏱',
        tone: 'orange' as const,
        title: 'خمس دقائق يومياً',
        body: 'جلسات قصيرة ومركّزة تبني عادة يومية — لا واجبات مرهقة، فقط تقدّم مستمر.',
      },
    ],
  },

  subjects: {
    title: 'أربع مواد. مغامرة واحدة.',
    grade: 'الصف ١–٦ ←',
    items: [
      { icon: '🧮', tone: 'indigo' as const, name: 'الرياضيات', topics: 'الأعداد · الكسور · الهندسة' },
      { icon: '🧪', tone: 'green' as const, name: 'العلوم', topics: 'النباتات · الحالات · الفضاء' },
      { icon: '📖', tone: 'orange' as const, name: 'العربية', topics: 'القراءة · القواعد · الشعر' },
      { icon: '🇬🇧', tone: 'purple' as const, name: 'الإنجليزية', topics: 'الصوتيات · الأفعال · القصص' },
    ],
  },

  // From ar-web-cta.html
  cta: {
    title: 'جاهز لبدء المغامرة؟',
    subtitle: 'مجانية للطفل الأول · لا حاجة لبطاقة ائتمان',
    button: 'أنشئ حساب ولي الأمر ←',
    star: '🌟',
  },

  footer: {
    rights: '© ٢٠٢٦ Learnexia · صُنع للأطفال الفضوليين',
    links: {
      privacy: 'الخصوصية',
      terms: 'الشروط',
      support: 'الدعم',
    },
  },

  // Benefits panel — AR translation from design spec §1.3
  benefits: {
    glyph: '🎮',
    heading: 'أعِدّه مرة واحدة. وشاهدهم يتعلمون للأبد.',
    items: [
      { icon: '✨', text: 'شروحات بالذكاء الاصطناعي مصمّمة لمستوى كل طفل' },
      { icon: '📊', text: 'تقارير أسبوعية تُظهر بالضبط ما أتقنوه' },
      { icon: '🛡️', text: 'متوافق مع COPPA — بلا إعلانات، بلا رسائل، بلا بيع للبيانات' },
    ],
  },

  // Activity chart — Arabic-Indic values from design spec §2.2
  activityChart: {
    title: 'النشاط اليومي',
    subtitle: 'النقاط المكتسبة كل يوم',
    exportBtn: 'تصدير CSV',
    days: ['الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت', 'الأحد'],
    // Arabic-Indic digit strings — bar heights always from numeric CHART_VALUES
    values: ['٤٥', '٨٠', '٣٠', '٩٥', '٥٠', '٧٠', '١١٠'],
  },

  // AI Tutor Bubble — from ar-tutor.html + design spec §3.2
  tutorBubble: {
    name: 'ليكسي · المعلم الذكي',
    msgLead: 'عندما نقارن عددين، الأكبر هو العدد الذي يحتوي على ',
    msgHighlight: 'عشرات',
    msgRest: ' أكثر. هل تريد أن أوضح لك ذلك بالمكعّبات؟',
    chips: ['نعم، أرني', 'أعطني تلميحاً', 'تخطي'],
  },

  // Child Card — from ar-child-card.html
  childCard: {
    monogram: 'س',
    name: 'سامي',
    grade: 'الصف ٣',
    email: 'sami@learnexia.com',
    chevron: '‹',
    statLevel: '🧠 المستوى ١٢',
    statXp: '⭐ ١٬٢٤٠',
    statStreak: '🔥 ٧ أيام',
    statusActive: 'نشط اليوم',
    langLabel: 'اللغة:',
    langValue: '🇸🇦 العربية',
    viewProgress: 'عرض التقدم ←',
  },
} as const;

export const COPY = { en, ar } as const;

/** Pick the copy block for the given locale. */
export function getCopy(locale: Locale): typeof COPY[Locale] {
  return COPY[locale];
}

/**
 * Backward-compat alias: LANDING_COPY was the previous export name.
 * All existing imports that haven't yet been migrated to getCopy() can still
 * use LANDING_COPY (it always returns the EN block).
 * @deprecated Prefer getCopy(locale).
 */
export const LANDING_COPY = COPY.en;
