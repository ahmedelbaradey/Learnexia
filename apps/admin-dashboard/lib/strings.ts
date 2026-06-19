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

  // ── P7-02 Lessons & Content Blocks (lesson* / block* namespace) ──────────────
  // Page titles
  lessonPageTitle: string;
  lessonContentEditorTitle: string;
  // Lessons list page
  lessonListHeading: string;
  lessonResultCount: string;
  lessonListLoadingLabel: string;
  lessonListError: string;
  lessonTableCaption: string;
  lessonNoResults: string;
  lessonNoResultsHint: string;
  lessonViewDetail: string;
  lessonNewBtn: string;
  lessonPrevPage: string;
  lessonNextPage: string;
  // Table columns
  lessonColOrder: string;
  lessonColTitle: string;
  lessonColDifficulty: string;
  lessonColDuration: string;
  lessonColLock: string;
  lessonColActive: string;
  // Difficulty badge labels
  lessonDifficultyEasy: string;
  lessonDifficultyMedium: string;
  lessonDifficultyHard: string;
  // Lock badge labels
  lessonLocked: string;
  lessonUnlocked: string;
  // Lesson form
  lessonFormCreateTitle: string;
  lessonFormCreateSubtitle: string;
  lessonFormEditTitle: string;
  lessonFormEditSubtitle: string;
  lessonFormNameLabel: string;
  lessonFormNamePlaceholder: string;
  lessonFormDifficultyLabel: string;
  lessonFormDifficultyPlaceholder: string;
  lessonFormMinutesLabel: string;
  lessonFormMinutesHint: string;
  lessonFormLockedLabel: string;
  lessonFormLockedOnHint: string;
  lessonFormLockedOffHint: string;
  lessonFormActiveLabel: string;
  lessonFormActiveOnHint: string;
  lessonFormActiveOffHint: string;
  lessonFormInheritedLangPrefix: string;
  lessonFormCancelBtn: string;
  lessonFormCreateBtn: string;
  lessonFormSaveBtn: string;
  lessonFormErrNameRequired: string;
  lessonFormErrDifficultyRequired: string;
  lessonFormErrMinutesInvalid: string;
  // Lesson detail actions
  lessonDetailEditBtn: string;
  lessonDetailActivateBtn: string;
  lessonDetailDeactivateBtn: string;
  lessonDetailDeleteBtn: string;
  lessonActivateSuccess: string;
  lessonDeactivateSuccess: string;
  lessonNotFound: string;
  lessonNotFoundBody: string;
  // Inherited language badge
  lessonInheritedLangLabel: string;
  lessonInheritedLangAr: string;
  lessonInheritedLangEn: string;
  // Reorder
  lessonReorderSaveBtn: string;
  lessonReorderMoveUp: string;
  lessonReorderMoveDown: string;
  lessonReorderPosition: string;
  // Delete lesson dialog
  lessonDeleteTitle: string;
  lessonDeleteSubtitle: string;
  lessonDeleteCascadeHeading: string;
  lessonDeleteCascadeBody: string;
  lessonDeleteConfirmBtn: string;
  lessonDeleteCancelBtn: string;
  lessonDeleteSuccess: string;
  // Block editor
  blockEditorHeading: string;
  blockEditorCountLabel: string;
  blockEditorAddBtn: string;
  blockEditorLoadingLabel: string;
  blockEditorListError: string;
  blockListError: string;
  blockEditorEmpty: string;
  blockEditorEmptyHint: string;
  blockEditorSaveOrderBtn: string;
  // Block type picker
  blockPickerHeading: string;
  blockPickerText: string;
  blockPickerImage: string;
  blockPickerVideo: string;
  blockPickerCallout: string;
  // Block type badge labels
  blockTypeText: string;
  blockTypeImage: string;
  blockTypeVideo: string;
  blockTypeCallout: string;
  // Block card
  blockCardNumberPrefix: string;
  blockCardInactiveNotice: string;
  blockCardEditBtn: string;
  blockCardDeleteBtn: string;
  blockCardMoveUpBtn: string;
  blockCardMoveDownBtn: string;
  /** Expand/collapse toggle — collapsed state label. */
  blockCardExpand: string;
  /** Expand/collapse toggle — expanded state label. */
  blockCardCollapse: string;
  // Block form
  blockFormAddTitle: string;
  blockFormEditTitle: string;
  blockFormCancelBtn: string;
  blockFormAddConfirm: string;
  blockFormSaveConfirm: string;
  blockFormTypeChangeWarning: string;
  blockFormPayloadTooLarge: string;
  blockFormMarkdownLabel: string;
  blockFormMarkdownPlaceholder: string;
  blockFormMarkdownRequired: string;
  blockFormImageUrlLabel: string;
  blockFormImageAltLabel: string;
  blockFormImageAltPlaceholder: string;
  blockFormImageAltHint: string;
  blockFormVideoUrlLabel: string;
  blockFormVideoCaptionLabel: string;
  blockFormVideoCaptionPlaceholder: string;
  blockFormCalloutVariantLabel: string;
  blockFormCalloutVariantInfo: string;
  blockFormCalloutVariantWarning: string;
  blockFormCalloutVariantTip: string;
  blockFormCalloutMarkdownLabel: string;
  blockFormUrlRequired: string;
  blockFormUrlHttpsRequired: string;
  blockFormUrlPrivateNotAllowed: string;
  blockFormVariantRequired: string;
  blockFormUrlHint: string;
  // Block preview
  blockPreviewImageAlt: string;
  blockPreviewParseError: string;
  blockPreviewOpenLink: string;
  blockPreviewCalloutInfo: string;
  blockPreviewCalloutWarning: string;
  blockPreviewCalloutTip: string;
  blockPreviewCannotPreview: string;
  // Delete block dialog
  blockDeleteTitle: string;
  blockDeleteSubtitle: string;
  blockDeleteBody: string;
  blockDeleteConfirmBtn: string;

  // ── P7-04 Questions surface (question* / difficulty* namespace — disjoint) ────

  // Page
  questionPageTitle: string;
  questionBreadcrumbLabel: string;
  questionLessonContextLabel: string;
  questionLessonIdLabel: string;

  // List page
  questionsLoadingLabel: string;
  questionsListError: string;
  questionsEmpty: string;
  questionsEmptyHint: string;
  questionsRetry: string;
  questionsResultCount: string;
  questionsNewQuestion: string;
  questionsSaveOrder: string;
  questionsOrderSaved: string;
  questionsOrderError: string;

  // Table columns
  questionsColOrder: string;
  questionsColQuestion: string;
  questionsColType: string;
  questionsColDifficulty: string;
  questionsColActive: string;

  // Type badge labels
  questionTypeMcq: string;
  questionTypeTrueFalse: string;
  questionTypeMatching: string;
  questionTypeFillInBlank: string;

  // Difficulty badge labels (question-specific; mirrors lessonDifficulty* but in question namespace)
  difficultyEasy: string;
  difficultyMedium: string;
  difficultyHard: string;

  // Row actions (aria-labels)
  questionEditAriaLabel: string;
  questionDeleteAriaLabel: string;
  questionActivateAriaLabel: string;
  questionDeactivateAriaLabel: string;
  questionMoveUpAriaLabel: string;
  questionMoveDownAriaLabel: string;

  // Reorder aria-live announcement
  questionMovedAnnouncement: string;

  // Question editor
  questionEditorCreateTitle: string;
  questionEditorCreateSubtitle: string;
  questionEditorEditTitle: string;
  questionEditorEditSubtitle: string;
  questionEditorCloseAriaLabel: string;
  questionEditorCancelBtn: string;
  questionEditorSaveBtn: string;
  questionEditorSaveChangesBtn: string;

  // Shared fields
  questionFieldTypeLabel: string;
  questionFieldTypeLockedHint: string;
  questionFieldTypePlaceholder: string;
  questionFieldTextLabel: string;
  questionFieldTextPlaceholder: string;
  questionFieldDiffLabel: string;
  questionFieldDiffPlaceholder: string;

  // Lifecycle section in editor
  questionLifecycleSectionLabel: string;

  // MCQ sub-form
  /** Fieldset legend for the MCQ options group (a11y). */
  questionMcqOptionsLegend: string;
  questionMcqAddOption: string;
  questionMcqOptionPlaceholder: string;
  questionMcqRemoveAriaLabel: string;
  questionMcqCorrectSummary: string;
  questionMcqCorrectRadioAriaLabel: string;

  // Validation errors — MCQ
  questionErrMcqMinOptions: string;
  questionErrMcqEmptyOptions: string;
  questionErrMcqNoCorrect: string;

  // TrueFalse sub-form
  questionTfCorrectLabel: string;
  questionTfTrueLabel: string;
  questionTfFalseLabel: string;
  questionErrTfNoChoice: string;

  // FillInBlank sub-form
  questionFibCorrectLabel: string;
  questionFibPlaceholder: string;
  questionFibHint: string;
  questionErrFibEmpty: string;

  // Matching sub-form
  /** Outer fieldset legend for the Matching configuration (a11y). */
  questionMatchConfigLegend: string;
  questionMatchLeftHeader: string;
  questionMatchRightHeader: string;
  questionMatchRightHint: string;
  questionMatchAddLeft: string;
  questionMatchAddRight: string;
  questionMatchPairsHeader: string;
  questionMatchPairsHint: string;
  questionMatchLeftPlaceholder: string;
  questionMatchRightPlaceholder: string;
  questionMatchSelectPlaceholder: string;
  questionMatchRemoveLeftAriaLabel: string;
  questionMatchRemoveRightAriaLabel: string;
  questionMatchPairSelectAriaLabel: string;

  // Validation errors — Matching
  questionErrMatchMinLeft: string;
  questionErrMatchMinRight: string;
  questionErrMatchEqualCount: string;
  questionErrMatchEmptyLeft: string;
  questionErrMatchEmptyRight: string;
  questionErrMatchAllPaired: string;
  questionErrMatchDuplicatePair: string;

  // Matching parse error
  questionMatchParseError: string;

  // Shared validation
  questionErrTypeRequired: string;
  questionErrTextRequired: string;
  questionErrTextTooLong: string;
  questionErrDiffRequired: string;

  // Delete dialog
  questionDeleteTitle: string;
  questionDeleteSubtitle: string;
  questionDeleteBody: string;
  questionDeleteConfirm: string;
  questionDeleteCancel: string;
  questionDeleteSuccess: string;

  // Deactivate dialog
  questionDeactivateTitle: string;
  questionDeactivateSubtitle: string;
  questionDeactivateBody: string;
  questionDeactivateConfirm: string;
  questionDeactivateSuccess: string;

  // Activate dialog
  questionActivateTitle: string;
  questionActivateSubtitle: string;
  questionActivateBody: string;
  questionActivateConfirm: string;
  questionActivateSuccess: string;

  // ── P7-03 Skills & Graph (skill* / skillGraph* namespace — disjoint) ──────────
  // Disjoint from: curriculum*/subject*/unit* (P7-01), lesson*/block* (P7-02),
  // question* (P7-04), clLifecycle* (P7-05), lifecycle* (P7-07 account).

  // Page
  skillPageTitle: string;
  skillListHeading: string;
  skillResultCount: string;
  skillListError: string;
  skillTableCaption: string;
  skillViewGraph: string;
  skillNoResults: string;
  skillNoResultsHint: string;
  skillNoResultsSelectSubject: string;
  skillNoSubjectSelected: string;

  // Subject picker (graph scope)
  skillSubjectPickerLabel: string;
  skillSubjectPickerPlaceholder: string;
  skillSubjectPickerLoading: string;
  skillSubjectPickerError: string;

  // Concept filter
  skillConceptFilterPlaceholder: string;

  // Search
  skillSearchPlaceholder: string;

  // "New Skill" button
  skillNewBtn: string;
  skillNewBtnAriaLabel: string;

  // Table columns
  skillColName: string;
  skillColThreshold: string;
  skillColTime: string;
  skillColActive: string;

  // Skill form
  skillFormCreateTitle: string;
  skillFormEditTitle: string;
  skillFormCreateSubtitle: string;
  skillFormEditSubtitle: string;
  skillFormNameLabel: string;
  skillFormNamePlaceholder: string;
  skillFormNameRequired: string;
  skillFormThresholdLabel: string;
  skillFormThresholdHint: string;
  skillFormThresholdRequired: string;
  skillFormThresholdRange: string;
  skillFormTimeLabel: string;
  skillFormTimeHint: string;
  skillFormTimeRequired: string;
  skillFormTimeRange: string;
  skillFormConceptLabel: string;
  skillFormConceptPlaceholder: string;
  skillFormConceptRequired: string;
  skillFormConceptLoading: string;
  skillFormConceptError: string;
  skillFormCreateBtn: string;
  skillFormSaveBtn: string;
  skillFormCancel: string;

  // Delete dialog
  skillDeleteTitle: string;
  skillDeleteBody: string;
  skillDeleteConfirm: string;
  skillDeleteCancel: string;

  // Graph editor
  skillGraphTitle: string;
  skillGraphNoSubject: string;
  skillGraphNoSubjectBody: string;
  skillGraphLoading: string;
  skillGraphError: string;
  skillGraphNodeCount: string;
  skillGraphEdgeCount: string;
  skillGraphNodesEmpty: string;
  skillGraphRetry: string;

  // Node list
  skillGraphNodeListLabel: string;

  // Prerequisites section
  skillGraphPrerequisitesHeading: string;
  skillGraphPrerequisitesEmpty: string;

  // Unlocks section
  skillGraphUnlocksHeading: string;
  skillGraphUnlocksEmpty: string;

  // Add prerequisite control
  skillGraphAddPrerequisiteHeading: string;
  skillGraphPickerPlaceholder: string;
  skillGraphPickerAllAdded: string;
  skillGraphAddBtn: string;
  skillGraphDeselectNode: string;
  skillGraphRemovePrerequisite: string;

  // Edge errors
  skillGraphErrCycle: string;
  skillGraphErrCrossLanguage: string;
  skillGraphErrDuplicate: string;
  skillGraphErrNodeNotFound: string;
  skillGraphErrSubjectUnresolvable: string;
  skillGraphErrStrengthOutOfRange: string;
  skillGraphErrGeneric: string;
  skillGraphErrNetwork: string;

  // Edge success announcements (aria-live)
  skillGraphEdgeAdded: string;
  skillGraphEdgeRemoved: string;

  // Skill detail panel
  skillDetailSection: string;
  skillDetailMastery: string;
  skillDetailTime: string;
  skillDetailEditLink: string;

  // Skills page — additional a11y / i18n (Nits #2/#3)
  /** "Clear filters" button label (skills page). */
  skillClearFilters: string;
  /** aria-label for the concept filter <select>. */
  skillConceptFilterAriaLabel: string;
  /** SR-only label for the "Actions" column header. */
  skillColActionsLabel: string;
  /** aria-label for the per-row Edit button: "Edit {name}". */
  skillEditAriaLabel: string;
  /** aria-label for the per-row Delete button: "Delete {name}". */
  skillDeleteAriaLabel: string;
  /** SR-only label for the "Previous page" pagination button. */
  skillPrevPage: string;
  /** SR-only label for the "Next page" pagination button. */
  skillNextPage: string;

  // SkillGraph — node-type words (aria-label fragments, Nit #3)
  /** Node-type word: "Skill". */
  skillGraphNodeTypeSkill: string;
  /** Node-type word: "Concept". */
  skillGraphNodeTypeConcept: string;
  /** Node-type word: "Review". */
  skillGraphNodeTypeReview: string;
  /** aria-label for the prerequisites list: "Prerequisites of {name}". */
  skillGraphPrerequisitesAriaLabel: string;
  /** aria-label for the unlocks list: "Skills unlocked by {name}". */
  skillGraphUnlocksAriaLabel: string;
  /** Accessible name for the remove-edge button (suffix after ": "): "Remove prerequisite: {name}". */
  skillGraphRemoveEdgeAriaLabel: string;

  // ── P7-12 Audit Log viewer (audit* namespace — DISJOINT from all prior namespaces) ──
  // Design Spec §G.1. Read-only surface — no mutation strings.

  // Navigation
  /** Nav item label for "/audit". */
  navAuditLog: string;

  // Page
  pageTitleAudit: string;
  auditPageHeading: string;

  // Filters
  auditFilterAdminIdLabel: string;
  auditFilterAdminIdPlaceholder: string;
  auditFilterActionTypeLabel: string;
  /** Default placeholder option: "All actions". */
  auditFilterActionTypePlaceholder: string;
  auditFilterTargetTypeLabel: string;
  /** Default placeholder option: "All targets". */
  auditFilterTargetTypePlaceholder: string;
  auditFilterDateFromLabel: string;
  auditFilterDateToLabel: string;
  auditClearFilters: string;
  /** Inline validation note when DateTo < DateFrom. */
  auditDateRangeError: string;

  // Table
  auditTableCaption: string;
  auditColAdmin: string;
  auditColAction: string;
  auditColTarget: string;
  auditColWhen: string;
  /** SR-only header for the expand-toggle column (col 5). */
  auditColDetailsHeader: string;

  // Result count
  /** Suffix appended to totalCount: "N entries" / "N سجل". */
  auditResultCount: string;

  // Loading / states
  /** role="status" aria-label on the skeleton wrapper. */
  auditLoadingLabel: string;
  auditEmptyHeading: string;
  /** Empty body text when filters are active. */
  auditEmptyBodyFiltered: string;
  /** Empty body text when the log is genuinely empty (no filters). */
  auditEmptyBodyEmpty: string;

  // Error
  auditListError: string;
  auditRetry: string;

  // Pagination
  auditPrevPage: string;
  auditNextPage: string;

  // Row expand/collapse (aria-label on the button)
  auditExpandEntry: string;
  auditCollapseEntry: string;

  // Detail panel field labels
  auditDetailEventId: string;
  auditDetailAdmin: string;
  auditDetailAction: string;
  auditDetailTargetType: string;
  auditDetailTargetId: string;
  auditDetailOccurredAt: string;
  auditDetailCreatedAt: string;
  auditDetailDetailsLabel: string;
  auditDetailCopy: string;
  auditDetailCopied: string;
  /** Shown when details is null: "—". */
  auditDetailNoDetails: string;

  // ── P7-13 Gamification section (gam* / gamification* namespace — DISJOINT) ──
  // Nav
  navGamification: string;
  // Hub page
  gamificationHubTitle: string;
  gamificationHubSubtitle: string;
  gamificationHubManage: string;
  gamificationStudentOverridesHeading: string;
  gamificationStudentOverridesNotice: string;
  // Shared action/button labels
  gamEditBtn: string;
  gamActivateBtn: string;
  gamDeactivateBtn: string;
  gamExpireBtn: string;
  gamDialogCancelBtn: string;
  // Badge catalog page
  gamBadgesPageTitle: string;
  gamBadgesNewBtn: string;
  gamBadgesEmptyHeading: string;
  gamBadgesEmptyBody: string;
  gamBadgeDeactivateTitle: string;
  gamBadgeDeactivateNotice: string;
  gamBadgeActivateTitle: string;
  gamBadgeActivateNotice: string;
  // BadgeForm
  gamBadgeFormCreateTitle: string;
  gamBadgeFormCreateSubtitle: string;
  gamBadgeFormEditTitle: string;
  gamBadgeFormEditSubtitle: string;
  gamBadgeFormCodeLabel: string;
  gamBadgeFormCodePlaceholder: string;
  gamBadgeFormCodeHint: string;
  gamBadgeFormNameLabel: string;
  gamBadgeFormNamePlaceholder: string;
  gamBadgeFormDescLabel: string;
  gamBadgeFormIconKeyLabel: string;
  gamBadgeFormIconKeyPlaceholder: string;
  gamBadgeFormIconKeyHint: string;
  gamBadgeFormRarityLabel: string;
  gamBadgeFormRarityPlaceholder: string;
  gamBadgeFormTriggerLabel: string;
  gamBadgeFormThresholdLabel: string;
  gamBadgeFormThresholdRequired: string;
  gamBadgeFormRewardXpLabel: string;
  gamBadgeFormSortOrderLabel: string;
  gamBadgeFormSortOrderHint: string;
  gamBadgeFormCancelBtn: string;
  gamBadgeFormCreateBtn: string;
  gamBadgeFormSaveBtn: string;
  // Mission catalog page
  gamMissionsPageTitle: string;
  gamMissionDeactivateTitle: string;
  gamMissionDeactivateNotice: string;
  gamMissionActivateTitle: string;
  gamMissionActivateNotice: string;
  gamMissionsNewBtn: string;
  gamMissionsEmptyHeading: string;
  gamMissionsEmptyBody: string;
  // MissionForm
  gamMissionFormCreateTitle: string;
  gamMissionFormCreateSubtitle: string;
  gamMissionFormEditTitle: string;
  gamMissionFormEditSubtitle: string;
  gamMissionFormCodeLabel: string;
  gamMissionFormIconKeyLabel: string;
  gamMissionFormTitleKeyLabel: string;
  gamMissionFormTitleKeyHint: string;
  gamMissionFormCadenceLabel: string;
  gamMissionFormTargetTypeLabel: string;
  gamMissionFormTargetLabel: string;
  gamMissionFormRewardXpLabel: string;
  gamMissionFormSortOrderLabel: string;
  gamMissionFormCancelBtn: string;
  gamMissionFormCreateBtn: string;
  gamMissionFormSaveBtn: string;
  // Timed events page
  gamEventsPageTitle: string;
  gamEventsNewBtn: string;
  gamEventsEmptyHeading: string;
  gamEventsEmptyBody: string;
  gamEventActivateTitle: string;
  gamEventActivateNotice: string;
  gamEventExpireTitle: string;
  gamEventExpireNotice: string;
  gamEventActivateConfirmBtn: string;
  gamEventExpireConfirmBtn: string;
  // TimedEventForm
  gamEventFormCreateTitle: string;
  gamEventFormCreateSubtitle: string;
  gamEventFormEditTitle: string;
  gamEventFormEditSubtitle: string;
  gamEventFormCodeLabel: string;
  gamEventFormNameEnLabel: string;
  gamEventFormNameArLabel: string;
  gamEventFormDescEnLabel: string;
  gamEventFormDescArLabel: string;
  gamEventFormStartLabel: string;
  gamEventFormEndLabel: string;
  gamEventFormUtcHint: string;
  gamEventFormEndBeforeStart: string;
  gamEventFormMultiplierLabel: string;
  gamEventFormMultiplierHint: string;
  gamEventFormScopeLabel: string;
  gamEventFormScopeNotice: string;
  gamEventFormCreateBtn: string;
  gamEventFormSaveBtn: string;
  gamEventFormCancelBtn: string;
  // Student overrides — shared
  gamOverridesHeading: string;
  // League tier override
  gamLeagueTierBtn: string;
  gamLeagueTierDialogTitle: string;
  gamLeagueTierCurrentLabel: string;
  gamLeagueTierUnknown: string;
  gamLeagueTierCaveat: string;
  gamLeagueTierNewLabel: string;
  gamLeagueTierSelectPlaceholder: string;
  gamLeagueTierAuditNotice: string;
  gamLeagueTierReasonLabel: string;
  gamLeagueTierConfirmBtn: string;
  gamLeagueTierSuccessBanner: string;
  gamLeagueTierErr400SameTier: string;
  gamLeagueTierErr404: string;
  gamLeagueTierErr422: string;
  gamLeagueTierErrNetwork: string;
  // Streak freeze
  gamFreezeFreezeBtn: string;
  gamFreezeDialogTitle: string;
  gamFreezeBalanceUnavailable: string;
  gamFreezeCountLabel: string;
  gamFreezeCountHint: string;
  gamFreezeReasonLabel: string;
  gamFreezeConfirmBtn: string;
  gamFreezeSuccessBanner: string;
  gamFreezeErrCount: string;
  gamFreezeErr404: string;
  gamFreezeErr422: string;
  gamFreezeErrNetwork: string;

  // ── P7-13 additional string keys used by page/component implementations ────
  // Generic shared
  gamification: string;
  gamCancelBtn: string;
  gamRetry: string;

  // Badge page
  gamBadgePageTitle: string;
  gamBadgeCreateBtn: string;
  gamBadgeActive: string;
  gamBadgeInactive: string;
  gamBadgeLoading: string;
  gamBadgeFetchError: string;
  gamBadgeEmpty: string;
  gamBadgeCreateFirst: string;
  gamBadgeEditBtn: string;
  gamBadgeActivateBtn: string;
  gamBadgeDeactivateBtn: string;
  gamBadgeTableCaption: string;
  gamBadgeColCode: string;
  gamBadgeColName: string;
  gamBadgeColRarity: string;
  gamBadgeColTrigger: string;
  gamBadgeColXp: string;
  gamBadgeColStatus: string;
  gamBadgeColActions: string;
  gamBadgeDeactivateSubtitle: string;
  gamBadgeDeactivateNote: string;
  gamBadgeDeactivateConfirmBtn: string;
  gamBadgeActivateSubtitle: string;
  gamBadgeActivateNote: string;
  gamBadgeActivateConfirmBtn: string;
  gamBadgeActivatedBanner: string;
  gamBadgeDeactivatedBanner: string;
  gamBadgeNotFoundError: string;
  gamBadgeActionError: string;

  // Mission page
  gamMissionPageTitle: string;
  gamMissionCreateBtn: string;
  gamMissionActive: string;
  gamMissionInactive: string;
  gamMissionLoading: string;
  gamMissionFetchError: string;
  gamMissionEmpty: string;
  gamMissionCreateFirst: string;
  gamMissionEditBtn: string;
  gamMissionActivateBtn: string;
  gamMissionDeactivateBtn: string;
  gamMissionTableCaption: string;
  gamMissionColTitle: string;
  gamMissionColType: string;
  gamMissionColTargetType: string;
  gamMissionColTargetCount: string;
  gamMissionColXp: string;
  gamMissionColStatus: string;
  gamMissionColActions: string;
  gamMissionDeactivateSubtitle: string;
  gamMissionDeactivateNote: string;
  gamMissionDeactivateConfirmBtn: string;
  gamMissionActivateSubtitle: string;
  gamMissionActivateNote: string;
  gamMissionActivateConfirmBtn: string;
  gamMissionActivatedBanner: string;
  gamMissionDeactivatedBanner: string;
  gamMissionNotFoundError: string;
  gamMissionActionError: string;
  gamMissionFormTitleLabel: string;
  gamMissionFormDescLabel: string;
  gamMissionFormTypeLabel: string;
  gamMissionFormTypePlaceholder: string;
  gamMissionFormTargetCountLabel: string;
  gamMissionFormSortOrderHint: string;

  // Events page
  gamEventPageTitle: string;
  gamEventCreateBtn: string;
  gamEventLoading: string;
  gamEventFetchError: string;
  gamEventEmpty: string;
  gamEventCreateFirst: string;
  gamEventEditBtn: string;
  gamEventActivateBtn: string;
  gamEventExpireBtn: string;
  gamEventTableCaption: string;
  gamEventColName: string;
  gamEventColScope: string;
  gamEventColMultiplier: string;
  gamEventColStart: string;
  gamEventColEnd: string;
  gamEventColStatus: string;
  gamEventColActions: string;
  gamEventActivateSubtitle: string;
  gamEventActivateNote: string;
  gamEventExpireSubtitle: string;
  gamEventExpireNote: string;
  gamEventActivatedBanner: string;
  gamEventExpiredBanner: string;
  gamEventNotFoundError: string;
  gamEventActionError: string;
  gamEventFormDescGapNotice: string;
  gamEventFormDescGapPlaceholder: string;
  gamEventFormDateHint: string;

  // League tier override dialog
  gamLeagueTierDialogBody: string;
  gamLeagueTierCancelBtn: string;
  gamLeagueTierError400: string;
  gamLeagueTierError404: string;
  gamLeagueTierError422: string;
  gamLeagueTierErrorNetwork: string;
  gamLeagueTierReasonPlaceholder: string;

  // Streak freeze dialog
  gamFreezeDialogBody: string;
  gamFreezeCancelBtn: string;
  gamFreezeError400: string;
  gamFreezeError404: string;
  gamFreezeError422: string;
  gamFreezeErrorNetwork: string;
  gamFreezeReasonPlaceholder: string;

  // ── P7-09 Moderation Queue (mod* namespace — DISJOINT from all prior namespaces) ──

  // Nav
  navModeration: string;

  // Page titles
  modPageTitleQueue: string;
  modPageTitleDetail: string;

  // List page
  modListHeading: string;
  modResultCount: string;
  modSearchPlaceholder: string;
  modFilterAllStatuses: string;
  modFilterAllSources: string;
  modFilterAllSubjects: string;
  modFilterAllGrades: string;
  modClearFilters: string;
  modListLoadingLabel: string;
  modListError: string;
  modListRetry: string;
  modTableCaption: string;
  modPrevPage: string;
  modNextPage: string;

  // Subject filter options
  modSubjectMath: string;
  modSubjectScience: string;
  modSubjectArabic: string;
  modSubjectEnglish: string;

  // Empty states
  modEmptyNoFilters: string;
  modEmptyNoFiltersBody: string;
  modEmptyFiltered: string;
  modEmptyFilteredBody: string;

  // Table columns
  modColSource: string;
  modColContentRef: string;
  modColSubjectGrade: string;
  modColTaskKind: string;
  modColStatus: string;
  modColDetected: string;
  modViewDetail: string;

  // Status badge labels
  modStatusPending: string;
  modStatusApproved: string;
  modStatusRejected: string;
  modStatusFlagged: string;

  // Source badge labels
  modSourceAiOutput: string;
  modSourceCurriculumUpload: string;

  // Detail page
  modDetailLoadingLabel: string;
  modDetailError: string;
  modNotFoundHeading: string;
  modNotFoundBody: string;
  modBackToQueue: string;

  // Detail sections
  modSectionDetails: string;
  modSectionReviewHistory: string;

  // Detail fields
  modFieldStudentId: string;
  modFieldDetectedAt: string;
  modFieldItemId: string;

  // Review history fields
  modReviewedBy: string;
  modReviewedAt: string;
  modReviewReason: string;

  // Terminal notice
  modTerminalNotice: string;

  // Safety verdict
  modVerdictSection: string;
  modVerdictPrivacyNote: string;
  modVerdictFailedChecks: string;
  modVerdictReasonCodes: string;
  modVerdictActionTaken: string;
  modVerdictModelId: string;
  modVerdictUnavailable: string;

  // Review actions panel
  modReviewActionsHeading: string;
  modReviewApprove: string;
  modReviewReject: string;
  modReviewFlag: string;
  modAlreadyFlagged: string;

  // Review dialogs (shared cancel)
  modDlgCancel: string;

  // Approve dialog
  modDlgApproveTitle: string;
  modDlgApproveSubtitle: string;
  modDlgApproveReasonLabel: string;
  modDlgApproveConfirm: string;

  // Reject dialog
  modDlgRejectTitle: string;
  modDlgRejectSubtitle: string;
  modDlgRejectReasonLabel: string;
  modDlgRejectConfirm: string;

  // Flag dialog
  modDlgFlagTitle: string;
  modDlgFlagSubtitle: string;
  modDlgFlagReasonLabel: string;
  modDlgFlagConfirm: string;

  // Error messages (dialog)
  modErrAlreadyTerminal: string;
  modErrAlreadyFlagged: string;
  modErr404: string;
  modErrValidation: string;
  modErrNetwork: string;

  // Success banner
  modReviewSuccess: string;
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

  // ── P7-02 Lessons & Content Blocks (EN) ──────────────────────────────────────
  lessonPageTitle: 'Lessons',
  lessonContentEditorTitle: 'Content Editor',
  lessonListHeading: 'Lessons',
  lessonResultCount: 'lessons',
  lessonListLoadingLabel: 'Loading lessons…',
  lessonListError: 'Unable to load lessons. Please try again.',
  lessonTableCaption: 'Lessons in this unit',
  lessonNoResults: 'No lessons yet',
  lessonNoResultsHint: 'Add the first lesson to this unit.',
  lessonViewDetail: 'View content',
  lessonNewBtn: 'New Lesson',
  lessonPrevPage: 'Previous page',
  lessonNextPage: 'Next page',
  lessonColOrder: 'Order',
  lessonColTitle: 'Title',
  lessonColDifficulty: 'Difficulty',
  lessonColDuration: 'Duration',
  lessonColLock: 'Lock',
  lessonColActive: 'Active',
  lessonDifficultyEasy: 'Easy',
  lessonDifficultyMedium: 'Medium',
  lessonDifficultyHard: 'Hard',
  lessonLocked: 'Locked',
  lessonUnlocked: 'Unlocked',
  lessonFormCreateTitle: 'New Lesson',
  lessonFormCreateSubtitle: 'Add a new lesson to this unit.',
  lessonFormEditTitle: 'Edit Lesson',
  lessonFormEditSubtitle: 'Update lesson details.',
  lessonFormNameLabel: 'Name',
  lessonFormNamePlaceholder: 'e.g. Introduction to Fractions',
  lessonFormDifficultyLabel: 'Difficulty',
  lessonFormDifficultyPlaceholder: 'Select difficulty',
  lessonFormMinutesLabel: 'Estimated Duration',
  lessonFormMinutesHint: 'Minutes. Enter 0 if duration is not applicable.',
  lessonFormLockedLabel: 'Locked',
  lessonFormLockedOnHint: 'Lesson requires prior completion to unlock',
  lessonFormLockedOffHint: 'Lesson is accessible immediately',
  lessonFormActiveLabel: 'Active',
  lessonFormActiveOnHint: 'Lesson visible to students',
  lessonFormActiveOffHint: 'Lesson hidden from students',
  lessonFormInheritedLangPrefix: 'Content language:',
  lessonFormCancelBtn: 'Cancel',
  lessonFormCreateBtn: 'Create Lesson',
  lessonFormSaveBtn: 'Save Changes',
  lessonFormErrNameRequired: 'Name is required',
  lessonFormErrDifficultyRequired: 'Difficulty is required',
  lessonFormErrMinutesInvalid: 'Duration must be 0 or more minutes',
  lessonDetailEditBtn: 'Edit',
  lessonDetailActivateBtn: 'Activate',
  lessonDetailDeactivateBtn: 'Deactivate',
  lessonDetailDeleteBtn: 'Delete',
  lessonActivateSuccess: 'Lesson activated.',
  lessonDeactivateSuccess: 'Lesson deactivated.',
  lessonNotFound: 'Lesson not found',
  lessonNotFoundBody: "This lesson doesn't exist or was removed.",
  lessonInheritedLangLabel: 'Language:',
  lessonInheritedLangAr: 'Arabic',
  lessonInheritedLangEn: 'English',
  lessonReorderSaveBtn: 'Save Order',
  lessonReorderMoveUp: 'Move {name} up',
  lessonReorderMoveDown: 'Move {name} down',
  lessonReorderPosition: '{name} moved to position {N} of {total}',
  lessonDeleteTitle: 'Delete Lesson',
  lessonDeleteSubtitle: '"{name}" will be removed.',
  lessonDeleteCascadeHeading: 'All content blocks will be deleted',
  lessonDeleteCascadeBody: 'Deleting this lesson will soft-delete all its content blocks. This lesson will no longer be visible to students. Content blocks cannot be restored individually from the dashboard in this version.',
  lessonDeleteConfirmBtn: 'Delete Lesson',
  lessonDeleteCancelBtn: 'Cancel',
  lessonDeleteSuccess: 'Lesson deleted.',
  blockEditorHeading: 'Content Blocks',
  blockEditorCountLabel: 'blocks',
  blockEditorAddBtn: 'Add Block',
  blockEditorLoadingLabel: 'Loading content blocks…',
  blockEditorListError: 'Unable to load content blocks. Please try again.',
  blockListError: 'Unable to load content blocks. Please try again.',
  blockEditorEmpty: 'No content yet',
  blockEditorEmptyHint: "Add the first block to build this lesson's content.",
  blockEditorSaveOrderBtn: 'Save Block Order',
  blockPickerHeading: 'Choose block type',
  blockPickerText: 'Text block',
  blockPickerImage: 'Image',
  blockPickerVideo: 'Video',
  blockPickerCallout: 'Callout',
  blockTypeText: 'Text',
  blockTypeImage: 'Image',
  blockTypeVideo: 'Video',
  blockTypeCallout: 'Callout',
  blockCardNumberPrefix: 'Block',
  blockCardInactiveNotice: 'This block is inactive (read-only — no toggle endpoint).',
  blockCardEditBtn: 'Edit block {N}',
  blockCardDeleteBtn: 'Delete block {N}',
  blockCardMoveUpBtn: 'Move {type} block up',
  blockCardMoveDownBtn: 'Move {type} block down',
  blockCardExpand: '▼ Expand',
  blockCardCollapse: '▲ Collapse',
  blockFormAddTitle: 'Add Block',
  blockFormEditTitle: 'Edit Block',
  blockFormCancelBtn: 'Cancel',
  blockFormAddConfirm: 'Add Block',
  blockFormSaveConfirm: 'Save Block',
  blockFormTypeChangeWarning: 'Changing the block type will discard the current content fields.',
  blockFormPayloadTooLarge: 'Content is too large (max 65,536 characters)',
  blockFormMarkdownLabel: 'Markdown content',
  blockFormMarkdownPlaceholder: 'Enter Markdown text…',
  blockFormMarkdownRequired: 'Content is required',
  blockFormImageUrlLabel: 'Image URL',
  blockFormImageAltLabel: 'Alt text (optional)',
  blockFormImageAltPlaceholder: 'Describe the image for screen readers…',
  blockFormImageAltHint: 'Recommended for accessibility.',
  blockFormVideoUrlLabel: 'Video URL',
  blockFormVideoCaptionLabel: 'Caption (optional)',
  blockFormVideoCaptionPlaceholder: 'Optional caption shown below the video…',
  blockFormCalloutVariantLabel: 'Callout type',
  blockFormCalloutVariantInfo: 'Info',
  blockFormCalloutVariantWarning: 'Warning',
  blockFormCalloutVariantTip: 'Tip',
  blockFormCalloutMarkdownLabel: 'Callout content',
  blockFormUrlRequired: 'URL is required',
  blockFormUrlHttpsRequired: 'URL must start with https://',
  blockFormUrlPrivateNotAllowed: 'Private/local addresses are not allowed',
  blockFormVariantRequired: 'Callout type is required',
  blockFormUrlHint: 'Must be an absolute https:// URL. Private IP addresses and localhost are not allowed.',
  blockPreviewImageAlt: 'Block image',
  blockPreviewParseError: 'Unable to parse block payload.',
  blockPreviewOpenLink: 'Open ↗',
  blockPreviewCalloutInfo: 'Info',
  blockPreviewCalloutWarning: 'Warning',
  blockPreviewCalloutTip: 'Tip',
  blockPreviewCannotPreview: 'Cannot preview',
  blockDeleteTitle: 'Delete Block',
  blockDeleteSubtitle: 'This block will be removed from the lesson.',
  blockDeleteBody: 'The block will be soft-deleted and hidden from students. This is reversible by the backend but there is no restore UI in this version.',
  blockDeleteConfirmBtn: 'Delete Block',

  // ── P7-04 Questions surface (EN) ─────────────────────────────────────────────

  // Page
  questionPageTitle: 'Questions',
  questionBreadcrumbLabel: 'Questions',
  questionLessonContextLabel: 'Lesson',
  questionLessonIdLabel: 'ID {id}',

  // List page
  questionsLoadingLabel: 'Loading questions…',
  questionsListError: 'Unable to load questions. Please try again.',
  questionsEmpty: 'No questions yet',
  questionsEmptyHint: 'Add the first question to this lesson.',
  questionsRetry: 'Try again',
  questionsResultCount: 'questions',
  questionsNewQuestion: 'New Question',
  questionsSaveOrder: 'Save Order',
  questionsOrderSaved: 'Order saved.',
  questionsOrderError: 'Failed to save order.',

  // Table columns
  questionsColOrder: 'Order',
  questionsColQuestion: 'Question',
  questionsColType: 'Type',
  questionsColDifficulty: 'Difficulty',
  questionsColActive: 'Active',

  // Type badge labels
  questionTypeMcq: 'MCQ',
  questionTypeTrueFalse: 'True/False',
  questionTypeMatching: 'Matching',
  questionTypeFillInBlank: 'Fill In Blank',

  // Difficulty badge labels
  difficultyEasy: 'Easy',
  difficultyMedium: 'Medium',
  difficultyHard: 'Hard',

  // Row actions
  questionEditAriaLabel: 'Edit question {N}',
  questionDeleteAriaLabel: 'Remove question {N}',
  questionActivateAriaLabel: 'Show question to students',
  questionDeactivateAriaLabel: 'Hide question from students',
  questionMoveUpAriaLabel: 'Move question up',
  questionMoveDownAriaLabel: 'Move question down',

  // Reorder aria-live
  questionMovedAnnouncement: 'Question moved to position {N} of {total}.',

  // Question editor
  questionEditorCreateTitle: 'New Question',
  questionEditorCreateSubtitle: 'Add a question to this lesson.',
  questionEditorEditTitle: 'Edit Question',
  questionEditorEditSubtitle: 'Update question content.',
  questionEditorCloseAriaLabel: 'Close editor',
  questionEditorCancelBtn: 'Cancel',
  questionEditorSaveBtn: 'Save Question',
  questionEditorSaveChangesBtn: 'Save Changes',

  // Shared fields
  questionFieldTypeLabel: 'Question Type',
  questionFieldTypeLockedHint: 'Question type cannot be changed after creation.',
  questionFieldTypePlaceholder: 'Select type',
  questionFieldTextLabel: 'Question',
  questionFieldTextPlaceholder: 'Enter the question text…',
  questionFieldDiffLabel: 'Difficulty',
  questionFieldDiffPlaceholder: 'Select difficulty',

  // Lifecycle section
  questionLifecycleSectionLabel: 'Publishing',

  // MCQ sub-form
  questionMcqOptionsLegend: 'Options',
  questionMcqAddOption: 'Add Option',
  questionMcqOptionPlaceholder: 'Option {N}',
  questionMcqRemoveAriaLabel: 'Remove option {N}',
  questionMcqCorrectSummary: 'Correct:',
  questionMcqCorrectRadioAriaLabel: 'Mark option {N} as correct',

  // Validation — MCQ
  questionErrMcqMinOptions: 'Add at least 2 options.',
  questionErrMcqEmptyOptions: 'All options must have text.',
  questionErrMcqNoCorrect: 'Select the correct answer.',

  // TrueFalse sub-form
  questionTfCorrectLabel: 'Correct Answer',
  questionTfTrueLabel: 'True',
  questionTfFalseLabel: 'False',
  questionErrTfNoChoice: 'Select True or False.',

  // FillInBlank sub-form
  questionFibCorrectLabel: 'Correct Answer',
  questionFibPlaceholder: 'Enter the accepted answer…',
  questionFibHint: "Student's typed answer must match exactly (case-insensitive).",
  questionErrFibEmpty: 'Correct answer is required.',

  // Matching sub-form
  questionMatchConfigLegend: 'Matching configuration',
  questionMatchLeftHeader: 'Left Items',
  questionMatchRightHeader: 'Right Items',
  questionMatchRightHint: "Order these randomly — don't put the correct match in the same row.",
  questionMatchAddLeft: 'Add Left Item',
  questionMatchAddRight: 'Add Right Item',
  questionMatchPairsHeader: 'Correct Pairs',
  questionMatchPairsHint: 'For each left item, select its matching right item.',
  questionMatchLeftPlaceholder: 'Left item {N}',
  questionMatchRightPlaceholder: 'Right item {N}',
  questionMatchSelectPlaceholder: 'Select match',
  questionMatchRemoveLeftAriaLabel: 'Remove left item {N}',
  questionMatchRemoveRightAriaLabel: 'Remove right item {N}',
  questionMatchPairSelectAriaLabel: 'Match for left item: {text}',

  // Validation — Matching
  questionErrMatchMinLeft: 'Add at least 1 left item.',
  questionErrMatchMinRight: 'Add at least 1 right item.',
  questionErrMatchEqualCount: 'Left and right must have the same number of items.',
  questionErrMatchEmptyLeft: 'All left items must have text.',
  questionErrMatchEmptyRight: 'All right items must have text.',
  questionErrMatchAllPaired: 'All pairs must be assigned.',
  questionErrMatchDuplicatePair: 'Each right item can only be used once.',

  // Matching parse error
  questionMatchParseError: 'Failed to load question data — invalid format. Delete and recreate.',

  // Shared validation
  questionErrTypeRequired: 'Question type is required.',
  questionErrTextRequired: 'Question text is required.',
  questionErrTextTooLong: 'Question text must be ≤4096 characters.',
  questionErrDiffRequired: 'Difficulty is required.',

  // Delete dialog
  questionDeleteTitle: 'Remove Question',
  questionDeleteSubtitle: 'Question will be hidden from this lesson.',
  questionDeleteBody: 'This is a soft delete — the question is hidden from students but can be restored by an engineer. Student progress is preserved.',
  questionDeleteConfirm: 'Remove Question',
  questionDeleteCancel: 'Cancel',
  questionDeleteSuccess: 'Question removed.',

  // Deactivate dialog
  questionDeactivateTitle: 'Hide Question',
  questionDeactivateSubtitle: 'The question will be hidden from students.',
  questionDeactivateBody: 'Hidden questions are not shown to students but remain visible to admins. The question can be re-activated at any time.',
  questionDeactivateConfirm: 'Hide from Students',
  questionDeactivateSuccess: 'Question hidden from students.',

  // Activate dialog
  questionActivateTitle: 'Show Question',
  questionActivateSubtitle: 'The question will be visible to students again.',
  questionActivateBody: 'Students will be able to see and answer this question once it is shown.',
  questionActivateConfirm: 'Show to Students',
  questionActivateSuccess: 'Question shown to students.',

  // ── P7-03 Skills & Graph (EN) ─────────────────────────────────────────────────

  // Page
  skillPageTitle: 'Skills',
  skillListHeading: 'Skills',
  skillResultCount: 'skills',
  skillListError: 'Failed to load skills. Please try again.',
  skillTableCaption: 'Skills list',
  skillViewGraph: 'View graph',
  skillNoResults: 'No skills found',
  skillNoResultsHint: 'Try adjusting the concept filter or search term.',
  skillNoResultsSelectSubject: 'Select a subject tree to view and author its skills.',
  skillNoSubjectSelected: 'No subject selected',

  // Subject picker
  skillSubjectPickerLabel: 'Subject Tree',
  skillSubjectPickerPlaceholder: '-- Select a subject --',
  skillSubjectPickerLoading: 'Loading subjects…',
  skillSubjectPickerError: 'Failed to load subjects.',

  // Concept filter
  skillConceptFilterPlaceholder: 'All Concepts',

  // Search
  skillSearchPlaceholder: 'Search skills…',

  // "New Skill" button
  skillNewBtn: 'New Skill',
  skillNewBtnAriaLabel: 'Create a new skill',

  // Table columns
  skillColName: 'Name',
  skillColThreshold: 'Mastery %',
  skillColTime: 'Time (min)',
  skillColActive: 'Active',

  // Skill form
  skillFormCreateTitle: 'New Skill',
  skillFormEditTitle: 'Edit Skill',
  skillFormCreateSubtitle: 'Add a new skill to the curriculum.',
  skillFormEditSubtitle: 'Update skill details.',
  skillFormNameLabel: 'Name',
  skillFormNamePlaceholder: 'e.g. Multiply Fractions',
  skillFormNameRequired: 'Name is required.',
  skillFormThresholdLabel: 'Mastery Threshold (%)',
  skillFormThresholdHint: 'Score percentage (0–100) required to mark this skill as mastered.',
  skillFormThresholdRequired: 'Mastery threshold is required.',
  skillFormThresholdRange: 'Must be a number between 0 and 100.',
  skillFormTimeLabel: 'Estimated Time (minutes)',
  skillFormTimeHint: 'Expected minutes to practice this skill.',
  skillFormTimeRequired: 'Estimated time is required.',
  skillFormTimeRange: 'Must be 0 or more.',
  skillFormConceptLabel: 'Concept',
  skillFormConceptPlaceholder: 'Select a concept…',
  skillFormConceptRequired: 'Concept is required.',
  skillFormConceptLoading: 'Loading concepts…',
  skillFormConceptError: 'Failed to load concepts.',
  skillFormCreateBtn: 'Create Skill',
  skillFormSaveBtn: 'Save Changes',
  skillFormCancel: 'Cancel',

  // Delete dialog
  skillDeleteTitle: 'Delete Skill',
  skillDeleteBody: 'This will permanently delete this skill. This action cannot be undone.',
  skillDeleteConfirm: 'Delete',
  skillDeleteCancel: 'Cancel',

  // Graph editor
  skillGraphTitle: 'Prerequisite Graph',
  skillGraphNoSubject: 'No subject selected',
  skillGraphNoSubjectBody: 'Select a subject tree above to view and edit the skill prerequisite graph.',
  skillGraphLoading: 'Loading skill graph…',
  skillGraphError: 'Failed to load the skill graph. Please try again.',
  skillGraphNodeCount: 'nodes',
  skillGraphEdgeCount: 'edges',
  skillGraphNodesEmpty: 'No nodes in this subject tree.',
  skillGraphRetry: 'Try again',

  // Node list
  skillGraphNodeListLabel: 'Nodes in subject tree',

  // Prerequisites
  skillGraphPrerequisitesHeading: 'Prerequisites',
  skillGraphPrerequisitesEmpty: 'No prerequisites — this skill is immediately accessible.',

  // Unlocks
  skillGraphUnlocksHeading: 'Unlocks',
  skillGraphUnlocksEmpty: 'This skill is not a prerequisite for any other skill.',

  // Add prerequisite
  skillGraphAddPrerequisiteHeading: 'Add Prerequisite',
  skillGraphPickerPlaceholder: 'Choose a prerequisite…',
  skillGraphPickerAllAdded: 'All nodes are already prerequisites.',
  skillGraphAddBtn: 'Add',
  skillGraphDeselectNode: 'Deselect node',
  skillGraphRemovePrerequisite: 'Remove as prerequisite',

  // Edge errors
  skillGraphErrCycle: 'This would create a cycle in the prerequisite graph. Choose a different node.',
  skillGraphErrCrossLanguage: 'Cannot connect nodes from different language trees.',
  skillGraphErrDuplicate: 'This prerequisite edge already exists.',
  skillGraphErrNodeNotFound: 'One of the nodes was not found. Refresh and try again.',
  skillGraphErrSubjectUnresolvable: 'Could not resolve the subject for this node.',
  skillGraphErrStrengthOutOfRange: 'Edge strength must be between 0.0 and 1.0.',
  skillGraphErrGeneric: 'Could not update the graph. Please try again.',
  skillGraphErrNetwork: 'Network error. Check your connection and try again.',

  // Edge aria-live announcements
  skillGraphEdgeAdded: '{name} added as a prerequisite.',
  skillGraphEdgeRemoved: '{name} removed from prerequisites.',

  // Skill detail panel
  skillDetailSection: 'Skill Details',
  skillDetailMastery: 'Mastery',
  skillDetailTime: 'Time',
  skillDetailEditLink: 'Edit this skill',

  // Skills page — additional a11y / i18n (Nits #2/#3)
  skillClearFilters: 'Clear filters',
  skillConceptFilterAriaLabel: 'Filter by concept',
  skillColActionsLabel: 'Actions',
  skillEditAriaLabel: 'Edit {name}',
  skillDeleteAriaLabel: 'Delete {name}',
  skillPrevPage: 'Previous page',
  skillNextPage: 'Next page',

  // SkillGraph — node-type words
  skillGraphNodeTypeSkill: 'Skill',
  skillGraphNodeTypeConcept: 'Concept',
  skillGraphNodeTypeReview: 'Review',
  skillGraphPrerequisitesAriaLabel: 'Prerequisites of {name}',
  skillGraphUnlocksAriaLabel: 'Skills unlocked by {name}',
  skillGraphRemoveEdgeAriaLabel: 'Remove prerequisite: {name}',

  // ── P7-12 Audit Log viewer (EN) ──────────────────────────────────────────────
  navAuditLog: 'Audit Log',
  pageTitleAudit: 'Audit Log',
  auditPageHeading: 'Audit Log',
  auditFilterAdminIdLabel: 'Admin user ID',
  auditFilterAdminIdPlaceholder: 'Admin ID',
  auditFilterActionTypeLabel: 'Action type',
  auditFilterActionTypePlaceholder: 'All actions',
  auditFilterTargetTypeLabel: 'Target type',
  auditFilterTargetTypePlaceholder: 'All targets',
  auditFilterDateFromLabel: 'From date',
  auditFilterDateToLabel: 'To date',
  auditClearFilters: 'Clear filters',
  auditDateRangeError: "End date must be on or after start date.",
  auditTableCaption: 'Admin audit log entries',
  auditColAdmin: 'Admin',
  auditColAction: 'Action',
  auditColTarget: 'Target',
  auditColWhen: 'When',
  auditColDetailsHeader: 'Details',
  auditResultCount: 'entries',
  auditLoadingLabel: 'Loading audit log…',
  auditEmptyHeading: 'No audit entries found',
  auditEmptyBodyFiltered: 'Try adjusting the filters.',
  auditEmptyBodyEmpty: 'No admin actions have been recorded yet.',
  auditListError: 'Failed to load audit log. Please try again.',
  auditRetry: 'Try again',
  auditPrevPage: 'Previous page',
  auditNextPage: 'Next page',
  auditExpandEntry: 'Expand details',
  auditCollapseEntry: 'Collapse details',
  auditDetailEventId: 'Event ID',
  auditDetailAdmin: 'Admin (actor)',
  auditDetailAction: 'Action',
  auditDetailTargetType: 'Target type',
  auditDetailTargetId: 'Target ID',
  auditDetailOccurredAt: 'Occurred at (UTC)',
  auditDetailCreatedAt: 'Record created at',
  auditDetailDetailsLabel: 'Details',
  auditDetailCopy: 'Copy',
  auditDetailCopied: 'Copied!',
  auditDetailNoDetails: '—',

  // ── P7-13 Gamification section (EN) ──────────────────────────────────────────
  navGamification: 'Gamification',
  gamificationHubTitle: 'Gamification',
  gamificationHubSubtitle: 'Manage badges, missions, timed events, and student overrides.',
  gamificationHubManage: 'Manage',
  gamificationStudentOverridesHeading: 'Student Overrides',
  gamificationStudentOverridesNotice:
    'League-tier override and streak-freeze grant are launched from a student\'s detail page. Navigate to Users, select a student account, then use the Gamification section in the Actions card.',
  gamEditBtn: 'Edit',
  gamActivateBtn: 'Activate',
  gamDeactivateBtn: 'Deactivate',
  gamExpireBtn: 'Expire',
  gamDialogCancelBtn: 'Cancel',
  gamBadgesPageTitle: 'Badge Catalog',
  gamBadgesNewBtn: 'New Badge',
  gamBadgesEmptyHeading: 'No badges defined',
  gamBadgesEmptyBody: 'Create the first badge definition to get started.',
  gamBadgeDeactivateTitle: 'Deactivate Badge',
  gamBadgeDeactivateNotice:
    'This badge will no longer be offered for new achievements. Students who have already earned it retain it — earned badges are never removed.',
  gamBadgeActivateTitle: 'Activate Badge',
  gamBadgeActivateNotice: 'This badge will be available for new achievements.',
  gamBadgeFormCreateTitle: 'New Badge',
  gamBadgeFormCreateSubtitle: 'Define a new badge for the catalog.',
  gamBadgeFormEditTitle: 'Edit Badge',
  gamBadgeFormEditSubtitle: 'Update badge details. Code cannot be changed.',
  gamBadgeFormCodeLabel: 'Code',
  gamBadgeFormCodePlaceholder: 'e.g. FIRST_LESSON',
  gamBadgeFormCodeHint: 'Code cannot be changed after creation.',
  gamBadgeFormNameLabel: 'Name',
  gamBadgeFormNamePlaceholder: 'e.g. First Steps',
  gamBadgeFormDescLabel: 'Description',
  gamBadgeFormIconKeyLabel: 'Icon Key',
  gamBadgeFormIconKeyPlaceholder: 'e.g. star, trophy, flame',
  gamBadgeFormIconKeyHint: 'Identifier used by the student app to render the badge icon.',
  gamBadgeFormRarityLabel: 'Rarity',
  gamBadgeFormRarityPlaceholder: 'Select rarity',
  gamBadgeFormTriggerLabel: 'Trigger',
  gamBadgeFormThresholdLabel: 'Threshold',
  gamBadgeFormThresholdRequired: 'Threshold is required for this trigger type.',
  gamBadgeFormRewardXpLabel: 'Reward XP',
  gamBadgeFormSortOrderLabel: 'Sort Order',
  gamBadgeFormSortOrderHint: 'Lower values appear first in the catalog.',
  gamBadgeFormCancelBtn: 'Cancel',
  gamBadgeFormCreateBtn: 'Create Badge',
  gamBadgeFormSaveBtn: 'Save Changes',
  gamMissionsPageTitle: 'Mission Catalog',
  gamMissionsNewBtn: 'New Mission',
  gamMissionsEmptyHeading: 'No missions defined',
  gamMissionsEmptyBody: 'Create the first mission definition to get started.',
  gamMissionDeactivateTitle: 'Deactivate Mission',
  gamMissionDeactivateNotice: 'This mission will no longer be assigned to students.',
  gamMissionActivateTitle: 'Activate Mission',
  gamMissionActivateNotice: 'This mission will be assigned to students again.',
  gamMissionFormCreateTitle: 'New Mission',
  gamMissionFormCreateSubtitle: 'Define a new mission for the catalog.',
  gamMissionFormEditTitle: 'Edit Mission',
  gamMissionFormEditSubtitle: 'Update mission details. Code cannot be changed.',
  gamMissionFormCodeLabel: 'Code',
  gamMissionFormIconKeyLabel: 'Icon Key',
  gamMissionFormTitleKeyLabel: 'Title Key (i18n)',
  gamMissionFormTitleKeyHint:
    "The i18n lookup key for the mission title — not the display text itself. E.g. 'mission.daily_lessons'",
  gamMissionFormCadenceLabel: 'Cadence',
  gamMissionFormTargetTypeLabel: 'Target Type',
  gamMissionFormTargetLabel: 'Target Count',
  gamMissionFormRewardXpLabel: 'Reward XP',
  gamMissionFormSortOrderLabel: 'Sort Order',
  gamMissionFormCancelBtn: 'Cancel',
  gamMissionFormCreateBtn: 'Create Mission',
  gamMissionFormSaveBtn: 'Save Changes',
  gamEventsPageTitle: 'Timed Events',
  gamEventsNewBtn: 'New Event',
  gamEventsEmptyHeading: 'No timed events',
  gamEventsEmptyBody: 'Create the first timed event to get started.',
  gamEventActivateTitle: 'Activate Event',
  gamEventActivateNotice:
    'This will immediately apply the XP multiplier for the event\'s active window.',
  gamEventExpireTitle: 'Expire Event',
  gamEventExpireNotice:
    'The event will be immediately marked as expired and stop applying the XP multiplier.',
  gamEventActivateConfirmBtn: 'Activate Event',
  gamEventExpireConfirmBtn: 'Expire Event',
  gamEventFormCreateTitle: 'New Timed Event',
  gamEventFormCreateSubtitle: 'Schedule a new XP multiplier event.',
  gamEventFormEditTitle: 'Edit Timed Event',
  gamEventFormEditSubtitle: 'Update event details. Code cannot be changed.',
  gamEventFormCodeLabel: 'Code',
  gamEventFormNameEnLabel: 'Event Name (English)',
  gamEventFormNameArLabel: 'Event Name (Arabic)',
  gamEventFormDescEnLabel: 'Description (English, optional)',
  gamEventFormDescArLabel: 'Description (Arabic, optional)',
  gamEventFormStartLabel: 'Start (UTC)',
  gamEventFormEndLabel: 'End (UTC)',
  gamEventFormUtcHint: 'Enter time in UTC. Times are stored and displayed in UTC.',
  gamEventFormEndBeforeStart: 'End time must be after start time.',
  gamEventFormMultiplierLabel: 'XP Multiplier',
  gamEventFormMultiplierHint: 'Range: 1.0 – 5.0. E.g. 2.0 doubles XP earned.',
  gamEventFormScopeLabel: 'Scope',
  gamEventFormScopeNotice:
    'Currently only All XP scope is active in the engine. Mission XP and League XP are available for future use.',
  gamEventFormCreateBtn: 'Create Event',
  gamEventFormSaveBtn: 'Save Changes',
  gamEventFormCancelBtn: 'Cancel',
  gamOverridesHeading: 'Gamification Overrides',
  gamLeagueTierBtn: 'Override League Tier',
  gamLeagueTierDialogTitle: 'Override League Tier',
  gamLeagueTierCurrentLabel: 'Current League Tier',
  gamLeagueTierUnknown: 'No data',
  gamLeagueTierCaveat:
    'Shown from weekly activity data — may differ from the stored tier.',
  gamLeagueTierNewLabel: 'New Tier',
  gamLeagueTierSelectPlaceholder: 'Select tier',
  gamLeagueTierAuditNotice:
    "This override is audited and applies to the student's current tier. XP and progress are not affected.",
  gamLeagueTierReasonLabel: 'Reason (required)',
  gamLeagueTierConfirmBtn: 'Override Tier',
  gamLeagueTierSuccessBanner: "{name}'s league tier has been overridden.",
  gamLeagueTierErr400SameTier: "This is already the student's current tier.",
  gamLeagueTierErr404: 'Student account not found.',
  gamLeagueTierErr422: 'Reason is required and must be under 500 characters.',
  gamLeagueTierErrNetwork: 'Something went wrong. Please try again.',
  gamFreezeFreezeBtn: 'Grant Streak Freeze',
  gamFreezeDialogTitle: 'Grant Streak Freeze',
  gamFreezeBalanceUnavailable:
    'Current freeze balance: not available (no read endpoint — see backend follow-up Q2).',
  gamFreezeCountLabel: 'Freeze Count',
  gamFreezeCountHint: 'Max 2 per grant',
  gamFreezeReasonLabel: 'Reason (required)',
  gamFreezeConfirmBtn: 'Grant Freeze',
  gamFreezeSuccessBanner: 'Streak freeze granted to {name}.',
  gamFreezeErrCount: 'Count must be between 1 and 2.',
  gamFreezeErr404: 'Student account not found.',
  gamFreezeErr422: 'Reason is required and must be under 500 characters.',
  gamFreezeErrNetwork: 'Something went wrong. Please try again.',

  // ── P7-13 additional keys ────────────────────────────────────────────────
  gamification: 'Gamification',
  gamCancelBtn: 'Cancel',
  gamRetry: 'Retry',

  gamBadgePageTitle: 'Badge Catalog',
  gamBadgeCreateBtn: 'New Badge',
  gamBadgeActive: 'Active',
  gamBadgeInactive: 'Inactive',
  gamBadgeLoading: 'Loading badge catalog…',
  gamBadgeFetchError: 'Failed to load badges.',
  gamBadgeEmpty: 'No badges defined. Create the first badge to get started.',
  gamBadgeCreateFirst: 'Create Badge',
  gamBadgeEditBtn: 'Edit',
  gamBadgeActivateBtn: 'Activate',
  gamBadgeDeactivateBtn: 'Deactivate',
  gamBadgeTableCaption: 'Badge catalog',
  gamBadgeColCode: 'Code',
  gamBadgeColName: 'Name',
  gamBadgeColRarity: 'Rarity',
  gamBadgeColTrigger: 'Trigger',
  gamBadgeColXp: 'XP',
  gamBadgeColStatus: 'Status',
  gamBadgeColActions: 'Actions',
  gamBadgeDeactivateSubtitle: 'Deactivate "{name}"?',
  gamBadgeDeactivateNote: 'This badge will no longer be awarded for new achievements.',
  gamBadgeDeactivateConfirmBtn: 'Deactivate',
  gamBadgeActivateSubtitle: 'Activate "{name}"?',
  gamBadgeActivateNote: 'This badge will be awarded again for qualifying achievements.',
  gamBadgeActivateConfirmBtn: 'Activate',
  gamBadgeActivatedBanner: 'Badge activated.',
  gamBadgeDeactivatedBanner: 'Badge deactivated.',
  gamBadgeNotFoundError: 'Badge not found.',
  gamBadgeActionError: 'Something went wrong. Please try again.',

  gamMissionPageTitle: 'Mission Catalog',
  gamMissionCreateBtn: 'New Mission',
  gamMissionActive: 'Active',
  gamMissionInactive: 'Inactive',
  gamMissionLoading: 'Loading mission catalog…',
  gamMissionFetchError: 'Failed to load missions.',
  gamMissionEmpty: 'No missions defined. Create the first mission to get started.',
  gamMissionCreateFirst: 'Create Mission',
  gamMissionEditBtn: 'Edit',
  gamMissionActivateBtn: 'Activate',
  gamMissionDeactivateBtn: 'Deactivate',
  gamMissionTableCaption: 'Mission catalog',
  gamMissionColTitle: 'Title',
  gamMissionColType: 'Type',
  gamMissionColTargetType: 'Target Type',
  gamMissionColTargetCount: 'Target',
  gamMissionColXp: 'XP',
  gamMissionColStatus: 'Status',
  gamMissionColActions: 'Actions',
  gamMissionDeactivateSubtitle: 'Deactivate "{name}"?',
  gamMissionDeactivateNote: 'This mission will no longer be assigned to students.',
  gamMissionDeactivateConfirmBtn: 'Deactivate',
  gamMissionActivateSubtitle: 'Activate "{name}"?',
  gamMissionActivateNote: 'This mission will be assigned to students again.',
  gamMissionActivateConfirmBtn: 'Activate',
  gamMissionActivatedBanner: 'Mission activated.',
  gamMissionDeactivatedBanner: 'Mission deactivated.',
  gamMissionNotFoundError: 'Mission not found.',
  gamMissionActionError: 'Something went wrong. Please try again.',
  gamMissionFormTitleLabel: 'Title',
  gamMissionFormDescLabel: 'Description (optional)',
  gamMissionFormTypeLabel: 'Mission Type',
  gamMissionFormTypePlaceholder: 'Select type',
  gamMissionFormTargetCountLabel: 'Target Count',
  gamMissionFormSortOrderHint: 'Lower values appear first.',

  gamEventPageTitle: 'Timed Events',
  gamEventCreateBtn: 'New Event',
  gamEventLoading: 'Loading timed events…',
  gamEventFetchError: 'Failed to load timed events.',
  gamEventEmpty: 'No timed events. Create one to schedule an XP multiplier.',
  gamEventCreateFirst: 'Create Event',
  gamEventEditBtn: 'Edit',
  gamEventActivateBtn: 'Activate',
  gamEventExpireBtn: 'Expire',
  gamEventTableCaption: 'Timed events',
  gamEventColName: 'Name',
  gamEventColScope: 'Scope',
  gamEventColMultiplier: 'Multiplier',
  gamEventColStart: 'Start (UTC)',
  gamEventColEnd: 'End (UTC)',
  gamEventColStatus: 'Status',
  gamEventColActions: 'Actions',
  gamEventActivateSubtitle: 'Activate "{name}"?',
  gamEventActivateNote: 'The event will become active and XP multiplier will apply.',
  gamEventExpireSubtitle: 'Expire "{name}" now?',
  gamEventExpireNote: 'The event will be immediately expired and XP multiplier will stop.',
  gamEventActivatedBanner: 'Event activated.',
  gamEventExpiredBanner: 'Event expired.',
  gamEventNotFoundError: 'Event not found.',
  gamEventActionError: 'Something went wrong. Please try again.',
  gamEventFormDescGapNotice:
    'Description fields cannot be pre-filled (not in list DTO). Re-enter if you want to update them.',
  gamEventFormDescGapPlaceholder: 'Re-enter if changing…',
  gamEventFormDateHint: 'Dates are entered in your local time and saved as UTC.',

  gamLeagueTierDialogBody: "Override {name}'s league tier. This is audited.",
  gamLeagueTierCancelBtn: 'Cancel',
  gamLeagueTierError400: "This is already the student's current tier.",
  gamLeagueTierError404: 'Student account not found.',
  gamLeagueTierError422: 'Validation failed. Reason is required.',
  gamLeagueTierErrorNetwork: 'Something went wrong. Please try again.',
  gamLeagueTierReasonPlaceholder: 'Reason for overriding league tier…',

  gamFreezeDialogBody: "Grant streak freeze tokens to {name}.",
  gamFreezeCancelBtn: 'Cancel',
  gamFreezeError400: 'Invalid request. Please check the count.',
  gamFreezeError404: 'Student account not found.',
  gamFreezeError422: 'Validation failed. Reason is required.',
  gamFreezeErrorNetwork: 'Something went wrong. Please try again.',
  gamFreezeReasonPlaceholder: 'Reason for granting streak freeze…',

  // ── P7-09 Moderation Queue (EN) ───────────────────────────────────────────
  navModeration: 'Moderation',
  modPageTitleQueue: 'Moderation Queue',
  modPageTitleDetail: 'Moderation Item',
  modListHeading: 'Moderation Queue',
  modResultCount: 'items',
  modSearchPlaceholder: 'Search by content reference…',
  modFilterAllStatuses: 'All Statuses',
  modFilterAllSources: 'All Sources',
  modFilterAllSubjects: 'All Subjects',
  modFilterAllGrades: 'All Grades',
  modClearFilters: 'Clear filters',
  modListLoadingLabel: 'Loading moderation queue…',
  modListError: 'Failed to load the moderation queue. Please try again.',
  modListRetry: 'Try again',
  modTableCaption: 'Moderation queue items',
  modPrevPage: 'Previous page',
  modNextPage: 'Next page',
  modSubjectMath: 'Math',
  modSubjectScience: 'Science',
  modSubjectArabic: 'Arabic',
  modSubjectEnglish: 'English',
  modEmptyNoFilters: 'No items in the queue',
  modEmptyNoFiltersBody: 'The moderation queue is empty.',
  modEmptyFiltered: 'No matching items',
  modEmptyFilteredBody: 'No items match the current filters.',
  modColSource: 'Source',
  modColContentRef: 'Content Ref',
  modColSubjectGrade: 'Subject / Grade',
  modColTaskKind: 'Task Kind',
  modColStatus: 'Status',
  modColDetected: 'Detected',
  modViewDetail: 'View item',
  modStatusPending: 'Pending',
  modStatusApproved: 'Approved',
  modStatusRejected: 'Rejected',
  modStatusFlagged: 'Flagged',
  modSourceAiOutput: 'AI Output',
  modSourceCurriculumUpload: 'Curriculum Upload',
  modDetailLoadingLabel: 'Loading moderation item…',
  modDetailError: 'Failed to load this moderation item.',
  modNotFoundHeading: 'Item not found',
  modNotFoundBody: "This moderation item doesn't exist or was removed.",
  modBackToQueue: 'Back to Queue',
  modSectionDetails: 'Item Details',
  modSectionReviewHistory: 'Review History',
  modFieldStudentId: 'Student ID',
  modFieldDetectedAt: 'Detected At',
  modFieldItemId: 'ID',
  modReviewedBy: 'Reviewed by',
  modReviewedAt: 'Reviewed at',
  modReviewReason: 'Reason',
  modTerminalNotice: 'This item has already been resolved and cannot be reviewed again.',
  modVerdictSection: 'Automated Safety Signal',
  modVerdictPrivacyNote: 'v1: no raw content stored — this signal contains only automated check names and reason codes.',
  modVerdictFailedChecks: 'Failed Checks',
  modVerdictReasonCodes: 'Reason Codes',
  modVerdictActionTaken: 'Action Taken',
  modVerdictModelId: 'Model',
  modVerdictUnavailable: 'Safety signal unavailable or not yet populated.',
  modReviewActionsHeading: 'Review Actions',
  modReviewApprove: 'Approve',
  modReviewReject: 'Reject',
  modReviewFlag: 'Flag for Escalation',
  modAlreadyFlagged: 'Flag option is unavailable — item is already flagged.',
  modDlgCancel: 'Cancel',
  modDlgApproveTitle: 'Approve Item',
  modDlgApproveSubtitle: 'Approve this flagged content item?',
  modDlgApproveReasonLabel: 'Note (optional)',
  modDlgApproveConfirm: 'Approve',
  modDlgRejectTitle: 'Reject Item',
  modDlgRejectSubtitle: 'Reject this flagged content item.',
  modDlgRejectReasonLabel: 'Reason for rejection (required)',
  modDlgRejectConfirm: 'Reject',
  modDlgFlagTitle: 'Flag for Escalation',
  modDlgFlagSubtitle: 'Flag this item for additional scrutiny.',
  modDlgFlagReasonLabel: 'Escalation note (optional)',
  modDlgFlagConfirm: 'Flag',
  modErrAlreadyTerminal: 'This item has already been resolved and cannot be reviewed again.',
  modErrAlreadyFlagged: 'This item is already flagged — use Approve or Reject.',
  modErr404: 'This item was not found.',
  modErrValidation: 'The reason is required and must be under 2000 characters.',
  modErrNetwork: 'Something went wrong. Please try again.',
  modReviewSuccess: 'Item reviewed — status updated.',
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

  // ── P7-02 Lessons & Content Blocks (AR) ──────────────────────────────────────
  lessonPageTitle: 'الدروس',
  lessonContentEditorTitle: 'محرر المحتوى',
  lessonListHeading: 'الدروس',
  lessonResultCount: 'درس',
  lessonListLoadingLabel: 'جارٍ تحميل الدروس…',
  lessonListError: 'تعذَّر تحميل الدروس. يرجى المحاولة مرة أخرى.',
  lessonTableCaption: 'الدروس في هذه الوحدة',
  lessonNoResults: 'لا توجد دروس بعد',
  lessonNoResultsHint: 'أضف الدرس الأول لهذه الوحدة.',
  lessonViewDetail: 'عرض المحتوى',
  lessonNewBtn: 'درس جديد',
  lessonPrevPage: 'الصفحة السابقة',
  lessonNextPage: 'الصفحة التالية',
  lessonColOrder: 'الترتيب',
  lessonColTitle: 'العنوان',
  lessonColDifficulty: 'الصعوبة',
  lessonColDuration: 'المدة',
  lessonColLock: 'القفل',
  lessonColActive: 'الحالة',
  lessonDifficultyEasy: 'سهل',
  lessonDifficultyMedium: 'متوسط',
  lessonDifficultyHard: 'صعب',
  lessonLocked: 'مقفل',
  lessonUnlocked: 'مفتوح',
  lessonFormCreateTitle: 'درس جديد',
  lessonFormCreateSubtitle: 'أضف درساً جديداً لهذه الوحدة.',
  lessonFormEditTitle: 'تعديل الدرس',
  lessonFormEditSubtitle: 'تحديث تفاصيل الدرس.',
  lessonFormNameLabel: 'اسم الدرس',
  lessonFormNamePlaceholder: 'مثال: مقدمة في الكسور',
  lessonFormDifficultyLabel: 'الصعوبة',
  lessonFormDifficultyPlaceholder: 'اختر الصعوبة',
  lessonFormMinutesLabel: 'المدة التقديرية',
  lessonFormMinutesHint: 'بالدقائق. أدخل ٠ إذا لم تنطبق المدة.',
  lessonFormLockedLabel: 'مقفل',
  lessonFormLockedOnHint: 'يتطلب الدرس إكمال سابق لفتحه',
  lessonFormLockedOffHint: 'الدرس متاح فوراً',
  lessonFormActiveLabel: 'نشط',
  lessonFormActiveOnHint: 'الدرس مرئي للطلاب',
  lessonFormActiveOffHint: 'الدرس مخفي عن الطلاب',
  lessonFormInheritedLangPrefix: 'لغة المحتوى:',
  lessonFormCancelBtn: 'إلغاء',
  lessonFormCreateBtn: 'إنشاء الدرس',
  lessonFormSaveBtn: 'حفظ التغييرات',
  lessonFormErrNameRequired: 'الاسم مطلوب',
  lessonFormErrDifficultyRequired: 'الصعوبة مطلوبة',
  lessonFormErrMinutesInvalid: 'يجب أن تكون المدة ٠ دقائق أو أكثر',
  lessonDetailEditBtn: 'تعديل',
  lessonDetailActivateBtn: 'تفعيل',
  lessonDetailDeactivateBtn: 'إلغاء التفعيل',
  lessonDetailDeleteBtn: 'حذف',
  lessonActivateSuccess: 'تم تفعيل الدرس.',
  lessonDeactivateSuccess: 'تم إلغاء تفعيل الدرس.',
  lessonNotFound: 'الدرس غير موجود',
  lessonNotFoundBody: 'هذا الدرس غير موجود أو تم حذفه.',
  lessonInheritedLangLabel: 'اللغة:',
  lessonInheritedLangAr: 'العربية',
  lessonInheritedLangEn: 'الإنجليزية',
  lessonReorderSaveBtn: 'حفظ الترتيب',
  lessonReorderMoveUp: 'تحريك {name} للأعلى',
  lessonReorderMoveDown: 'تحريك {name} للأسفل',
  lessonReorderPosition: '{name} نُقل إلى الموضع {N} من {total}',
  lessonDeleteTitle: 'حذف الدرس',
  lessonDeleteSubtitle: 'سيُحذف "{name}".',
  lessonDeleteCascadeHeading: 'ستُحذف جميع كتل المحتوى',
  lessonDeleteCascadeBody: 'سيؤدي حذف هذا الدرس إلى حذف جميع كتل محتواه ناعمياً. لن يكون الدرس مرئياً للطلاب بعد الآن. لا يمكن استعادة كتل المحتوى بشكل فردي من لوحة التحكم في هذا الإصدار.',
  lessonDeleteConfirmBtn: 'حذف الدرس',
  lessonDeleteCancelBtn: 'إلغاء',
  lessonDeleteSuccess: 'تم حذف الدرس.',
  blockEditorHeading: 'كتل المحتوى',
  blockEditorCountLabel: 'كتلة',
  blockEditorAddBtn: 'إضافة كتلة',
  blockEditorLoadingLabel: 'جارٍ تحميل كتل المحتوى…',
  blockEditorListError: 'تعذَّر تحميل كتل المحتوى. يرجى المحاولة مرة أخرى.',
  blockListError: 'تعذَّر تحميل كتل المحتوى. يرجى المحاولة مرة أخرى.',
  blockEditorEmpty: 'لا يوجد محتوى بعد',
  blockEditorEmptyHint: 'أضف الكتلة الأولى لبناء محتوى هذا الدرس.',
  blockEditorSaveOrderBtn: 'حفظ ترتيب الكتل',
  blockPickerHeading: 'اختر نوع الكتلة',
  blockPickerText: 'كتلة نصية',
  blockPickerImage: 'صورة',
  blockPickerVideo: 'فيديو',
  blockPickerCallout: 'تنبيه',
  blockTypeText: 'نص',
  blockTypeImage: 'صورة',
  blockTypeVideo: 'فيديو',
  blockTypeCallout: 'تنبيه',
  blockCardNumberPrefix: 'الكتلة',
  blockCardInactiveNotice: 'هذه الكتلة غير نشطة (للقراءة فقط — لا يوجد نقطة نهاية للتبديل).',
  blockCardEditBtn: 'تعديل الكتلة {N}',
  blockCardDeleteBtn: 'حذف الكتلة {N}',
  blockCardMoveUpBtn: 'تحريك كتلة {type} للأعلى',
  blockCardMoveDownBtn: 'تحريك كتلة {type} للأسفل',
  blockCardExpand: '▼ توسيع',
  blockCardCollapse: '▲ طي',
  blockFormAddTitle: 'إضافة كتلة',
  blockFormEditTitle: 'تعديل الكتلة',
  blockFormCancelBtn: 'إلغاء',
  blockFormAddConfirm: 'إضافة الكتلة',
  blockFormSaveConfirm: 'حفظ الكتلة',
  blockFormTypeChangeWarning: 'تغيير نوع الكتلة سيؤدي إلى حذف حقول المحتوى الحالية.',
  blockFormPayloadTooLarge: 'المحتوى كبير جداً (الحد الأقصى ٦٥,٥٣٦ حرف)',
  blockFormMarkdownLabel: 'محتوى Markdown',
  blockFormMarkdownPlaceholder: 'أدخل نص Markdown…',
  blockFormMarkdownRequired: 'المحتوى مطلوب',
  blockFormImageUrlLabel: 'رابط الصورة',
  blockFormImageAltLabel: 'النص البديل (اختياري)',
  blockFormImageAltPlaceholder: 'صف الصورة لقراء الشاشة…',
  blockFormImageAltHint: 'يُوصى به لسهولة الوصول.',
  blockFormVideoUrlLabel: 'رابط الفيديو',
  blockFormVideoCaptionLabel: 'التسمية التوضيحية (اختياري)',
  blockFormVideoCaptionPlaceholder: 'تسمية توضيحية اختيارية تظهر أسفل الفيديو…',
  blockFormCalloutVariantLabel: 'نوع التنبيه',
  blockFormCalloutVariantInfo: 'معلومة',
  blockFormCalloutVariantWarning: 'تحذير',
  blockFormCalloutVariantTip: 'نصيحة',
  blockFormCalloutMarkdownLabel: 'محتوى التنبيه',
  blockFormUrlRequired: 'الرابط مطلوب',
  blockFormUrlHttpsRequired: 'يجب أن يبدأ الرابط بـ https://',
  blockFormUrlPrivateNotAllowed: 'العناوين المحلية/الخاصة غير مسموح بها',
  blockFormVariantRequired: 'نوع التنبيه مطلوب',
  blockFormUrlHint: 'يجب أن يكون رابطاً مطلقاً يبدأ بـ https://. عناوين IP الخاصة و localhost غير مسموح بها.',
  blockPreviewImageAlt: 'صورة الكتلة',
  blockPreviewParseError: 'تعذّر تحليل محتوى الكتلة.',
  blockPreviewOpenLink: 'فتح ↗',
  blockPreviewCalloutInfo: 'معلومة',
  blockPreviewCalloutWarning: 'تحذير',
  blockPreviewCalloutTip: 'نصيحة',
  blockPreviewCannotPreview: 'لا يمكن المعاينة',
  blockDeleteTitle: 'حذف الكتلة',
  blockDeleteSubtitle: 'ستُحذف هذه الكتلة من الدرس.',
  blockDeleteBody: 'سيُحذف هذه الكتلة ناعمياً وتُخفى عن الطلاب. هذا قابل للتراجع في الخلفية لكن لا توجد واجهة استعادة في هذا الإصدار.',
  blockDeleteConfirmBtn: 'حذف الكتلة',

  // ── P7-04 Questions surface (AR) ─────────────────────────────────────────────

  // Page
  questionPageTitle: 'الأسئلة',
  questionBreadcrumbLabel: 'الأسئلة',
  questionLessonContextLabel: 'الدرس',
  questionLessonIdLabel: 'المعرّف {id}',

  // List page
  questionsLoadingLabel: 'جارٍ تحميل الأسئلة…',
  questionsListError: 'تعذَّر تحميل الأسئلة. حاول مرة أخرى.',
  questionsEmpty: 'لا توجد أسئلة بعد',
  questionsEmptyHint: 'أضف السؤال الأول إلى هذا الدرس.',
  questionsRetry: 'حاول مرة أخرى',
  questionsResultCount: 'سؤال',
  questionsNewQuestion: 'سؤال جديد',
  questionsSaveOrder: 'حفظ الترتيب',
  questionsOrderSaved: 'تم حفظ الترتيب.',
  questionsOrderError: 'فشل حفظ الترتيب.',

  // Table columns
  questionsColOrder: 'الترتيب',
  questionsColQuestion: 'السؤال',
  questionsColType: 'النوع',
  questionsColDifficulty: 'الصعوبة',
  questionsColActive: 'الحالة',

  // Type badge labels
  questionTypeMcq: 'اختيار متعدد',
  questionTypeTrueFalse: 'صح/خطأ',
  questionTypeMatching: 'مطابقة',
  questionTypeFillInBlank: 'أكمل الفراغ',

  // Difficulty badge labels
  difficultyEasy: 'سهل',
  difficultyMedium: 'متوسط',
  difficultyHard: 'صعب',

  // Row actions
  questionEditAriaLabel: 'تعديل السؤال {N}',
  questionDeleteAriaLabel: 'إزالة السؤال {N}',
  questionActivateAriaLabel: 'إظهار السؤال للطلاب',
  questionDeactivateAriaLabel: 'إخفاء السؤال عن الطلاب',
  questionMoveUpAriaLabel: 'تحريك السؤال للأعلى',
  questionMoveDownAriaLabel: 'تحريك السؤال للأسفل',

  // Reorder aria-live
  questionMovedAnnouncement: 'السؤال في الموضع {N} من {total}.',

  // Question editor
  questionEditorCreateTitle: 'سؤال جديد',
  questionEditorCreateSubtitle: 'أضف سؤالاً إلى هذا الدرس.',
  questionEditorEditTitle: 'تعديل السؤال',
  questionEditorEditSubtitle: 'تحديث محتوى السؤال.',
  questionEditorCloseAriaLabel: 'إغلاق المحرر',
  questionEditorCancelBtn: 'إلغاء',
  questionEditorSaveBtn: 'حفظ السؤال',
  questionEditorSaveChangesBtn: 'حفظ التغييرات',

  // Shared fields
  questionFieldTypeLabel: 'نوع السؤال',
  questionFieldTypeLockedHint: 'لا يمكن تغيير نوع السؤال بعد إنشائه.',
  questionFieldTypePlaceholder: 'اختر النوع',
  questionFieldTextLabel: 'نص السؤال',
  questionFieldTextPlaceholder: 'أدخل نص السؤال…',
  questionFieldDiffLabel: 'الصعوبة',
  questionFieldDiffPlaceholder: 'اختر الصعوبة',

  // Lifecycle section
  questionLifecycleSectionLabel: 'النشر',

  // MCQ sub-form
  questionMcqOptionsLegend: 'الخيارات',
  questionMcqAddOption: 'إضافة خيار',
  questionMcqOptionPlaceholder: 'الخيار {N}',
  questionMcqRemoveAriaLabel: 'إزالة الخيار {N}',
  questionMcqCorrectSummary: 'الإجابة الصحيحة:',
  questionMcqCorrectRadioAriaLabel: 'اجعل الخيار {N} صحيحاً',

  // Validation — MCQ
  questionErrMcqMinOptions: 'أضف خيارَين على الأقل.',
  questionErrMcqEmptyOptions: 'يجب أن تحتوي كل الخيارات على نص.',
  questionErrMcqNoCorrect: 'اختر الإجابة الصحيحة.',

  // TrueFalse sub-form
  questionTfCorrectLabel: 'الإجابة الصحيحة',
  questionTfTrueLabel: 'صح',
  questionTfFalseLabel: 'خطأ',
  questionErrTfNoChoice: 'اختر صح أو خطأ.',

  // FillInBlank sub-form
  questionFibCorrectLabel: 'الإجابة الصحيحة',
  questionFibPlaceholder: 'أدخل الإجابة المقبولة…',
  questionFibHint: 'يجب أن تتطابق إجابة الطالب المكتوبة مع هذا النص تماماً (التحقق غير حساس لحالة الأحرف).',
  questionErrFibEmpty: 'الإجابة الصحيحة مطلوبة.',

  // Matching sub-form
  questionMatchConfigLegend: 'إعداد المطابقة',
  questionMatchLeftHeader: 'العناصر اليسارية',
  questionMatchRightHeader: 'العناصر اليمينية',
  questionMatchRightHint: 'رتِّب هذه العناصر بشكل عشوائي — لا تضع التطابق الصحيح في الصف نفسه.',
  questionMatchAddLeft: 'إضافة عنصر أيسر',
  questionMatchAddRight: 'إضافة عنصر أيمن',
  questionMatchPairsHeader: 'التطابقات الصحيحة',
  questionMatchPairsHint: 'لكل عنصر أيسر، اختر العنصر الأيمن المطابق له.',
  questionMatchLeftPlaceholder: 'عنصر أيسر {N}',
  questionMatchRightPlaceholder: 'عنصر أيمن {N}',
  questionMatchSelectPlaceholder: 'اختر التطابق',
  questionMatchRemoveLeftAriaLabel: 'إزالة العنصر الأيسر {N}',
  questionMatchRemoveRightAriaLabel: 'إزالة العنصر الأيمن {N}',
  questionMatchPairSelectAriaLabel: 'التطابق لـ: {text}',

  // Validation — Matching
  questionErrMatchMinLeft: 'أضف عنصراً أيسر واحداً على الأقل.',
  questionErrMatchMinRight: 'أضف عنصراً أيمن واحداً على الأقل.',
  questionErrMatchEqualCount: 'يجب أن يتطابق عدد العناصر الأيسرة واليمينية.',
  questionErrMatchEmptyLeft: 'يجب أن تحتوي جميع العناصر الأيسرة على نص.',
  questionErrMatchEmptyRight: 'يجب أن تحتوي جميع العناصر اليمينية على نص.',
  questionErrMatchAllPaired: 'يجب ربط جميع الأزواج.',
  questionErrMatchDuplicatePair: 'لا يمكن استخدام كل عنصر أيمن أكثر من مرة.',

  // Matching parse error
  questionMatchParseError: 'فشل تحميل بيانات السؤال — تنسيق غير صالح. احذف وأعد الإنشاء.',

  // Shared validation
  questionErrTypeRequired: 'نوع السؤال مطلوب.',
  questionErrTextRequired: 'نص السؤال مطلوب.',
  questionErrTextTooLong: 'يجب ألا يتجاوز النص 4096 حرفاً.',
  questionErrDiffRequired: 'الصعوبة مطلوبة.',

  // Delete dialog
  questionDeleteTitle: 'إزالة السؤال',
  questionDeleteSubtitle: 'سيُخفى السؤال من هذا الدرس.',
  questionDeleteBody: 'هذا حذف مبدئي — سيُخفى السؤال عن الطلاب لكن يمكن استعادته. يُحفظ تقدم الطلاب على هذا السؤال.',
  questionDeleteConfirm: 'إزالة السؤال',
  questionDeleteCancel: 'إلغاء',
  questionDeleteSuccess: 'تم إزالة السؤال.',

  // Deactivate dialog
  questionDeactivateTitle: 'إخفاء السؤال',
  questionDeactivateSubtitle: 'سيُخفى هذا السؤال عن الطلاب.',
  questionDeactivateBody: 'الأسئلة المخفية لا تظهر للطلاب لكن تبقى مرئية للمسؤولين. يمكن إعادة تفعيل السؤال في أي وقت.',
  questionDeactivateConfirm: 'إخفاء عن الطلاب',
  questionDeactivateSuccess: 'تم إخفاء السؤال عن الطلاب.',

  // Activate dialog
  questionActivateTitle: 'إظهار السؤال',
  questionActivateSubtitle: 'سيظهر هذا السؤال للطلاب مجدداً.',
  questionActivateBody: 'سيتمكن الطلاب من رؤية هذا السؤال والإجابة عليه بعد إظهاره.',
  questionActivateConfirm: 'إظهار للطلاب',
  questionActivateSuccess: 'تم إظهار السؤال للطلاب.',

  // ── P7-03 Skills & Graph (AR) ─────────────────────────────────────────────────

  // Page
  skillPageTitle: 'المهارات',
  skillListHeading: 'المهارات',
  skillResultCount: 'مهارة',
  skillListError: 'فشل تحميل المهارات. حاول مرة أخرى.',
  skillTableCaption: 'قائمة المهارات',
  skillViewGraph: 'عرض الشجرة',
  skillNoResults: 'لم يُعثر على مهارات',
  skillNoResultsHint: 'جرِّب تعديل تصفية المفهوم أو مصطلح البحث.',
  skillNoResultsSelectSubject: 'اختر شجرة مادة لعرض مهاراتها وتأليفها.',
  skillNoSubjectSelected: 'لم يُختَر موضوع',

  // Subject picker
  skillSubjectPickerLabel: 'شجرة المادة',
  skillSubjectPickerPlaceholder: '-- اختر مادة --',
  skillSubjectPickerLoading: 'جارٍ تحميل المواد…',
  skillSubjectPickerError: 'فشل تحميل المواد.',

  // Concept filter
  skillConceptFilterPlaceholder: 'كل المفاهيم',

  // Search
  skillSearchPlaceholder: 'ابحث عن المهارات…',

  // "New Skill" button
  skillNewBtn: 'مهارة جديدة',
  skillNewBtnAriaLabel: 'إنشاء مهارة جديدة',

  // Table columns
  skillColName: 'الاسم',
  skillColThreshold: 'نسبة الإتقان',
  skillColTime: 'الوقت (دق)',
  skillColActive: 'الحالة',

  // Skill form
  skillFormCreateTitle: 'مهارة جديدة',
  skillFormEditTitle: 'تعديل المهارة',
  skillFormCreateSubtitle: 'أضف مهارة جديدة إلى المناهج.',
  skillFormEditSubtitle: 'تحديث تفاصيل المهارة.',
  skillFormNameLabel: 'اسم المهارة',
  skillFormNamePlaceholder: 'مثال: ضرب الكسور',
  skillFormNameRequired: 'الاسم مطلوب.',
  skillFormThresholdLabel: 'نسبة الإتقان (%)',
  skillFormThresholdHint: 'نسبة الدرجة (٠–١٠٠) المطلوبة لاعتبار المهارة متقنة.',
  skillFormThresholdRequired: 'نسبة الإتقان مطلوبة.',
  skillFormThresholdRange: 'يجب أن يكون رقمًا بين ٠ و١٠٠.',
  skillFormTimeLabel: 'الوقت التقديري (دقائق)',
  skillFormTimeHint: 'الدقائق المتوقعة للتدرب على هذه المهارة.',
  skillFormTimeRequired: 'الوقت التقديري مطلوب.',
  skillFormTimeRange: 'يجب أن يكون ٠ أو أكثر.',
  skillFormConceptLabel: 'المفهوم',
  skillFormConceptPlaceholder: 'اختر مفهومًا…',
  skillFormConceptRequired: 'المفهوم مطلوب.',
  skillFormConceptLoading: 'جارٍ تحميل المفاهيم…',
  skillFormConceptError: 'فشل تحميل المفاهيم.',
  skillFormCreateBtn: 'إنشاء المهارة',
  skillFormSaveBtn: 'حفظ التغييرات',
  skillFormCancel: 'إلغاء',

  // Delete dialog
  skillDeleteTitle: 'حذف المهارة',
  skillDeleteBody: 'سيؤدي هذا إلى حذف هذه المهارة بشكل دائم. لا يمكن التراجع عن هذا الإجراء.',
  skillDeleteConfirm: 'حذف',
  skillDeleteCancel: 'إلغاء',

  // Graph editor
  skillGraphTitle: 'شجرة المتطلبات السابقة',
  skillGraphNoSubject: 'لم يُختَر موضوع',
  skillGraphNoSubjectBody: 'اختر شجرة مادة أعلاه لعرض المتطلبات السابقة وتعديلها.',
  skillGraphLoading: 'جارٍ تحميل شجرة المهارات…',
  skillGraphError: 'فشل تحميل شجرة المهارات. حاول مرة أخرى.',
  skillGraphNodeCount: 'عقدة',
  skillGraphEdgeCount: 'حافة',
  skillGraphNodesEmpty: 'لا توجد عقد في هذه الشجرة.',
  skillGraphRetry: 'حاول مرة أخرى',

  // Node list
  skillGraphNodeListLabel: 'عقد الشجرة',

  // Prerequisites
  skillGraphPrerequisitesHeading: 'المتطلبات السابقة',
  skillGraphPrerequisitesEmpty: 'لا توجد متطلبات سابقة — هذه المهارة متاحة مباشرةً.',

  // Unlocks
  skillGraphUnlocksHeading: 'تفتح',
  skillGraphUnlocksEmpty: 'هذه المهارة ليست متطلبًا سابقًا لأي مهارة أخرى.',

  // Add prerequisite
  skillGraphAddPrerequisiteHeading: 'إضافة متطلب سابق',
  skillGraphPickerPlaceholder: 'اختر متطلبًا سابقًا…',
  skillGraphPickerAllAdded: 'جميع العقد متطلبات سابقة بالفعل.',
  skillGraphAddBtn: 'إضافة',
  skillGraphDeselectNode: 'إلغاء تحديد العقدة',
  skillGraphRemovePrerequisite: 'إزالة من المتطلبات السابقة',

  // Edge errors
  skillGraphErrCycle: 'سيؤدي هذا إلى إنشاء حلقة في شجرة المتطلبات. اختر عقدة مختلفة.',
  skillGraphErrCrossLanguage: 'لا يمكن ربط عقد من أشجار لغات مختلفة.',
  skillGraphErrDuplicate: 'هذا المتطلب السابق موجود بالفعل.',
  skillGraphErrNodeNotFound: 'لم يُعثر على إحدى العقد. حدِّث الصفحة وحاول مجددًا.',
  skillGraphErrSubjectUnresolvable: 'تعذَّر تحديد المادة لهذه العقدة.',
  skillGraphErrStrengthOutOfRange: 'يجب أن تكون قوة الحافة بين ٠٫٠ و١٫٠.',
  skillGraphErrGeneric: 'تعذَّر تحديث الشجرة. حاول مرة أخرى.',
  skillGraphErrNetwork: 'خطأ في الشبكة. تحقق من الاتصال وحاول مجددًا.',

  // Edge aria-live announcements
  skillGraphEdgeAdded: 'تمت إضافة {name} متطلبًا سابقًا.',
  skillGraphEdgeRemoved: 'تمت إزالة {name} من المتطلبات السابقة.',

  // Skill detail panel
  skillDetailSection: 'تفاصيل المهارة',
  skillDetailMastery: 'الإتقان',
  skillDetailTime: 'الوقت',
  skillDetailEditLink: 'تعديل هذه المهارة',

  // Skills page — additional a11y / i18n (Nits #2/#3)
  skillClearFilters: 'مسح التصفية',
  skillConceptFilterAriaLabel: 'تصفية حسب المفهوم',
  skillColActionsLabel: 'الإجراءات',
  skillEditAriaLabel: 'تعديل {name}',
  skillDeleteAriaLabel: 'حذف {name}',
  skillPrevPage: 'الصفحة السابقة',
  skillNextPage: 'الصفحة التالية',

  // SkillGraph — node-type words
  skillGraphNodeTypeSkill: 'مهارة',
  skillGraphNodeTypeConcept: 'مفهوم',
  skillGraphNodeTypeReview: 'مراجعة',
  skillGraphPrerequisitesAriaLabel: 'المتطلبات السابقة لـ {name}',
  skillGraphUnlocksAriaLabel: 'المهارات التي تفتحها {name}',
  skillGraphRemoveEdgeAriaLabel: 'إزالة المتطلب السابق: {name}',

  // ── P7-12 Audit Log viewer (AR) ──────────────────────────────────────────────
  navAuditLog: 'سجل التدقيق',
  pageTitleAudit: 'سجل التدقيق',
  auditPageHeading: 'سجل التدقيق',
  auditFilterAdminIdLabel: 'معرّف المسؤول',
  auditFilterAdminIdPlaceholder: 'معرّف المسؤول',
  auditFilterActionTypeLabel: 'نوع الإجراء',
  auditFilterActionTypePlaceholder: 'جميع الإجراءات',
  auditFilterTargetTypeLabel: 'نوع الكيان',
  auditFilterTargetTypePlaceholder: 'جميع الكيانات',
  auditFilterDateFromLabel: 'من تاريخ',
  auditFilterDateToLabel: 'إلى تاريخ',
  auditClearFilters: 'مسح التصفية',
  auditDateRangeError: 'يجب أن يكون تاريخ النهاية بعد تاريخ البداية أو مساوياً له.',
  auditTableCaption: 'سجلات تدقيق الإجراءات الإدارية',
  auditColAdmin: 'المسؤول',
  auditColAction: 'الإجراء',
  auditColTarget: 'الكيان',
  auditColWhen: 'التوقيت',
  auditColDetailsHeader: 'التفاصيل',
  auditResultCount: 'سجل',
  auditLoadingLabel: 'جارٍ تحميل سجل التدقيق…',
  auditEmptyHeading: 'لم يُعثر على سجلات تدقيق',
  auditEmptyBodyFiltered: 'جرِّب تعديل التصفية.',
  auditEmptyBodyEmpty: 'لم يتم تسجيل أي إجراءات إدارية حتى الآن.',
  auditListError: 'فشل تحميل سجل التدقيق. يُرجى المحاولة مرة أخرى.',
  auditRetry: 'حاول مرة أخرى',
  auditPrevPage: 'الصفحة السابقة',
  auditNextPage: 'الصفحة التالية',
  auditExpandEntry: 'توسيع التفاصيل',
  auditCollapseEntry: 'طي التفاصيل',
  auditDetailEventId: 'معرّف الحدث',
  auditDetailAdmin: 'المسؤول (المنفّذ)',
  auditDetailAction: 'الإجراء',
  auditDetailTargetType: 'نوع الكيان',
  auditDetailTargetId: 'معرّف الكيان',
  auditDetailOccurredAt: 'وقت الحدث (UTC)',
  auditDetailCreatedAt: 'تاريخ تسجيل السجل',
  auditDetailDetailsLabel: 'التفاصيل',
  auditDetailCopy: 'نسخ',
  auditDetailCopied: 'تم النسخ!',
  auditDetailNoDetails: '—',

  // ── P7-13 Gamification section (AR) ──────────────────────────────────────────
  navGamification: 'الألعاب التعليمية',
  gamificationHubTitle: 'الألعاب التعليمية',
  gamificationHubSubtitle: 'إدارة الشارات والمهام والأحداث الزمنية وتجاوزات الطلاب.',
  gamificationHubManage: 'إدارة',
  gamificationStudentOverridesHeading: 'تجاوزات الطلاب',
  gamificationStudentOverridesNotice:
    'يُطلق تجاوز درجة الدوري ومنح تجميد السلسلة من صفحة تفاصيل الطالب. انتقل إلى المستخدمين، واختر حساب طالب، ثم استخدم قسم الألعاب التعليمية في بطاقة الإجراءات.',
  gamEditBtn: 'تعديل',
  gamActivateBtn: 'تفعيل',
  gamDeactivateBtn: 'إلغاء التفعيل',
  gamExpireBtn: 'إنهاء',
  gamDialogCancelBtn: 'إلغاء',
  gamBadgesPageTitle: 'كتالوج الشارات',
  gamBadgesNewBtn: 'شارة جديدة',
  gamBadgesEmptyHeading: 'لا توجد شارات محددة',
  gamBadgesEmptyBody: 'أنشئ أول شارة للبدء.',
  gamBadgeDeactivateTitle: 'إلغاء تفعيل الشارة',
  gamBadgeDeactivateNotice:
    'لن تُعرض هذه الشارة للإنجازات الجديدة. يحتفظ الطلاب الذين كسبوها بها — لا تُحذف الشارات المكتسبة أبدًا.',
  gamBadgeActivateTitle: 'تفعيل الشارة',
  gamBadgeActivateNotice: 'ستكون هذه الشارة متاحة للإنجازات الجديدة.',
  gamBadgeFormCreateTitle: 'شارة جديدة',
  gamBadgeFormCreateSubtitle: 'أنشئ شارة جديدة للكتالوج.',
  gamBadgeFormEditTitle: 'تعديل الشارة',
  gamBadgeFormEditSubtitle: 'حدِّث تفاصيل الشارة. لا يمكن تغيير الكود.',
  gamBadgeFormCodeLabel: 'الكود',
  gamBadgeFormCodePlaceholder: 'مثال: FIRST_LESSON',
  gamBadgeFormCodeHint: 'لا يمكن تغيير الكود بعد الإنشاء.',
  gamBadgeFormNameLabel: 'الاسم',
  gamBadgeFormNamePlaceholder: 'مثال: الخطوات الأولى',
  gamBadgeFormDescLabel: 'الوصف',
  gamBadgeFormIconKeyLabel: 'مفتاح الأيقونة',
  gamBadgeFormIconKeyPlaceholder: 'مثال: star, trophy, flame',
  gamBadgeFormIconKeyHint: 'المعرف الذي يستخدمه تطبيق الطالب لعرض أيقونة الشارة.',
  gamBadgeFormRarityLabel: 'الندرة',
  gamBadgeFormRarityPlaceholder: 'اختر الندرة',
  gamBadgeFormTriggerLabel: 'المُشغِّل',
  gamBadgeFormThresholdLabel: 'العتبة',
  gamBadgeFormThresholdRequired: 'العتبة مطلوبة لهذا النوع من المشغلات.',
  gamBadgeFormRewardXpLabel: 'نقاط المكافأة',
  gamBadgeFormSortOrderLabel: 'ترتيب الفرز',
  gamBadgeFormSortOrderHint: 'تظهر القيم الأقل أولًا في الكتالوج.',
  gamBadgeFormCancelBtn: 'إلغاء',
  gamBadgeFormCreateBtn: 'إنشاء الشارة',
  gamBadgeFormSaveBtn: 'حفظ التغييرات',
  gamMissionsPageTitle: 'كتالوج المهام',
  gamMissionsNewBtn: 'مهمة جديدة',
  gamMissionsEmptyHeading: 'لا توجد مهام محددة',
  gamMissionsEmptyBody: 'أنشئ أول مهمة للبدء.',
  gamMissionDeactivateTitle: 'إلغاء تفعيل المهمة',
  gamMissionDeactivateNotice: 'لن تُسنَد هذه المهمة للطلاب بعد الآن.',
  gamMissionActivateTitle: 'تفعيل المهمة',
  gamMissionActivateNotice: 'ستُسنَد هذه المهمة للطلاب مجددًا.',
  gamMissionFormCreateTitle: 'مهمة جديدة',
  gamMissionFormCreateSubtitle: 'أنشئ مهمة جديدة للكتالوج.',
  gamMissionFormEditTitle: 'تعديل المهمة',
  gamMissionFormEditSubtitle: 'حدِّث تفاصيل المهمة. لا يمكن تغيير الكود.',
  gamMissionFormCodeLabel: 'الكود',
  gamMissionFormIconKeyLabel: 'مفتاح الأيقونة',
  gamMissionFormTitleKeyLabel: 'مفتاح العنوان (ترجمة)',
  gamMissionFormTitleKeyHint:
    "مفتاح البحث في الترجمة لعنوان المهمة، وليس النص المعروض. مثال: 'mission.daily_lessons'",
  gamMissionFormCadenceLabel: 'الدورية',
  gamMissionFormTargetTypeLabel: 'نوع الهدف',
  gamMissionFormTargetLabel: 'عدد الهدف',
  gamMissionFormRewardXpLabel: 'نقاط المكافأة',
  gamMissionFormSortOrderLabel: 'ترتيب الفرز',
  gamMissionFormCancelBtn: 'إلغاء',
  gamMissionFormCreateBtn: 'إنشاء المهمة',
  gamMissionFormSaveBtn: 'حفظ التغييرات',
  gamEventsPageTitle: 'الأحداث الزمنية',
  gamEventsNewBtn: 'حدث جديد',
  gamEventsEmptyHeading: 'لا توجد أحداث زمنية',
  gamEventsEmptyBody: 'أنشئ أول حدث زمني للبدء.',
  gamEventActivateTitle: 'تفعيل الحدث',
  gamEventActivateNotice:
    'سيُطبَّق مُضاعِف النقاط فورًا خلال نافذة الحدث النشطة.',
  gamEventExpireTitle: 'إنهاء الحدث',
  gamEventExpireNotice:
    'سيُصنَّف الحدث على الفور كمنتهٍ ويتوقف تطبيق مُضاعِف النقاط.',
  gamEventActivateConfirmBtn: 'تفعيل الحدث',
  gamEventExpireConfirmBtn: 'إنهاء الحدث',
  gamEventFormCreateTitle: 'حدث زمني جديد',
  gamEventFormCreateSubtitle: 'جدوِل حدثًا جديدًا لمضاعفة النقاط.',
  gamEventFormEditTitle: 'تعديل الحدث الزمني',
  gamEventFormEditSubtitle: 'حدِّث تفاصيل الحدث. لا يمكن تغيير الكود.',
  gamEventFormCodeLabel: 'الكود',
  gamEventFormNameEnLabel: 'اسم الحدث (إنجليزي)',
  gamEventFormNameArLabel: 'اسم الحدث (عربي)',
  gamEventFormDescEnLabel: 'الوصف (إنجليزي، اختياري)',
  gamEventFormDescArLabel: 'الوصف (عربي، اختياري)',
  gamEventFormStartLabel: 'البدء (UTC)',
  gamEventFormEndLabel: 'الانتهاء (UTC)',
  gamEventFormUtcHint: 'أدخل الوقت بتوقيت UTC. تُخزَّن الأوقات وتُعرض بتوقيت UTC.',
  gamEventFormEndBeforeStart: 'يجب أن يكون وقت الانتهاء بعد وقت البدء.',
  gamEventFormMultiplierLabel: 'مُضاعِف النقاط',
  gamEventFormMultiplierHint: 'النطاق: ١٫٠ – ٥٫٠. مثال: ٢٫٠ تضاعف النقاط المكتسبة.',
  gamEventFormScopeLabel: 'النطاق',
  gamEventFormScopeNotice:
    'نطاق كل النقاط فقط هو النشط حاليًا في المحرك. نقاط المهام ونقاط الدوري متاحة للاستخدام المستقبلي.',
  gamEventFormCreateBtn: 'إنشاء الحدث',
  gamEventFormSaveBtn: 'حفظ التغييرات',
  gamEventFormCancelBtn: 'إلغاء',
  gamOverridesHeading: 'تجاوزات الألعاب التعليمية',
  gamLeagueTierBtn: 'تجاوز درجة الدوري',
  gamLeagueTierDialogTitle: 'تجاوز درجة الدوري',
  gamLeagueTierCurrentLabel: 'درجة الدوري الحالية',
  gamLeagueTierUnknown: 'لا توجد بيانات',
  gamLeagueTierCaveat:
    'مأخوذ من بيانات النشاط الأسبوعي — قد يختلف عن الدرجة المخزَّنة.',
  gamLeagueTierNewLabel: 'الدرجة الجديدة',
  gamLeagueTierSelectPlaceholder: 'اختر الدرجة',
  gamLeagueTierAuditNotice:
    'يُسجَّل هذا التجاوز ويُطبَّق على درجة الدوري الحالية للطالب. النقاط والتقدم غير متأثرَين.',
  gamLeagueTierReasonLabel: 'السبب (مطلوب)',
  gamLeagueTierConfirmBtn: 'تجاوز الدرجة',
  gamLeagueTierSuccessBanner: 'تم تجاوز درجة دوري {name}.',
  gamLeagueTierErr400SameTier: 'هذه هي درجة الدوري الحالية للطالب بالفعل.',
  gamLeagueTierErr404: 'لم يُعثر على حساب الطالب.',
  gamLeagueTierErr422: 'السبب مطلوب ويجب أن يكون أقل من ٥٠٠ حرف.',
  gamLeagueTierErrNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  gamFreezeFreezeBtn: 'منح تجميد السلسلة',
  gamFreezeDialogTitle: 'منح تجميد السلسلة',
  gamFreezeBalanceUnavailable:
    'رصيد التجميد الحالي: غير متاح (لا يوجد نقطة قراءة — راجع المتابعة Q2).',
  gamFreezeCountLabel: 'عدد التجميدات',
  gamFreezeCountHint: 'الحد الأقصى ٢ لكل منحة',
  gamFreezeReasonLabel: 'السبب (مطلوب)',
  gamFreezeConfirmBtn: 'منح التجميد',
  gamFreezeSuccessBanner: 'تم منح تجميد السلسلة لـ {name}.',
  gamFreezeErrCount: 'يجب أن يكون العدد بين ١ و٢.',
  gamFreezeErr404: 'لم يُعثر على حساب الطالب.',
  gamFreezeErr422: 'السبب مطلوب ويجب أن يكون أقل من ٥٠٠ حرف.',
  gamFreezeErrNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',

  // ── P7-13 additional keys (AR) ────────────────────────────────────────────
  gamification: 'التلعيب',
  gamCancelBtn: 'إلغاء',
  gamRetry: 'إعادة المحاولة',

  gamBadgePageTitle: 'كتالوج الشارات',
  gamBadgeCreateBtn: 'شارة جديدة',
  gamBadgeActive: 'نشط',
  gamBadgeInactive: 'غير نشط',
  gamBadgeLoading: 'جارٍ تحميل كتالوج الشارات…',
  gamBadgeFetchError: 'فشل تحميل الشارات.',
  gamBadgeEmpty: 'لا توجد شارات محددة. أنشئ أول شارة للبدء.',
  gamBadgeCreateFirst: 'إنشاء شارة',
  gamBadgeEditBtn: 'تعديل',
  gamBadgeActivateBtn: 'تفعيل',
  gamBadgeDeactivateBtn: 'تعطيل',
  gamBadgeTableCaption: 'كتالوج الشارات',
  gamBadgeColCode: 'الرمز',
  gamBadgeColName: 'الاسم',
  gamBadgeColRarity: 'الندرة',
  gamBadgeColTrigger: 'المُشغِّل',
  gamBadgeColXp: 'نقاط الخبرة',
  gamBadgeColStatus: 'الحالة',
  gamBadgeColActions: 'الإجراءات',
  gamBadgeDeactivateSubtitle: 'تعطيل "{name}"؟',
  gamBadgeDeactivateNote: 'لن يُمنح هذا الوسام لأي إنجازات جديدة.',
  gamBadgeDeactivateConfirmBtn: 'تعطيل',
  gamBadgeActivateSubtitle: 'تفعيل "{name}"؟',
  gamBadgeActivateNote: 'سيُمنح هذا الوسام مجددًا للإنجازات المستحقة.',
  gamBadgeActivateConfirmBtn: 'تفعيل',
  gamBadgeActivatedBanner: 'تم تفعيل الشارة.',
  gamBadgeDeactivatedBanner: 'تم تعطيل الشارة.',
  gamBadgeNotFoundError: 'الشارة غير موجودة.',
  gamBadgeActionError: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',

  gamMissionPageTitle: 'كتالوج المهام',
  gamMissionCreateBtn: 'مهمة جديدة',
  gamMissionActive: 'نشطة',
  gamMissionInactive: 'غير نشطة',
  gamMissionLoading: 'جارٍ تحميل كتالوج المهام…',
  gamMissionFetchError: 'فشل تحميل المهام.',
  gamMissionEmpty: 'لا توجد مهام محددة. أنشئ أول مهمة للبدء.',
  gamMissionCreateFirst: 'إنشاء مهمة',
  gamMissionEditBtn: 'تعديل',
  gamMissionActivateBtn: 'تفعيل',
  gamMissionDeactivateBtn: 'تعطيل',
  gamMissionTableCaption: 'كتالوج المهام',
  gamMissionColTitle: 'العنوان',
  gamMissionColType: 'النوع',
  gamMissionColTargetType: 'نوع الهدف',
  gamMissionColTargetCount: 'الهدف',
  gamMissionColXp: 'نقاط الخبرة',
  gamMissionColStatus: 'الحالة',
  gamMissionColActions: 'الإجراءات',
  gamMissionDeactivateSubtitle: 'تعطيل "{name}"؟',
  gamMissionDeactivateNote: 'لن تُسند هذه المهمة للطلاب بعد الآن.',
  gamMissionDeactivateConfirmBtn: 'تعطيل',
  gamMissionActivateSubtitle: 'تفعيل "{name}"؟',
  gamMissionActivateNote: 'ستُسند هذه المهمة للطلاب مجددًا.',
  gamMissionActivateConfirmBtn: 'تفعيل',
  gamMissionActivatedBanner: 'تم تفعيل المهمة.',
  gamMissionDeactivatedBanner: 'تم تعطيل المهمة.',
  gamMissionNotFoundError: 'المهمة غير موجودة.',
  gamMissionActionError: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  gamMissionFormTitleLabel: 'العنوان',
  gamMissionFormDescLabel: 'الوصف (اختياري)',
  gamMissionFormTypeLabel: 'نوع المهمة',
  gamMissionFormTypePlaceholder: 'اختر النوع',
  gamMissionFormTargetCountLabel: 'عدد الهدف',
  gamMissionFormSortOrderHint: 'القيم الأصغر تظهر أولاً.',

  gamEventPageTitle: 'الأحداث الزمنية',
  gamEventCreateBtn: 'حدث جديد',
  gamEventLoading: 'جارٍ تحميل الأحداث الزمنية…',
  gamEventFetchError: 'فشل تحميل الأحداث الزمنية.',
  gamEventEmpty: 'لا توجد أحداث زمنية. أنشئ حدثًا لجدولة مضاعف النقاط.',
  gamEventCreateFirst: 'إنشاء حدث',
  gamEventEditBtn: 'تعديل',
  gamEventActivateBtn: 'تفعيل',
  gamEventExpireBtn: 'إنهاء',
  gamEventTableCaption: 'الأحداث الزمنية',
  gamEventColName: 'الاسم',
  gamEventColScope: 'النطاق',
  gamEventColMultiplier: 'المضاعف',
  gamEventColStart: 'البدء (UTC)',
  gamEventColEnd: 'الانتهاء (UTC)',
  gamEventColStatus: 'الحالة',
  gamEventColActions: 'الإجراءات',
  gamEventActivateSubtitle: 'تفعيل "{name}"؟',
  gamEventActivateNote: 'سيصبح الحدث نشطًا وسيُطبَّق مضاعف نقاط الخبرة.',
  gamEventExpireSubtitle: 'إنهاء "{name}" الآن؟',
  gamEventExpireNote: 'سيتوقف الحدث فورًا وسيُوقف مضاعف نقاط الخبرة.',
  gamEventActivatedBanner: 'تم تفعيل الحدث.',
  gamEventExpiredBanner: 'تم إنهاء الحدث.',
  gamEventNotFoundError: 'الحدث غير موجود.',
  gamEventActionError: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  gamEventFormDescGapNotice:
    'لا يمكن تعبئة حقول الوصف مسبقًا (غير متوفرة في القائمة). أعد إدخالها إذا أردت تحديثها.',
  gamEventFormDescGapPlaceholder: 'أعد الإدخال إذا كنت تريد التغيير…',
  gamEventFormDateHint: 'تُدخَل التواريخ بتوقيتك المحلي وتُحفظ بصيغة UTC.',

  gamLeagueTierDialogBody: 'تعديل دوري {name}. هذا الإجراء مسجَّل.',
  gamLeagueTierCancelBtn: 'إلغاء',
  gamLeagueTierError400: 'هذا هو مستوى الدوري الحالي للطالب.',
  gamLeagueTierError404: 'لم يُعثر على حساب الطالب.',
  gamLeagueTierError422: 'فشل التحقق. السبب مطلوب.',
  gamLeagueTierErrorNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  gamLeagueTierReasonPlaceholder: 'سبب تعديل مستوى الدوري…',

  gamFreezeDialogBody: 'منح رموز تجميد السلسلة لـ {name}.',
  gamFreezeCancelBtn: 'إلغاء',
  gamFreezeError400: 'طلب غير صالح. يُرجى مراجعة العدد.',
  gamFreezeError404: 'لم يُعثر على حساب الطالب.',
  gamFreezeError422: 'فشل التحقق. السبب مطلوب.',
  gamFreezeErrorNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  gamFreezeReasonPlaceholder: 'سبب منح تجميد السلسلة…',

  // ── P7-09 Moderation Queue (AR) ───────────────────────────────────────────
  navModeration: 'الإشراف',
  modPageTitleQueue: 'قائمة الإشراف',
  modPageTitleDetail: 'عنصر الإشراف',
  modListHeading: 'قائمة الإشراف',
  modResultCount: 'عنصر',
  modSearchPlaceholder: 'ابحث برقم مرجع المحتوى…',
  modFilterAllStatuses: 'كل الحالات',
  modFilterAllSources: 'كل المصادر',
  modFilterAllSubjects: 'كل المواد',
  modFilterAllGrades: 'كل الصفوف',
  modClearFilters: 'مسح التصفية',
  modListLoadingLabel: 'جار تحميل قائمة الإشراف…',
  modListError: 'فشل تحميل قائمة الإشراف. يُرجى المحاولة مرة أخرى.',
  modListRetry: 'حاول مرة أخرى',
  modTableCaption: 'عناصر قائمة الإشراف',
  modPrevPage: 'الصفحة السابقة',
  modNextPage: 'الصفحة التالية',
  modSubjectMath: 'رياضيات',
  modSubjectScience: 'علوم',
  modSubjectArabic: 'عربي',
  modSubjectEnglish: 'إنجليزي',
  modEmptyNoFilters: 'لا توجد عناصر في القائمة',
  modEmptyNoFiltersBody: 'قائمة الإشراف فارغة.',
  modEmptyFiltered: 'لا توجد عناصر مطابقة',
  modEmptyFilteredBody: 'لا تتطابق أي عناصر مع التصفية الحالية.',
  modColSource: 'المصدر',
  modColContentRef: 'مرجع المحتوى',
  modColSubjectGrade: 'المادة / الصف',
  modColTaskKind: 'نوع المهمة',
  modColStatus: 'الحالة',
  modColDetected: 'اكتُشف',
  modViewDetail: 'عرض العنصر',
  modStatusPending: 'قيد المراجعة',
  modStatusApproved: 'موافق عليه',
  modStatusRejected: 'مرفوض',
  modStatusFlagged: 'مُعلَّق',
  modSourceAiOutput: 'مخرج الذكاء الاصطناعي',
  modSourceCurriculumUpload: 'رفع المنهج',
  modDetailLoadingLabel: 'جار تحميل عنصر الإشراف…',
  modDetailError: 'فشل تحميل عنصر الإشراف هذا.',
  modNotFoundHeading: 'العنصر غير موجود',
  modNotFoundBody: 'عنصر الإشراف هذا غير موجود أو تم إزالته.',
  modBackToQueue: 'العودة إلى القائمة',
  modSectionDetails: 'تفاصيل العنصر',
  modSectionReviewHistory: 'سجل المراجعة',
  modFieldStudentId: 'معرّف الطالب',
  modFieldDetectedAt: 'اكتُشف في',
  modFieldItemId: 'المُعرِّف',
  modReviewedBy: 'راجعه',
  modReviewedAt: 'وقت المراجعة',
  modReviewReason: 'السبب',
  modTerminalNotice: 'تم حل هذا العنصر بالفعل ولا يمكن مراجعته مجدداً.',
  modVerdictSection: 'إشارة السلامة الآلية',
  modVerdictPrivacyNote: 'الإصدار الأول: لا يُخزَّن أي محتوى خام — تحتوي هذه الإشارة فقط على أسماء الفحوصات الآلية ورموز الأسباب.',
  modVerdictFailedChecks: 'الفحوصات الفاشلة',
  modVerdictReasonCodes: 'رموز الأسباب',
  modVerdictActionTaken: 'الإجراء المتخذ',
  modVerdictModelId: 'النموذج',
  modVerdictUnavailable: 'إشارة السلامة غير متوفرة أو لم تُملأ بعد.',
  modReviewActionsHeading: 'إجراءات المراجعة',
  modReviewApprove: 'موافقة',
  modReviewReject: 'رفض',
  modReviewFlag: 'الإحالة للتصعيد',
  modAlreadyFlagged: 'خيار الإحالة غير متاح — العنصر مُحال بالفعل.',
  modDlgCancel: 'إلغاء',
  modDlgApproveTitle: 'الموافقة على العنصر',
  modDlgApproveSubtitle: 'هل تريد الموافقة على عنصر المحتوى هذا؟',
  modDlgApproveReasonLabel: 'ملاحظة (اختياري)',
  modDlgApproveConfirm: 'موافقة',
  modDlgRejectTitle: 'رفض العنصر',
  modDlgRejectSubtitle: 'رفض عنصر المحتوى هذا.',
  modDlgRejectReasonLabel: 'سبب الرفض (مطلوب)',
  modDlgRejectConfirm: 'رفض',
  modDlgFlagTitle: 'الإحالة للتصعيد',
  modDlgFlagSubtitle: 'إحالة هذا العنصر للمراجعة الإضافية.',
  modDlgFlagReasonLabel: 'ملاحظة التصعيد (اختياري)',
  modDlgFlagConfirm: 'إحالة',
  modErrAlreadyTerminal: 'تم حل هذا العنصر بالفعل ولا يمكن مراجعته مجدداً.',
  modErrAlreadyFlagged: 'هذا العنصر مُحال بالفعل — استخدم موافقة أو رفض.',
  modErr404: 'لم يُعثر على هذا العنصر.',
  modErrValidation: 'السبب مطلوب ويجب أن يكون أقل من ٢٠٠٠ حرف.',
  modErrNetwork: 'حدث خطأ ما. يُرجى المحاولة مرة أخرى.',
  modReviewSuccess: 'تمت المراجعة — تم تحديث الحالة.',
};

const STRINGS: Record<Locale, AdminStrings> = { en, ar };

/** Default admin locale (English-first per Design Spec §7). */
export const ADMIN_LOCALE: Locale = 'en';

export function getStrings(locale: Locale = ADMIN_LOCALE): AdminStrings {
  return STRINGS[locale] ?? en;
}
