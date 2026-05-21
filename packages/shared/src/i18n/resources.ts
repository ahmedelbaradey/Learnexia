/**
 * i18n resource scaffolding — namespaces only, minimal keys.
 *
 * Full translations are filled in per-screen during feature work. We define
 * the namespace shape (`common`, `auth`) and a couple of seed keys so the
 * init is valid and TypeScript can infer the resource type.
 */

export const NAMESPACES = ['common', 'auth'] as const;
export type Namespace = (typeof NAMESPACES)[number];

export const defaultNS: Namespace = 'common';

export const en = {
  common: {
    appName: 'Learnexia',
    ok: 'OK',
    cancel: 'Cancel',
    retry: 'Retry',
    loading: 'Loading…',
  },
  auth: {
    signIn: 'Sign in',
    signOut: 'Sign out',
    userName: 'Username',
    password: 'Password',
    errors: {
      userNameRequired: 'Username is required',
      passwordRequired: 'Password is required',
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
  },
  auth: {
    signIn: 'تسجيل الدخول',
    signOut: 'تسجيل الخروج',
    userName: 'اسم المستخدم',
    password: 'كلمة المرور',
    errors: {
      userNameRequired: 'اسم المستخدم مطلوب',
      passwordRequired: 'كلمة المرور مطلوبة',
    },
  },
} as const;

export const resources = {
  en,
  ar,
} as const;
