/**
 * i18n resources — ar (Arabic-first) + en.
 *
 * Namespaces: `common`, `auth`, `onboarding`, `parent`, `child`, `nav`. All
 * P1-09 auth & onboarding copy slots from the Design Spec live here.
 * `defaultNS` is `common`. The `en` object is the canonical shape; `ar` mirrors
 * it exactly so no key is missing in either locale (the QA pass verifies this).
 */

export const NAMESPACES = [
  'common',
  'auth',
  'onboarding',
  'parent',
  'child',
  'nav',
  'quiz',
  'xp',
  'streak',
  'hearts',
  'badges',
  'missions',
  'league',
  'events',
] as const;
export type Namespace = (typeof NAMESPACES)[number];

export const defaultNS: Namespace = 'common';

export const en = {
  common: {
    appName: 'Learnexia',
    ok: 'OK',
    cancel: 'Cancel',
    retry: 'Retry',
    loading: 'Loading…',
    done: 'Done',
    error: {
      serverError: 'Something went wrong. Please try again.',
      networkError:
        'No internet connection. Check your connection and try again.',
    },
    restartPrompt: {
      title: 'Restart to change language',
      body: 'The app needs to restart to apply the new language settings.',
      confirm: 'Restart',
      cancel: 'Later',
    },
    prefs: {
      switchToLight: 'Switch to light mode',
      switchToDark: 'Switch to dark mode',
      switchToArabic: 'العربية',
      switchToEnglish: 'English',
      language: 'Language',
      theme: 'Theme',
      themeDark: 'Night',
      themeLight: 'Light',
    },
    splash: {
      subtitle: 'AI Learning Adventure Begins',
      loading: 'Loading… ⚡',
      poweredBy: 'POWERED BY AI',
      tagline: '✦ Family Learning ✦',
    },
  },
  auth: {
    signIn: 'Sign in',
    signOut: 'Sign out',
    userName: 'Username',
    password: 'Password',
    showPassword: 'Show password',
    hidePassword: 'Hide password',
    sessionExpired: 'Your session expired. Please sign in again.',
    errors: {
      userNameRequired: 'Username is required',
      passwordRequired: 'Password is required',
    },
    register: {
      step: 'Step 1 of 2',
      title: 'Create your parent account',
      subtitle: "You'll add your children's accounts next.",
      parentOnlyTitle: 'Parent / Guardian only',
      parentOnlyBody:
        "Children can't self-register. You'll create their accounts in the next step.",
      labelFullName: 'Full name',
      labelCountry: 'Country',
      countryPlaceholder: 'Select country',
      labelEmail: 'Email',
      labelPassword: 'Password',
      passwordHelper: 'At least 6 characters',
      strength: {
        weak: 'Weak',
        fair: 'Fair',
        good: 'Good',
        strong: 'Strong',
        a11y: 'Password strength: {{label}}',
      },
      termsLabel:
        "I'm a parent or legal guardian and I agree to the {{terms}} and {{privacy}}, including consent to create accounts for my children.",
      termsLink: 'Terms',
      privacyLink: 'Privacy Policy',
      termsA11y:
        "I'm a parent or legal guardian and I agree to the Terms and Privacy Policy, including consent to create accounts for my children.",
      submitButton: 'Continue → Add Children',
      progressA11y: 'Registration progress: step 1 of 2',
      feature: {
        title: 'Set up once. Watch them learn forever.',
        bullet1: "AI-powered explanations tailored to each child's grade",
        bullet2: "Weekly reports show exactly what they've mastered",
        bullet3: 'Daily missions keep them coming back without nagging',
        bullet4: 'COPPA-compliant — no ads, no DMs, no data resold',
      },
      haveAccount: 'Already have an account?',
      signInLink: 'Sign in',
      backToSignIn: 'Back to Sign in',
      captcha: {
        a11y: 'Security check',
        loadError:
          "The security check couldn't load. Please refresh the page and try again.",
      },
      errors: {
        duplicateEmail: 'An account with this email already exists.',
        weakPassword:
          'Password must be at least 6 characters with uppercase, lowercase, number, and special character.',
        invalidEmail: 'Please enter a valid email address.',
        countryRequired: 'Please select your country.',
        termsRequired: 'Please accept the Terms to continue.',
        captchaFailed: 'Security check failed. Please try again.',
      },
    },
    roleSelect: {
      title: 'Welcome to Learnexia',
      subtitle: "Who's signing in?",
      parentLabel: 'Parent',
      parentSub: "Track your child's progress",
      studentLabel: 'Student',
      studentSub: 'Learn, play, level up',
      footnote:
        "Children log in with the email a parent assigned — they never create their own account.",
      parentA11y: 'Sign in as Parent',
      studentA11y: 'Sign in as Student',
    },
    login: {
      eyebrow: 'Log in',
      title: 'Welcome back',
      /** Student subtitle (existing key repurposed — was generic, now student-only). */
      subtitle: 'Log in to keep your streak alive 🔥',
      /** Parent subtitle (NEW — Batch A). */
      subtitleParent: "Sign in to follow your children's progress",
      /** Role badge label keys (NEW — Batch A, two literal keys to avoid interpolated-noun gender issues). */
      signingInAsParent: 'Signing in as Parent',
      signingInAsStudent: 'Signing in as Student',
      /** "Change" link on the role badge (NEW — Batch A). */
      change: 'Change',
      /** Student no-account notice (NEW — Batch A). */
      studentNoAccountTitle: 'Need an account?',
      studentNoAccountBody: 'Ask a parent to add you.',
      labelUsername: 'Email',
      labelPassword: 'Password',
      showPassword: 'Show',
      hidePassword: 'Hide',
      rememberMe: 'Remember me',
      forgotPassword: 'Forgot password?',
      submitButton: 'Log in →',
      orDivider: 'Or',
      orContinueWith: 'Or continue with',
      socialGoogle: 'Google',
      socialApple: 'Apple',
      socialMicrosoft: 'Microsoft',
      newParent: 'New to Learnexia?',
      createAccount: 'Create parent account',
      brand: {
        title: 'Welcome back to\nthe adventure.',
        body: 'Pick up your streak, keep your hearts full, and watch your kids fly through new skills.',
        socialProof: '240,000+ kids learning today',
      },
      errors: {
        // Anti-enumeration (P1-13): credential failures are ALWAYS this one
        // uniform message — never a "no account found" variant.
        invalidCredentials: 'Incorrect username or password.',
        accountLocked:
          'Too many failed attempts. Your account is temporarily locked — please try again later.',
        accountDeactivated:
          'This account has been deactivated. Please contact support for help.',
        socialFailed: "Couldn't sign in with Google. Please try again.",
      },
      social: {
        loading: 'Signing in with Google…',
      },
    },
    forgotPassword: {
      eyebrow: 'Reset password',
      title: 'Forgot your password?',
      subtitle: "Enter your email and we'll send you a reset link.",
      labelEmail: 'Email',
      submitButton: 'Send reset link →',
      successTitle: 'Check your email',
      successBody:
        "If an account exists for that email, we've sent a password reset link.",
      backToSignIn: 'Back to Sign in',
      errors: {
        invalidEmail: 'Please enter a valid email address.',
        generic: 'Something went wrong. Please try again.',
      },
    },
    resetPassword: {
      eyebrow: 'Set a new password',
      title: 'Create a new password',
      subtitle: 'Choose a strong password for',
      labelEmail: 'Email',
      labelNewPassword: 'New password',
      labelConfirmPassword: 'Confirm password',
      passwordHelper: 'At least 6 characters.',
      submitButton: 'Reset password →',
      successBody: 'Your password has been reset. Please sign in.',
      requestNewLink: 'Request a new link',
      errors: {
        weakPassword:
          'Password must be at least 6 characters with uppercase, lowercase, number, and special character.',
        mismatch: "Passwords don't match.",
        tokenInvalid: 'This reset link is invalid. Please request a new one.',
        tokenExpired:
          'This reset link has expired. Please request a new one.',
        generic: 'Something went wrong. Please try again.',
      },
    },
  },
  onboarding: {
    stepLabel: 'Step {{current}} of {{total}}',
    back: 'Back',
    close: 'Close',
    editChild: 'Edit child',
    removeChild: 'Remove child',
    saveChanges: 'Save Changes',
    addChild: {
      title: 'Add your children',
      subtitle:
        'Add each of your children. You can add more than one.',
      // Continue button on the new My-Children-style onboarding screen.
      continue: 'Continue',
      // Section label above the already-added children list.
      listLabel: 'Children added ({{count}})',
      // Empty-state hint shown when no children have been added yet.
      emptyHint: 'Add your first child to continue',
      // Keys below are kept for AddChildForm / EditChildSheet (still used by
      // the dashboard) and for backward-compat. Do not remove this PR.
      labelName: "Child's full name",
      labelEmail: 'Login email',
      labelPassword: 'Password',
      labelGrade: 'Grade',
      labelLanguage: 'Preferred language',
      labelLearningLanguage: 'Learning language',
      labelAppLanguage: 'App language',
      languageGroupLabel: 'Languages',
      labelCountry: 'Country',
      gradePlaceholder: 'Select grade',
      languagePlaceholder: 'Select language',
      learningLanguagePlaceholder: 'Choose learning language',
      learningLanguageHelper:
        'The language your child will study Math & Science in.',
      appLanguageHelper:
        "The language for buttons, menus, and messages. We've matched it to the learning language — you can change it.",
      addToListButton: 'Add Child to List',
      submitButton: 'Add {{count}} Child(ren) and Continue',
      added: 'Added!',
      partialFailureBanner:
        'Some children could not be added. Please fix the errors and try again.',
      errors: {
        nameRequired: "Child's name is required.",
        languageRequired: 'Please select a language.',
        learningLanguageRequired: 'Please choose a learning language.',
        countryRequired: 'Country is required.',
      },
    },
    grade: {
      1: 'Grade 1',
      2: 'Grade 2',
      3: 'Grade 3',
      4: 'Grade 4',
      5: 'Grade 5',
      6: 'Grade 6',
    },
    language: {
      ar: 'Arabic / عربي',
      en: 'English / الإنجليزية',
    },
    child: {
      errors: {
        duplicateEmail: 'This email is already in use.',
        invalidGrade: 'Please choose a grade between 1 and 6.',
        weakPassword: 'Password does not meet security requirements.',
        generic: 'Could not add this child. Please try again.',
      },
    },
    complete: {
      title: "You're all set!",
      body: 'Your children have been added. Start exploring the learning world!',
      cta: 'Go to Dashboard',
    },
  },
  parent: {
    nav: {
      myChildren: 'My Children',
      /** Shorter tab-bar label for the Children tab (PMTabBar spec). */
      children: 'Children',
      overview: 'Overview',
      reports: 'Reports',
      /** Helper Energy tab (PMTabBar spec; full IAP surface is a later batch). */
      energy: 'Energy',
      activity: 'Activity',
      subjects: 'Subjects',
      settings: 'Settings',
      logout: 'Log out',
      sectionLabel: 'Parent menu',
      /** a11y label for the parent tab bar tablist container. */
      barLabel: 'Parent navigation',
    },
    // TODO(P5): weekly-XP delta is server analytics — static stub copy for now.
    xpWidget: {
      eyebrow: 'This week',
      value: '+{{xp}} XP',
      delta: 'Up {{percent}}% from last week',
    },
    childSelector: {
      label: 'Active child',
      meta: 'Grade {{grade}} · Level {{level}}',
      switchChild: 'SWITCH CHILD',
      addChild: 'Add a child',
    },
    myChildren: {
      title: 'My Children',
      subtitle: '{{count}} children linked to your account',
      sectionLabel: 'Children ({{count}})',
      empty: 'No children linked yet.',
      linkButton: 'Link existing child',
      loadError: 'Could not load your children. Tap to retry.',
      periodLabel: 'Reporting period',
      periodThisWeek: 'This week',
      sendReport: 'Send Report',
      pickChild: 'Pick a child to view their progress',
      addChild: '+ Add Child',
      activeToday: 'Active today',
      inactive: 'Inactive',
      statLevel: 'Level',
      statLevelShort: 'Lv',
      statXp: 'XP',
      statStreak: 'Streak',
      statStreakValue: '{{n}}d',
      statEnergy: 'Energy',
      mastery: 'Mastery',
      weakest: 'Weakest:',
      viewDashboard: 'View dashboard →',
      addCardTitle: 'Add a child',
      addCardSubtitle: 'Set their grade, language, and login email',
      topics: {
        fractions: 'Fractions',
        letters: 'Letters',
        geometry: 'Geometry',
        reading: 'Reading',
        numbers: 'Numbers',
      },
      editChild: 'Edit {{name}}',
      editError: "Couldn't save changes. Please try again.",
      editSuccess: 'Changes saved.',
    },
    familySummary: {
      eyebrow: 'This Week · Combined',
      headline: 'Your family is on a roll',
      subline: '{{learners}} active learners · {{lessons}} lessons completed',
      totalXp: 'Total XP',
      lessons: 'Lessons',
      bestStreak: 'Best Streak',
      badgesEarned: 'Badges Earned',
      childrenCluster: '{{count}} children',
    },
    overview: {
      title: "{{name}}'s progress",
      // TODO(P5-05): real week range from analytics; placeholder copy for now.
      dateRange: 'Mon, Nov 18 → Sun, Nov 24',
      periodLabel: 'Reporting period',
      periodThisWeek: 'This week',
      sendReport: 'Send Report',
      empty: 'Add a child to see their weekly progress.',
      loadError: 'Could not load progress. Tap to retry.',
      kpi: {
        timeLearning: 'Time learning',
        timeDelta: '+{{value}} vs last week',
        xpEarned: 'XP earned',
        /** GAP-8 fix: xpDelta is ABSOLUTE (not a percent). */
        xpDelta: '+{{value}} XP this week',
        lessonsDone: 'Lessons done',
        lessonsDelta: '+{{value}} vs last week',
        streak: 'Day streak',
        streakDelta: '+{{value}} vs last week',
      },
      dailyActivity: {
        title: 'Daily activity',
        subtitle: 'XP earned per day',
        exportCsv: 'Export CSV',
        placeholder: 'Activity chart — coming soon',
        /** P5-05: empty state when all xpEarned === 0. */
        empty: 'No activity yet this week',
      },
      subjectMastery: {
        title: 'Subject mastery',
        subtitle: 'Last 7 days',
        /** P5-05: empty state when no mastery data yet. */
        empty: 'Complete lessons to see mastery',
      },
      subjects: {
        math: 'Math',
        science: 'Science',
        arabic: 'Arabic',
        english: 'English',
      },
      focusAreas: {
        title: 'Areas to focus on',
        subtitle: 'Topics where {{name}} is still building confidence',
        seeAll: 'See all',
        /** P5-05 FIX: shown when no weak areas are detected (new child, zero-state). */
        empty: 'No focus areas right now — keep going!',
      },
      recommendations: {
        title: 'Recommendations from Lexi',
        subtitle: 'Personalised suggestions for this week',
        /** P5-05 real wiring: shown when items[] is empty (cold-start / no weak areas yet). */
        empty: 'No recommendations yet — keep learning to unlock personalised tips!',
      },
    },
    // Per-child Child Overview drill-down (Batch C; (parent)/child/[id]).
    childOverview: {
      title: "{{name}}'s progress",
      back: 'Go back',
      notFound: "We couldn't find that child.",
      backToChildren: 'Back to My Children',
      kpi: {
        time: 'Time',
        xp: 'XP earned',
        lessons: 'Lessons',
      },
      fullReport: '📈 Full report',
      energy: '⚡ Energy',
    },
    // A4 Reports page (CO-FE-1 / P1-11-FE-9; charts deferred to P5-05).
    reports: {
      title: "{{name}}'s reports",
      subtitle: 'Detailed breakdown · Switch child in header',
      range: {
        label: 'Report range',
        week: 'This week',
        month: 'This month',
        all: 'All time',
      },
      send: 'Send Report',
      // L6: stub toast — real delivery ships with P5-04 / Phase-9 email.
      sendToast: 'Report sent to your email — coming soon!',
      kpi: {
        time: 'Time learning',
        xp: 'XP earned',
        lessons: 'Lessons done',
        accuracy: 'Avg. accuracy',
        noActivity: 'No activity yet',
        // G-2: no parent-readable child XP endpoint yet.
        xpComingSoon: 'Coming with full reports',
      },
      delta: {
        vsLastWeek: '{{value}}% vs last week',
      },
      mastery: {
        title: 'Skills mastery',
        sub: 'Mastery levels across subjects',
        // G-1: no per-subject mastery endpoint yet (P5-05 aggregate).
        empty: 'Mastery appears after the first lessons',
      },
      charts: {
        xpTitle: 'Last 20 days · XP earned',
        /** P5-05 NEW: subtitle for the 20-day chart panel. */
        xpSubtitle: 'Today highlighted in indigo',
        todTitle: 'Time of day',
        /** P5-05 NEW: subtitle for time-of-day chart panel ({{name}} = child name). */
        todSubtitle: 'When {{name}} learns best',
        /** P5-05 NEW: peak-focus insight tip ({{start}} / {{end}} = Latin hour labels). */
        peakInsight: 'Peak focus is {{start}}–{{end}} — great time for new material',
        /**
         * P5-05 FIX: peak-focus insight for named bucket (replaces peakInsight with
         * hour-range labels; the real DTO has named buckets, not hourly bars).
         * {{bucket}} = translated bucket name (Morning / Afternoon / Evening / Night).
         */
        peakBucketInsight: '{{bucket}} is the peak focus time — great for new material',
        /** P5-05 NEW: empty state caption for 20-day chart when no data. */
        noData: 'No data yet — check back soon',
        /** P5-05 NEW: empty state caption for time-of-day chart. */
        todEmpty: 'No session data yet',
        comingSoon: 'Charts coming soon',
        /** P5-05 FIX: Named time-of-day bucket labels (4 buckets from backend). */
        tod: {
          morning: 'Morning',
          afternoon: 'Afternoon',
          evening: 'Evening',
          night: 'Night',
        },
      },
      empty: {
        firstWeek:
          "{{name}} hasn't started yet — their first lesson will light this page up!",
        addChild: 'Add a child to see reports',
      },
      loadError: 'Could not load reports. Tap to retry.',
      // A5 parent surface — "Recent attempts" panel inside Reports.
      attempts: {
        title: 'Recent attempts',
        sub: "{{name}}'s latest quizzes",
        empty: 'No attempts in this period',
        showAll: 'Show all',
        showLess: 'Show less',
        row: {
          // G-1 (A5): the DTO has lessonId only — localized fallback.
          lessonFallback: 'Lesson {{n}}',
          today: 'Today',
          yesterday: 'Yesterday',
          completed: 'Completed',
          inProgress: 'Not finished',
          hints_one: '💡 {{count}} hint',
          hints_other: '💡 {{count}} hints',
        },
      },
    },
    settings: {
      title: 'Settings',
      subtitle: 'Manage your account and preferences',
      thisWeekEyebrow: 'This week',
      thisWeekXp: '+{{value}} XP',
      thisWeekDelta: 'Up {{value}}% from last week',
      tabs: {
        navLabel: 'Settings sections',
        profile: 'Profile',
        notifications: 'Notifications',
        linkedChildren: 'Linked children',
        security: 'Security',
        billing: 'Plan & billing',
        language: 'Language & region',
      },
      profile: {
        title: 'Profile',
        subtitle: 'This is how Learnexia knows you',
        uploadPhoto: 'Upload photo',
        changePhoto: 'Change photo',
        removePhoto: 'Remove',
        fullName: 'Full name',
        email: 'Email',
        emailLockedHelper: "Your email is your sign-in and can't be changed here.",
        phone: 'Phone',
        country: 'Country',
        countryPlaceholder: 'Select country',
        cancel: 'Cancel',
        save: 'Save changes',
        loading: 'Loading your profile…',
        saveSuccess: 'Your profile has been saved.',
        saveError: "Couldn't save your profile. Please try again.",
        avatar: {
          helper: 'PNG or JPG, up to 5 MB',
          uploading: 'Uploading your photo…',
          uploadSuccess: 'Photo updated.',
          uploadError: "Couldn't upload that photo. Please try again.",
          tooLarge: 'That image is too large (max 5 MB).',
          wrongType: 'Please choose a PNG or JPG image.',
          removeSuccess: 'Photo removed.',
          removeError: "Couldn't remove the photo. Please try again.",
          uploadZoneTitle: 'Upload a photo',
          uploadZoneTitleChange: 'Change photo',
          uploadZoneSub: 'PNG or JPG · or pick a color below',
        },
      },
      language: {
        title: 'Language & region',
        subtitle: "Affects your dashboard, not your children's apps",
        languageLabel: 'Language',
        regionLabel: 'Region',
        regionPlaceholder: 'Select region',
        saveSuccess: 'Language preference saved.',
        save: 'Save',
        cancel: 'Cancel',
        regionUiOnlyNote: 'Region preference is saved locally only.',
      },
      comingSoon: {
        // TODO(P2-12): Notifications / Linked children / Security / Plan & billing.
        title: 'Coming soon',
        body: 'This section is on its way. Check back soon.',
      },
      notifications: {
        title: 'Notifications',
        subtitle: 'Choose what we ping you about',
        category: {
          weeklyReport: {
            label: 'Weekly report',
            helper: "A weekend recap of your child's progress",
          },
          streakAtRisk: {
            label: 'Streak at risk',
            helper: "Heads-up before today's streak breaks",
          },
          productAnnouncement: {
            label: 'Product announcements',
            helper: 'New features and seasonal events',
          },
          achievement: {
            label: 'Achievements',
            helper: 'Badges, level-ups, and big wins',
          },
        },
        channel: {
          email: 'Email',
          push: 'Push',
        },
        saveSuccess: 'Saved. Your preferences are up to date.',
        saveError: "We couldn't save that. Try again.",
      },
      linkedChildren: {
        title: 'Linked children',
        subtitle: 'Manage who is on your account',
        addChild: 'Add Child',
        emptyTitle: 'No children yet',
        emptyBody: 'Link your first child to start tracking their progress.',
        edit: 'Edit',
        unlink: 'Unlink',
        unlinkConfirmTitle: 'Unlink {{name}}?',
        unlinkConfirmBody: "They'll lose access to their progress on your account.",
        unlinkLastParentError: "You can't unlink the only parent on this child's account.",
        updateSuccess: 'Saved.',
        updateError: "We couldn't save that. Try again.",
        unlinkSuccess: '{{name}} has been unlinked.',
        unlinkError: "We couldn't unlink. Try again.",
        fullName: 'Full name',
        grade: 'Grade',
        language: 'Language',
        country: 'Country',
        countryPlaceholder: 'Choose a country',
        gradeOption: {
          1: 'Grade 1',
          2: 'Grade 2',
          3: 'Grade 3',
          4: 'Grade 4',
          5: 'Grade 5',
          6: 'Grade 6',
        },
        learningLanguage: {
          rowLabel: 'Learning language',
          change: 'Change',
          pickerLabel: 'Medium of instruction (Math & Science)',
          pickerHelper: 'The language your child studies Math and Science in. This is separate from the app language.',
          changeCta: 'Change Language',
          noChange: 'This is already the learning language.',
          confirm: {
            title: 'Reset Math & Science Progress?',
            fromTo: '{{from}} → {{to}}',
            willReset: 'Your child\'s Math and Science progress will be reset. They start fresh in those two subjects.',
            willKeep: 'Arabic, English, XP, streak and badges are kept.',
            rareNote: 'Only do this at the start of a school year.',
            ack: 'I understand Math and Science progress will be reset.',
            cta: 'Reset & Change',
          },
          success: 'Done. {{name}} now studies Math and Science in {{language}}, and that progress was reset for a fresh start.',
          error: {
            forbidden: 'You can only change the learning language for your own children.',
            confirmMissing: 'Please confirm the reset before changing the language.',
            generic: 'Something went wrong. Please try again.',
          },
        },
      },
      security: {
        title: 'Security',
        subtitle: 'Manage your password and sessions',
        currentPassword: 'Current password',
        newPassword: 'New password',
        confirmPassword: 'Confirm new password',
        save: 'Update Password',
        saveSuccess: 'Password updated. Other sessions have been signed out.',
        saveErrorPolicy: "Your new password doesn't meet our policy.",
        saveErrorCurrent: "That current password isn't right. Try again.",
        mismatchError: "The two new passwords don't match.",
        sameAsCurrent: "New password can't match your current one.",
        sessionsTitle: 'Active sessions',
        sessionsSubtitle: 'Devices currently signed in to your account',
        signOutOthers: 'Sign Out Other Sessions',
        signOutOthersSuccess: 'Signed out {{count}} other sessions.',
        sessionActive: 'Active',
        sessionExpired: 'Expired',
        expiresAt: 'Expires {{date}}',
        noActiveSessions: 'No active sessions.',
      },
      billing: {
        title: 'Plan & billing',
        subtitle: 'Your current plan and upgrade options',
        planLabel: 'Plan',
        statusActive: 'Active',
        statusInactive: 'Inactive',
        manage: 'Manage Subscription',
        managePending: 'Subscription management coming soon.',
        upgradeComingSoon: "You're on the Free plan. Paid upgrades land soon — we'll let you know.",
        planName: {
          free: 'Free',
          pro: 'Pro',
        },
      },
    },
    addChildModal: {
      title: 'Add a child',
      subtitle: "They'll log in with the email you set",
      labelName: "Child's name",
      labelEmail: 'Login email',
      labelPassword: 'Login password',
      labelGrade: 'Grade',
      labelAppLanguage: 'App language',
      labelLearningLanguage: 'Learning language',
      labelCountry: 'Country',
      countryPlaceholder: 'Select country',
      learningLanguageHelper: 'Math & Science are taught in this language',
      photoUpload: 'Upload a photo',
      photoSub: 'PNG or JPG · or pick a color below',
      cancel: 'Cancel',
      ctaDefault: 'Add child',
      ctaWithName: 'Add {{name}} →',
      flagAr: 'AR',
      flagEn: 'EN',
      errors: {
        nameRequired: "Child's name is required.",
        emailRequired: 'Login email is required.',
        passwordRequired: 'Password is required.',
        gradeRequired: 'Please select a grade.',
        languageRequired: 'Please select a language.',
        learningLanguageRequired: 'Please select a learning language.',
        countryRequired: 'Country is required.',
        generic: 'Could not add child. Please try again.',
      },
    },
    childSwitcher: {
      addChild: '+ Add child',
      noChildren: 'Add a child',
      metaLabel: 'Grade {{grade}} · Level {{level}}',
    },
    linkChild: {
      title: 'Link a Child',
      explanation:
        'Enter the email address of an existing Learnexia child account to link it to your family.',
      labelEmail: "Child's email address",
      submitButton: 'Link Child',
      successTitle: 'Child linked!',
      doneButton: 'Done',
      errors: {
        notFound: 'No child account found with this email.',
        alreadyLinked: 'This child is already linked to your account.',
      },
    },
    dashboard: {
      title: 'Dashboard coming soon!',
      body: 'Your children are all set. Learning adventures are on their way.',
      viewChildren: 'View my children',
    },
    // AccountMenu — avatar-triggered dropdown (post-login parent header).
    account: {
      /** aria-label for the trigger avatar button. */
      menuLabel: 'Account menu',
      /** Section heading for the language segmented control. */
      languageLabel: 'Language',
      /** Section heading for the theme segmented control. */
      themeLabel: 'Theme',
      /** "Night" segment — maps to the dark theme. */
      themeNight: 'Night',
      /** "Light" segment — maps to the light theme. */
      themeLight: 'Light',
    },
    // Helper Energy screen (Batch D). ⚡ Energy = AI-helper fuel (teal), kept
    // distinct from ❤️ Hearts (lives, rose). IAP buy-flow is a gated stub.
    energy: {
      title: 'Helper Energy',
      subtitle: 'Fuel for your child\'s AI study helpers',
      balance: {
        heading: 'Energy left this month',
        // {{used}} / {{total}} stay Latin (technical), e.g. "180 / 300".
        ofTotal: 'of {{total}}',
        reset: 'Resets in {{days}}',
        resetDays_one: '{{value}} day',
        resetDays_other: '{{value}} days',
        dailyCap: '{{value}}/day cap',
        a11y: 'Energy balance: {{balance}} of {{total}} credits left this month.',
      },
      meters: {
        heading: 'Two separate meters',
        energyName: 'Energy',
        energyDesc: '= AI-helper fuel (this page)',
        heartsName: 'Hearts',
        heartsDesc: '= lives in practice',
      },
      usage: {
        heading: 'Helpers used this week',
        hints: 'Hints',
        explain: 'Explain',
        deep: 'Deep',
        practice: 'Practice',
      },
      topUp: {
        // {{credits}} is localized, the ⚡ glyph is fixed.
        packTitle: '+{{credits}} ⚡',
        packSubtitle: 'Top-up pack · added instantly',
        buy: 'Buy {{credits}} credits',
        // Gated IAP stub — surfaced when the disabled-style CTA is tapped.
        comingSoon: 'In-app purchases are coming soon.',
        priceNote: 'Children never see prices — you manage energy.',
      },
    },
    // Activity timeline (Batch D): filter chips + event rows. Stub feed.
    activity: {
      title: 'Activity',
      subtitle: 'Recent moments across your children',
      filters: {
        navLabel: 'Filter activity',
        all: 'All',
        badges: '🏅 Badges',
        energy: '⚡ Energy',
        alerts: '🔔 Alerts',
      },
      // Event action text — the child name is rendered (bold) separately by the
      // row, so these are the action half only. `{{value}}` carries the
      // locale-formatted digits (Eastern-Arabic in AR); `count` drives plurals.
      events: {
        badgeEarned: 'earned a new badge',
        levelUp: 'leveled up to level {{value}}',
        lessonCompleted: 'completed a lesson · {{value}}/5 correct',
        energyUsed: 'used {{value}} AI helpers in a lesson',
        energyLow: 'is low on energy · {{value}} ⚡ left',
        streakReached: 'reached a {{value}}-day streak',
        inactive: 'has been inactive for {{value}} days',
        // Composed a11y label per row: "<name> <action>, <time>".
        a11y: '{{name}} {{action}}, {{time}}',
      },
      // Relative time labels — `{{value}}` carries locale-formatted digits.
      time: {
        justNow: 'Just now',
        minutes_one: '{{value}} min ago',
        minutes_other: '{{value}} min ago',
        hours_one: '{{value}} hr ago',
        hours_other: '{{value}} hrs ago',
        days_one: '{{value}} day ago',
        days_other: '{{value}} days ago',
      },
      empty: {
        title: 'Nothing here yet',
        body: 'No activity matches this filter.',
      },
    },
  },
  child: {
    home: {
      greeting: 'Hi, {{childName}}!',
      welcomeBack: 'Welcome back',
      gradeCaption: 'Grade {{grade}}',
      subtitle: 'Your adventure is coming soon!',
      mascotMessage: "I'm Lexi! I'll be your learning guide.",
      mascotSender: 'Lexi · Guide',
      continue: {
        eyebrow: 'Pick up where you left off',
        eyebrowReplay: 'Replay last lesson',
        cta: 'Continue',
        replayCta: 'Replay',
      },
      continueA11y: 'Continue lesson {{lesson}}',
      continueHintA11y: 'Opens the lesson player',
      mission: {
        eyebrow: "Today's mission",
        startCta: 'Start Mission',
      },
      yourSubjects: 'Your subjects',
      welcomeEmpty: 'Welcome! Tap a subject to start.',
      errorRetry: "Couldn't load your dashboard. Try again",
      errorRetryCta: 'Retry',
      statsA11y: '{{hearts}} hearts, {{streak}} day streak, {{xp}} XP',
      stats: {
        hearts: 'Hearts',
        streak: 'Streak',
        xp: 'XP this week',
      },
      practiceMode: 'Practice Mode',
      practiceModeA11y: 'Practice Mode active — wrong answers cost no hearts',
      boss: 'Boss',
      leagueTier: {
        bronze: 'Bronze',
        silver: 'Silver',
        gold: 'Gold',
        diamond: 'Diamond',
      },
      leaguePreview: {
        rankLabel: 'Rank {{rank}} of {{total}}',
        rankUnknown: 'Earning your rank…',
        a11y: '{{tier}} league, rank {{rank}} of {{total}}, {{xp}} XP this week',
        hintA11y: 'Opens your league standings',
      },
      // 3c (B-int) — gamification entry points on the Home dashboard
      // (Design Spec P4-08 §10: HUD-chip tap-throughs + event banner + activity link).
      statsNav: {
        hearts: '{{value}} hearts — see details',
        streak: '{{value}} day streak — see details',
        xp: '{{value}} XP this week — see details',
        freeze: '{{value}} streak freezes — see events',
      },
      events: {
        seeAll: 'See all →',
        bannerA11y: '{{name}} — {{multiplier}} event. Opens events and challenges',
      },
      activity: {
        seeAll: 'See all →',
        a11y: 'My activity — see all your lesson attempts',
      },
      // 3c (B-int) — dashboard celebration popups (useDashboardDiff; level-up
      // and mission-complete copy reuses xp.levelUp.* / missions.complete.*).
      celebrate: {
        cta: 'Keep Going',
        badgeTitle: 'New Badge Unlocked!',
        badgeA11y: 'New badge unlocked: {{name}}. You earned {{xp}} XP',
        streakTitle: 'Streak Milestone!',
        streakSubtitle_one: '{{value}}-day streak — keep it alive!',
        streakSubtitle_other: '{{value}}-day streak — keep it alive!',
        streakA11y_one: 'Streak milestone! {{value}} day streak. You earned {{xp}} XP',
        streakA11y_other: 'Streak milestone! {{value}} days streak. You earned {{xp}} XP',
      },
    },
    subjects: {
      title: 'Subjects',
      gradeLabel: 'Grade {{grade}}',
      masteryLabel: '{{percent}}% mastered',
      empty: 'Coming soon — no lessons yet',
      subjectNotFound: 'Subject not found',
      errorRetry: "Couldn't load. Try again",
      backToSubjects: 'Back to Subjects',
      signOut: 'Sign out',
      tabs: {
        lessons: 'Lessons',
        tree: 'Skill Tree',
      },
      lessons: {
        unitLabel: 'Unit {{n}}',
        unitsCount: '{{units}} units · {{lessons}} lessons',
        meta: {
          minutes: '{{n}} min',
          questions: '{{n}} questions',
          xp: '+{{n}} XP',
        },
        tagCompleted: 'Completed',
        tagLocked: 'Locked',
        emptyUnit: 'Coming soon',
      },
      whyLocked: {
        eyebrow: 'Locked',
        title: 'Why is this locked?',
        intro: 'Finish these to unlock:',
        needLine: "You're at {{currentAccuracy}}% — need {{requiredAccuracy}}%",
        generic: 'Finish the previous lesson first',
        cta: 'Got it!',
      },
    },
    skillTree: {
      unitOf: 'Unit {{current}} of {{total}}',
      mastery: 'Mastery {{percent}}%',
      conceptEyebrow: 'Concept · {{name}}',
      subInProgress: 'In progress',
      subCompleted: 'Mastered',
      subLocked: 'Locked',
      subBossChallenge: 'Boss Challenge',
      subBossBeaten: 'Boss Beaten',
      bossLabel: 'Boss',
      a11y: {
        lockedHint: 'double tap to see what unlocks this',
      },
    },
    lessons: {
      intro: {
        eyebrow: 'Lesson',
        startCta: 'Start lesson',
        aiBubbleFallback: "I'll walk you through this — tap Start when you're ready.",
        noQuestions: 'This lesson has no quiz yet — come back later',
        noQuestionsSub: "Come back later — we're adding more soon.",
        back: 'Back to lessons',
        errorRetry: "Couldn't load lesson. Try again",
        notFound: 'Lesson not found',
      },
      a11y: {
        optionState: {
          selected: 'selected',
          correct: 'correct answer',
          incorrect: 'incorrect',
        },
        feedback: {
          correctRegion: 'Answer correct. Great job!',
          incorrectRegion: 'Answer incorrect. Correct answer is {{answer}}',
        },
      },
    },
    quiz: {
      questionOf: 'Question {{current}} of {{total}}',
      hint: 'Hint',
      hintComingSoon: 'Hint coming in v2',
      submit: 'Check answer',
      next: 'Next',
      true: 'True',
      false: 'False',
      fillPlaceholder: 'Type your answer',
      matchingStub: 'Matching questions coming soon',
      matchingSkip: 'Tap Next to skip',
      networkError: "Couldn't check your answer — try again",
      empty: 'This lesson has no questions yet',
    },
    feedback: {
      correct: 'Great job!',
      incorrect: 'Not quite',
      correctAnswer: 'Correct answer: {{answer}}',
    },
    summary: {
      title: 'Lesson complete!',
      score: '{{correct}} / {{total}}',
      scoreLabel: 'Correct',
      accuracy: '{{percent}}%',
      accuracyLabel: 'Accuracy',
      duration: '{{seconds}}s',
      durationLabel: 'Time',
      backToSubject: 'Back to lessons',
      tryAgain: 'Try again',
      xpStub: '+10 XP (coming soon)',
    },
    // A5 child surface — "My activity" attempt-history push screen
    // (Design Spec design-system/ui_kits/student-app/A5-attempt-history.md §2/§4).
    attempts: {
      title: 'My activity',
      sub: "Look how much you've done!",
      back: 'Back',
      summary: {
        attempts: 'Attempts',
        completed: 'Completed',
        best: 'Best score',
      },
      group: {
        thisWeek: 'This week',
        earlier: 'Earlier',
      },
      row: {
        lessonFallback: 'Lesson {{n}}',
        today: 'Today',
        yesterday: 'Yesterday',
        completed: 'Completed',
        inProgress: 'In progress',
      },
      empty: {
        title: 'No adventures yet — start your first lesson!',
        cta: 'Start learning',
      },
      error: "Couldn't load your activity. Try again",
      showMore: 'Show more',
      listA11y: '{{count}} attempts',
    },
  },
  // B0-nav — child app bottom TabBar (Design Spec design-system/ui_kits/student-app/B0-nav-tabbar.md §1).
  nav: {
    tabs: {
      home: 'Home',
      missions: 'Missions',
      league: 'League',
      badges: 'Badges',
      barLabel: 'Main navigation',
    },
    stub: {
      comingSoon: 'Coming soon',
    },
  },
  // C5 — Matching tap-to-pair panel (Design Spec design-system/ui_kits/student-app/C5-matching-panel.md §5).
  quiz: {
    matching: {
      instruction: 'Tap two cards to pair them',
      promptHeader: 'Match these',
      answerHeader: '…with these',
      armedA11y: '{{text}}, selected, choose its match',
      pairedA11y: '{{text}}, paired with {{partner}}, tap to unpair',
      pairedAnnounce: 'Paired: {{a}} with {{b}}',
      unpairedAnnounce: 'Unpaired: {{a}} and {{b}}',
      pairFormat: '{{left}} – {{right}}',
      pairJoin: ', ',
    },
  },
  // B1 — XP & Level screen + level-up moment (Design Spec P4-08 §1).
  // XP counters / "n / m XP" strings keep Latin digits + LTR in BOTH locales
  // (SKILL.md numerals rule); level numbers are prose counts (locale digits).
  xp: {
    back: 'Back',
    hero: {
      eyebrow: 'Your level',
      level: 'Level {{value}}',
      a11y: 'You are level {{level}}',
    },
    progress: {
      header: '📈 Level {{from}} → {{to}}',
      counter: '{{current}} / {{total}} XP',
      counterTotalOnly: '{{xp}} XP',
      footerPercent: '{{percent}} to next level',
      footerLeft: '{{xp}} XP left',
      barA11y: '{{current}} of {{total}} XP to level {{next}}',
    },
    totalXp: {
      label: 'Total XP',
      a11y: 'Total XP {{xp}}',
    },
    levelUp: {
      title: 'Level Up!',
      subtitle: 'You reached Level {{level}}',
      cta: 'Keep Going',
      a11y: 'Level up! You reached level {{level}} and earned {{xp}} XP',
    },
    error: 'We could not load your XP. Try again!',
  },
  // B2 — Streak screen (Design Spec P4-08 §2). No per-day history endpoint
  // (gap G-2) → milestone markers + honest "coming soon" calendar placeholder.
  streak: {
    back: 'Back',
    eyebrow: 'Streak',
    days_one: '{{value}} day',
    days_other: '{{value}} days',
    keepAlive: 'Keep it alive!',
    zeroTitle: 'Start your streak today!',
    freeze: {
      title: 'Streak freezes',
      explainer: 'Freezes save your streak automatically when you miss a day',
      countA11y_zero: 'No streak freezes yet — keep learning to earn them!',
      countA11y_one: '{{value}} streak freeze ready',
      countA11y_other: '{{value}} streak freezes ready',
    },
    milestones: {
      eyebrow: 'Milestones',
      dayLabel: 'Day {{value}}',
      next: 'Next stop: day {{target}}!',
      allDone: 'You passed every milestone — amazing!',
      reachedA11y: 'Day {{value}} milestone reached',
      upcomingA11y: 'Day {{value}} milestone — coming up!',
    },
    history: {
      comingSoon: 'Your streak calendar is coming soon!',
      body: "Soon you'll see every day you practiced right here.",
    },
    error: 'We could not load your streak. Try again!',
  },
  // B3 — Hearts status screen + Practice Mode (Design Spec P4-08 §3.1).
  // No refill-countdown field on DashboardDto (gap G-5) → static refillNote only.
  hearts: {
    back: 'Back',
    title: 'Your Hearts',
    sub: 'You lose one for each wrong answer',
    subFull: 'All hearts full — go learn!',
    rowA11y: '{{current}} of {{max}} hearts',
    refillNote: 'Hearts come back over time — or earn them by practicing',
    lost: {
      title: 'You lost a heart',
      sub: 'Take your time — try again',
    },
    practiceMode: {
      title: 'Practice Mode is on',
      body: "Practice answers don't cost hearts and earn them back — keep learning!",
      cta: 'Practice Mode (no hearts)',
    },
    error: "Couldn't load your hearts. Try again",
  },
  // B4 — Badges collection tab (Design Spec P4-08 §4). `names.*` / `hints.*`
  // are keyed by the server badge `code` (Gamification BadgeSeeder catalog);
  // unknown codes fall back to the raw code (technical identifier).
  badges: {
    title: 'Badges',
    sub: '{{earned}} of {{total}} earned · Collect them all',
    subEmpty: 'Earn your first badge by finishing a lesson!',
    empty: 'No badges yet — earn your first!',
    error: "Couldn't load your badges. Try again",
    stats: {
      bronze: 'Bronze',
      silver: 'Silver',
      gold: 'Gold',
      legend: 'Legend',
      cellA11y: '{{label}}: {{value}} earned',
    },
    status: {
      earned: 'Earned',
      locked: 'Locked',
    },
    earnedOn: 'Earned {{date}}',
    tileA11y: {
      earnedOn: '{{name}} — {{rarity}} badge, earned {{date}}',
      earned: '{{name}} — {{rarity}} badge, earned',
      locked: '{{name}} — locked. {{hint}}',
      lockedNoHint: '{{name}} — locked',
    },
    rarity: {
      bronze: 'Bronze',
      silver: 'Silver',
      gold: 'Gold',
      legendary: 'Legendary',
    },
    names: {
      FIRST_LESSON: 'First Steps',
      STREAK_3: 'Warming Up',
      STREAK_7: 'On Fire',
      STREAK_14: 'Two-Week Champion',
      STREAK_30: 'Unstoppable',
      STREAK_100: 'Centurion',
      LEVEL_5: 'Rookie',
      LEVEL_10: 'Scholar',
      LEVEL_20: 'Master',
      LEGENDARY_50: 'Legend',
    },
    hints: {
      FIRST_LESSON: 'Complete your first lesson',
      STREAK_3: 'Keep a 3-day streak',
      STREAK_7: 'Keep a 7-day streak',
      STREAK_14: 'Keep a 14-day streak',
      STREAK_30: 'Keep a 30-day streak',
      STREAK_100: 'Keep a 100-day streak',
      LEVEL_5: 'Reach Level 5',
      LEVEL_10: 'Reach Level 10',
      LEVEL_20: 'Reach Level 20',
      LEGENDARY_50: 'Reach Level 50',
    },
  },
  // B5 — Missions screen (Design Spec P4-08 §5). XP amounts keep Latin digits
  // + LTR in BOTH locales (SKILL.md rule); mission counts are prose (locale
  // digits). `titles.*` resolves the server-sent `titleKey` path
  // (e.g. "mission.daily_3_lessons.title" — MissionSeeder catalog);
  // `titles.unknown` is the fallback for unseeded keys (never the raw key).
  missions: {
    header: {
      eyebrow: "Today's Mission",
      progress: '{{done}} of {{total}} done',
      resets: 'Resets at midnight',
    },
    hero: {
      xp: '+{{xp}} XP',
      sub: 'Finish all to claim it!',
      a11y: 'Finish all daily missions to earn {{xp}} XP',
    },
    weekly: {
      eyebrow: 'This Week',
      chip: 'Weekly',
    },
    row: {
      countSub: '{{progress}} of {{target}}',
      completed: 'Completed',
      expired: "Time's up — back tomorrow",
      reward: '⭐ +{{xp}}',
      a11y: '{{title}}, {{progress}} of {{target}} done, reward {{xp}} XP',
      completedA11y: '{{title}}, completed, you earned {{xp}} XP',
      expiredA11y: '{{title}}, time is up — back tomorrow',
    },
    complete: {
      title: 'Mission Complete!',
      subtitle: 'You finished all of today’s missions',
      cta: 'Keep Going',
      a11y: 'Mission complete! You earned {{xp}} XP',
    },
    empty: 'New missions arrive at midnight',
    error: 'We could not load your missions. Try again!',
    titles: {
      mission: {
        daily_3_lessons: { title: 'Complete 3 lessons' },
        daily_10_correct: { title: 'Get 10 answers right' },
        daily_keep_streak: { title: 'Keep your streak alive' },
        daily_1_lesson_early: { title: 'Finish a lesson early today' },
        daily_20_correct: { title: 'Get 20 answers right' },
        weekly_15_lessons: { title: 'Complete 15 lessons this week' },
        weekly_75_correct: { title: 'Get 75 answers right this week' },
        weekly_5_day_streak: { title: 'Keep a 5-day streak' },
        challenge_25_lessons: { title: 'Challenge: 25 lessons in one week' },
        challenge_100_correct: { title: 'Challenge: 100 correct answers' },
        challenge_5_day_streak: { title: 'Challenge: 5 days in a row' },
      },
      unknown: 'New mission',
    },
  },
  // B6 — League standings screen (Design Spec P4-08 §6). Tier display names
  // reuse `child.home.leagueTier.*`. XP strings keep Latin digits + "XP" in
  // BOTH locales (SKILL.md rule, spec G-8).
  league: {
    banner: {
      title: '{{tier}} League',
      daysLeft_one: '{{value}} day left',
      daysLeft_other: '{{value}} days left',
      hoursLeft_one: '{{value}} hour left',
      hoursLeft_other: '{{value}} hours left',
      endingSoon: 'Ending soon',
      promote_one: 'Top {{value}} promotes',
      promote_other: 'Top {{value}} promote',
      a11y: '{{tier}} League. {{timeLeft}}. {{promote}}',
    },
    countdown: {
      full: 'Resets in {{days}} {{hours}}',
      hoursOnly: 'Resets in {{hours}}',
      soon: 'Resets soon!',
      days_one: '{{value}}d',
      days_other: '{{value}}d',
      hours_one: '{{value}}h',
      hours_other: '{{value}}h',
    },
    list: {
      xp: '{{xp}} XP',
      youChip: 'You',
      rowA11y: 'Rank {{rank}}, {{name}}, {{xp}} XP this week',
      youRowA11y: 'Rank {{rank}}, you, {{xp}} XP this week',
      aloneHint: 'More learners join during the week — keep earning XP!',
    },
    zones: {
      promotion: '↑ Promotion zone',
      demotion: '↓ Demotion zone',
    },
    empty: {
      title: 'Earn XP this week to join a league!',
    },
    error: 'We could not load your league. Try again!',
  },
  // B8 — Events screen: streak freeze + timed events + weekly challenge
  // (Design Spec P4-08 §8). Freezes are EARNED-only — no purchase copy.
  events: {
    back: 'Back',
    title: 'Events & Challenges',
    error: 'We could not load your events. Try again!',
    freeze: {
      eyebrow: 'Streak Freeze',
      countA11y_one: '{{value}} streak freeze ready',
      countA11y_other: '{{value}} streak freezes ready',
      explainer: 'Freezes save your streak automatically when you miss a day',
      earnedNote: 'You earn freezes by learning — nothing to buy!',
      zeroNote: 'No freezes yet — keep learning to earn them!',
    },
    timed: {
      eyebrow: 'Timed Events',
      multiplier: '×{{value}} XP',
      endsInDayHour: 'Ends in {{d}}d {{h}}h',
      endsInHourMinute: 'Ends in {{h}}h {{m}}m',
      endsInMinute: 'Ends in {{m}}m',
      cardA11y: '{{name}} — {{multiplier}} — {{ends}}',
      more_one: '+{{value}} more event',
      more_other: '+{{value}} more events',
      empty: {
        title: 'No events right now',
        body: 'Special XP events will appear here — check back soon!',
      },
    },
    challenges: {
      eyebrow: 'Weekly Challenge',
      progressLabel: '{{progress}} of {{target}}',
      completed: 'Completed',
      expired: "Time's up — a new challenge is coming!",
      reward: '⭐ +{{xp}}',
      cardA11y: '{{title}} — {{status}} — reward {{xp}} XP',
      error: 'We could not load your challenges. Try again!',
      titles: {
        lessons25: 'Finish 25 lessons this week',
        correct100: 'Get 100 answers right this week',
        streak5: 'Learn 5 days in a row',
        fallback: 'Weekly challenge',
      },
      empty: {
        title: 'No challenge right now',
        body: 'Weekly challenges will appear here — check back soon!',
      },
    },
  },
  /**
   * Backend recommendation item i18n keys (P5-05 real wiring).
   *
   * The backend sends `titleKey`, `bodyKey`, `ctaKey` as bare string constants
   * (e.g. "RecReviewTitle") matching `SharedResourcesKey` values. The FE maps
   * each backend key to one of these entries via `REC_ITEM_KEY_MAP` in
   * `RecommendationsCard.tsx`. All 9 keys (3 action types × title/body/cta) are
   * required; both locales must be complete.
   *
   * Source of truth: SharedResources.en-US.resx / SharedResources.ar-EG.resx
   * (backend/src/Shared/Learnexia.Shared.Resources/).
   */
  rec: {
    RecReviewTitle: 'Time to review',
    RecReviewBody: 'This topic needs a concept review before more practice.',
    RecReviewCta: 'Review concept',
    RecPracticeTitle: 'Keep practising',
    RecPracticeBody: 'Practise this skill to strengthen understanding.',
    RecPracticeCta: 'Start practice',
    RecColdStartTitle: 'Great start!',
    RecColdStartBody: 'No weak areas yet — keep up the great work!',
    RecColdStartCta: 'Continue learning',
  },
} as const;

export const ar = {
  common: {
    appName: 'Learnexia',
    ok: 'حسناً',
    cancel: 'إلغاء',
    retry: 'إعادة المحاولة',
    loading: 'جارٍ التحميل…',
    done: 'تم',
    error: {
      serverError: 'حدث خطأ ما. يرجى المحاولة مجدداً.',
      networkError: 'لا يوجد اتصال بالإنترنت. تحقق من اتصالك وأعد المحاولة.',
    },
    restartPrompt: {
      title: 'أعد التشغيل لتغيير اللغة',
      body: 'يحتاج التطبيق إلى إعادة التشغيل لتطبيق إعدادات اللغة الجديدة.',
      confirm: 'إعادة التشغيل',
      cancel: 'لاحقاً',
    },
    prefs: {
      switchToLight: 'التبديل إلى الوضع الفاتح',
      switchToDark: 'التبديل إلى الوضع الداكن',
      switchToArabic: 'العربية',
      switchToEnglish: 'English',
      language: 'اللغة',
      theme: 'المظهر',
      themeDark: 'ليل',
      themeLight: 'فاتح',
    },
    splash: {
      subtitle: 'تبدأ مغامرة التعلّم بالذكاء الاصطناعي',
      loading: 'جارٍ التحميل… ⚡',
      poweredBy: 'مدعوم بالذكاء الاصطناعي',
      tagline: '✦ تعلّم عائلي ✦',
    },
  },
  auth: {
    signIn: 'تسجيل الدخول',
    signOut: 'تسجيل الخروج',
    userName: 'اسم المستخدم',
    password: 'كلمة المرور',
    showPassword: 'إظهار كلمة المرور',
    hidePassword: 'إخفاء كلمة المرور',
    sessionExpired: 'انتهت جلستك. يرجى تسجيل الدخول مجدداً.',
    errors: {
      userNameRequired: 'اسم المستخدم مطلوب',
      passwordRequired: 'كلمة المرور مطلوبة',
    },
    register: {
      step: 'الخطوة ١ من ٢',
      title: 'أنشئ حساب ولي الأمر',
      subtitle: 'ستضيف حسابات أطفالك في الخطوة التالية.',
      parentOnlyTitle: 'ولي أمر / وصي قانوني فقط',
      parentOnlyBody:
        'الأطفال لا يستطيعون التسجيل بأنفسهم. ستنشئ حساباتهم في الخطوة التالية.',
      labelFullName: 'الاسم الكامل',
      labelCountry: 'الدولة',
      countryPlaceholder: 'اختر الدولة',
      labelEmail: 'البريد الإلكتروني',
      labelPassword: 'كلمة المرور',
      passwordHelper: '٦ أحرف على الأقل',
      strength: {
        weak: 'ضعيفة',
        fair: 'مقبولة',
        good: 'جيدة',
        strong: 'قوية',
        a11y: 'قوة كلمة المرور: {{label}}',
      },
      termsLabel:
        'أنا ولي الأمر أو الوصي القانوني وأوافق على {{terms}} و{{privacy}}، بما في ذلك الموافقة على إنشاء حسابات لأطفالي.',
      termsLink: 'الشروط',
      privacyLink: 'سياسة الخصوصية',
      termsA11y:
        'أنا ولي الأمر أو الوصي القانوني وأوافق على الشروط وسياسة الخصوصية، بما في ذلك الموافقة على إنشاء حسابات لأطفالي.',
      submitButton: 'متابعة ← إضافة الأطفال',
      progressA11y: 'تقدّم التسجيل: الخطوة ١ من ٢',
      feature: {
        title: 'جهّز الحساب مرة. شاهدهم يتعلمون للأبد.',
        bullet1: 'شروحات مدعومة بالذكاء الاصطناعي مخصصة لمستوى كل طفل',
        bullet2: 'تقارير أسبوعية توضّح بالضبط ما أتقنوه',
        bullet3: 'مهام يومية تبقيهم متحمسين للعودة دون إلحاح',
        bullet4: 'متوافق مع COPPA — بلا إعلانات ولا رسائل خاصة ولا بيع للبيانات',
      },
      haveAccount: 'لديك حساب بالفعل؟',
      signInLink: 'تسجيل الدخول',
      backToSignIn: 'العودة لتسجيل الدخول',
      captcha: {
        a11y: 'فحص الأمان',
        loadError:
          'تعذّر تحميل فحص الأمان. يرجى تحديث الصفحة والمحاولة مرة أخرى.',
      },
      errors: {
        duplicateEmail: 'يوجد حساب بهذا البريد الإلكتروني بالفعل.',
        weakPassword:
          'يجب أن تحتوي كلمة المرور على 6 أحرف على الأقل مع حروف كبيرة وصغيرة ورقم وحرف خاص.',
        invalidEmail: 'يرجى إدخال بريد إلكتروني صحيح.',
        countryRequired: 'يرجى اختيار دولتك.',
        termsRequired: 'يرجى الموافقة على الشروط للمتابعة.',
        captchaFailed: 'فشل فحص الأمان. يرجى المحاولة مرة أخرى.',
      },
    },
    roleSelect: {
      title: 'مرحباً بك في Learnexia',
      subtitle: 'من الذي يسجّل الدخول؟',
      parentLabel: 'ولي الأمر',
      parentSub: 'تابع تقدّم طفلك',
      studentLabel: 'طالب',
      studentSub: 'تعلّم والعب وتقدّم',
      footnote:
        'يدخل الأطفال بالبريد الذي يحدده ولي الأمر — لا ينشئون حساباتهم بأنفسهم.',
      parentA11y: 'تسجيل الدخول كولي أمر',
      studentA11y: 'تسجيل الدخول كطالب',
    },
    login: {
      eyebrow: 'تسجيل الدخول',
      title: 'أهلاً بعودتك',
      /** طالب: المفتاح الحالي مُعاد توظيفه للطلاب فقط. */
      subtitle: 'سجّل الدخول للحفاظ على سلسلتك 🔥',
      /** ولي الأمر: مفتاح عنوان فرعي جديد (Batch A). */
      subtitleParent: 'سجّل الدخول لمتابعة تقدّم أطفالك',
      /** مفاتيح شارة الدور (Batch A، مفتاحان حرفيان لتجنّب مشكلات الجندر في العربية). */
      signingInAsParent: 'تسجيل الدخول كولي أمر',
      signingInAsStudent: 'تسجيل الدخول كطالب',
      /** رابط "تغيير" على شارة الدور (Batch A). */
      change: 'تغيير',
      /** إشعار الطالب بلا حساب (Batch A). */
      studentNoAccountTitle: 'هل تحتاج إلى حساب؟',
      studentNoAccountBody: 'اطلب من ولي أمرك إضافتك.',
      labelUsername: 'البريد الإلكتروني',
      labelPassword: 'كلمة المرور',
      showPassword: 'إظهار',
      hidePassword: 'إخفاء',
      rememberMe: 'تذكّرني',
      forgotPassword: 'نسيت كلمة المرور؟',
      submitButton: 'تسجيل الدخول ←',
      orDivider: 'أو',
      orContinueWith: 'أو واصل بـ',
      socialGoogle: 'Google',
      socialApple: 'آبل',
      socialMicrosoft: 'مايكروسوفت',
      newParent: 'جديد على  Learnexia',
      createAccount: 'إنشاء حساب ولي أمر',
      brand: {
        title: '.أهلاً بعودتك للمغامرة',
        body: '.تابع تحدى إنجازاتك، حافظ على قلوبك ممتلئة، وشاهد أطفالك يتقنون مهارات جديدة',
        socialProof: 'أكثر من ٢٤٠٬٠٠٠ طفل يتعلمون اليوم',
      },
      errors: {
        // Anti-enumeration (P1-13): credential failures are ALWAYS this one
        // uniform message — never a "no account found" variant.
        invalidCredentials: 'اسم المستخدم أو كلمة المرور غير صحيحة.',
        accountLocked:
          'محاولات فاشلة كثيرة. تم قفل حسابك مؤقتاً — يرجى المحاولة لاحقاً.',
        accountDeactivated:
          'تم إيقاف هذا الحساب. يرجى التواصل مع الدعم للمساعدة.',
        socialFailed: 'تعذّر تسجيل الدخول عبر Google. حاول مرة أخرى.',
      },
      social: {
        loading: 'جارٍ تسجيل الدخول عبر Google…',
      },
    },
    forgotPassword: {
      eyebrow: 'إعادة تعيين كلمة المرور',
      title: 'هل نسيت كلمة المرور؟',
      subtitle: 'أدخل بريدك الإلكتروني وسنرسل لك رابط إعادة التعيين.',
      labelEmail: 'البريد الإلكتروني',
      submitButton: 'إرسال رابط الإعادة ←',
      successTitle: 'تحقّق من بريدك',
      successBody:
        'إذا كان هناك حساب بهذا البريد، فقد أرسلنا رابط إعادة تعيين كلمة المرور.',
      backToSignIn: 'العودة لتسجيل الدخول',
      errors: {
        invalidEmail: 'يرجى إدخال بريد إلكتروني صحيح.',
        generic: 'حدث خطأ ما. حاول مرة أخرى.',
      },
    },
    resetPassword: {
      eyebrow: 'تعيين كلمة مرور جديدة',
      title: 'أنشئ كلمة مرور جديدة',
      subtitle: 'اختر كلمة مرور قوية لـ',
      labelEmail: 'البريد الإلكتروني',
      labelNewPassword: 'كلمة المرور الجديدة',
      labelConfirmPassword: 'تأكيد كلمة المرور',
      passwordHelper: '٦ أحرف على الأقل.',
      submitButton: 'إعادة تعيين كلمة المرور ←',
      successBody: 'تم إعادة تعيين كلمة المرور. يرجى تسجيل الدخول.',
      requestNewLink: 'طلب رابط جديد',
      errors: {
        weakPassword:
          'يجب أن تتكوّن كلمة المرور من ٦ أحرف على الأقل وتشمل حرفاً كبيراً وصغيراً ورقماً ورمزاً خاصاً.',
        mismatch: 'كلمتا المرور غير متطابقتين.',
        tokenInvalid: 'رابط إعادة التعيين غير صالح. يرجى طلب رابط جديد.',
        tokenExpired: 'انتهت صلاحية رابط إعادة التعيين. يرجى طلب رابط جديد.',
        generic: 'حدث خطأ ما. حاول مرة أخرى.',
      },
    },
  },
  onboarding: {
    stepLabel: 'خطوة {{current}} من {{total}}',
    back: 'رجوع',
    close: 'إغلاق',
    editChild: 'تعديل بيانات الطفل',
    removeChild: 'إزالة الطفل',
    saveChanges: 'حفظ التغييرات',
    addChild: {
      title: 'أضف أطفالك',
      subtitle: 'أضف كل طفل من أطفالك. يمكنك إضافة أكثر من طفل.',
      // زر المتابعة في شاشة الإعداد الجديدة.
      continue: 'متابعة',
      // تسمية القسم فوق قائمة الأطفال المُضافين.
      listLabel: 'الأطفال المُضافون ({{count}})',
      // تلميح الحالة الفارغة عند عدم إضافة أي أطفال بعد.
      emptyHint: 'أضف طفلك الأول للمتابعة',
      // المفاتيح التالية محفوظة لـ AddChildForm / EditChildSheet (تُستخدم
      // في لوحة التحكم) وللتوافق مع الإصدارات السابقة. لا تُحذف في هذا PR.
      labelName: 'الاسم الكامل للطفل',
      labelEmail: 'البريد الإلكتروني للدخول',
      labelPassword: 'كلمة المرور',
      labelGrade: 'الصف الدراسي',
      labelLanguage: 'اللغة المفضلة',
      labelLearningLanguage: 'لغة الدراسة',
      labelAppLanguage: 'لغة التطبيق',
      languageGroupLabel: 'اللغات',
      labelCountry: 'الدولة',
      gradePlaceholder: 'اختر الصف',
      languagePlaceholder: 'اختر اللغة',
      learningLanguagePlaceholder: 'اختر لغة الدراسة',
      learningLanguageHelper: 'اللغة التي سيدرس بها طفلك الرياضيات والعلوم.',
      appLanguageHelper:
        'لغة الأزرار والقوائم والرسائل. قمنا بمطابقتها مع لغة الدراسة — ويمكنك تغييرها.',
      addToListButton: 'إضافة الطفل إلى القائمة',
      submitButton: 'إضافة {{count}} طفل/أطفال والمتابعة',
      added: 'تمت الإضافة!',
      partialFailureBanner:
        'تعذر إضافة بعض الأطفال. يرجى تصحيح الأخطاء والمحاولة مجدداً.',
      errors: {
        nameRequired: 'اسم الطفل مطلوب.',
        languageRequired: 'يرجى اختيار لغة.',
        learningLanguageRequired: 'يرجى اختيار لغة الدراسة.',
        countryRequired: 'الدولة مطلوبة.',
      },
    },
    grade: {
      1: 'الصف الأول',
      2: 'الصف الثاني',
      3: 'الصف الثالث',
      4: 'الصف الرابع',
      5: 'الصف الخامس',
      6: 'الصف السادس',
    },
    language: {
      ar: 'عربي / Arabic',
      en: 'الإنجليزية / English',
    },
    child: {
      errors: {
        duplicateEmail: 'هذا البريد الإلكتروني مستخدم بالفعل.',
        invalidGrade: 'يرجى اختيار صف بين 1 و 6.',
        weakPassword: 'كلمة المرور لا تستوفي متطلبات الأمان.',
        generic: 'تعذر إضافة هذا الطفل. يرجى المحاولة مجدداً.',
      },
    },
    complete: {
      title: 'أنت جاهز تماماً!',
      body: 'تمت إضافة أطفالك. ابدأ استكشاف عالم التعلم!',
      cta: 'الذهاب إلى لوحة التحكم',
    },
  },
  parent: {
    nav: {
      myChildren: 'أطفالي',
      /** تسمية شريط التبويب المختصرة لتبويب الأطفال (مواصفات PMTabBar). */
      children: 'الأطفال',
      overview: 'نظرة عامة',
      reports: 'التقارير',
      /** تبويب طاقة المساعد (مواصفات PMTabBar؛ واجهة الشراء الكامل في دفعة لاحقة). */
      energy: 'الطاقة',
      activity: 'النشاط',
      subjects: 'المواد',
      settings: 'الإعدادات',
      logout: 'تسجيل الخروج',
      sectionLabel: 'قائمة ولي الأمر',
      /** تسمية إمكانية الوصول لحاوية قائمة التبويبات. */
      barLabel: 'تنقل ولي الأمر',
    },
    // TODO(P5): فرق نقاط الخبرة الأسبوعي بيانات تحليلية من الخادم — نص مؤقت الآن.
    xpWidget: {
      eyebrow: 'هذا الأسبوع',
      value: '+{{xp}} نقطة',
      delta: 'متقدم {{percent}}٪ عن الأسبوع الماضي',
    },
    childSelector: {
      label: 'الطفل النشط',
      meta: 'الصف {{grade}} · المستوى {{level}}',
      switchChild: 'تبديل الطفل',
      addChild: 'إضافة طفل',
    },
    myChildren: {
      title: 'أطفالي',
      subtitle: '{{count}} أطفال مربوطين بحسابك',
      sectionLabel: 'الأطفال ({{count}})',
      empty: 'لم يتم ربط أي أطفال بعد.',
      linkButton: 'ربط طفل موجود',
      loadError: 'تعذر تحميل قائمة أطفالك. اضغط للمحاولة مجدداً.',
      periodLabel: 'فترة التقرير',
      periodThisWeek: 'هذا الأسبوع',
      sendReport: 'إرسال التقرير',
      pickChild: 'اختر طفلاً لعرض تقدّمه',
      addChild: '+ أضف طفلاً',
      activeToday: 'نشط اليوم',
      inactive: 'غير نشط',
      statLevel: 'المستوى',
      statLevelShort: 'المستوى',
      statXp: 'النقاط',
      statStreak: 'التحدى',
      statStreakValue: '{{n}} أيام',
      statEnergy: 'الطاقة',
      mastery: 'الإتقان',
      weakest: 'الأضعف:',
      viewDashboard: 'عرض اللوحة ←',
      addCardTitle: 'أضف طفلاً',
      addCardSubtitle: 'حدّد صفه ولغته وبريد دخوله',
      topics: {
        fractions: 'الكسور',
        letters: 'الحروف',
        geometry: 'الهندسة',
        reading: 'القراءة',
        numbers: 'الأرقام',
      },
      editChild: 'تعديل {{name}}',
      editError: 'تعذّر حفظ التغييرات. حاول مرة أخرى.',
      editSuccess: 'تم حفظ التغييرات.',
    },
    familySummary: {
      eyebrow: 'هذا الأسبوع · الإجمالي',
      headline: 'عائلتك في تقدّم رائع',
      subline: '{{learners}} متعلمين نشطين · {{lessons}} درساً',
      totalXp: 'إجمالي النقاط',
      lessons: 'دروس',
      bestStreak: 'أطول تحدى',
      badgesEarned: 'شارات',
      childrenCluster: '{{count}} أطفال',
    },
    overview: {
      title: 'تقدّم {{name}}',
      // TODO(P5-05): real week range from analytics; placeholder copy for now.
      dateRange: 'الاثنين، ١٨ نوفمبر ← الأحد، ٢٤ نوفمبر',
      periodLabel: 'فترة التقرير',
      periodThisWeek: 'هذا الأسبوع',
      sendReport: 'إرسال تقرير',
      empty: 'أضف طفلاً لعرض تقدّمه الأسبوعي.',
      loadError: 'تعذر تحميل التقدّم. اضغط للمحاولة مجدداً.',
      kpi: {
        timeLearning: 'وقت التعلم',
        timeDelta: '+{{value}} عن الأسبوع الماضي',
        xpEarned: 'النقاط المكتسبة',
        /** GAP-8 fix: xpDelta مطلق (لا نسبة مئوية). Eastern-Arabic digit + نقطة. */
        xpDelta: '+{{value}} نقطة هذا الأسبوع',
        lessonsDone: 'دروس منجزة',
        lessonsDelta: '+{{value}} عن الأسبوع الماضي',
        streak: 'التحدى',
        streakDelta: '+{{value}} عن الأسبوع الماضي',
      },
      dailyActivity: {
        title: 'النشاط اليومي',
        subtitle: 'النقاط المكتسبة لكل يوم',
        exportCsv: 'تصدير CSV',
        placeholder: 'الرسم البياني قريباً',
        /** P5-05: الحالة الفارغة عندما لا يوجد نشاط. */
        empty: 'لا نشاط هذا الأسبوع',
      },
      subjectMastery: {
        title: 'إتقان المواد',
        subtitle: 'آخر ٧ أيام',
        /** P5-05: الحالة الفارغة قبل بدء الدروس. */
        empty: 'أكمل الدروس لعرض الإتقان',
      },
      subjects: {
        math: 'الرياضيات',
        science: 'العلوم',
        arabic: 'العربية',
        english: 'الإنجليزية',
      },
      focusAreas: {
        title: 'مجالات للتركيز عليها',
        subtitle: 'مواضيع لا يزال {{name}} يبني ثقته فيها',
        seeAll: 'رؤية الكل',
        /** P5-05 FIX: عندما لا توجد مجالات ضعف (طفل جديد، حالة الأصفار). */
        empty: 'لا مجالات تحتاج تركيزاً الآن — واصل المسيرة!',
      },
      recommendations: {
        title: 'توصيات من ليكسي',
        subtitle: 'اقتراحات مخصصة لهذا الأسبوع',
        /** P5-05 real wiring: shown when items[] is empty (cold-start / no weak areas yet). */
        empty: 'لا توصيات بعد — واصل التعلم لتحصل على نصائح مخصصة!',
      },
    },
    // نظرة عامة على الطفل (Batch C؛ (parent)/child/[id]).
    childOverview: {
      title: 'تقدّم {{name}}',
      back: 'رجوع',
      notFound: 'تعذّر العثور على هذا الطفل.',
      backToChildren: 'العودة إلى أطفالي',
      kpi: {
        time: 'الوقت',
        xp: 'النقاط المكتسبة',
        lessons: 'الدروس',
      },
      fullReport: '📈 التقرير الكامل',
      energy: '⚡ الطاقة',
    },
    // A4 صفحة التقارير (CO-FE-1 / P1-11-FE-9؛ الرسوم البيانية مؤجلة إلى P5-05).
    reports: {
      title: 'تقارير {{name}}',
      subtitle: 'تفاصيل كاملة · بدّل الطفل من الأعلى',
      range: {
        label: 'نطاق التقرير',
        week: 'هذا الأسبوع',
        month: 'هذا الشهر',
        all: 'كل الوقت',
      },
      send: 'إرسال التقرير',
      // L6: إشعار مؤقت — الإرسال الفعلي يصل مع P5-04 / المرحلة 9.
      sendToast: 'سيصلك التقرير على بريدك — قريبًا!',
      kpi: {
        time: 'وقت التعلم',
        xp: 'النقاط المكتسبة',
        lessons: 'الدروس المكتملة',
        accuracy: 'متوسط الدقة',
        noActivity: 'لا يوجد نشاط بعد',
        // G-2: لا توجد نقطة نهاية لنقاط الطفل للوالدين بعد.
        xpComingSoon: 'يتوفر مع التقارير الكاملة',
      },
      delta: {
        vsLastWeek: '{{value}}٪ عن الأسبوع الماضي',
      },
      mastery: {
        title: 'إتقان المهارات',
        sub: 'مستويات الإتقان حسب المادة',
        // G-1: لا توجد نقطة نهاية لإتقان المواد بعد (P5-05).
        empty: 'يظهر الإتقان بعد أولى الدروس',
      },
      charts: {
        xpTitle: 'آخر ٢٠ يوماً · النقاط',
        /** P5-05 NEW: عنوان فرعي للرسم البياني ٢٠ يوم. */
        xpSubtitle: 'اليوم بالنيلي',
        todTitle: 'وقت اليوم',
        /** P5-05 NEW: عنوان فرعي لرسم وقت اليوم ({{name}} = اسم الطفل). */
        todSubtitle: 'متى يتعلم {{name}} بأفضل ما يمكن',
        /** P5-05 NEW: تلميح ذروة التركيز ({{start}} / {{end}} = توقيتات Latin). */
        peakInsight: 'أفضل تركيز في {{start}}–{{end}} — وقت مثالي للمادة الجديدة',
        /**
         * P5-05 FIX: تلميح ذروة التركيز للحزمة المسمّاة (الشبكة الحقيقية تحتوي على حزم
         * مسمّاة لا أعمدة ساعية). {{bucket}} = اسم الحزمة المترجم.
         */
        peakBucketInsight: '{{bucket}} هو وقت ذروة التركيز — مثالي للمادة الجديدة',
        /** P5-05 NEW: الحالة الفارغة للرسم البياني ٢٠ يوم. */
        noData: 'لا بيانات بعد — تحقق قريباً',
        /** P5-05 NEW: الحالة الفارغة لرسم وقت اليوم. */
        todEmpty: 'لا بيانات جلسات بعد',
        comingSoon: 'الرسوم البيانية قريبًا',
        /** P5-05 FIX: تسميات حزم وقت اليوم المسمّاة (٤ حزم من الخلفية). */
        tod: {
          morning: 'الصباح',
          afternoon: 'بعد الظهر',
          evening: 'المساء',
          night: 'الليل',
        },
      },
      empty: {
        firstWeek: 'لم يبدأ {{name}} بعد — أول درس سيضيء هذه الصفحة!',
        addChild: 'أضف طفلًا لعرض التقارير',
      },
      loadError: 'تعذّر تحميل التقارير. اضغط لإعادة المحاولة.',
      // A5 واجهة الوالدين — لوحة «المحاولات الأخيرة» داخل التقارير.
      attempts: {
        title: 'المحاولات الأخيرة',
        sub: 'آخر اختبارات {{name}}',
        empty: 'لا توجد محاولات في هذه الفترة',
        showAll: 'عرض الكل',
        showLess: 'عرض أقل',
        row: {
          // G-1 (A5): الكائن يحمل lessonId فقط — نص بديل مترجم.
          lessonFallback: 'الدرس {{n}}',
          today: 'اليوم',
          yesterday: 'أمس',
          completed: 'مكتمل',
          inProgress: 'غير مكتمل',
          hints_zero: '💡 بدون تلميحات',
          hints_one: '💡 تلميح واحد',
          hints_two: '💡 تلميحان',
          hints_few: '💡 {{count}} تلميحات',
          hints_many: '💡 {{count}} تلميحًا',
          hints_other: '💡 {{count}} تلميح',
        },
      },
    },
    settings: {
      title: 'الإعدادات',
      subtitle: 'إدارة حسابك وتفضيلاتك',
      thisWeekEyebrow: 'هذا الأسبوع',
      thisWeekXp: '+{{value}} نقطة خبرة',
      thisWeekDelta: 'ارتفاع بنسبة {{value}}٪ عن الأسبوع الماضي',
      tabs: {
        navLabel: 'أقسام الإعدادات',
        profile: 'الملف الشخصي',
        notifications: 'الإشعارات',
        linkedChildren: 'الأطفال المربوطون',
        security: 'الأمان',
        billing: 'الخطة والفوترة',
        language: 'اللغة والمنطقة',
      },
      profile: {
        title: 'الملف الشخصي',
        subtitle: 'هكذا يعرفك Learnexia',
        uploadPhoto: 'رفع صورة',
        changePhoto: 'تغيير الصورة',
        removePhoto: 'إزالة',
        fullName: 'الاسم الكامل',
        email: 'البريد الإلكتروني',
        emailLockedHelper: 'بريدك الإلكتروني هو معرّف تسجيل الدخول ولا يمكن تغييره من هنا.',
        phone: 'الهاتف',
        country: 'الدولة',
        countryPlaceholder: 'اختر الدولة',
        cancel: 'إلغاء',
        save: 'حفظ التغييرات',
        loading: 'جارٍ تحميل ملفك الشخصي…',
        saveSuccess: 'تم حفظ ملفك الشخصي.',
        saveError: 'تعذّر حفظ ملفك الشخصي. يرجى المحاولة مجدداً.',
        avatar: {
          helper: 'PNG أو JPG، حتى ٥ ميجابايت',
          uploading: 'جارٍ رفع صورتك…',
          uploadSuccess: 'تم تحديث الصورة.',
          uploadError: 'تعذّر رفع الصورة. حاول مرة أخرى.',
          tooLarge: 'الصورة كبيرة جداً (الحد ٥ ميجابايت).',
          wrongType: 'اختر صورة بصيغة PNG أو JPG.',
          removeSuccess: 'تمت إزالة الصورة.',
          removeError: 'تعذّر إزالة الصورة. حاول مرة أخرى.',
          uploadZoneTitle: 'ارفع صورة',
          uploadZoneTitleChange: 'تغيير الصورة',
          uploadZoneSub: 'PNG أو JPG · أو اختر لوناً أدناه',
        },
      },
      language: {
        title: 'اللغة والمنطقة',
        subtitle: 'يؤثر على لوحتك، لا على تطبيقات أطفالك',
        languageLabel: 'اللغة',
        regionLabel: 'المنطقة',
        regionPlaceholder: 'اختر المنطقة',
        saveSuccess: 'تم حفظ تفضيل اللغة.',
        save: 'حفظ',
        cancel: 'إلغاء',
        regionUiOnlyNote: 'تفضيل المنطقة يُحفظ محلياً فقط.',
      },
      comingSoon: {
        // TODO(P2-12): Notifications / Linked children / Security / Plan & billing.
        title: 'قريباً',
        body: 'هذا القسم في الطريق. عُد قريباً للاطلاع عليه.',
      },
      notifications: {
        title: 'الإشعارات',
        subtitle: 'اختر ما نُبلغك به',
        category: {
          weeklyReport: {
            label: 'التقرير الأسبوعي',
            helper: 'ملخص أسبوعي عن تقدّم طفلك',
          },
          streakAtRisk: {
            label: 'التحدى في خطر',
            helper: 'تنبيه قبل أن ينكسر التحدى اليوم',
          },
          productAnnouncement: {
            label: 'إعلانات المنتج',
            helper: 'الميزات الجديدة والفعاليات الموسمية',
          },
          achievement: {
            label: 'الإنجازات',
            helper: 'الشارات وتجاوز المستويات والإنجازات الكبيرة',
          },
        },
        channel: {
          email: 'البريد',
          push: 'الإشعارات الفورية',
        },
        saveSuccess: 'تم الحفظ. تفضيلاتك محدّثة.',
        saveError: 'تعذّر الحفظ. حاول مرّة أخرى.',
      },
      linkedChildren: {
        title: 'الأطفال المرتبطون',
        subtitle: 'أدِر من على حسابك',
        addChild: 'إضافة طفل',
        emptyTitle: 'لا يوجد أطفال بعد',
        emptyBody: 'اربط طفلك الأول لتبدأ متابعة تقدّمه.',
        edit: 'تعديل',
        unlink: 'فك الارتباط',
        unlinkConfirmTitle: 'فك ارتباط {{name}}؟',
        unlinkConfirmBody: 'سيفقد الوصول إلى تقدّمه على حسابك.',
        unlinkLastParentError: 'لا يمكنك فك ارتباط ولي الأمر الوحيد لهذا الطفل.',
        updateSuccess: 'تم الحفظ.',
        updateError: 'تعذّر الحفظ. حاول مرّة أخرى.',
        unlinkSuccess: 'تم فك ارتباط {{name}}.',
        unlinkError: 'تعذّر فك الارتباط. حاول مرّة أخرى.',
        fullName: 'الاسم الكامل',
        grade: 'الصف',
        language: 'اللغة',
        country: 'الدولة',
        countryPlaceholder: 'اختر دولة',
        gradeOption: {
          1: 'الصف الأول',
          2: 'الصف الثاني',
          3: 'الصف الثالث',
          4: 'الصف الرابع',
          5: 'الصف الخامس',
          6: 'الصف السادس',
        },
        learningLanguage: {
          rowLabel: 'لغة الدراسة',
          change: 'تغيير',
          pickerLabel: 'لغة دراسة الرياضيات والعلوم',
          pickerHelper: 'اللغة التي يدرس بها طفلك الرياضيات والعلوم. وهي منفصلة عن لغة التطبيق.',
          changeCta: 'تغيير اللغة',
          noChange: 'هذه هي لغة الدراسة الحالية بالفعل.',
          confirm: {
            title: 'إعادة ضبط تقدّم الرياضيات والعلوم؟',
            fromTo: '{{from}} ← {{to}}',
            willReset: 'سيُعاد ضبط تقدّم طفلك في الرياضيات والعلوم، وسيبدأ من جديد في هاتين المادتين.',
            willKeep: 'تبقى مادتا العربية والإنجليزية، والنقاط، والتحدى  والشارات كما هي.',
            rareNote: 'يُفضّل القيام بهذا في بداية العام الدراسي فقط.',
            ack: 'أفهم أنه سيُعاد ضبط تقدّم الرياضيات والعلوم.',
            cta: 'إعادة الضبط والتغيير',
          },
          success: 'تم. يدرس {{name}} الآن الرياضيات والعلوم باللغة {{language}}، وأُعيد ضبط ذلك التقدّم للبدء من جديد.',
          error: {
            forbidden: 'يمكنك تغيير لغة الدراسة لأطفالك فقط.',
            confirmMissing: 'يُرجى تأكيد إعادة الضبط قبل تغيير اللغة.',
            generic: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
          },
        },
      },
      security: {
        title: 'الأمان',
        subtitle: 'أدِر كلمة المرور والجلسات',
        currentPassword: 'كلمة المرور الحالية',
        newPassword: 'كلمة المرور الجديدة',
        confirmPassword: 'تأكيد كلمة المرور الجديدة',
        save: 'تحديث كلمة المرور',
        saveSuccess: 'تم تحديث كلمة المرور. تم تسجيل خروج الجلسات الأخرى.',
        saveErrorPolicy: 'كلمة المرور الجديدة لا تستوفي السياسة.',
        saveErrorCurrent: 'كلمة المرور الحالية غير صحيحة. حاول مرّة أخرى.',
        mismatchError: 'كلمتا المرور غير متطابقتين.',
        sameAsCurrent: 'كلمة المرور الجديدة لا يمكن أن تطابق الحالية.',
        sessionsTitle: 'الجلسات النشطة',
        sessionsSubtitle: 'الأجهزة المسجّلة دخولًا حاليًا على حسابك',
        signOutOthers: 'تسجيل خروج الجلسات الأخرى',
        signOutOthersSuccess: 'تم تسجيل خروج {{count}} جلسة أخرى.',
        sessionActive: 'نشطة',
        sessionExpired: 'منتهية',
        expiresAt: 'تنتهي {{date}}',
        noActiveSessions: 'لا توجد جلسات نشطة.',
      },
      billing: {
        title: 'الخطة والفوترة',
        subtitle: 'خطتك الحالية وخيارات الترقية',
        planLabel: 'الخطة',
        statusActive: 'نشطة',
        statusInactive: 'غير نشطة',
        manage: 'إدارة الاشتراك',
        managePending: 'إدارة الاشتراك قريبًا.',
        upgradeComingSoon: 'أنت على الخطة المجانية. خطط الاشتراك قادمة قريبًا، سنُعلمك.',
        planName: {
          free: 'مجاني',
          pro: 'برو',
        },
      },
    },
    addChildModal: {
      title: 'أضف طفلاً',
      subtitle: 'سيسجّل الدخول بالبريد الذي تحدّده',
      labelName: 'اسم الطفل',
      labelEmail: 'البريد الإلكتروني للدخول',
      labelPassword: 'كلمة مرور الدخول',
      labelGrade: 'الصف',
      labelAppLanguage: 'لغة التطبيق',
      labelLearningLanguage: 'لغة التعلّم',
      labelCountry: 'الدولة',
      countryPlaceholder: 'اختر الدولة',
      learningLanguageHelper: 'تُدرَّس الرياضيات والعلوم بهذه اللغة',
      photoUpload: 'ارفع صورة',
      photoSub: 'PNG أو JPG · أو اختر لوناً أدناه',
      cancel: 'إلغاء',
      ctaDefault: 'أضف الطفل',
      ctaWithName: 'أضف {{name}} ←',
      flagAr: 'ع',
      flagEn: 'EN',
      errors: {
        nameRequired: 'اسم الطفل مطلوب.',
        emailRequired: 'البريد الإلكتروني مطلوب.',
        passwordRequired: 'كلمة المرور مطلوبة.',
        gradeRequired: 'يرجى اختيار صف.',
        languageRequired: 'يرجى اختيار لغة.',
        learningLanguageRequired: 'يرجى اختيار لغة التعلّم.',
        countryRequired: 'الدولة مطلوبة.',
        generic: 'تعذّر إضافة الطفل. يرجى المحاولة مجدداً.',
      },
    },
    childSwitcher: {
      addChild: '+ أضف طفلاً',
      noChildren: 'أضف طفلاً',
      metaLabel: 'الصف {{grade}} · المستوى {{level}}',
    },
    linkChild: {
      title: 'ربط طفل',
      explanation:
        'أدخل البريد الإلكتروني لحساب طفل موجود على ليرنيكسيا لربطه بعائلتك.',
      labelEmail: 'البريد الإلكتروني للطفل',
      submitButton: 'ربط الطفل',
      successTitle: 'تم ربط الطفل!',
      doneButton: 'تم',
      errors: {
        notFound: 'لا يوجد حساب طفل بهذا البريد الإلكتروني.',
        alreadyLinked: 'هذا الطفل مرتبط بحسابك بالفعل.',
      },
    },
    dashboard: {
      title: 'لوحة التحكم قادمة قريباً!',
      body: 'أطفالك جاهزون. مغامرات التعلم في الطريق.',
      viewChildren: 'عرض أطفالي',
    },
    // AccountMenu — قائمة الحساب بالضغط على الصورة الرمزية (رأس ولي الأمر بعد تسجيل الدخول).
    account: {
      /** تسمية إمكانية الوصول لزر الصورة الرمزية. */
      menuLabel: 'قائمة الحساب',
      /** عنوان قسم عنصر التحكم في اللغة. */
      languageLabel: 'اللغة',
      /** عنوان قسم عنصر التحكم في المظهر. */
      themeLabel: 'المظهر',
      /** خيار «ليل» — يُعيَّن على المظهر الداكن. */
      themeNight: 'ليل',
      /** خيار «فاتح» — يُعيَّن على المظهر الفاتح. */
      themeLight: 'فاتح',
    },
    // شاشة طاقة المساعد (الدفعة د). ⚡ الطاقة = وقود مساعد الذكاء الاصطناعي
    // (لون أزرق مخضرّ)، منفصلة عن ❤️ القلوب (الأرواح، وردي). الشراء معطّل.
    energy: {
      title: 'طاقة المساعد',
      subtitle: 'وقود مساعدي الدراسة بالذكاء الاصطناعي لطفلك',
      balance: {
        heading: 'الطاقة المتبقية هذا الشهر',
        ofTotal: 'من {{total}}',
        reset: 'تتجدد بعد {{days}}',
        resetDays_one: 'يوم واحد',
        resetDays_two: 'يومين',
        resetDays_few: '{{value}} أيام',
        resetDays_many: '{{value}} يومًا',
        resetDays_other: '{{value}} يوم',
        dailyCap: 'حد {{value}}/يوم',
        a11y: 'رصيد الطاقة: {{balance}} من {{total}} نقطة متبقية هذا الشهر.',
      },
      meters: {
        heading: 'مقياسان منفصلان',
        energyName: 'الطاقة',
        energyDesc: '= وقود مساعد الذكاء الاصطناعي (هذه الصفحة)',
        heartsName: 'القلوب',
        heartsDesc: '= الأرواح في التدريب',
      },
      usage: {
        heading: 'المساعدون المستخدمون هذا الأسبوع',
        hints: 'تلميحات',
        explain: 'شرح',
        deep: 'تعمّق',
        practice: 'تدريب',
      },
      topUp: {
        packTitle: '+{{credits}} ⚡',
        packSubtitle: 'باقة شحن · تُضاف فورًا',
        buy: 'اشترِ {{credits}} نقطة',
        comingSoon: 'الشراء داخل التطبيق قادم قريبًا.',
        priceNote: 'الأطفال لا يرون الأسعار — أنت من يدير الطاقة.',
      },
    },
    // الجدول الزمني للنشاط (الدفعة د): رقائق التصفية + صفوف الأحداث. بيانات تجريبية.
    activity: {
      title: 'النشاط',
      subtitle: 'أحدث اللحظات عبر أطفالك',
      filters: {
        navLabel: 'تصفية النشاط',
        all: 'الكل',
        badges: '🏅 الأوسمة',
        energy: '⚡ الطاقة',
        alerts: '🔔 التنبيهات',
      },
      events: {
        badgeEarned: 'حصل على وسام جديد',
        levelUp: 'ارتقى إلى المستوى {{value}}',
        lessonCompleted: 'أكمل درسًا · {{value}}/5 صحيحة',
        energyUsed: 'استخدم {{value}} مساعدين في درس',
        energyLow: 'طاقته منخفضة · {{value}} ⚡ متبقية',
        streakReached: 'وصل إلى سلسلة {{value}} أيام',
        inactive: 'غير نشط منذ {{value}} أيام',
        a11y: '{{name}} {{action}}، {{time}}',
      },
      time: {
        justNow: 'الآن',
        minutes_one: 'منذ دقيقة',
        minutes_two: 'منذ دقيقتين',
        minutes_few: 'منذ {{value}} دقائق',
        minutes_many: 'منذ {{value}} دقيقة',
        minutes_other: 'منذ {{value}} دقيقة',
        hours_one: 'منذ ساعة',
        hours_two: 'منذ ساعتين',
        hours_few: 'منذ {{value}} ساعات',
        hours_many: 'منذ {{value}} ساعة',
        hours_other: 'منذ {{value}} ساعة',
        days_one: 'منذ يوم',
        days_two: 'منذ يومين',
        days_few: 'منذ {{value}} أيام',
        days_many: 'منذ {{value}} يومًا',
        days_other: 'منذ {{value}} يوم',
      },
      empty: {
        title: 'لا شيء هنا بعد',
        body: 'لا يوجد نشاط يطابق هذا التصفية.',
      },
    },
  },
  child: {
    home: {
      greeting: 'مرحبا {{childName}}!',
      welcomeBack: 'أهلاً بعودتك',
      gradeCaption: 'الصف {{grade}}',
      subtitle: 'مغامرتك قادمة قريباً!',
      mascotMessage: 'أنا ليكسي! سأكون دليلك في رحلة التعلم.',
      mascotSender: 'ليكسي · الدليل',
      continue: {
        eyebrow: 'أكمل من حيث توقفت',
        eyebrowReplay: 'أعد آخر درس',
        cta: 'متابعة',
        replayCta: 'إعادة',
      },
      continueA11y: 'متابعة درس {{lesson}}',
      continueHintA11y: 'يفتح مشغّل الدروس',
      mission: {
        eyebrow: 'مهمة اليوم',
        startCta: 'ابدأ المهمة',
      },
      yourSubjects: 'موادك',
      welcomeEmpty: 'أهلاً! اختر مادة لتبدأ.',
      errorRetry: 'تعذّر تحميل لوحتك. أعد المحاولة',
      errorRetryCta: 'أعد المحاولة',
      statsA11y: '{{hearts}} قلوب، التحدى {{streak}} أيام، {{xp}} نقطة',
      stats: {
        hearts: 'قلوب',
        streak: 'التحدى',
        xp: 'نقاط الخبرة هذا الأسبوع',
      },
      practiceMode: 'وضع التدريب',
      practiceModeA11y: 'وضع التدريب مفعّل — الإجابات الخاطئة لا تُكلّف قلوباً',
      boss: 'بوس',
      leagueTier: {
        bronze: 'برونز',
        silver: 'فضة',
        gold: 'ذهب',
        diamond: 'ألماس',
      },
      leaguePreview: {
        rankLabel: 'المرتبة {{rank}} من {{total}}',
        rankUnknown: 'جارٍ احتساب مرتبتك…',
        a11y: 'دوري {{tier}}، المرتبة {{rank}} من {{total}}، {{xp}} نقطة هذا الأسبوع',
        hintA11y: 'يفتح ترتيب دوريك',
      },
      // 3c (B-int) — نقاط الدخول للتلعيب على لوحة الطفل الرئيسية
      // (Design Spec P4-08 §10: الرقائق + لافتة الفعاليات + رابط النشاط).
      statsNav: {
        hearts: '{{value}} قلوب — اعرض التفاصيل',
        streak: 'التحدى {{value}} أيام — اعرض التفاصيل',
        xp: '{{value}} نقطة هذا الأسبوع — اعرض التفاصيل',
        freeze: '{{value}} مجمِّدات — اعرض الفعاليات',
      },
      events: {
        seeAll: 'عرض الكل ←',
        bannerA11y: '{{name}} — فعالية {{multiplier}}. يفتح الفعاليات والتحديات',
      },
      activity: {
        seeAll: 'عرض الكل ←',
        a11y: 'نشاطي — اعرض كل محاولاتك في الدروس',
      },
      // 3c (B-int) — نوافذ الاحتفال على اللوحة (useDashboardDiff؛ نص الارتقاء
      // وإكمال المهمة يعيد استخدام xp.levelUp.* / missions.complete.*).
      celebrate: {
        cta: 'واصل التقدم',
        badgeTitle: 'شارة جديدة!',
        badgeA11y: 'شارة جديدة: {{name}}. كسبت {{xp}} نقطة',
        streakTitle: 'محطة جديدة في التحدى !',
        streakSubtitle_zero: 'التحدى {{value}} أيام — حافظ عليها!',
        streakSubtitle_one: 'التحدى يوم واحد — حافظ عليها!',
        streakSubtitle_two: 'التحدى يومين — حافظ عليها!',
        streakSubtitle_few: 'التحدى {{value}} أيام — حافظ عليها!',
        streakSubtitle_many: 'التحدى {{value}} يومًا — حافظ عليها!',
        streakSubtitle_other: 'التحدى {{value}} يوم — حافظ عليها!',
        streakA11y_zero: 'محطة جديدة! التحدى {{value}} أيام. كسبت {{xp}} نقطة',
        streakA11y_one: 'محطة جديدة! التحدى يوم واحد. كسبت {{xp}} نقطة',
        streakA11y_two: 'محطة جديدة! التحدى يومين. كسبت {{xp}} نقطة',
        streakA11y_few: 'محطة جديدة! التحدى {{value}} أيام. كسبت {{xp}} نقطة',
        streakA11y_many: 'محطة جديدة! التحدى {{value}} يومًا. كسبت {{xp}} نقطة',
        streakA11y_other: 'محطة جديدة! التحدى {{value}} يوم. كسبت {{xp}} نقطة',
      },
    },
    subjects: {
      title: 'المواد',
      gradeLabel: 'الصف {{grade}}',
      masteryLabel: '{{percent}}٪ إتقان',
      empty: 'قريباً — لا توجد دروس بعد',
      subjectNotFound: 'المادة غير موجودة',
      errorRetry: 'تعذّر التحميل. حاول مرة أخرى',
      backToSubjects: 'عودة إلى المواد',
      signOut: 'تسجيل الخروج',
      tabs: {
        lessons: 'الدروس',
        tree: 'شجرة المهارات',
      },
      lessons: {
        unitLabel: 'الوحدة {{n}}',
        unitsCount: '{{units}} وحدات · {{lessons}} دروس',
        meta: {
          minutes: '{{n}} دقائق',
          questions: '{{n}} أسئلة',
          xp: '+{{n}} نقطة',
        },
        tagCompleted: 'مكتمل',
        tagLocked: 'مقفل',
        emptyUnit: 'قريباً',
      },
      whyLocked: {
        eyebrow: 'مقفل',
        title: 'لماذا هذا مقفل؟',
        intro: 'أكمل هذه لفتح الدرس:',
        needLine: 'أنت عند {{currentAccuracy}}٪ — تحتاج {{requiredAccuracy}}٪',
        generic: 'أكمل الدرس السابق أولاً',
        cta: 'حسناً!',
      },
    },
    skillTree: {
      unitOf: 'الوحدة {{current}} من {{total}}',
      mastery: 'الإتقان {{percent}}٪',
      conceptEyebrow: 'المفهوم · {{name}}',
      subInProgress: 'قيد التقدم',
      subCompleted: 'مُتقن',
      subLocked: 'مقفل',
      subBossChallenge: 'تحدي البوس',
      subBossBeaten: 'تم هزم البوس',
      bossLabel: 'بوس',
      a11y: {
        lockedHint: 'اضغط مرتين لمعرفة ما يفتح هذا',
      },
    },
    lessons: {
      intro: {
        eyebrow: 'درس',
        startCta: 'ابدأ الدرس',
        aiBubbleFallback: 'سأشرح لك — اضغط ابدأ عندما تكون مستعدًا.',
        noQuestions: 'لا توجد أسئلة في هذا الدرس بعد — عُد لاحقًا',
        noQuestionsSub: 'عُد لاحقًا — نضيف المزيد قريبًا.',
        back: 'الرجوع إلى الدروس',
        errorRetry: 'تعذّر تحميل الدرس. حاول مرة أخرى',
        notFound: 'الدرس غير موجود',
      },
      a11y: {
        optionState: {
          selected: 'مختار',
          correct: 'الإجابة الصحيحة',
          incorrect: 'إجابة خاطئة',
        },
        feedback: {
          correctRegion: 'إجابتك صحيحة. أحسنت!',
          incorrectRegion: 'إجابتك خاطئة. الإجابة الصحيحة هي {{answer}}',
        },
      },
    },
    quiz: {
      questionOf: 'السؤال {{current}} من {{total}}',
      hint: 'تلميح',
      hintComingSoon: 'التلميح قريبًا',
      submit: 'تحقّق',
      next: 'التالي',
      true: 'صحيح',
      false: 'خطأ',
      fillPlaceholder: 'اكتب إجابتك',
      matchingStub: 'أسئلة المطابقة قريبًا',
      matchingSkip: 'اضغط التالي للتخطي',
      networkError: 'تعذر التحقق — حاول مرة أخرى',
      empty: 'لا توجد أسئلة بعد',
    },
    feedback: {
      correct: 'أحسنت!',
      incorrect: 'ليست الإجابة الصحيحة',
      correctAnswer: 'الإجابة الصحيحة: {{answer}}',
    },
    summary: {
      title: 'اكتمل الدرس!',
      score: '{{correct}} / {{total}}',
      scoreLabel: 'صحيحة',
      accuracy: '{{percent}}٪',
      accuracyLabel: 'الدقة',
      duration: '{{seconds}}ث',
      durationLabel: 'الوقت',
      backToSubject: 'الرجوع إلى الدروس',
      tryAgain: 'حاول مجددًا',
      xpStub: '+١٠ نقطة خبرة (قريبًا)',
    },
    // A5 — شاشة «نشاطي» لسجل المحاولات (Design Spec A5-attempt-history.md §2/§4 — النص العربي نهائي وفق المواصفة).
    attempts: {
      title: 'نشاطي',
      sub: 'انظر كم أنجزت!',
      back: 'رجوع',
      summary: {
        attempts: 'المحاولات',
        completed: 'مكتملة',
        best: 'أفضل نتيجة',
      },
      group: {
        thisWeek: 'هذا الأسبوع',
        earlier: 'سابقًا',
      },
      row: {
        lessonFallback: 'الدرس {{n}}',
        today: 'اليوم',
        yesterday: 'أمس',
        completed: 'مكتملة',
        inProgress: 'قيد التنفيذ',
      },
      empty: {
        title: 'لا مغامرات بعد — ابدأ أول درس!',
        cta: 'ابدأ التعلم',
      },
      error: 'تعذر تحميل نشاطك. حاول مرة أخرى',
      showMore: 'عرض المزيد',
      listA11y: '{{count}} محاولة',
    },
  },
  // B0-nav — شريط التنقّل السفلي لتطبيق الطفل (Design Spec §1 — AR copy is final per spec §4).
  nav: {
    tabs: {
      home: 'الرئيسية',
      missions: 'المهام',
      league: 'الدوري',
      badges: 'الشارات',
      barLabel: 'التنقل الرئيسي',
    },
    stub: {
      comingSoon: 'قريبًا',
    },
  },
  // C5 — لوحة المطابقة بالنقر للوصل (Design Spec §5 — النص العربي نهائي وفق المواصفة).
  quiz: {
    matching: {
      instruction: 'اضغط بطاقتين لتصل بينهما',
      promptHeader: 'صِل هذه',
      answerHeader: '…بهذه',
      armedA11y: '{{text}}، مختار، اختر ما يطابقه',
      pairedA11y: '{{text}}، موصول مع {{partner}}، اضغط لفك الوصل',
      pairedAnnounce: 'تم الوصل: {{a}} مع {{b}}',
      unpairedAnnounce: 'تم فك الوصل: {{a}} و{{b}}',
      pairFormat: '{{left}} – {{right}}',
      pairJoin: '، ',
    },
  },
  // B1 — شاشة النقاط والمستوى + لحظة الارتقاء (Design Spec P4-08 §1 — النص العربي وفق المواصفة).
  // عدّادات XP بصيغة "n / m XP" تبقى بأرقام لاتينية واتجاه LTR (قاعدة الأرقام في SKILL.md)؛
  // أرقام المستويات نصية وتُعرض بالأرقام الشرقية.
  xp: {
    back: 'رجوع',
    hero: {
      eyebrow: 'مستواك',
      level: 'المستوى {{value}}',
      a11y: 'أنت في المستوى {{level}}',
    },
    progress: {
      header: '📈 المستوى {{from}} ← {{to}}',
      counter: '{{current}} / {{total}} XP',
      counterTotalOnly: '{{xp}} XP',
      footerPercent: '{{percent}} للوصول للمستوى {{next}}',
      footerLeft: 'متبقي {{xp}} نقطة',
      barA11y: '{{current}} من {{total}} نقطة للوصول للمستوى {{next}}',
    },
    totalXp: {
      label: 'إجمالي النقاط',
      a11y: 'إجمالي النقاط {{xp}}',
    },
    levelUp: {
      title: 'ارتقيت في المستوى!',
      subtitle: 'وصلت إلى المستوى {{level}}',
      cta: 'واصل التقدم',
      a11y: 'ارتقيت في المستوى! وصلت إلى المستوى {{level}} وكسبت {{xp}} نقطة',
    },
    error: 'تعذّر تحميل نقاطك. حاول مجددًا!',
  },
  // B2 — شاشة التحدى (Design Spec P4-08 §2 — النص العربي وفق المواصفة).
  streak: {
    back: 'رجوع',
    eyebrow: 'التحدى',
    days_zero: '{{value}} أيام',
    days_one: 'يوم واحد',
    days_two: 'يومان',
    days_few: '{{value}} أيام',
    days_many: '{{value}} يومًا',
    days_other: '{{value}} يوم',
    keepAlive: 'حافظ عليها!',
    zeroTitle: 'ابدأ سلسلتك اليوم!',
    freeze: {
      title: 'مجمِّدات التحدى',
      explainer: 'تحمي المجمِّدات سلسلتك تلقائيًا عند تفويت يوم',
      countA11y_zero: 'لا مجمِّدات بعد — واصل التعلم لتكسبها!',
      countA11y_one: 'مجمِّد واحد جاهز',
      countA11y_two: 'مجمِّدان جاهزان',
      countA11y_few: '{{value}} مجمِّدات جاهزة',
      countA11y_many: '{{value}} مجمِّدًا جاهزًا',
      countA11y_other: '{{value}} مجمِّد جاهز',
    },
    milestones: {
      eyebrow: 'محطات التحدى',
      dayLabel: 'اليوم {{value}}',
      next: 'المحطة التالية: اليوم {{target}}!',
      allDone: 'تجاوزت كل المحطات — مذهل!',
      reachedA11y: 'وصلت إلى محطة اليوم {{value}}',
      upcomingA11y: 'محطة اليوم {{value}} — قريبًا!',
    },
    history: {
      comingSoon: 'تقويم سلسلتك قادم قريبًا!',
      body: 'قريبًا سترى هنا كل يوم تتدرب فيه.',
    },
    error: 'تعذر تحميل سلسلتك. حاول مجددًا!',
  },
  // B3 — شاشة القلوب + وضع التدريب (Design Spec P4-08 §3.1 — النص العربي وفق المواصفة).
  hearts: {
    back: 'رجوع',
    title: 'قلوبك',
    sub: 'تفقد قلبًا مع كل إجابة خاطئة',
    subFull: 'قلوبك مكتملة — هيا نتعلم!',
    rowA11y: '{{current}} من {{max}} قلوب',
    refillNote: 'تعود القلوب مع الوقت — أو اكسبها بالتدريب',
    lost: {
      title: 'فقدت قلبًا',
      sub: 'خذ وقتك — حاول مجددًا',
    },
    practiceMode: {
      title: 'وضع التدريب مفعّل',
      body: 'إجابات التدريب لا تُكلّف قلوبًا وتعيدها إليك — واصل التعلم!',
      cta: 'وضع التدريب (بدون قلوب)',
    },
    error: 'تعذّر تحميل قلوبك. حاول مجددًا',
  },
  // B4 — شارات الطفل (Design Spec P4-08 §4). الأسماء والتلميحات وفق كتالوج BadgeSeeder.
  badges: {
    title: 'الشارات',
    sub: '{{earned}} من {{total}} مكتسبة · اجمعها كلها',
    subEmpty: 'اكسب أول شارة بإكمال درس!',
    empty: 'لا شارات بعد — اكسب أولى شاراتك!',
    error: 'تعذّر تحميل شاراتك. حاول مجددًا',
    stats: {
      bronze: 'برونزية',
      silver: 'فضية',
      gold: 'ذهبية',
      legend: 'أسطورية',
      cellA11y: '{{label}}: {{value}} مكتسبة',
    },
    status: {
      earned: 'مكتسبة',
      locked: 'مقفلة',
    },
    earnedOn: 'اكتُسبت في {{date}}',
    tileA11y: {
      earnedOn: '{{name}} — شارة {{rarity}}، اكتُسبت في {{date}}',
      earned: '{{name}} — شارة {{rarity}}، مكتسبة',
      locked: '{{name}} — مقفلة. {{hint}}',
      lockedNoHint: '{{name}} — مقفلة',
    },
    rarity: {
      bronze: 'برونزية',
      silver: 'فضية',
      gold: 'ذهبية',
      legendary: 'أسطورية',
    },
    names: {
      FIRST_LESSON: 'الخطوات الأولى',
      STREAK_3: 'بداية الحماس',
      STREAK_7: 'متوهج',
      STREAK_14: 'بطل الأسبوعين',
      STREAK_30: 'لا يُوقف',
      STREAK_100: 'قائد المئة',
      LEVEL_5: 'مبتدئ واعد',
      LEVEL_10: 'عالِم صغير',
      LEVEL_20: 'محترف',
      LEGENDARY_50: 'أسطورة',
    },
    hints: {
      FIRST_LESSON: 'أكمل درسك الأول',
      STREAK_3: 'حافظ على التحدى لمدة ٣ أيام',
      STREAK_7: 'حافظ على التحدى لمدة ٧ أيام',
      STREAK_14: 'حافظ على التحدى لمدة ١٤ يومًا',
      STREAK_30: 'حافظ على التحدى لمدة ٣٠ يومًا',
      STREAK_100: 'حافظ على التحدى لمدة ١٠٠ يوم',
      LEVEL_5: 'صِل إلى المستوى ٥',
      LEVEL_10: 'صِل إلى المستوى ١٠',
      LEVEL_20: 'صِل إلى المستوى ٢٠',
      LEGENDARY_50: 'صِل إلى المستوى ٥٠',
    },
  },
  // B5 — شاشة المهام (Design Spec P4-08 §5 — النص العربي وفق المواصفة).
  // قيم XP تبقى بأرقام لاتينية واتجاه LTR (قاعدة الأرقام في SKILL.md)؛
  // عدّادات المهام نصية وتُعرض بالأرقام الشرقية.
  missions: {
    header: {
      eyebrow: 'مهمة اليوم',
      progress: '{{done}} من {{total}} مكتملة',
      resets: 'تتجدد عند منتصف الليل',
    },
    hero: {
      xp: '+{{xp}} XP',
      sub: 'أكملها كلها واحصل عليها!',
      a11y: 'أكمل كل مهام اليوم لتكسب {{xp}} نقطة',
    },
    weekly: {
      eyebrow: 'هذا الأسبوع',
      chip: 'أسبوعية',
    },
    row: {
      countSub: '{{progress}} من {{target}}',
      completed: 'اكتملت',
      expired: 'انتهى الوقت — عُد غدًا',
      reward: '⭐ +{{xp}}',
      a11y: '{{title}}، أنجزت {{progress}} من {{target}}، المكافأة {{xp}} نقطة',
      completedA11y: '{{title}}، اكتملت وكسبت {{xp}} نقطة',
      expiredA11y: '{{title}}، انتهى الوقت — عُد غدًا',
    },
    complete: {
      title: 'أكملت المهمة!',
      subtitle: 'أنهيت كل مهام اليوم',
      cta: 'واصل التقدم',
      a11y: 'أكملت المهمة! كسبت {{xp}} نقطة',
    },
    empty: 'مهام جديدة عند منتصف الليل',
    error: 'تعذّر تحميل مهامك. حاول مجددًا!',
    titles: {
      mission: {
        daily_3_lessons: { title: 'أكمل ٣ دروس' },
        daily_10_correct: { title: 'أجب عن ١٠ أسئلة إجابة صحيحة' },
        daily_keep_streak: { title: 'حافظ على سلسلتك' },
        daily_1_lesson_early: { title: 'أكمل درسًا مبكرًا اليوم' },
        daily_20_correct: { title: 'أجب عن ٢٠ سؤالًا إجابة صحيحة' },
        weekly_15_lessons: { title: 'أكمل ١٥ درسًا هذا الأسبوع' },
        weekly_75_correct: { title: 'أجب عن ٧٥ سؤالًا إجابة صحيحة هذا الأسبوع' },
        weekly_5_day_streak: { title: 'حافظ على التحدى ٥ أيام' },
        challenge_25_lessons: { title: 'تحدٍّ: ٢٥ درسًا في أسبوع واحد' },
        challenge_100_correct: { title: 'تحدٍّ: ١٠٠ إجابة صحيحة' },
        challenge_5_day_streak: { title: 'تحدٍّ: ٥ أيام متتالية' },
      },
      unknown: 'مهمة جديدة',
    },
  },
  // B6 — شاشة ترتيب الدوري (Design Spec P4-08 §6 — النص العربي وفق المواصفة).
  // عدّادات النقاط تبقى بأرقام لاتينية + "XP" في كلتا اللغتين (SKILL.md، G-8).
  league: {
    banner: {
      title: 'دوري {{tier}}',
      daysLeft_zero: '{{value}} أيام متبقية',
      daysLeft_one: 'يوم واحد متبقٍ',
      daysLeft_two: 'يومان متبقيان',
      daysLeft_few: '{{value}} أيام متبقية',
      daysLeft_many: '{{value}} يومًا متبقيًا',
      daysLeft_other: '{{value}} يوم متبقٍ',
      hoursLeft_zero: '{{value}} ساعات متبقية',
      hoursLeft_one: 'ساعة واحدة متبقية',
      hoursLeft_two: 'ساعتان متبقيتان',
      hoursLeft_few: '{{value}} ساعات متبقية',
      hoursLeft_many: '{{value}} ساعة متبقية',
      hoursLeft_other: '{{value}} ساعة متبقية',
      endingSoon: 'ينتهي قريبًا',
      promote_zero: 'لا أحد يصعد',
      promote_one: 'الأول يصعد',
      promote_two: 'أفضل اثنين يصعدان',
      promote_few: 'أفضل {{value}} يصعدون',
      promote_many: 'أفضل {{value}} يصعدون',
      promote_other: 'أفضل {{value}} يصعدون',
      a11y: 'دوري {{tier}}. {{timeLeft}}. {{promote}}',
    },
    countdown: {
      full: 'تتجدد بعد {{days}} و{{hours}}',
      hoursOnly: 'تتجدد بعد {{hours}}',
      soon: 'تتجدد قريبًا!',
      days_zero: '{{value}} أيام',
      days_one: 'يوم واحد',
      days_two: 'يومين',
      days_few: '{{value}} أيام',
      days_many: '{{value}} يومًا',
      days_other: '{{value}} يوم',
      hours_zero: '{{value}} ساعات',
      hours_one: 'ساعة واحدة',
      hours_two: 'ساعتين',
      hours_few: '{{value}} ساعات',
      hours_many: '{{value}} ساعة',
      hours_other: '{{value}} ساعة',
    },
    list: {
      xp: '{{xp}} XP',
      youChip: 'أنت',
      rowA11y: 'المركز {{rank}}، {{name}}، {{xp}} نقطة هذا الأسبوع',
      youRowA11y: 'المركز {{rank}}، أنت، {{xp}} نقطة هذا الأسبوع',
      aloneHint: 'ينضم متعلمون أكثر خلال الأسبوع — واصل كسب النقاط!',
    },
    zones: {
      promotion: '↑ منطقة الصعود',
      demotion: '↓ منطقة الهبوط',
    },
    empty: {
      title: 'اكسب نقاطًا هذا الأسبوع للانضمام إلى دوري!',
    },
    error: 'تعذّر تحميل الدوري. حاول مجددًا!',
  },
  // B8 — شاشة الفعاليات: مجمِّد التحدى + الفعاليات المؤقتة + تحدي الأسبوع
  // (Design Spec P4-08 §8). المجمِّدات تُكتسب فقط — لا نصوص شراء.
  events: {
    back: 'رجوع',
    title: 'الفعاليات والتحديات',
    error: 'تعذّر تحميل الفعاليات. حاول مجددًا!',
    freeze: {
      eyebrow: 'مجمِّد التحدى',
      countA11y_zero: 'لا مجمِّدات بعد — واصل التعلم لتكسبها!',
      countA11y_one: 'مجمِّد واحد جاهز',
      countA11y_two: 'مجمِّدان جاهزان',
      countA11y_few: '{{value}} مجمِّدات جاهزة',
      countA11y_many: '{{value}} مجمِّدًا جاهزًا',
      countA11y_other: '{{value}} مجمِّد جاهز',
      explainer: 'تحمي المجمِّدات سلسلتك تلقائيًا عند تفويت يوم',
      earnedNote: 'تكسب المجمِّدات بالتعلم — لا شيء للشراء!',
      zeroNote: 'لا مجمِّدات بعد — واصل التعلم لتكسبها!',
    },
    timed: {
      eyebrow: 'فعاليات مؤقتة',
      multiplier: '×{{value}} XP',
      endsInDayHour: 'ينتهي بعد {{d}} يوم و{{h}} ساعة',
      endsInHourMinute: 'ينتهي بعد {{h}} س {{m}} د',
      endsInMinute: 'ينتهي بعد {{m}} د',
      cardA11y: '{{name}} — {{multiplier}} — {{ends}}',
      more_zero: 'لا فعاليات أخرى',
      more_one: '+ فعالية أخرى',
      more_two: '+ فعاليتان أخريان',
      more_few: '+ {{value}} فعاليات أخرى',
      more_many: '+ {{value}} فعالية أخرى',
      more_other: '+ {{value}} فعالية أخرى',
      empty: {
        title: 'لا فعاليات الآن',
        body: 'ستظهر فعاليات النقاط الخاصة هنا — عُد قريبًا!',
      },
    },
    challenges: {
      eyebrow: 'تحدي الأسبوع',
      progressLabel: '{{progress}} من {{target}}',
      completed: 'اكتملت',
      expired: 'انتهى الوقت — تحدٍّ جديد قادم!',
      reward: '⭐ +{{xp}}',
      cardA11y: '{{title}} — {{status}} — المكافأة {{xp}} نقطة',
      error: 'تعذّر تحميل التحديات. حاول مجددًا!',
      titles: {
        lessons25: 'أكمل ٢٥ درسًا هذا الأسبوع',
        correct100: 'أجب عن ١٠٠ سؤال إجابة صحيحة هذا الأسبوع',
        streak5: 'تعلّم ٥ أيام متتالية',
        fallback: 'تحدي الأسبوع',
      },
      empty: {
        title: 'لا تحدي الآن',
        body: 'ستظهر تحديات الأسبوع هنا — عُد قريبًا!',
      },
    },
  },
  /**
   * Backend recommendation item i18n keys (P5-05 real wiring) — AR.
   * Mirrors `en.rec` exactly. Source: SharedResources.ar-EG.resx.
   */
  rec: {
    RecReviewTitle: 'حان وقت المراجعة',
    RecReviewBody: 'هذا الموضوع يحتاج مراجعة المفهوم قبل مزيد من التدريب.',
    RecReviewCta: 'مراجعة المفهوم',
    RecPracticeTitle: 'واصل التدريب',
    RecPracticeBody: 'تدرّب على هذه المهارة لتعزيز فهمك.',
    RecPracticeCta: 'ابدأ التدريب',
    RecColdStartTitle: 'بداية رائعة!',
    RecColdStartBody: 'ليس لديك مناطق ضعف حتى الآن — أحسنت!',
    RecColdStartCta: 'تابع التعلم',
  },
} as const;

export const resources = {
  en,
  ar,
} as const;
