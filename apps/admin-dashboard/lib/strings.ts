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

  /** Success message after a harmless profile PATCH (country / preferredLanguage). */
  childProfileSaveSuccess: string;
  /** Validation error shown when PATCH /profile returns 422 (unsupported language/country). */
  childProfileError422: string;

  // ── P7-01 Curriculum surface (curriculum* / subject* / unit* namespace) ──────

  // Page titles
  curriculumPageTitle: string;
  curriculumDetailTitle: string;

  // Subjects list page
  subjectsListHeading: string;
  subjectsResultCount: string;
  subjectsListSearchPlaceholder: string;
  subjectsFilterAllGrades: string;
  subjectsFilterAllLanguages: string;
  subjectsFilterLangAr: string;
  subjectsFilterLangEn: string;
  subjectsClearFilters: string;
  subjectsListLoadingLabel: string;
  subjectsListError: string;
  subjectsTableCaption: string;
  subjectsNoResults: string;
  subjectsNoResultsHint: string;
  subjectsViewDetail: string;
  subjectsPrevPage: string;
  subjectsNextPage: string;
  subjectsNewSubject: string;

  // Subjects table columns
  subjectsColSubject: string;
  subjectsColLanguage: string;
  subjectsColGrade: string;
  subjectsColOrder: string;
  subjectsColActive: string;

  // Subject code badge labels
  subjectCodeMath: string;
  subjectCodeScience: string;
  subjectCodeArabic: string;
  subjectCodeEnglish: string;

  // Language badge labels
  contentLangAr: string;
  contentLangEn: string;

  // Active badge labels
  subjectActiveBadge: string;
  subjectInactiveBadge: string;

  // Coverage panel
  coveragePanelHeading: string;
  coverageGapBadge: string;
  coverageCreateShortcut: string;
  coverageMissingSlot: string;

  // Subject detail
  subjectsDetailBreadcrumb: string;
  subjectsDetailGradePrefix: string;
  subjectsDetailOrderPrefix: string;
  subjectsDetailEditBtn: string;
  subjectsDetailToggleActiveBtn: string;
  subjectsDetailDeactivateBtn: string;
  subjectsDetailDeleteBtn: string;
  subjectsDetailNotFound: string;
  subjectsDetailNotFoundBody: string;
  subjectsDetailBackBtn: string;

  // Subject form
  subjectFormCreateTitle: string;
  subjectFormCreateSubtitle: string;
  subjectFormEditTitle: string;
  subjectFormEditSubtitle: string;
  subjectFormNameLabel: string;
  subjectFormNamePlaceholder: string;
  subjectFormGradeLabel: string;
  subjectFormGradePlaceholder: string;
  subjectFormCodeLabel: string;
  subjectFormCodePlaceholder: string;
  subjectFormLangLabel: string;
  subjectFormLangPlaceholder: string;
  subjectFormPinnedLangHint: string;
  subjectFormOrderLabel: string;
  subjectFormOrderHint: string;
  subjectFormActiveLabel: string;
  subjectFormActiveLabelOn: string;
  subjectFormActiveLabelOff: string;
  subjectFormCancelBtn: string;
  subjectFormCreateBtn: string;
  subjectFormSaveBtn: string;
  subjectFormErrNameRequired: string;
  subjectFormErrGradeRequired: string;
  subjectFormErrCodeRequired: string;
  subjectFormErrLangRequired: string;
  subjectFormErrOrderInvalid: string;

  // Units section
  unitsHeading: string;
  unitsResultCount: string;
  unitsNewUnit: string;
  unitsNoResults: string;
  unitsNoResultsHint: string;
  unitsTableCaption: string;
  unitsColOrder: string;
  unitsColName: string;
  unitsColActive: string;
  unitsLoadingLabel: string;
  unitsListError: string;

  // Unit form
  unitFormCreateTitle: string;
  unitFormCreateSubtitle: string;
  unitFormEditTitle: string;
  unitFormEditSubtitle: string;
  unitFormInheritedLangNotice: string;
  unitFormNameLabel: string;
  unitFormNamePlaceholder: string;
  unitFormOrderLabel: string;
  unitFormOrderHint: string;
  unitFormActiveLabel: string;
  unitFormActiveLabelOn: string;
  unitFormActiveLabelOff: string;
  unitFormCancelBtn: string;
  unitFormCreateBtn: string;
  unitFormSaveBtn: string;
  unitFormErrNameRequired: string;

  // Reorder
  reorderSaveBtn: string;
  reorderMoveUp: string;
  reorderMoveDown: string;
  reorderDisabledHint: string;
  reorderPosition: string;
  reorderSavedMsg: string;
  reorderErrorMsg: string;

  // Delete dialog
  curriculumDeleteSubjectTitle: string;
  curriculumDeleteUnitTitle: string;
  curriculumDeleteSubjectBody: string;
  curriculumDeleteUnitBody: string;
  curriculumDeleteConfirmSubject: string;
  curriculumDeleteConfirmUnit: string;
  curriculumDeleteSuccessSubject: string;
  curriculumDeleteSuccessUnit: string;
  curriculumDeleteCancel: string;

  // Toggle active
  subjectActivateBtn: string;
  subjectDeactivateBtn: string;
  unitActivateBtn: string;
  unitDeactivateBtn: string;
  subjectToggleSuccess: string;
  unitToggleSuccess: string;
  subjectToggleError: string;
  unitToggleError: string;

  // Shared error
  curriculumNotEmptyError: string;
  curriculumNetworkError: string;

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

  // ── P7-05 Curriculum lifecycle copy (clLifecycle* namespace — DISJOINT from P7-07 lifecycle*) ──

  // Entity type labels (used in LifecycleBadge + CurriculumPreview headers)
  clEntityTypeSubject: string;
  clEntityTypeUnit: string;
  clEntityTypeLesson: string;
  clEntityTypeQuestion: string;

  // Lifecycle state badge labels
  clLifecycleDraft: string;
  clLifecyclePublished: string;
  clLifecycleArchived: string;

  // Lifecycle state aria-labels (screen reader)
  clLifecycleDraftAriaLabel: string;
  clLifecyclePublishedAriaLabel: string;
  clLifecycleArchivedAriaLabel: string;

  // CurriculumLifecycleControl — status row
  clLifecycleStatusLabel: string;
  clLifecycleViewPreview: string;
  clLifecycleSuccessBannerTransitioned: string;

  // Publish dialog
  clPublishTitle: string;
  clPublishSubtitle: string;
  clPublishNoticeBody: string;
  clPublishConfirm: string;

  // Unpublish dialog
  clUnpublishTitle: string;
  clUnpublishSubtitle: string;
  clUnpublishNoticeBody: string;
  clUnpublishConfirm: string;

  // Archive dialog
  clArchiveTitle: string;
  clArchiveSubtitle: string;
  clArchiveNoticeHeading: string;
  clArchiveNoticeBody: string;
  clArchiveConfirm: string;

  // Restore dialog
  clRestoreTitle: string;
  clRestoreSubtitle: string;
  clRestoreNotice: string;
  clRestoreConfirm: string;

  // Rollback dialog
  clRollbackTitle: string;     // includes {N} version number placeholder
  clRollbackSubtitle: string;  // includes {formattedDate} placeholder
  clRollbackVersionDetails: string;
  clRollbackWarningBody: string;
  clRollbackReasonLabel: string;
  clRollbackReasonPlaceholder: string;
  clRollbackGuardrailNotice: string;
  clRollbackConfirm: string;   // includes {N} version number placeholder

  // Version history panel
  clVersionHistoryHeading: string;
  clVersionHistoryCount: string;   // "versions" — appended to count integer
  clVersionHistoryEmpty: string;
  clVersionHistoryError: string;
  clVersionPublishedBy: string;    // "Published by ID {N}" — {N} replaced at runtime
  clVersionRollbackBtn: string;
  clVersionRollbackAriaLabel: string; // "Roll back to v{N}" — {N} replaced at runtime

  // CurriculumPreview — page/route strings
  clLifecyclePreviewTitle: string;  // "Preview (snapshot)"
  clPreviewBack: string;
  clPreviewDraftBanner: string;
  clPreviewError: string;
  clPreviewInvalidJson: string;
  clPreviewImageAlt: string;

  // CurriculumPreview — field labels
  clPreviewFieldName: string;
  clPreviewFieldCountry: string;
  clPreviewFieldSubject: string;
  clPreviewFieldLanguage: string;
  clPreviewFieldOrder: string;
  clPreviewFieldActive: string;
  clPreviewFieldDifficulty: string;
  clPreviewFieldLocked: string;
  clPreviewFieldDuration: string;
  clPreviewFieldExplanation: string;
  clPreviewFieldVisual: string;
  clPreviewFieldBoss: string;
  clPreviewFieldQuestion: string;
  clPreviewFieldType: string;
  clPreviewFieldOptions: string;
  clPreviewFieldCorrectAnswer: string;
  clPreviewFieldGeneratedBy: string;
  clPreviewYes: string;
  clPreviewNo: string;

  // PublicationCoverage — landing page
  clCoverageHeading: string;
  clCoverageSubheading: string;
  clCoverageGradeLabel: string;
  clCoverageGradePlaceholder: string;
  clCoverageGradeHint: string;
  clCoverageTableCaption: string;
  clCoverageColSubject: string;
  clCoverageColStatus: string;
  clCoverageSlotNotCreated: string;
  clCoverageSlotDraft: string;
  clCoverageSlotPublished: string;
  clCoverageSlotArchived: string;
  clCoverageWarningMissingChip: string;       // aria-label for one-sided warning chip
  clCoverageSuccess: string;
  clCoverageError: string;
  clCoverageGoToSubjects: string;
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

  // Profile PATCH copy (Bug fixes for edit/page.tsx — Job 2)
  childProfileSaveSuccess: 'Profile has been updated successfully.',
  childProfileError422: 'The country or display language value is not supported.',

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

  // ── P7-01 curriculum (EN) ─────────────────────────────────────────────────────
  curriculumPageTitle: 'Curriculum',
  curriculumDetailTitle: 'Subject',

  subjectsListHeading: 'Subjects',
  subjectsResultCount: 'subjects',
  subjectsListSearchPlaceholder: 'Search subjects…',
  subjectsFilterAllGrades: 'All Grades',
  subjectsFilterAllLanguages: 'All',
  subjectsFilterLangAr: 'Arabic (Ar)',
  subjectsFilterLangEn: 'English (En)',
  subjectsClearFilters: 'Clear filters',
  subjectsListLoadingLabel: 'Loading subjects…',
  subjectsListError: 'Unable to load subjects. Please try again.',
  subjectsTableCaption: 'Curriculum subjects list',
  subjectsNoResults: 'No subjects found',
  subjectsNoResultsHint: 'Try adjusting the grade or language filter.',
  subjectsViewDetail: 'View subject',
  subjectsPrevPage: 'Previous page',
  subjectsNextPage: 'Next page',
  subjectsNewSubject: 'New Subject',

  subjectsColSubject: 'Subject',
  subjectsColLanguage: 'Language',
  subjectsColGrade: 'Grade',
  subjectsColOrder: 'Order',
  subjectsColActive: 'Active',

  subjectCodeMath: 'MATH',
  subjectCodeScience: 'SCI',
  subjectCodeArabic: 'AR',
  subjectCodeEnglish: 'EN',

  contentLangAr: 'AR',
  contentLangEn: 'EN',

  subjectActiveBadge: 'Active',
  subjectInactiveBadge: 'Inactive',

  coveragePanelHeading: 'Language Coverage — Grade',
  coverageGapBadge: 'gap',
  coverageCreateShortcut: 'Create',
  coverageMissingSlot: 'Missing',

  subjectsDetailBreadcrumb: 'Curriculum',
  subjectsDetailGradePrefix: 'Grade',
  subjectsDetailOrderPrefix: 'Order:',
  subjectsDetailEditBtn: 'Edit',
  subjectsDetailToggleActiveBtn: 'Activate',
  subjectsDetailDeactivateBtn: 'Deactivate',
  subjectsDetailDeleteBtn: 'Remove',
  subjectsDetailNotFound: 'Subject not found',
  subjectsDetailNotFoundBody: 'This subject does not exist or was removed.',
  subjectsDetailBackBtn: 'Back to Curriculum',

  subjectFormCreateTitle: 'New Subject',
  subjectFormCreateSubtitle: 'Add a new subject to the curriculum.',
  subjectFormEditTitle: 'Edit Subject',
  subjectFormEditSubtitle: 'Update subject details.',
  subjectFormNameLabel: 'Name',
  subjectFormNamePlaceholder: 'e.g. Mathematics',
  subjectFormGradeLabel: 'Grade',
  subjectFormGradePlaceholder: 'Select grade',
  subjectFormCodeLabel: 'Subject Code',
  subjectFormCodePlaceholder: 'Select code',
  subjectFormLangLabel: 'Content Language',
  subjectFormLangPlaceholder: 'Select language',
  subjectFormPinnedLangHint: 'Language is pinned for this subject.',
  subjectFormOrderLabel: 'Order',
  subjectFormOrderHint: 'Sets display order within the same language tree.',
  subjectFormActiveLabel: 'Active',
  subjectFormActiveLabelOn: 'Subject visible to students',
  subjectFormActiveLabelOff: 'Subject hidden from students',
  subjectFormCancelBtn: 'Cancel',
  subjectFormCreateBtn: 'Create Subject',
  subjectFormSaveBtn: 'Save Changes',
  subjectFormErrNameRequired: 'Name is required.',
  subjectFormErrGradeRequired: 'Grade is required.',
  subjectFormErrCodeRequired: 'Subject code is required.',
  subjectFormErrLangRequired: 'Language is required.',
  subjectFormErrOrderInvalid: 'Order must be a number ≥ 0.',

  unitsHeading: 'Units',
  unitsResultCount: 'units',
  unitsNewUnit: 'New Unit',
  unitsNoResults: 'No units yet',
  unitsNoResultsHint: 'Add the first unit to this subject.',
  unitsTableCaption: 'Subject units list',
  unitsColOrder: 'Order',
  unitsColName: 'Name',
  unitsColActive: 'Active',
  unitsLoadingLabel: 'Loading units…',
  unitsListError: 'Unable to load units. Please try again.',

  unitFormCreateTitle: 'New Unit',
  unitFormCreateSubtitle: 'Units inherit language from the owning subject.',
  unitFormEditTitle: 'Edit Unit',
  unitFormEditSubtitle: 'Update unit details.',
  unitFormInheritedLangNotice: 'Language: {lang} — inherited from parent subject',
  unitFormNameLabel: 'Unit Name',
  unitFormNamePlaceholder: 'e.g. Introduction to Algebra',
  unitFormOrderLabel: 'Order',
  unitFormOrderHint: 'Display order within this subject.',
  unitFormActiveLabel: 'Active',
  unitFormActiveLabelOn: 'Unit visible to students',
  unitFormActiveLabelOff: 'Unit hidden from students',
  unitFormCancelBtn: 'Cancel',
  unitFormCreateBtn: 'Create Unit',
  unitFormSaveBtn: 'Save Changes',
  unitFormErrNameRequired: 'Unit name is required.',

  reorderSaveBtn: 'Save Order',
  reorderMoveUp: 'Move up',
  reorderMoveDown: 'Move down',
  reorderDisabledHint: 'Select a single language to enable reorder',
  reorderPosition: 'moved to position',
  reorderSavedMsg: 'Order saved.',
  reorderErrorMsg: 'Unable to save order. Please try again.',

  curriculumDeleteSubjectTitle: 'Remove Subject',
  curriculumDeleteUnitTitle: 'Remove Unit',
  curriculumDeleteSubjectBody: 'This subject will be hidden from the curriculum. It can be reactivated if needed. Note: subjects with units cannot be removed — all units must be deleted first.',
  curriculumDeleteUnitBody: 'This unit will be hidden from the curriculum. It can be reactivated if needed. Note: units with lessons cannot be removed — all lessons must be deleted first.',
  curriculumDeleteConfirmSubject: 'Remove Subject',
  curriculumDeleteConfirmUnit: 'Remove Unit',
  curriculumDeleteSuccessSubject: 'Subject removed from curriculum.',
  curriculumDeleteSuccessUnit: 'Unit removed from curriculum.',
  curriculumDeleteCancel: 'Cancel',

  subjectActivateBtn: 'Activate',
  subjectDeactivateBtn: 'Deactivate',
  unitActivateBtn: 'Activate',
  unitDeactivateBtn: 'Deactivate',
  subjectToggleSuccess: 'Subject status updated.',
  unitToggleSuccess: 'Unit status updated.',
  subjectToggleError: 'Unable to update subject status. Please try again.',
  unitToggleError: 'Unable to update unit status. Please try again.',

  curriculumNotEmptyError: 'This item has children and cannot be removed. Delete all children first.',
  curriculumNetworkError: 'Something went wrong. Please try again.',

  // ── P7-05 curriculum lifecycle copy (EN) — clLifecycle* namespace ─────────────

  clEntityTypeSubject: 'Subject',
  clEntityTypeUnit: 'Unit',
  clEntityTypeLesson: 'Lesson',
  clEntityTypeQuestion: 'Question',

  clLifecycleDraft: 'Draft',
  clLifecyclePublished: 'Published',
  clLifecycleArchived: 'Archived',

  clLifecycleDraftAriaLabel: 'Lifecycle state: Draft',
  clLifecyclePublishedAriaLabel: 'Lifecycle state: Published',
  clLifecycleArchivedAriaLabel: 'Lifecycle state: Archived',

  clLifecycleStatusLabel: 'Lifecycle status',
  clLifecycleViewPreview: 'View Preview',
  clLifecycleSuccessBannerTransitioned: 'Lifecycle state updated successfully.',

  clPublishTitle: 'Publish this content?',
  clPublishSubtitle: 'Once published, students will be able to see this content.',
  clPublishNoticeBody: 'Publishing makes this content visible to enrolled students. You can unpublish or archive it later.',
  clPublishConfirm: 'Publish',

  clUnpublishTitle: 'Unpublish this content?',
  clUnpublishSubtitle: 'The content will return to Draft and will no longer be visible to students.',
  clUnpublishNoticeBody: 'Students will immediately lose access. You can re-publish it at any time.',
  clUnpublishConfirm: 'Unpublish',

  clArchiveTitle: 'Archive this content?',
  clArchiveSubtitle: 'Archived content is hidden from students and cannot be published directly.',
  clArchiveNoticeHeading: 'Archive is a terminal state for the current version.',
  clArchiveNoticeBody: 'Students will immediately lose access. You can restore it to Draft at any time, then review and re-publish from there.',
  clArchiveConfirm: 'Archive',

  clRestoreTitle: 'Restore to Draft?',
  clRestoreSubtitle: 'The content will be moved back to Draft for review.',
  clRestoreNotice: 'Restoring returns this content to Draft. Review it, then re-publish when ready.',
  clRestoreConfirm: 'Restore to Draft',

  clRollbackTitle: 'Roll back to version {N}?',
  clRollbackSubtitle: 'Published on {formattedDate}',
  clRollbackVersionDetails: 'Version details',
  clRollbackWarningBody: 'Rolling back replaces the current published content with this older snapshot. The current version will be saved in version history.',
  clRollbackReasonLabel: 'Reason for rollback',
  clRollbackReasonPlaceholder: 'Explain why you are rolling back to this version…',
  clRollbackGuardrailNotice: 'Reason is a front-end-only audit field and is not sent to the server. It is recorded here for team accountability only.',
  clRollbackConfirm: 'Roll back to v{N}',

  clVersionHistoryHeading: 'Published Versions',
  clVersionHistoryCount: 'versions',
  clVersionHistoryEmpty: 'No published versions yet.',
  clVersionHistoryError: 'Unable to load version history. Please try again.',
  clVersionPublishedBy: 'Published by ID {N}',
  clVersionRollbackBtn: 'Roll back',
  clVersionRollbackAriaLabel: 'Roll back to version {N}',

  clLifecyclePreviewTitle: 'Preview (snapshot)',
  clPreviewBack: 'Back',
  clPreviewDraftBanner: 'This content is in Draft — it is not yet visible to students.',
  clPreviewError: 'Unable to load preview. Please try again.',
  clPreviewInvalidJson: 'Preview snapshot could not be parsed as JSON. Showing raw data.',
  clPreviewImageAlt: 'Content visual',

  clPreviewFieldName: 'Name',
  clPreviewFieldCountry: 'Country',
  clPreviewFieldSubject: 'Subject Code',
  clPreviewFieldLanguage: 'Content Language',
  clPreviewFieldOrder: 'Sequence Order',
  clPreviewFieldActive: 'Active',
  clPreviewFieldDifficulty: 'Difficulty',
  clPreviewFieldLocked: 'Locked',
  clPreviewFieldDuration: 'Est. Duration',
  clPreviewFieldExplanation: 'Explanation',
  clPreviewFieldVisual: 'Visual',
  clPreviewFieldBoss: 'Boss Lesson',
  clPreviewFieldQuestion: 'Question Text',
  clPreviewFieldType: 'Question Type',
  clPreviewFieldOptions: 'Options (JSON)',
  clPreviewFieldCorrectAnswer: 'Correct Answer',
  clPreviewFieldGeneratedBy: 'Generated By',
  clPreviewYes: 'Yes',
  clPreviewNo: 'No',

  clCoverageHeading: 'Publication Coverage',
  clCoverageSubheading: 'See which subjects are published for each grade and language.',
  clCoverageGradeLabel: 'Grade',
  clCoverageGradePlaceholder: 'Select a grade',
  clCoverageGradeHint: 'Select a grade to view its publication status across all subjects.',
  clCoverageTableCaption: 'Publication coverage by subject and language',
  clCoverageColSubject: 'Subject',
  clCoverageColStatus: 'Status',
  clCoverageSlotNotCreated: 'Not Created',
  clCoverageSlotDraft: 'Draft',
  clCoverageSlotPublished: 'Published',
  clCoverageSlotArchived: 'Archived',
  clCoverageWarningMissingChip: 'Warning: this subject is not yet published for this grade',
  clCoverageSuccess: 'All subjects are published for this grade.',
  clCoverageError: 'Unable to load coverage data. Please try again.',
  clCoverageGoToSubjects: 'Manage Subjects',
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

  // Profile PATCH copy (Bug fixes for edit/page.tsx — Job 2)
  childProfileSaveSuccess: 'تم تحديث الملف الشخصي بنجاح.',
  childProfileError422: 'قيمة البلد أو لغة الواجهة غير مدعومة.',

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

  // ── P7-01 curriculum (AR) ─────────────────────────────────────────────────────
  curriculumPageTitle: 'المناهج',
  curriculumDetailTitle: 'المادة',

  subjectsListHeading: 'المواد الدراسية',
  subjectsResultCount: 'مادة',
  subjectsListSearchPlaceholder: 'ابحث عن المواد…',
  subjectsFilterAllGrades: 'كل الصفوف',
  subjectsFilterAllLanguages: 'الكل',
  subjectsFilterLangAr: 'عربي',
  subjectsFilterLangEn: 'إنجليزي',
  subjectsClearFilters: 'مسح التصفية',
  subjectsListLoadingLabel: 'جارٍ تحميل المواد…',
  subjectsListError: 'تعذَّر تحميل المواد. يرجى المحاولة مرة أخرى.',
  subjectsTableCaption: 'قائمة المواد الدراسية',
  subjectsNoResults: 'لم يُعثر على مواد',
  subjectsNoResultsHint: 'جرِّب تعديل تصفية الصف أو اللغة.',
  subjectsViewDetail: 'عرض المادة',
  subjectsPrevPage: 'الصفحة السابقة',
  subjectsNextPage: 'الصفحة التالية',
  subjectsNewSubject: 'مادة جديدة',

  subjectsColSubject: 'المادة',
  subjectsColLanguage: 'اللغة',
  subjectsColGrade: 'الصف',
  subjectsColOrder: 'الترتيب',
  subjectsColActive: 'الحالة',

  subjectCodeMath: 'رياضيات',
  subjectCodeScience: 'علوم',
  subjectCodeArabic: 'عربية',
  subjectCodeEnglish: 'إنجليزية',

  contentLangAr: 'عر',
  contentLangEn: 'إن',

  subjectActiveBadge: 'نشط',
  subjectInactiveBadge: 'غير نشط',

  coveragePanelHeading: 'تغطية اللغة — الصف',
  coverageGapBadge: 'ثغرة',
  coverageCreateShortcut: 'إنشاء',
  coverageMissingSlot: 'مفقود',

  subjectsDetailBreadcrumb: 'المناهج',
  subjectsDetailGradePrefix: 'الصف',
  subjectsDetailOrderPrefix: 'الترتيب:',
  subjectsDetailEditBtn: 'تعديل',
  subjectsDetailToggleActiveBtn: 'تفعيل',
  subjectsDetailDeactivateBtn: 'إلغاء التفعيل',
  subjectsDetailDeleteBtn: 'إزالة',
  subjectsDetailNotFound: 'المادة غير موجودة',
  subjectsDetailNotFoundBody: 'هذه المادة غير موجودة أو تمت إزالتها.',
  subjectsDetailBackBtn: 'العودة للمناهج',

  subjectFormCreateTitle: 'مادة جديدة',
  subjectFormCreateSubtitle: 'أضف مادة جديدة إلى المناهج.',
  subjectFormEditTitle: 'تعديل المادة',
  subjectFormEditSubtitle: 'تحديث تفاصيل المادة.',
  subjectFormNameLabel: 'الاسم',
  subjectFormNamePlaceholder: 'مثال: الرياضيات',
  subjectFormGradeLabel: 'الصف',
  subjectFormGradePlaceholder: 'اختر الصف',
  subjectFormCodeLabel: 'رمز المادة',
  subjectFormCodePlaceholder: 'اختر الرمز',
  subjectFormLangLabel: 'لغة المحتوى',
  subjectFormLangPlaceholder: 'اختر اللغة',
  subjectFormPinnedLangHint: 'اللغة محددة مسبقًا لهذه المادة.',
  subjectFormOrderLabel: 'الترتيب',
  subjectFormOrderHint: 'يحدد ترتيب العرض ضمن شجرة اللغة ذاتها.',
  subjectFormActiveLabel: 'نشط',
  subjectFormActiveLabelOn: 'المادة مرئية للطلاب',
  subjectFormActiveLabelOff: 'المادة مخفية عن الطلاب',
  subjectFormCancelBtn: 'إلغاء',
  subjectFormCreateBtn: 'إنشاء المادة',
  subjectFormSaveBtn: 'حفظ التغييرات',
  subjectFormErrNameRequired: 'الاسم مطلوب.',
  subjectFormErrGradeRequired: 'الصف مطلوب.',
  subjectFormErrCodeRequired: 'رمز المادة مطلوب.',
  subjectFormErrLangRequired: 'اللغة مطلوبة.',
  subjectFormErrOrderInvalid: 'يجب أن يكون الترتيب رقمًا أكبر من أو يساوي ٠.',

  unitsHeading: 'الوحدات',
  unitsResultCount: 'وحدة',
  unitsNewUnit: 'وحدة جديدة',
  unitsNoResults: 'لا توجد وحدات بعد',
  unitsNoResultsHint: 'أضف الوحدة الأولى لهذه المادة.',
  unitsTableCaption: 'قائمة وحدات المادة',
  unitsColOrder: 'الترتيب',
  unitsColName: 'الاسم',
  unitsColActive: 'الحالة',
  unitsLoadingLabel: 'جارٍ تحميل الوحدات…',
  unitsListError: 'تعذَّر تحميل الوحدات. يرجى المحاولة مرة أخرى.',

  unitFormCreateTitle: 'وحدة جديدة',
  unitFormCreateSubtitle: 'الوحدات ترث اللغة من المادة الأم.',
  unitFormEditTitle: 'تعديل الوحدة',
  unitFormEditSubtitle: 'تحديث تفاصيل الوحدة.',
  unitFormInheritedLangNotice: 'اللغة: {lang} — موروثة من المادة الأم',
  unitFormNameLabel: 'اسم الوحدة',
  unitFormNamePlaceholder: 'مثال: مقدمة في الجبر',
  unitFormOrderLabel: 'الترتيب',
  unitFormOrderHint: 'ترتيب العرض ضمن هذه المادة.',
  unitFormActiveLabel: 'نشط',
  unitFormActiveLabelOn: 'الوحدة مرئية للطلاب',
  unitFormActiveLabelOff: 'الوحدة مخفية عن الطلاب',
  unitFormCancelBtn: 'إلغاء',
  unitFormCreateBtn: 'إنشاء الوحدة',
  unitFormSaveBtn: 'حفظ التغييرات',
  unitFormErrNameRequired: 'اسم الوحدة مطلوب.',

  reorderSaveBtn: 'حفظ الترتيب',
  reorderMoveUp: 'تحريك للأعلى',
  reorderMoveDown: 'تحريك للأسفل',
  reorderDisabledHint: 'اختر لغة واحدة لتمكين إعادة الترتيب',
  reorderPosition: 'نُقل إلى الموضع',
  reorderSavedMsg: 'تم حفظ الترتيب.',
  reorderErrorMsg: 'تعذَّر حفظ الترتيب. يرجى المحاولة مرة أخرى.',

  curriculumDeleteSubjectTitle: 'إزالة المادة',
  curriculumDeleteUnitTitle: 'إزالة الوحدة',
  curriculumDeleteSubjectBody: 'ستُخفى هذه المادة من المناهج. يمكن إعادة تفعيلها عند الحاجة. ملاحظة: لا يمكن إزالة المواد التي تحتوي على وحدات — يجب حذف جميع الوحدات أولاً.',
  curriculumDeleteUnitBody: 'ستُخفى هذه الوحدة من المناهج. يمكن إعادة تفعيلها عند الحاجة. ملاحظة: لا يمكن إزالة الوحدات التي تحتوي على دروس — يجب حذف جميع الدروس أولاً.',
  curriculumDeleteConfirmSubject: 'إزالة المادة',
  curriculumDeleteConfirmUnit: 'إزالة الوحدة',
  curriculumDeleteSuccessSubject: 'تمت إزالة المادة من المناهج.',
  curriculumDeleteSuccessUnit: 'تمت إزالة الوحدة من المناهج.',
  curriculumDeleteCancel: 'إلغاء',

  subjectActivateBtn: 'تفعيل',
  subjectDeactivateBtn: 'إلغاء التفعيل',
  unitActivateBtn: 'تفعيل',
  unitDeactivateBtn: 'إلغاء التفعيل',
  subjectToggleSuccess: 'تم تحديث حالة المادة.',
  unitToggleSuccess: 'تم تحديث حالة الوحدة.',
  subjectToggleError: 'تعذَّر تحديث حالة المادة. يرجى المحاولة مرة أخرى.',
  unitToggleError: 'تعذَّر تحديث حالة الوحدة. يرجى المحاولة مرة أخرى.',

  curriculumNotEmptyError: 'هذا العنصر يحتوي على عناصر فرعية ولا يمكن إزالته. احذف جميع العناصر الفرعية أولاً.',
  curriculumNetworkError: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',

  // ── P7-05 curriculum lifecycle copy (AR) — clLifecycle* namespace ─────────────

  clEntityTypeSubject: 'مادة',
  clEntityTypeUnit: 'وحدة',
  clEntityTypeLesson: 'درس',
  clEntityTypeQuestion: 'سؤال',

  clLifecycleDraft: 'مسودة',
  clLifecyclePublished: 'منشور',
  clLifecycleArchived: 'مؤرشف',

  clLifecycleDraftAriaLabel: 'حالة الدورة: مسودة',
  clLifecyclePublishedAriaLabel: 'حالة الدورة: منشور',
  clLifecycleArchivedAriaLabel: 'حالة الدورة: مؤرشف',

  clLifecycleStatusLabel: 'حالة الدورة',
  clLifecycleViewPreview: 'معاينة',
  clLifecycleSuccessBannerTransitioned: 'تم تحديث حالة الدورة بنجاح.',

  clPublishTitle: 'نشر هذا المحتوى؟',
  clPublishSubtitle: 'سيتمكن الطلاب من رؤية هذا المحتوى بعد النشر.',
  clPublishNoticeBody: 'يجعل النشرُ هذا المحتوىَ مرئيًا للطلاب المسجلين. يمكنك إلغاء نشره أو أرشفته لاحقًا.',
  clPublishConfirm: 'نشر',

  clUnpublishTitle: 'إلغاء نشر هذا المحتوى؟',
  clUnpublishSubtitle: 'سيعود المحتوى إلى حالة المسودة ولن يكون مرئيًا للطلاب.',
  clUnpublishNoticeBody: 'سيفقد الطلاب الوصول فورًا. يمكنك إعادة نشره في أي وقت.',
  clUnpublishConfirm: 'إلغاء النشر',

  clArchiveTitle: 'أرشفة هذا المحتوى؟',
  clArchiveSubtitle: 'المحتوى المؤرشف مخفي عن الطلاب ولا يمكن نشره مباشرةً.',
  clArchiveNoticeHeading: 'الأرشفة حالة نهائية للإصدار الحالي.',
  clArchiveNoticeBody: 'سيفقد الطلاب الوصول فورًا. يمكنك استعادته إلى المسودة في أي وقت، ثم مراجعته وإعادة نشره.',
  clArchiveConfirm: 'أرشفة',

  clRestoreTitle: 'استعادة إلى المسودة؟',
  clRestoreSubtitle: 'سيُنقل المحتوى إلى المسودة للمراجعة.',
  clRestoreNotice: 'تُعيد الاستعادةُ هذا المحتوى إلى المسودة. راجعه ثم أعد نشره عند الاستعداد.',
  clRestoreConfirm: 'استعادة إلى المسودة',

  clRollbackTitle: 'التراجع إلى الإصدار {N}؟',
  clRollbackSubtitle: 'نُشر في {formattedDate}',
  clRollbackVersionDetails: 'تفاصيل الإصدار',
  clRollbackWarningBody: 'يستبدل التراجعُ المحتوى المنشور الحالي بهذه النسخة القديمة. سيُحفظ الإصدار الحالي في سجل الإصدارات.',
  clRollbackReasonLabel: 'سبب التراجع',
  clRollbackReasonPlaceholder: 'اشرح سبب التراجع إلى هذا الإصدار…',
  clRollbackGuardrailNotice: 'السبب حقل مراجعة على مستوى الواجهة الأمامية فقط ولا يُرسل إلى الخادم. يُسجَّل هنا لأغراض المساءلة.',
  clRollbackConfirm: 'التراجع إلى الإصدار {N}',

  clVersionHistoryHeading: 'الإصدارات المنشورة',
  clVersionHistoryCount: 'إصدارات',
  clVersionHistoryEmpty: 'لا توجد إصدارات منشورة بعد.',
  clVersionHistoryError: 'تعذَّر تحميل سجل الإصدارات. يرجى المحاولة مرة أخرى.',
  clVersionPublishedBy: 'نشر بواسطة المعرِّف {N}',
  clVersionRollbackBtn: 'تراجع',
  clVersionRollbackAriaLabel: 'التراجع إلى الإصدار {N}',

  clLifecyclePreviewTitle: 'معاينة (لقطة)',
  clPreviewBack: 'رجوع',
  clPreviewDraftBanner: 'هذا المحتوى في حالة مسودة — غير مرئي للطلاب بعد.',
  clPreviewError: 'تعذَّر تحميل المعاينة. يرجى المحاولة مرة أخرى.',
  clPreviewInvalidJson: 'تعذَّر تحليل لقطة المعاينة كـ JSON. عرض البيانات الخام.',
  clPreviewImageAlt: 'صورة المحتوى',

  clPreviewFieldName: 'الاسم',
  clPreviewFieldCountry: 'الدولة',
  clPreviewFieldSubject: 'رمز المادة',
  clPreviewFieldLanguage: 'لغة المحتوى',
  clPreviewFieldOrder: 'الترتيب',
  clPreviewFieldActive: 'نشط',
  clPreviewFieldDifficulty: 'الصعوبة',
  clPreviewFieldLocked: 'مقفل',
  clPreviewFieldDuration: 'المدة التقديرية',
  clPreviewFieldExplanation: 'الشرح',
  clPreviewFieldVisual: 'الصورة',
  clPreviewFieldBoss: 'درس المدير',
  clPreviewFieldQuestion: 'نص السؤال',
  clPreviewFieldType: 'نوع السؤال',
  clPreviewFieldOptions: 'الخيارات (JSON)',
  clPreviewFieldCorrectAnswer: 'الإجابة الصحيحة',
  clPreviewFieldGeneratedBy: 'أنشأه',
  clPreviewYes: 'نعم',
  clPreviewNo: 'لا',

  clCoverageHeading: 'تغطية النشر',
  clCoverageSubheading: 'اعرض حالة النشر لكل مادة في كل صف وكل لغة.',
  clCoverageGradeLabel: 'الصف',
  clCoverageGradePlaceholder: 'اختر صفًا',
  clCoverageGradeHint: 'اختر صفًا لعرض حالة النشر عبر جميع المواد.',
  clCoverageTableCaption: 'تغطية النشر حسب المادة واللغة',
  clCoverageColSubject: 'المادة',
  clCoverageColStatus: 'الحالة',
  clCoverageSlotNotCreated: 'لم يُنشأ',
  clCoverageSlotDraft: 'مسودة',
  clCoverageSlotPublished: 'منشور',
  clCoverageSlotArchived: 'مؤرشف',
  clCoverageWarningMissingChip: 'تحذير: هذه المادة لم تُنشر لهذا الصف بعد',
  clCoverageSuccess: 'جميع المواد منشورة لهذا الصف.',
  clCoverageError: 'تعذَّر تحميل بيانات التغطية. يرجى المحاولة مرة أخرى.',
  clCoverageGoToSubjects: 'إدارة المواد',
};

const STRINGS: Record<Locale, AdminStrings> = { en, ar };

/** Default admin locale (English-first per Design Spec §7). */
export const ADMIN_LOCALE: Locale = 'en';

export function getStrings(locale: Locale = ADMIN_LOCALE): AdminStrings {
  return STRINGS[locale] ?? en;
}
