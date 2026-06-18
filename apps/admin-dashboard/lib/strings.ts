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
  /** P7-06-FE-5 — real nav item for the Users surface. */
  navUsers: string;
  signOutButton: string;
  pageTitleDashboard: string;
  dashboardHeading: string;
  dashboardSubtext: string;
  dashboardPlaceholder: string;
  openNav: string;
  closeNav: string;

  // Loading
  loadingLabel: string;

  // ── P7-06 Users surface (Batch A — base copy seeded here; B/C/D append) ────
  /** Page title shown in AdminTopBar for the Users list. */
  pageTitleUsers: string;
  /** Page title shown in AdminTopBar for the User detail view. */
  pageTitleUserDetail: string;

  // List page
  usersListHeading: string;
  usersListRoleFilterLabel: string;
  usersListStatusFilterLabel: string;
  usersListSearchPlaceholder: string;
  usersListRoleOptionParent: string;
  usersListRoleOptionStudent: string;
  usersListStatusOptionActive: string;
  usersListStatusOptionSuspended: string;
  usersListEmpty: string;
  usersListLoadingLabel: string;
  usersListRetry: string;

  // Table columns
  usersColName: string;
  usersColEmail: string;
  usersColRole: string;
  usersColStatus: string;
  usersColCreated: string;

  // Status badge labels (Active / Suspended / Deleted — used across P7-06/07/08)
  statusActive: string;
  statusSuspended: string;
  statusDeleted: string;

  // Role badge labels
  roleParent: string;
  roleStudent: string;
  roleAdmin: string;

  // Detail page
  userDetailProfileCard: string;
  userDetailCreatedAt: string;
  userDetailStatusReason: string;
  userDetailStatusChangedAt: string;
  /** Label for the PreferredLanguage row (UI/UX locale). */
  userDetailPreferredLanguage: string;
  /** Label for the LearningLanguage row (curriculum medium). */
  userDetailLearningLanguage: string;
  userDetailGrade: string;
  userDetailCountry: string;
  userDetailSignInNotTracked: string;
  userDetailNotFound: string;
  userDetailLoadingLabel: string;

  // Family panel
  userFamilyPanelHeading: string;
  userFamilyChildren: string;
  userFamilyParents: string;
  userFamilyNoMembers: string;
  userFamilyViewProfile: string;

  // Activity panel
  userActivityPanelHeading: string;
  userActivityXp: string;
  userActivityLevel: string;
  userActivityStreak: string;
  userActivityLongestStreak: string;
  userActivityBadges: string;
  userActivityMissions: string;
  userActivityLeague: string;
  userActivityLeagueTier: string;
  userActivityLeagueRank: string;
  userActivityNoData: string;
  userActivitySignInNotTracked: string;

  // ── P7-06 Batch B — additional copy (list page + detail + panels) ──────────
  /** Plural counter label appended to the count number: "N accounts". */
  usersResultCount: string;
  /** Label for "All Roles" default option in the role filter. */
  usersFilterAllRoles: string;
  /** Label for "All Statuses" default option in the status filter. */
  usersFilterAllStatuses: string;
  /** Clear-filters button label. */
  usersClearFilters: string;
  /** Generic error message when the users list request fails. */
  usersListError: string;
  /** Screen-reader caption for the users table. */
  usersTableCaption: string;
  /** SR-only label for "Previous page" pagination button. */
  usersPrevPage: string;
  /** SR-only label for "Next page" pagination button. */
  usersNextPage: string;
  /** Heading shown in the empty-results state. */
  usersNoResults: string;
  /** Body text shown below the empty-results heading. */
  usersNoResultsHint: string;
  /** Row aria-label suffix: "View profile". */
  usersViewProfile: string;

  // Detail page — extended fields
  /** Label for "Back to Users" button in the not-found state. */
  userDetailBackToUsers: string;
  /** Not-found body text. */
  userDetailNotFoundBody: string;
  /** Sign-in field label. */
  userDetailSignInLabel: string;
  /** Status field label. */
  userDetailStatusLabel: string;
  /** Sub-section heading for child-only fields. */
  userDetailStudentDetails: string;
  /** Heading: Linked Children (parent view). */
  userFamilyLinkedChildren: string;
  /** Heading: Linked Parents (child view). */
  userFamilyLinkedParents: string;
  /** Empty state: no children linked. */
  userFamilyNoChildren: string;
  /** Empty state: no parents linked. */
  userFamilyNoParents: string;
  /** Grade prefix, e.g. "Grade" in "Grade 3". */
  userDetailGradePrefix: string;
  /** Preferred language row label. */
  userDetailPreferredLanguageLabel: string;
  /** Preferred language row hint text. */
  userDetailPreferredLanguageHint: string;
  /** Learning language row label. */
  userDetailLearningLanguageLabel: string;
  /** Learning language row hint text. */
  userDetailLearningLanguageHint: string;
  /** "Arabic" language display name. */
  langArabic: string;
  /** "English" language display name. */
  langEnglish: string;
  /** Activity panel sign-in note. */
  userActivitySignInNote: string;
  /** Activity: current streak label. */
  userActivityCurrentStreak: string;
  /** Activity: best streak label. */
  userActivityBestStreak: string;
  /** Activity: daily missions fraction label. */
  userActivityDailyMissions: string;
  /** Activity: league rank label. */
  userActivityLeagueRankOf: string;
  /** Activity: weekly XP label. */
  userActivityWeeklyXp: string;

  // ── P7-07 lifecycle copy (Batch C — lifecycle.* namespace per Design Spec §E) ─

  // Shared actions menu
  lifecycleActionsHeading: string;
  lifecycleDeletedTerminalNotice: string;

  // Suspend dialog
  lifecycleSuspendButton: string;
  lifecycleSuspendTitle: string;
  lifecycleSuspendSubtitle: string;
  lifecycleSuspendNotice: string;
  lifecycleSuspendReasonLabel: string;
  lifecycleSuspendConfirm: string;

  // Reactivate dialog
  lifecycleReactivateButton: string;
  lifecycleReactivateTitle: string;
  lifecycleReactivateSubtitle: string;
  lifecycleReactivateNotice: string;
  lifecycleReactivatePriorLabel: string;
  lifecycleReactivateSuspendedOn: string;
  lifecycleReactivateReasonLabel: string;
  lifecycleReactivateConfirm: string;

  // Delete dialog
  lifecycleDeleteButton: string;
  lifecycleDeleteTitle: string;
  lifecycleDeleteSubtitle: string;
  lifecycleDeleteNoticeHeading: string;
  lifecycleDeleteNoticeBody: string;
  lifecycleDeleteCascadeLabel: string;
  lifecycleDeleteCascadeWarning: string;
  lifecycleDeleteReasonLabel: string;
  lifecycleDeleteTypedInstruction: string;
  lifecycleDeleteTypedPlaceholder: string;
  lifecycleDeleteTypedMatchLabel: string;
  lifecycleDeleteConfirm: string;

  // Success banners
  lifecycleSuccessSuspended: string;
  lifecycleSuccessReactivated: string;
  lifecycleSuccessDeleted: string;

  // Error copy (400 / 422 / 424 / 5xx)
  lifecycleErrorAlreadySuspended: string;
  lifecycleErrorAlreadyActive: string;
  lifecycleErrorAlreadyDeleted: string;
  lifecycleErrorProtected: string;
  lifecycleErrorValidation: string;
  lifecycleErrorConfirmMissing: string;
  lifecycleErrorNetwork: string;

  // ── P7-08 child-edit copy (Batch D — childEdit.* namespace per Design Spec §F.5) ─

  // Child edit page
  childEditPageTitle: string;
  childEditBreadcrumbEdit: string;
  childEditSaveChanges: string;
  childEditCancel: string;
  childEditNotStudent: string;

  // Profile fields (harmless PATCH)
  childEditCountryLabel: string;
  childEditDisplayLanguageLabel: string;
  childEditDisplayLanguageOptionAr: string;
  childEditDisplayLanguageOptionEn: string;

  // Learning language section (DESTRUCTIVE — opens dialog)
  childEditLearningLanguageLabel: string;
  childEditLearningLanguageSub: string;
  childEditLearningLanguageWarning: string;
  childEditChangeLearningLanguage: string;

  // Grade override section (non-destructive — opens dialog)
  childEditGradeLabel: string;
  childEditGradeNote: string;
  childEditOverrideGrade: string;

  // Edit Profile entry button (on the detail page — P7-08 child-edit slot)
  childEditEditProfileButton: string;

  // Grade override dialog (GradeOverrideDialog)
  gradeDialogTitle: string;
  gradeDialogSubtitle: string;
  gradeDialogCurrentLabel: string;
  gradeDialogNewLabel: string;
  gradeDialogSelectPlaceholder: string;
  gradeDialogPreserveNotice: string;
  gradeDialogReasonLabel: string;
  gradeDialogConfirm: string;
  gradeDialogSuccess: string;

  // Arabic ordinal grade labels (Gap 5 — full ordinal words, not digits)
  gradeLabel1: string;
  gradeLabel2: string;
  gradeLabel3: string;
  gradeLabel4: string;
  gradeLabel5: string;
  gradeLabel6: string;

  // Grade error copy
  gradeError422: string;
  gradeError400SameGrade: string;
  gradeError400Confirm: string;
  gradeError404: string;
  gradeErrorNetwork: string;

  // Change learning language dialog (ChangeLearningLanguageDialog — DESTRUCTIVE)
  langDialogTitle: string;
  langDialogSubtitle: string;
  langDialogLossTitle: string;
  langDialogLossLine: string;
  langDialogKeptLine: string;
  langDialogFromLabel: string;
  langDialogNewLabel: string;
  langDialogTypedInstruction: string;
  langDialogTypedPlaceholder: string;
  langDialogTypedMatchLabel: string;
  langDialogConfirm: string;
  langDialogSuccess: string;

  // Language error copy
  langError424: string;
  langError422: string;
  langErrorNoOp: string;
  langErrorNetwork: string;
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
  navUsers: 'Users',
  signOutButton: 'Sign Out',
  pageTitleDashboard: 'Dashboard',
  dashboardHeading: 'Welcome to Learnexia Admin',
  dashboardSubtext: 'Select a section from the navigation to get started.',
  dashboardPlaceholder: 'Admin features coming soon.',
  openNav: 'Open navigation',
  closeNav: 'Close navigation',

  loadingLabel: 'Loading, please wait',

  // P7-06 Users surface
  pageTitleUsers: 'Users',
  pageTitleUserDetail: 'User Profile',

  usersListHeading: 'User Management',
  usersListRoleFilterLabel: 'Role',
  usersListStatusFilterLabel: 'Status',
  usersListSearchPlaceholder: 'Search by name or email…',
  usersListRoleOptionParent: 'Parent',
  usersListRoleOptionStudent: 'Student',
  usersListStatusOptionActive: 'Active',
  usersListStatusOptionSuspended: 'Suspended',
  usersListEmpty: 'No users match your search.',
  usersListLoadingLabel: 'Loading users…',
  usersListRetry: 'Try again',

  usersColName: 'Name',
  usersColEmail: 'Email',
  usersColRole: 'Role',
  usersColStatus: 'Status',
  usersColCreated: 'Created',

  statusActive: 'Active',
  statusSuspended: 'Suspended',
  statusDeleted: 'Deleted',

  roleParent: 'Parent',
  roleStudent: 'Student',
  roleAdmin: 'Admin',

  userDetailProfileCard: 'Profile',
  userDetailCreatedAt: 'Member since',
  userDetailStatusReason: 'Status reason',
  userDetailStatusChangedAt: 'Status changed',
  userDetailPreferredLanguage: 'Preferred language (UI)',
  userDetailLearningLanguage: 'Learning language (curriculum)',
  userDetailGrade: 'Grade',
  userDetailCountry: 'Country',
  userDetailSignInNotTracked: 'Sign-in activity: not tracked',
  userDetailNotFound: 'User not found.',
  userDetailLoadingLabel: 'Loading profile…',

  userFamilyPanelHeading: 'Family',
  userFamilyChildren: 'Children',
  userFamilyParents: 'Parent(s)',
  userFamilyNoMembers: 'No family members linked.',
  userFamilyViewProfile: 'View profile',

  userActivityPanelHeading: 'Learning activity',
  userActivityXp: 'Total XP',
  userActivityLevel: 'Level',
  userActivityStreak: 'Current streak',
  userActivityLongestStreak: 'Longest streak',
  userActivityBadges: 'Badges earned',
  userActivityMissions: 'Daily missions',
  userActivityLeague: 'League',
  userActivityLeagueTier: 'Tier',
  userActivityLeagueRank: 'Rank',
  userActivityNoData: 'No data available.',
  userActivitySignInNotTracked: 'Sign-in activity: not tracked',

  // P7-06 Batch B
  usersResultCount: 'accounts',
  usersFilterAllRoles: 'All Roles',
  usersFilterAllStatuses: 'All Statuses',
  usersClearFilters: 'Clear filters',
  usersListError: 'Unable to load users. Please try again.',
  usersTableCaption: 'User accounts list',
  usersPrevPage: 'Previous page',
  usersNextPage: 'Next page',
  usersNoResults: 'No accounts found',
  usersNoResultsHint: 'Try adjusting the filters or search term.',
  usersViewProfile: 'View profile',

  userDetailBackToUsers: 'Back to Users',
  userDetailNotFoundBody: "This account doesn't exist or was removed.",
  userDetailSignInLabel: 'Sign-in Activity',
  userDetailStatusLabel: 'Status',
  userDetailStudentDetails: 'Student Details',
  userFamilyLinkedChildren: 'Linked Children',
  userFamilyLinkedParents: 'Linked Parents',
  userFamilyNoChildren: 'No linked children',
  userFamilyNoParents: 'No linked parents',
  userDetailGradePrefix: 'Grade',
  userDetailPreferredLanguageLabel: 'Display Language (UI & Communication)',
  userDetailPreferredLanguageHint: 'Language used for the app interface.',
  userDetailLearningLanguageLabel: 'Learning Language (Math & Science)',
  userDetailLearningLanguageHint:
    'Language used to teach Math and Science. Changing this resets those subjects.',
  langArabic: 'Arabic / العربية',
  langEnglish: 'English / الإنجليزية',
  userActivitySignInNote: 'Sign-in activity: not tracked',
  userActivityCurrentStreak: 'Current Streak',
  userActivityBestStreak: 'Best Streak',
  userActivityDailyMissions: 'Daily Missions',
  userActivityLeagueRankOf: 'Rank {N} of {M}',
  userActivityWeeklyXp: 'Weekly XP',

  // ── P7-07 lifecycle copy (Batch C) ──────────────────────────────────────────

  lifecycleActionsHeading: 'Actions',
  lifecycleDeletedTerminalNotice: 'Account deleted — no further actions',

  // Suspend
  lifecycleSuspendButton: 'Suspend',
  lifecycleSuspendTitle: 'Suspend Account',
  lifecycleSuspendSubtitle: 'Block sign-in and revoke active sessions.',
  lifecycleSuspendNotice:
    'This is a governance action. Suspending this account will revoke active sessions and block sign-in until an admin reactivates the account. This is not the same as a temporary failed-login lockout.',
  lifecycleSuspendReasonLabel: 'Reason for suspension',
  lifecycleSuspendConfirm: 'Suspend Account',

  // Reactivate
  lifecycleReactivateButton: 'Reactivate',
  lifecycleReactivateTitle: 'Reactivate Account',
  lifecycleReactivateSubtitle: 'Restore sign-in access to this account.',
  lifecycleReactivateNotice:
    'Reactivating this account will restore sign-in access. The user will need to sign in fresh to receive a new session.',
  lifecycleReactivatePriorLabel: 'Prior suspension reason',
  lifecycleReactivateSuspendedOn: 'Suspended on',
  lifecycleReactivateReasonLabel: 'Reason for reactivation (optional)',
  lifecycleReactivateConfirm: 'Reactivate Account',

  // Delete
  lifecycleDeleteButton: 'Delete Account',
  lifecycleDeleteTitle: 'Delete Account',
  lifecycleDeleteSubtitle: 'Permanently disable this account.',
  lifecycleDeleteNoticeHeading: 'Account will be permanently disabled',
  lifecycleDeleteNoticeBody:
    'This action cannot be undone. The account will be blocked from sign-in, but the learning history and account record are retained. Personal data is not yet erased — that happens in a scheduled review.',
  lifecycleDeleteCascadeLabel: 'Also delete all linked children',
  lifecycleDeleteCascadeWarning:
    'Their accounts will also be disabled and blocked from sign-in. History retained.',
  lifecycleDeleteReasonLabel: 'Reason for deletion (required)',
  lifecycleDeleteTypedInstruction: 'Type the account email address to confirm:',
  lifecycleDeleteTypedPlaceholder: 'Enter email to confirm',
  lifecycleDeleteTypedMatchLabel: 'Confirmed',
  lifecycleDeleteConfirm: 'Delete Account',

  // Success banners
  lifecycleSuccessSuspended: "account has been suspended.",
  lifecycleSuccessReactivated: "account has been reactivated.",
  lifecycleSuccessDeleted: "account has been deleted.",

  // Errors
  lifecycleErrorAlreadySuspended: 'This account is already suspended.',
  lifecycleErrorAlreadyActive: 'This account is already active.',
  lifecycleErrorAlreadyDeleted: 'This account has already been deleted.',
  lifecycleErrorProtected: 'This account cannot be modified.',
  lifecycleErrorValidation: 'Reason is required and must be under 500 characters.',
  lifecycleErrorConfirmMissing: 'Please confirm by typing the email address.',
  lifecycleErrorNetwork: 'Something went wrong. Please try again.',

  // ── P7-08 child-edit copy (Batch D) ─────────────────────────────────────────

  // Child edit page
  childEditPageTitle: 'Edit Student Profile',
  childEditBreadcrumbEdit: 'Edit Profile',
  childEditSaveChanges: 'Save Changes',
  childEditCancel: 'Cancel',
  childEditNotStudent: 'Profile editing is only available for student accounts.',

  // Profile fields (harmless PATCH)
  childEditCountryLabel: 'Country',
  childEditDisplayLanguageLabel: 'Display Language (UI)',
  childEditDisplayLanguageOptionAr: 'Arabic — العربية',
  childEditDisplayLanguageOptionEn: 'English — الإنجليزية',

  // Learning language section
  childEditLearningLanguageLabel: 'Learning Language (Math & Science)',
  childEditLearningLanguageSub: 'Changes this at the subject level — Math & Science only.',
  childEditLearningLanguageWarning:
    'Changing the learning language resets Math and Science progress. This cannot be undone.',
  childEditChangeLearningLanguage: 'Change Learning Language',

  // Grade override section
  childEditGradeLabel: 'Grade',
  childEditGradeNote:
    'Overriding the grade re-scopes the curriculum. XP, badges, and progress history are preserved.',
  childEditOverrideGrade: 'Override Grade',

  // Edit Profile entry button
  childEditEditProfileButton: 'Edit Profile',

  // Grade override dialog
  gradeDialogTitle: 'Override Grade',
  gradeDialogSubtitle: 'Change the grade level for this student.',
  gradeDialogCurrentLabel: 'Current grade',
  gradeDialogNewLabel: 'New grade',
  gradeDialogSelectPlaceholder: 'Select a grade',
  gradeDialogPreserveNotice:
    'Curriculum will re-scope to the new grade. XP, level, badges, streaks and mastery records are preserved.',
  gradeDialogReasonLabel: 'Reason for override (required)',
  gradeDialogConfirm: 'Override Grade',
  gradeDialogSuccess: 'grade has been updated.',

  // Arabic ordinal grade labels (used for EN label "Grade N" in selects)
  gradeLabel1: 'Grade 1',
  gradeLabel2: 'Grade 2',
  gradeLabel3: 'Grade 3',
  gradeLabel4: 'Grade 4',
  gradeLabel5: 'Grade 5',
  gradeLabel6: 'Grade 6',

  // Grade errors
  gradeError422: 'Grade must be between 1 and 6.',
  gradeError400SameGrade: "This is already the child's current grade.",
  gradeError400Confirm: 'Please confirm the grade override.',
  gradeError404: 'This account is not a student account.',
  gradeErrorNetwork: 'Something went wrong. Please try again.',

  // Change learning language dialog
  langDialogTitle: 'Change Learning Language',
  langDialogSubtitle: 'Reset Math and Science and switch to a new language of instruction.',
  langDialogLossTitle: 'This will permanently reset Math and Science progress',
  langDialogLossLine:
    'All Math and Science lesson attempts, mastery records, and progress are deleted. They cannot be recovered.',
  langDialogKeptLine:
    'Arabic, English, XP, streak and badges are not affected.',
  langDialogFromLabel: 'From',
  langDialogNewLabel: 'New learning language',
  langDialogTypedInstruction: 'Type CONFIRM to proceed with the fresh start:',
  langDialogTypedPlaceholder: 'Type CONFIRM',
  langDialogTypedMatchLabel: 'Confirmed',
  langDialogConfirm: 'Reset & Change Language',
  langDialogSuccess: 'learning language has been changed. Math and Science progress has been reset.',

  // Language errors
  langError424: 'Fresh start was not confirmed. No changes were made.',
  langError422: 'Language must be "ar" or "en".',
  langErrorNoOp: 'No change made — the selected language is already in use.',
  langErrorNetwork: 'Something went wrong. Please try again.',
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
  navUsers: 'المستخدمون',
  signOutButton: 'تسجيل الخروج',
  pageTitleDashboard: 'لوحة التحكم',
  dashboardHeading: 'مرحبًا بك في إدارة Learnexia',
  dashboardSubtext: 'اختر قسمًا من القائمة للبدء.',
  dashboardPlaceholder: 'ميزات الإدارة قادمة قريبًا.',
  openNav: 'فتح القائمة',
  closeNav: 'إغلاق القائمة',

  loadingLabel: 'جارٍ التحميل، يرجى الانتظار',

  // P7-06 Users surface
  pageTitleUsers: 'المستخدمون',
  pageTitleUserDetail: 'ملف المستخدم',

  usersListHeading: 'إدارة المستخدمين',
  usersListRoleFilterLabel: 'الدور',
  usersListStatusFilterLabel: 'الحالة',
  usersListSearchPlaceholder: 'ابحث بالاسم أو البريد الإلكتروني…',
  usersListRoleOptionParent: 'ولي الأمر',
  usersListRoleOptionStudent: 'الطالب',
  usersListStatusOptionActive: 'نشط',
  usersListStatusOptionSuspended: 'موقوف',
  usersListEmpty: 'لا يوجد مستخدمون يطابقون بحثك.',
  usersListLoadingLabel: 'جارٍ تحميل المستخدمين…',
  usersListRetry: 'حاول مجدداً',

  usersColName: 'الاسم',
  usersColEmail: 'البريد الإلكتروني',
  usersColRole: 'الدور',
  usersColStatus: 'الحالة',
  usersColCreated: 'تاريخ الإنشاء',

  statusActive: 'نشط',
  statusSuspended: 'موقوف',
  statusDeleted: 'محذوف',

  roleParent: 'ولي الأمر',
  roleStudent: 'طالب',
  roleAdmin: 'مسؤول',

  userDetailProfileCard: 'الملف الشخصي',
  userDetailCreatedAt: 'عضو منذ',
  userDetailStatusReason: 'سبب الحالة',
  userDetailStatusChangedAt: 'تغيُّر الحالة',
  userDetailPreferredLanguage: 'اللغة المفضَّلة (واجهة المستخدم)',
  userDetailLearningLanguage: 'لغة التعلُّم (المنهج الدراسي)',
  userDetailGrade: 'الصف الدراسي',
  userDetailCountry: 'البلد',
  userDetailSignInNotTracked: 'نشاط تسجيل الدخول: غير متاح',
  userDetailNotFound: 'المستخدم غير موجود.',
  userDetailLoadingLabel: 'جارٍ تحميل الملف الشخصي…',

  userFamilyPanelHeading: 'العائلة',
  userFamilyChildren: 'الأبناء',
  userFamilyParents: 'ولي (أولياء) الأمر',
  userFamilyNoMembers: 'لا توجد أفراد عائلة مرتبطون.',
  userFamilyViewProfile: 'عرض الملف الشخصي',

  userActivityPanelHeading: 'نشاط التعلُّم',
  userActivityXp: 'مجموع نقاط الخبرة',
  userActivityLevel: 'المستوى',
  userActivityStreak: 'الرصيد الحالي',
  userActivityLongestStreak: 'أطول رصيد',
  userActivityBadges: 'الشارات المكتسبة',
  userActivityMissions: 'المهام اليومية',
  userActivityLeague: 'الدوري',
  userActivityLeagueTier: 'المستوى',
  userActivityLeagueRank: 'الترتيب',
  userActivityNoData: 'لا تتوفر بيانات.',
  userActivitySignInNotTracked: 'نشاط تسجيل الدخول: غير متاح',

  // P7-06 Batch B
  usersResultCount: 'حساب',
  usersFilterAllRoles: 'كل الأدوار',
  usersFilterAllStatuses: 'كل الحالات',
  usersClearFilters: 'مسح التصفية',
  usersListError: 'تعذَّر تحميل المستخدمين. يرجى المحاولة مرة أخرى.',
  usersTableCaption: 'قائمة حسابات المستخدمين',
  usersPrevPage: 'الصفحة السابقة',
  usersNextPage: 'الصفحة التالية',
  usersNoResults: 'لم يُعثر على حسابات',
  usersNoResultsHint: 'جرِّب تعديل التصفية أو مصطلح البحث.',
  usersViewProfile: 'عرض الملف الشخصي',

  userDetailBackToUsers: 'العودة للمستخدمين',
  userDetailNotFoundBody: 'هذا الحساب غير موجود أو تم حذفه.',
  userDetailSignInLabel: 'آخر تسجيل دخول',
  userDetailStatusLabel: 'الحالة',
  userDetailStudentDetails: 'تفاصيل الطالب',
  userFamilyLinkedChildren: 'الأبناء المرتبطون',
  userFamilyLinkedParents: 'الوالدان المرتبطان',
  userFamilyNoChildren: 'لا يوجد أبناء مرتبطون',
  userFamilyNoParents: 'لا يوجد والدان مرتبطان',
  userDetailGradePrefix: 'الصف',
  userDetailPreferredLanguageLabel: 'لغة الواجهة (التواصل والعرض)',
  userDetailPreferredLanguageHint: 'اللغة المستخدمة في واجهة التطبيق.',
  userDetailLearningLanguageLabel: 'لغة الدراسة (الرياضيات والعلوم)',
  userDetailLearningLanguageHint:
    'اللغة التي تُدرَّس بها الرياضيات والعلوم. تغييرها يعيد ضبط هاتين المادتين.',
  langArabic: 'عربي / Arabic',
  langEnglish: 'إنجليزي / English',
  userActivitySignInNote: 'آخر تسجيل دخول: غير مُتتبَّع',
  userActivityCurrentStreak: 'السلسلة الحالية',
  userActivityBestStreak: 'أفضل سلسلة',
  userActivityDailyMissions: 'المهام اليومية',
  userActivityLeagueRankOf: 'الترتيب {N} من {M}',
  userActivityWeeklyXp: 'نقاط الأسبوع',

  // ── P7-07 lifecycle copy (Batch C) ──────────────────────────────────────────

  lifecycleActionsHeading: 'الإجراءات',
  lifecycleDeletedTerminalNotice: 'الحساب محذوف — لا يوجد مزيد من الإجراءات',

  // Suspend
  lifecycleSuspendButton: 'إيقاف مؤقت',
  lifecycleSuspendTitle: 'إيقاف الحساب مؤقتاً',
  lifecycleSuspendSubtitle: 'منع تسجيل الدخول وإلغاء الجلسات النشطة.',
  lifecycleSuspendNotice:
    'هذا إجراء حوكمة. سيُلغي إيقاف هذا الحساب جلساته النشطة ويمنع تسجيل الدخول حتى يُعيد المسؤول تفعيل الحساب. لا يُقصد بهذا قفل الحساب التلقائي الناتج عن محاولات الدخول الفاشلة.',
  lifecycleSuspendReasonLabel: 'سبب الإيقاف المؤقت',
  lifecycleSuspendConfirm: 'إيقاف الحساب مؤقتاً',

  // Reactivate
  lifecycleReactivateButton: 'إعادة تفعيل',
  lifecycleReactivateTitle: 'إعادة تفعيل الحساب',
  lifecycleReactivateSubtitle: 'استعادة إمكانية تسجيل الدخول لهذا الحساب.',
  lifecycleReactivateNotice:
    'ستؤدي إعادة تفعيل هذا الحساب إلى استعادة إمكانية تسجيل الدخول. سيحتاج المستخدم إلى تسجيل الدخول من جديد للحصول على جلسة جديدة.',
  lifecycleReactivatePriorLabel: 'سبب الإيقاف السابق',
  lifecycleReactivateSuspendedOn: 'تم الإيقاف بتاريخ',
  lifecycleReactivateReasonLabel: 'سبب إعادة التفعيل (اختياري)',
  lifecycleReactivateConfirm: 'إعادة تفعيل الحساب',

  // Delete
  lifecycleDeleteButton: 'حذف الحساب',
  lifecycleDeleteTitle: 'حذف الحساب',
  lifecycleDeleteSubtitle: 'تعطيل هذا الحساب نهائياً.',
  lifecycleDeleteNoticeHeading: 'سيُعطَّل الحساب نهائياً',
  lifecycleDeleteNoticeBody:
    'لا يمكن التراجع عن هذا الإجراء. سيُمنع الحساب من تسجيل الدخول، لكن سجل التعلّم وبيانات الحساب ستبقى محفوظة. لم تُحذف البيانات الشخصية بعد — يحدث ذلك في مراجعة مجدولة.',
  lifecycleDeleteCascadeLabel: 'حذف جميع الأبناء المرتبطين أيضاً',
  lifecycleDeleteCascadeWarning:
    'سيُعطَّل حساب أبنائهم أيضاً ويُمنعون من تسجيل الدخول. يبقى السجل محفوظاً.',
  lifecycleDeleteReasonLabel: 'سبب الحذف (مطلوب)',
  lifecycleDeleteTypedInstruction: 'اكتب عنوان البريد الإلكتروني للحساب للتأكيد:',
  lifecycleDeleteTypedPlaceholder: 'أدخل البريد الإلكتروني للتأكيد',
  lifecycleDeleteTypedMatchLabel: 'تم التأكيد',
  lifecycleDeleteConfirm: 'حذف الحساب',

  // Success banners
  lifecycleSuccessSuspended: 'تم إيقاف الحساب مؤقتاً.',
  lifecycleSuccessReactivated: 'تم إعادة تفعيل الحساب.',
  lifecycleSuccessDeleted: 'تم حذف الحساب.',

  // Errors
  lifecycleErrorAlreadySuspended: 'هذا الحساب موقوف مؤقتاً بالفعل.',
  lifecycleErrorAlreadyActive: 'هذا الحساب نشط بالفعل.',
  lifecycleErrorAlreadyDeleted: 'تم حذف هذا الحساب بالفعل.',
  lifecycleErrorProtected: 'لا يمكن تعديل هذا الحساب.',
  lifecycleErrorValidation: 'السبب مطلوب ويجب أن يكون أقل من ٥٠٠ حرف.',
  lifecycleErrorConfirmMissing: 'يُرجى التأكيد بكتابة عنوان البريد الإلكتروني.',
  lifecycleErrorNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',

  // ── P7-08 child-edit copy (Batch D) ─────────────────────────────────────────

  // Child edit page
  childEditPageTitle: 'تعديل ملف الطالب',
  childEditBreadcrumbEdit: 'تعديل الملف',
  childEditSaveChanges: 'حفظ التغييرات',
  childEditCancel: 'إلغاء',
  childEditNotStudent: 'تعديل الملف الشخصي متاح فقط لحسابات الطلاب.',

  // Profile fields (harmless PATCH)
  childEditCountryLabel: 'البلد',
  childEditDisplayLanguageLabel: 'لغة الواجهة (التطبيق)',
  childEditDisplayLanguageOptionAr: 'عربي — Arabic',
  childEditDisplayLanguageOptionEn: 'إنجليزي — English',

  // Learning language section
  childEditLearningLanguageLabel: 'لغة الدراسة (الرياضيات والعلوم)',
  childEditLearningLanguageSub: 'يؤثر فقط على مادتَي الرياضيات والعلوم.',
  childEditLearningLanguageWarning:
    'تغيير لغة الدراسة يُعيد ضبط تقدم الرياضيات والعلوم. لا يمكن التراجع عن ذلك.',
  childEditChangeLearningLanguage: 'تغيير لغة الدراسة',

  // Grade override section
  childEditGradeLabel: 'الصف',
  childEditGradeNote:
    'تجاوز الصف يُعيد تحديد المناهج. تبقى النقاط والشارات وسجل التقدم محفوظة.',
  childEditOverrideGrade: 'تجاوز الصف',

  // Edit Profile entry button
  childEditEditProfileButton: 'تعديل الملف الشخصي',

  // Grade override dialog
  gradeDialogTitle: 'تجاوز الصف',
  gradeDialogSubtitle: 'تغيير مستوى الصف لهذا الطالب.',
  gradeDialogCurrentLabel: 'الصف الحالي',
  gradeDialogNewLabel: 'الصف الجديد',
  gradeDialogSelectPlaceholder: 'اختر الصف',
  gradeDialogPreserveNotice:
    'ستُعاد معايرة المناهج للصف الجديد. تبقى النقاط والمستوى والشارات والسلاسل وسجلات الإتقان محفوظة.',
  gradeDialogReasonLabel: 'سبب التجاوز (مطلوب)',
  gradeDialogConfirm: 'تجاوز الصف',
  gradeDialogSuccess: 'تم تحديث الصف.',

  // Arabic ordinal grade labels (Gap 5 — full Arabic ordinal words)
  gradeLabel1: 'الصف الأول',
  gradeLabel2: 'الصف الثاني',
  gradeLabel3: 'الصف الثالث',
  gradeLabel4: 'الصف الرابع',
  gradeLabel5: 'الصف الخامس',
  gradeLabel6: 'الصف السادس',

  // Grade errors
  gradeError422: 'يجب أن يكون الصف بين ١ و٦.',
  gradeError400SameGrade: 'هذا هو الصف الحالي للطفل بالفعل.',
  gradeError400Confirm: 'يرجى تأكيد تجاوز الصف.',
  gradeError404: 'هذا الحساب ليس حساب طالب.',
  gradeErrorNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',

  // Change learning language dialog
  langDialogTitle: 'تغيير لغة الدراسة',
  langDialogSubtitle: 'إعادة ضبط الرياضيات والعلوم والتبديل إلى لغة تعليم جديدة.',
  langDialogLossTitle: 'سيُعيد هذا ضبط تقدم الرياضيات والعلوم بشكل دائم',
  langDialogLossLine:
    'ستُحذف جميع محاولات دروس الرياضيات والعلوم وسجلات الإتقان والتقدم. لا يمكن استردادها.',
  langDialogKeptLine:
    'العربية والإنجليزية والنقاط والسلسلة والشارات غير متأثرة.',
  langDialogFromLabel: 'من',
  langDialogNewLabel: 'لغة الدراسة الجديدة',
  langDialogTypedInstruction: 'اكتب CONFIRM للمتابعة مع إعادة البدء:',
  langDialogTypedPlaceholder: 'اكتب CONFIRM',
  langDialogTypedMatchLabel: 'تم التأكيد',
  langDialogConfirm: 'إعادة الضبط وتغيير اللغة',
  langDialogSuccess: 'تم تغيير لغة الدراسة. أُعيد ضبط تقدم الرياضيات والعلوم.',

  // Language errors
  langError424: 'لم يتم تأكيد إعادة البدء. لم يُجرَ أي تغيير.',
  langError422: 'اللغة غير مدعومة. يجب أن تكون "ar" أو "en".',
  langErrorNoOp: 'لم يُجرَ أي تغيير — اللغة المختارة مستخدمة بالفعل.',
  langErrorNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
};

const STRINGS: Record<Locale, AdminStrings> = { en, ar };

/** Default admin locale (English-first per Design Spec §7). */
export const ADMIN_LOCALE: Locale = 'en';

export function getStrings(locale: Locale = ADMIN_LOCALE): AdminStrings {
  return STRINGS[locale] ?? en;
}
