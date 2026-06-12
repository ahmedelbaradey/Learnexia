/**
 * Admin-local copy slots (EN + AR), keyed per the Design Spec §3f / §4.
 *
 * The admin dashboard ships English-first (v1) but the providers are wired for
 * RTL/AR readiness, so all copy is bilingual. A locale toggle is out of scope
 * for P1-10 (Design Spec Gap 4) — `getStrings('en')` is used by default.
 */

import type { Locale } from '@learnexia/shared/constants';

export interface AdminStrings {
  // Login page
  loginPageTitle: string;
  loginHeading: string;
  loginSubheading: string;
  usernameLabel: string;
  usernamePlaceholder: string;
  passwordLabel: string;
  passwordPlaceholder: string;
  signInButton: string;
  signingInButton: string;
  errInvalidCredentials: string;
  errAccountLocked: string;
  errAccountDeactivated: string;
  errForbidden: string;
  errNetwork: string;
  finePrint: string;
  showPassword: string;
  hidePassword: string;

  // Shell
  navCurriculum: string;
  navContent: string;
  signOutButton: string;
  pageTitleDashboard: string;
  dashboardHeading: string;
  dashboardSubtext: string;
  dashboardPlaceholder: string;
  openNav: string;
  closeNav: string;

  // Loading
  loadingLabel: string;
}

const en: AdminStrings = {
  loginPageTitle: 'Admin Sign In — Learnexia',
  loginHeading: 'Admin Sign In',
  loginSubheading: 'Authorised administrators only',
  usernameLabel: 'Username',
  usernamePlaceholder: 'Enter your username',
  passwordLabel: 'Password',
  passwordPlaceholder: 'Enter your password',
  signInButton: 'Sign In',
  signingInButton: 'Signing In…',
  errInvalidCredentials: 'Incorrect username or password.',
  errAccountLocked:
    'Account temporarily locked after too many failed attempts. Please try again later.',
  errAccountDeactivated: 'This account has been deactivated. Please contact your administrator.',
  errForbidden: 'Access denied. This portal is for administrators only.',
  errNetwork: 'Something went wrong. Please try again.',
  finePrint: 'Authorised personnel only. No self-registration.',
  showPassword: 'Show password',
  hidePassword: 'Hide password',

  navCurriculum: 'Curriculum',
  navContent: 'Content',
  signOutButton: 'Sign Out',
  pageTitleDashboard: 'Dashboard',
  dashboardHeading: 'Welcome to Learnexia Admin',
  dashboardSubtext: 'Select a section from the navigation to get started.',
  dashboardPlaceholder: 'Admin features coming soon.',
  openNav: 'Open navigation',
  closeNav: 'Close navigation',

  loadingLabel: 'Loading, please wait',
};

const ar: AdminStrings = {
  loginPageTitle: 'تسجيل الدخول للمسؤول — Learnexia',
  loginHeading: 'تسجيل الدخول للمسؤول',
  loginSubheading: 'للمسؤولين المعتمدين فقط',
  usernameLabel: 'اسم المستخدم',
  usernamePlaceholder: 'أدخل اسم المستخدم',
  passwordLabel: 'كلمة المرور',
  passwordPlaceholder: 'أدخل كلمة المرور',
  signInButton: 'تسجيل الدخول',
  signingInButton: 'جارٍ تسجيل الدخول…',
  errInvalidCredentials: 'اسم المستخدم أو كلمة المرور غير صحيحة.',
  errAccountLocked: 'تم قفل الحساب مؤقتًا بعد عدة محاولات فاشلة. يرجى المحاولة لاحقًا.',
  errAccountDeactivated: 'تم إلغاء تنشيط هذا الحساب. يرجى التواصل مع المسؤول.',
  errForbidden: 'الوصول مرفوض. هذه البوابة مخصصة للمسؤولين فقط.',
  errNetwork: 'حدث خطأ ما. يرجى المحاولة مرة أخرى.',
  finePrint: 'للموظفين المعتمدين فقط. لا يوجد تسجيل ذاتي.',
  showPassword: 'إظهار كلمة المرور',
  hidePassword: 'إخفاء كلمة المرور',

  navCurriculum: 'المناهج',
  navContent: 'المحتوى',
  signOutButton: 'تسجيل الخروج',
  pageTitleDashboard: 'لوحة التحكم',
  dashboardHeading: 'مرحبًا بك في إدارة Learnexia',
  dashboardSubtext: 'اختر قسمًا من القائمة للبدء.',
  dashboardPlaceholder: 'ميزات الإدارة قادمة قريبًا.',
  openNav: 'فتح القائمة',
  closeNav: 'إغلاق القائمة',

  loadingLabel: 'جارٍ التحميل، يرجى الانتظار',
};

const STRINGS: Record<Locale, AdminStrings> = { en, ar };

/** Default admin locale (English-first per Design Spec §7). */
export const ADMIN_LOCALE: Locale = 'en';

export function getStrings(locale: Locale = ADMIN_LOCALE): AdminStrings {
  return STRINGS[locale] ?? en;
}
