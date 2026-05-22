/**
 * i18n resources — ar (Arabic-first) + en.
 *
 * Namespaces: `common`, `auth`, `onboarding`, `parent`, `child`. All P1-09 auth
 * & onboarding copy slots from the Design Spec live here. `defaultNS` is
 * `common`. The `en` object is the canonical shape; `ar` mirrors it exactly so
 * no key is missing in either locale (the QA pass verifies this).
 */

export const NAMESPACES = [
  'common',
  'auth',
  'onboarding',
  'parent',
  'child',
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
      title: 'Create your account',
      subtitle: 'Set up your parent account to get started',
      labelFullName: 'Full Name (optional)',
      labelEmail: 'Email address',
      labelPassword: 'Password',
      labelConfirmPassword: 'Confirm password',
      submitButton: 'Create Account',
      haveAccount: 'Already have an account?',
      signInLink: 'Sign in',
      backToSignIn: 'Back to Sign in',
      errors: {
        duplicateEmail: 'An account with this email already exists.',
        weakPassword:
          'Password must be at least 6 characters with uppercase, lowercase, number, and special character.',
        passwordMismatch: 'Passwords do not match.',
        invalidEmail: 'Please enter a valid email address.',
      },
    },
    login: {
      title: 'Welcome back!',
      subtitle: 'Sign in to continue your learning journey',
      labelUsername: 'Username or email',
      labelPassword: 'Password',
      submitButton: 'Sign In',
      newParent: 'New parent?',
      createAccount: 'Create account',
      errors: {
        invalidCredentials: 'Incorrect username or password.',
        notFound: 'No account found with this email.',
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
      title: 'Add your child',
      subtitle:
        "Fill in your child's details. You can add more children after.",
      labelName: "Child's full name",
      labelEmail: 'Login email',
      labelPassword: 'Password',
      labelGrade: 'Grade',
      labelLanguage: 'Preferred language',
      labelCountry: 'Country',
      gradePlaceholder: 'Select grade',
      languagePlaceholder: 'Select language',
      addToListButton: 'Add Child to List',
      submitButton: 'Add {{count}} Child(ren) and Continue',
      listLabel: 'Children to add ({{count}})',
      added: 'Added!',
      partialFailureBanner:
        'Some children could not be added. Please fix the errors and try again.',
      errors: {
        nameRequired: "Child's name is required.",
        languageRequired: 'Please select a language.',
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
    myChildren: {
      title: 'My Children',
      sectionLabel: 'Children ({{count}})',
      empty: 'No children linked yet.',
      linkButton: 'Link existing child',
      loadError: 'Could not load your children. Tap to retry.',
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
  },
  child: {
    home: {
      greeting: 'Hi, {{childName}}!',
      subtitle: 'Your adventure is coming soon!',
      mascotMessage: "I'm Lexi! I'll be your learning guide.",
      mascotSender: 'Lexi · Guide',
    },
  },
} as const;

export const ar = {
  common: {
    appName: 'ليرنيكسيا',
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
      title: 'أنشئ حسابك',
      subtitle: 'أنشئ حسابك كولي أمر للبدء',
      labelFullName: 'الاسم الكامل (اختياري)',
      labelEmail: 'البريد الإلكتروني',
      labelPassword: 'كلمة المرور',
      labelConfirmPassword: 'تأكيد كلمة المرور',
      submitButton: 'إنشاء حساب',
      haveAccount: 'لديك حساب بالفعل؟',
      signInLink: 'تسجيل الدخول',
      backToSignIn: 'العودة لتسجيل الدخول',
      errors: {
        duplicateEmail: 'يوجد حساب بهذا البريد الإلكتروني بالفعل.',
        weakPassword:
          'يجب أن تحتوي كلمة المرور على 6 أحرف على الأقل مع حروف كبيرة وصغيرة ورقم وحرف خاص.',
        passwordMismatch: 'كلمتا المرور غير متطابقتين.',
        invalidEmail: 'يرجى إدخال بريد إلكتروني صحيح.',
      },
    },
    login: {
      title: 'مرحباً بعودتك!',
      subtitle: 'سجّل دخولك لمتابعة رحلتك التعليمية',
      labelUsername: 'اسم المستخدم أو البريد الإلكتروني',
      labelPassword: 'كلمة المرور',
      submitButton: 'تسجيل الدخول',
      newParent: 'ولي أمر جديد؟',
      createAccount: 'إنشاء حساب',
      errors: {
        invalidCredentials: 'اسم المستخدم أو كلمة المرور غير صحيحة.',
        notFound: 'لا يوجد حساب بهذا البريد الإلكتروني.',
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
      title: 'أضف طفلك',
      subtitle: 'أدخل بيانات طفلك. يمكنك إضافة أطفال آخرين بعد ذلك.',
      labelName: 'الاسم الكامل للطفل',
      labelEmail: 'البريد الإلكتروني للدخول',
      labelPassword: 'كلمة المرور',
      labelGrade: 'الصف الدراسي',
      labelLanguage: 'اللغة المفضلة',
      labelCountry: 'الدولة',
      gradePlaceholder: 'اختر الصف',
      languagePlaceholder: 'اختر اللغة',
      addToListButton: 'إضافة الطفل إلى القائمة',
      submitButton: 'إضافة {{count}} طفل/أطفال والمتابعة',
      listLabel: 'الأطفال المراد إضافتهم ({{count}})',
      added: 'تمت الإضافة!',
      partialFailureBanner:
        'تعذر إضافة بعض الأطفال. يرجى تصحيح الأخطاء والمحاولة مجدداً.',
      errors: {
        nameRequired: 'اسم الطفل مطلوب.',
        languageRequired: 'يرجى اختيار لغة.',
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
    myChildren: {
      title: 'أطفالي',
      sectionLabel: 'الأطفال ({{count}})',
      empty: 'لم يتم ربط أي أطفال بعد.',
      linkButton: 'ربط طفل موجود',
      loadError: 'تعذر تحميل قائمة أطفالك. اضغط للمحاولة مجدداً.',
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
  },
  child: {
    home: {
      greeting: 'مرحباً، {{childName}}!',
      subtitle: 'مغامرتك قادمة قريباً!',
      mascotMessage: 'أنا ليكسي! سأكون دليلك في رحلة التعلم.',
      mascotSender: 'ليكسي · الدليل',
    },
  },
} as const;

export const resources = {
  en,
  ar,
} as const;
