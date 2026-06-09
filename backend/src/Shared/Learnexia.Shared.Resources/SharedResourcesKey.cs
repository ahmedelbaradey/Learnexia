namespace Resources
{
    public static class SharedResourcesKey
    {
        public const string EmptyIdValidation = "EmptyIdValidation";
        public const string EmptyRequestValidation = "EmptyRequestValidation";
        public const string EmptyNameValidation = "EmptyNameValidation";
        public const string ExistICNumberValidation = "ExistICNumberValidation";
        public const string HasAcceptedTerms = "ExistICNumberValidation";
        public const string EmptyCustomerNameValidtion = "EmptyCustomerNameValidtion";
        public const string MaximumCharsCustomerNameValidtion = "MaximumCharsCustomerNameValidtion";
        public const string EmptyCustomerPhoneValidation = "EmptyCustomerNameValidtion";
        public const string ExistCustomerPhoneValidation = "EmptyCustomerNameValidtion";
        public const string EmptyCustomerEmailValidation = "EmptyCustomerEmailValidation";
        public const string ExistCustomerEmailValidation = "EmptyCustomerNameValidtion";
        public const string EmptyTOTPValidation = "EmptyTOTPValidation";
        public const string MaximumDigitsTOTPValidation = "MaximumDigitsTOTPValidation";
        public const string MinimumDigitsTOTPValidation = "MinimumDigitsTOTPValidation";
        public const string NotValidOrExpiredTOTPValidation = "NotValidOrExpiredTOTPValidation";
        public const string NotValidPINCodeValidation = "NotValidPINCodeValidation";
        public const string MaximumDigitsPINCodeValidation = "MaximumDigitsPINCodeValidation";
        public const string MinimumDigitsPINCodeValidation = "MinimumDigitsPINCodeValidation";
        public const string CustomerCreationFailed = "CustomerCreationFailed";
        public const string PhoneTOTPIs = "PhoneTOTPIs";
        public const string TOTPExpireAfter = "TOTPExpireAfter";
        public const string PINCodeUpdated = "PINCodeUpdated";
        public const string PINCodeCreationFailed = "PINCodeCreationFailed";
        public const string BiometricLoginEnabledSucessfully = "BiometricLoginEnabledSucessfully";
        public const string BiometricLoginEnabledFailed = "BiometricLoginEnabledFailed";
        public const string TermsAcceptedSucessfully = "TermsAcceptedSucessfully";
        public const string TermsAcceptedFailed = "TermsAcceptedFailed";
        public const string EmailVerirfiedSucessfully = "EmailVerirfiedSucessfully";
        public const string EmailVerificationFailed = "EmailVerificationFailed";
        public const string PhoneVerifiedAndEmailTOTPIs = "PhoneVerifiedAndEmailTOTPIs";
        public const string PhoneVerificationFailed = "PhoneVerificationFailed";
        public const string PINCodeVerirfiedSucessfully = "PINCodeVerirfiedSucessfully";
        public const string PINCodeVerificationFailed = "PINCodeVerificationFailed";
        public const string ValidEmailValidation = "ValidEmailValidation";
        public const string TheCustomerWithICNumber = "TheCustomerWithICNumber";
        public const string DoesntExist = "DoesntExist";
        public const string Required = "Required";
        public const string MaxLength = "MaxLength";
        public const string MinLength = "MinLength";
        public const string Unique = "Unique";
        public const string RecordSavedSuccessfully = "RecordSavedSuccessfully";
        public const string FundManagersListValidation = "FundManagersListValidation";
        public const string FundBoardSecretariesListValidation = "FundBoardSecretariesListValidation";
        public const string RequiredField = "RequiredField";
        public const string VotingTypeRangeValidator = "VotingTypeRangeValidator";
        public const string AnErrorIsOccurredWhileSavingData = "AnErrorIsOccurredWhileSavingData";
        public const string InitiationDateRangeValidator = "InitiationDateRangeValidator";


        public const string AddFundNotificationBody = "AddFundNotificationBody";
        public const string AddFundForManagerNotificationBody = "AddFundForManagerNotificationBody";
        public const string AddFundNotificationTitle = "AddFundNotificationTitle";

        public const string ChangeExitDateNotificationBody = "ChangeExitDateNotificationBody";
        public const string ChangeExitDateNotificationTitle = "ChangeExitDateNotificationTitle";

        public const string CompeleteFundNotificationBody = "CompeleteFundNotificationBody";
        public const string CompeleteFundNotificationTitle = "CompeleteFundNotificationTitle";

        public const string RemoveFromFundNotificationBody = "RemoveFromFundNotificationBody";
        public const string RemoveFromFundNotificationTitle = "RemoveFromFundNotificationTitle";

        public const string AddedToFundNotificationBody = "AddedToFundNotificationBody";
        public const string AddedToFundNotificationTitle = "AddedToFundNotificationTitle";

        public const string OldFundCodeAlreadyExist = "OldFundCodeAlreadyExist";
        public const string FundAlreadyExist = "FundAlreadyExist";
        public const string InvalidFund = "InvalidFund";
        public const string InvalidFundName = "InvalidFundName";

        public const string MaxFileSize = "MaxFileSize";

        public const string PropertiesNumberValidator = "PropertiesNumberValidator";
        public const string FundSavedSuccessfully = "FundSavedSuccessfully";

        // Board Member Type Localization Keys
        public const string BoardMemberTypeIndependent = "BoardMemberTypeIndependent";
        public const string BoardMemberTypeNotIndependent = "BoardMemberTypeNotIndependent";

        // Fund Action Localization Keys
        public const string FundCreationAction = "FundCreationAction";
        public const string FundDataCompletionAction = "FundDataCompletionAction";
        public const string FundDataEditAction = "FundDataEditAction";
        public const string FundActivationAction = "FundActivationAction";
        public const string FundExitDateEditAction = "FundExitDateEditAction";
        public const string BoardMemberAdditionAction = "BoardMemberAdditionAction";
        public const string FundStatusChangeAction = "FundStatusChangeAction";

        // Fund Status Localization Keys
        public const string FundStatusUnderConstruction = "FundStatusUnderConstruction";
        public const string FundStatusWaitingForMembers = "FundStatusWaitingForMembers";
        public const string FundStatusActive = "FundStatusActive";
        public const string FundStatusExited = "FundStatusExited";

        // Fund Audit Localization Keys
        public const string FundStatusTransition = "FundStatusTransition";
        public const string FundActivatedDueToMembers = "FundActivatedDueToMembers";
        public const string UnknownAction = "UnknownAction";
        public const string UnknownStatus = "UnknownStatus";


        // Vote Decision Localization Keys
        public const string VoteDecisionApprove = "VoteDecisionApprove";
        public const string VoteDecisionReject = "VoteDecisionReject";
        public const string VoteDecisionAbstain = "VoteDecisionAbstain";

        // Voting Type Localization Keys
        public const string VotingTypeAllMembers = "VotingTypeAllMembers";
        public const string VotingTypeMajority = "VotingTypeMajority";

        // Member Voting Result Localization Keys
        public const string MemberVotingResultAllItems = "MemberVotingResultAllItems";
        public const string MemberVotingResultMajorityOfItems = "MemberVotingResultMajorityOfItems";

        // Board Member Validation Messages
        public const string InvalidIdValidation = "InvalidIdValidation";
        public const string InvalidBoardMemberType = "InvalidBoardMemberType";
        public const string UserAlreadyBoardMember = "UserAlreadyBoardMember";
        public const string MaxIndependentMembersReached = "MaxIndependentMembersReached";
        public const string FundAlreadyHasChairman = "FundAlreadyHasChairman";
        public const string BoardMemberAddedSuccessfully = "BoardMemberAddedSuccessfully";
        public const string MeetingDateMustBeFuture = "MeetingDateMustBeFuture";
        public const string InvalidVotingType = "InvalidVotingType";
        public const string MaxLengthValidation = "MaxLengthValidation";
        public const string BoardMemberUpdatedSuccessfully = "BoardMemberUpdatedSuccessfully";
        public const string BoardMemberDeletedSuccessfully = "BoardMemberDeletedSuccessfully";
        public const string BoardMemberNotFound = "BoardMemberNotFound";
        public const string BoardMemberAlreadyDeleted = "BoardMemberAlreadyDeleted";
        public const string FundNotFound = "FundNotFound";
        public const string UserNotFound = "UserNotFound";

        // Resolution Validation Messages
        public const string ResolutionCodeExists = "ResolutionCodeExists";
        public const string ResolutionCreatedSuccessfully = "ResolutionCreatedSuccessfully";
        public const string ResolutionUpdatedSuccessfully = "ResolutionUpdatedSuccessfully";
        public const string ResolutionDeletedSuccessfully = "ResolutionDeletedSuccessfully";
        public const string ResolutionNotFound = "ResolutionNotFound";
        public const string ResolutionTypeNotFound = "ResolutionTypeNotFound";
        public const string AttachmentNotFound = "AttachmentNotFound";
        public const string InvalidResolutionDate = "InvalidResolutionDate";
        public const string InvalidVotingMethodology = "InvalidVotingMethodology";
        public const string ResolutionDateMustBeAfterFundInitiation = "ResolutionDateMustBeAfterFundInitiation";
        public const string ResolutionDateCannotBeFuture = "ResolutionDateCannotBeFuture";
        public const string InvalidFileType = "InvalidFileType";
        public const string FileSizeExceedsLimit = "FileSizeExceedsLimit";
        public const string ResolutionCodeGenerationFailed = "ResolutionCodeGenerationFailed";
        public const string OnlyFundManagerCanCreateResolution = "OnlyFundManagerCanCreateResolution";
        public const string ResolutionSavedAsDraft = "ResolutionSavedAsDraft";
        public const string ResolutionSentForReview = "ResolutionSentForReview";
        public const string NewTypeRequiredForOtherResolutionType = "NewTypeRequiredForOtherResolutionType";
        public const string CannotEditApprovedOrRejectedResolution = "CannotEditApprovedOrRejectedResolution";

        // Board Member Notification Keys
        public const string BoardMemberAddedNotificationTitle = "BoardMemberAddedNotificationTitle";
        public const string BoardMemberAddedNotificationBody = "BoardMemberAddedNotificationBody";
        public const string BoardMemberAddedToFundNotificationTitle = "BoardMemberAddedToFundNotificationTitle";
        public const string BoardMemberAddedToFundNotificationBody = "BoardMemberAddedToFundNotificationBody";
        public const string MaximumIndependentMembersReached = "MaximumIndependentMembersReached";

        // Board Member Display Keys
        public const string Independent = "Independent";
        public const string NotIndependent = "NotIndependent";
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Chairman = "Chairman";
        public const string Member = "Member";

        // User Role Localization Keys
        public const string FundManager = "FundManager";
        public const string LegalCouncil = "LegalCouncil";
        public const string BoardSecretary = "BoardSecretary";
        public const string BoardMember = "BoardMember";
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
        public const string Basic = "Basic";
        public const string User = "User";
        public const string FinanceController = "FinanceController";
        public const string ComplianceLegalManagingDirector = "ComplianceLegalManagingDirector";
        public const string HeadOfRealEstate = "HeadOfRealEstate";
        public const string AssociateFundManager = "AssociateFundManager";

        // Resolution Notification Messages (MSG002, MSG007, MSG008 from Sprint.md)
        public const string ResolutionCreatedNotificationTitle = "ResolutionCreatedNotificationTitle";
        public const string ResolutionCreatedNotificationBody = "ResolutionCreatedNotificationBody";
        public const string ResolutionUpdatedNotificationTitle = "ResolutionUpdatedNotificationTitle";
        public const string ResolutionUpdatedNotificationBody = "ResolutionUpdatedNotificationBody";
        public const string FundActivatedNotificationTitle = "FundActivatedNotificationTitle";
        public const string FundActivatedNotificationBody = "FundActivatedNotificationBody";

        // Access Control Messages
        public const string UnauthorizedAccess = "UnauthorizedAccess";

        // Fund Board Member Management Messages (JDWA-1258)
 
        public const string MaxBoardMembersReached = "MaxBoardMembersReached";
        public const string MaxIndependentBoardMembersReached = "MaxIndependentBoardMembersReached";
        public const string BoardChairmanAlreadyExists = "BoardChairmanAlreadyExists";
 

        // Enhanced User Activation/Deactivation Messages (JDWA-1253)
        public const string CannotDeactivateIndependentBoardMember = "CannotDeactivateIndependentBoardMember";
        public const string CannotDeactivateSoleFundManager = "CannotDeactivateSoleFundManager";
        public const string CannotDeactivateSingleHolderRole = "CannotDeactivateSingleHolderRole";
        public const string UserActivatedSuccessfully = "UserActivatedSuccessfully";
        public const string UserDeactivatedSuccessfully = "UserDeactivatedSuccessfully";
        public const string RoleReplacementConfirmation = "RoleReplacementConfirmation";

        // Enhanced Password Reset Messages (JDWA-1257)
        public const string UserNotEligibleForPasswordReset = "UserNotEligibleForPasswordReset";
        public const string PasswordResetConfirmation = "PasswordResetConfirmation";
        public const string WhatsAppPasswordResetSent = "WhatsAppPasswordResetSent";
        public const string WhatsAppPasswordResetFailed = "WhatsAppPasswordResetFailed";

        // General Success Messages
        public const string OperationCompletedSuccessfully = "OperationCompletedSuccessfully";
        public const string SystemErrorSavingData = "SystemErrorSavingData";

        // Resolution Cancel and Delete Messages (JDWA-508, JDWA-510)
        public const string ConfirmCancelResolution = "ConfirmCancelResolution";
        public const string ConfirmDeleteResolution = "ConfirmDeleteResolution";
        public const string ResolutionCancelledSuccessfully = "ResolutionCancelledSuccessfully";
        public const string ItemDeletedSuccessfully = "ItemDeletedSuccessfully";
        public const string SystemErrorUpdatingData = "SystemErrorUpdatingData";
        public const string SystemErrorDeletingData = "SystemErrorDeletingData";
        public const string SystemErrorDisplayingData = "SystemErrorDisplayingData";
        public const string CannotCancelNonPendingResolution = "CannotCancelNonPendingResolution";
        public const string CannotDeleteNonDraftResolution = "CannotDeleteNonDraftResolution";

        // Resolution Cancel Notification Messages (MSG004 from Sprint.md)
        public const string ResolutionCancelledNotificationTitle = "ResolutionCancelledNotificationTitle";
        public const string ResolutionCancelledNotificationBody = "ResolutionCancelledNotificationBody";

        // Resolution Items and Conflicts Management (JDWA-566, JDWA-507)
        public const string ResolutionItemAddedSuccessfully = "ResolutionItemAddedSuccessfully";
        public const string ResolutionItemUpdatedSuccessfully = "ResolutionItemUpdatedSuccessfully";
        public const string ResolutionItemDeletedSuccessfully = "ResolutionItemDeletedSuccessfully";
        public const string ResolutionItemNotFound = "ResolutionItemNotFound";
        public const string ResolutionItemTitleRequired = "ResolutionItemTitleRequired";
        public const string ResolutionItemDescriptionMaxLength = "ResolutionItemDescriptionMaxLength";
        public const string ConflictMembersRequired = "ConflictMembersRequired";
        public const string InvalidBoardMemberForConflict = "InvalidBoardMemberForConflict";
        public const string ResolutionDataCompletedSuccessfully = "ResolutionDataCompletedSuccessfully";
        public const string NoItemsAddedConfirmation = "NoItemsAddedConfirmation"; // MSG006
        public const string ResolutionDataCompletedNotificationTitle = "ResolutionDataCompletedNotificationTitle"; // MSG003
        public const string ResolutionDataCompletedNotificationBody = "ResolutionDataCompletedNotificationBody"; // MSG003

        // Resolution Attachments Management (JDWA-568, JDWA-505)
        public const string AttachmentAddedSuccessfully = "AttachmentAddedSuccessfully";
        public const string AttachmentDeletedSuccessfully = "AttachmentDeletedSuccessfully";
        public const string MaxAttachmentsReached = "MaxAttachmentsReached"; // Max 10 files
        public const string AttachmentCounterLabel = "AttachmentCounterLabel";
        public const string InvalidAttachmentType = "InvalidAttachmentType";
        public const string AttachmentSizeExceedsLimit = "AttachmentSizeExceedsLimit";
        public const string DuplicateAttachmentsNotAllowed = "DuplicateAttachmentsNotAllowed";

        // Resolution Status Management
        public const string CannotEditResolutionInCurrentStatus = "CannotEditResolutionInCurrentStatus";
        public const string InvalidResolutionStatusForOperation = "InvalidResolutionStatusForOperation";

        // JDWA-509 Authorization Messages
        public const string OnlyCreatorCanEditDraftResolution = "OnlyCreatorCanEditDraftResolution";

        // Resolution Confirmation and Rejection (JDWA-570)
        public const string ResolutionConfirmedSuccessfully = "ResolutionConfirmedSuccessfully";
        public const string ResolutionRejectedSuccessfully = "ResolutionRejectedSuccessfully";
        public const string InvalidResolutionStatusForConfirmation = "InvalidResolutionStatusForConfirmation";
        public const string InvalidResolutionStatusForRejection = "InvalidResolutionStatusForRejection";
        public const string RejectionReasonRequired = "RejectionReasonRequired";
        public const string RejectionReasonLength = "RejectionReasonLength";

        // Resolution Confirmation Notification Messages (MSG002 from Sprint.md)
        public const string ResolutionConfirmedNotificationTitle = "ResolutionConfirmedNotificationTitle";
        public const string ResolutionConfirmedNotificationBody = "ResolutionConfirmedNotificationBody";

        // Resolution Rejection Notification Messages (MSG004 from Sprint.md)
        public const string ResolutionRejectedNotificationTitle = "ResolutionRejectedNotificationTitle";
        public const string ResolutionRejectedNotificationBody = "ResolutionRejectedNotificationBody";

        // Resolution Send to Vote (JDWA-569)
        public const string ResolutionSentToVoteSuccessfully = "ResolutionSentToVoteSuccessfully";
        public const string InvalidResolutionStatusForVoting = "InvalidResolutionStatusForVoting";

        // Resolution Send to Vote Notification Messages (MSG002 from Sprint.md)
        public const string ResolutionSentToVoteNotificationTitle = "ResolutionSentToVoteNotificationTitle";
        public const string ResolutionSentToVoteNotificationBody = "ResolutionSentToVoteNotificationBody";

        // Alternative 2 Resolution Messages (MSG008, MSG009 from Sprint.md)
        public const string ConfirmCreateNewResolutionFromApproved = "ConfirmCreateNewResolutionFromApproved"; // MSG008
        public const string NewResolutionCreatedFromApprovedNotificationTitle = "NewResolutionCreatedFromApprovedNotificationTitle"; // MSG009
        public const string NewResolutionCreatedFromApprovedNotificationBody = "NewResolutionCreatedFromApprovedNotificationBody"; // MSG009

        // WhatsApp Notification Messages (Sprint 3 Requirements)
        public const string WhatsAppPasswordResetMessage = "WhatsAppPasswordResetMessage"; // MSG-RESET-006
        public const string WhatsAppUserRegistrationMessage = "WhatsAppUserRegistrationMessage"; // MSG-ADD-008
        public const string WhatsAppAccountActivationMessage = "WhatsAppAccountActivationMessage"; // MSG-ACTDEACT-009
        public const string WhatsAppAccountDeactivationMessage = "WhatsAppAccountDeactivationMessage"; // MSG-ACTDEACT-010
        public const string WhatsAppRegistrationResendMessage = "WhatsAppRegistrationResendMessage"; // MSG-ADD-008 Resend
        public const string WhatsAppFundMemberAddedMessage = "WhatsAppFundMemberAddedMessage"; // Fund member addition notification

        // Alternative 1 Resolution Messages (MSG007 from Sprint.md)
        public const string ConfirmSuspendVotingForEdit = "ConfirmSuspendVotingForEdit"; // MSG006 - Confirmation message for voting suspension
        public const string ResolutionVotingSuspendedNotificationTitle = "ResolutionVotingSuspendedNotificationTitle"; // MSG007
        public const string ResolutionVotingSuspendedNotificationBody = "ResolutionVotingSuspendedNotificationBody"; // MSG007
        public const string VotingSuspendedSuccessfully = "VotingSuspendedSuccessfully"; // Success message after voting suspension
        public const string CannotEditVotingResolutionWithoutSuspension = "CannotEditVotingResolutionWithoutSuspension"; // Business rule violation

        // Activity Counter Validation Messages (JDWA-996)
        public const string FundIdRequired = "FundIdRequired";
        public const string AccessDenied = "AccessDenied";
        public const string RecentActivityDaysInvalid = "RecentActivityDaysInvalid";
        public const string RecentActivityDaysMaxExceeded = "RecentActivityDaysMaxExceeded";
        public const string IncludeDetailsRequired = "IncludeDetailsRequired";
        public const string RecentNotificationDaysInvalid = "RecentNotificationDaysInvalid";
        public const string RecentNotificationDaysMaxExceeded = "RecentNotificationDaysMaxExceeded";
        public const string IncludeFundBreakdownRequired = "IncludeFundBreakdownRequired";
        public const string HighPriorityOnlyRequired = "HighPriorityOnlyRequired";

        // State Pattern Transition Error Messages
        public const string InvalidStatusTransition = "InvalidStatusTransition";
        public const string CannotTransitionFromCurrentState = "CannotTransitionFromCurrentState";
        public const string CannotEditInCurrentState = "CannotEditInCurrentState";
        public const string CannotCompleteInCurrentState = "CannotCompleteInCurrentState";
        public const string CannotCancelInCurrentState = "CannotCancelInCurrentState";
        public const string ResolutionStateValidationFailed = "ResolutionStateValidationFailed";

        // User Language Management Messages
        public const string InvalidCultureCode = "InvalidCultureCode";
        public const string PreferredLanguageUpdatedSuccessfully = "PreferredLanguageUpdatedSuccessfully";
        public const string Unauthorized = "Unauthorized";

        // State-Specific Messages
        public const string ResolutionInDraftState = "ResolutionInDraftState";
        public const string ResolutionInPendingState = "ResolutionInPendingState";
        public const string ResolutionInCompletingDataState = "ResolutionInCompletingDataState";
        public const string ResolutionInWaitingForConfirmationState = "ResolutionInWaitingForConfirmationState";
        public const string ResolutionInConfirmedState = "ResolutionInConfirmedState";
        public const string ResolutionInRejectedState = "ResolutionInRejectedState";
        public const string ResolutionInVotingInProgressState = "ResolutionInVotingInProgressState";
        public const string ResolutionInApprovedState = "ResolutionInApprovedState";
        public const string ResolutionInNotApprovedState = "ResolutionInNotApprovedState";
        public const string ResolutionInCancelledState = "ResolutionInCancelledState";

        // Exit Confirmation Messages (MSG005 - Confirmation type)
        public const string ExitWithoutSavingConfirmation = "ExitWithoutSavingConfirmation"; // MSG005 - Exit confirmation

        // Resolution Edit Notification Messages (MSG005 - Notification type)
        // Note: ResolutionUpdatedNotificationBody is already defined above for MSG005 notifications

        // Meeting Time Proposal Messages (User Story 1)
        public const string MeetingTimeProposalCreatedSuccessfully = "MeetingTimeProposalCreatedSuccessfully"; // MSG-MTV-SUC-01
        public const string MeetingSubjectRequired = "MeetingSubjectRequired"; // MSG-MTV-ERR-01
        public const string AtLeastOneProposedTimeRequired = "AtLeastOneProposedTimeRequired"; // MSG-MTV-ERR-02
        public const string InvalidFileTypePdfOnly = "InvalidFileTypePdfOnly"; // MSG-MTV-ERR-03
        public const string FileUploadFailed = "FileUploadFailed"; // MSG-MTV-ERR-04
        public const string ConfirmDiscardChanges = "ConfirmDiscardChanges"; // MSG-MTV-WRN-01
        public const string NewVoteStartedNotificationTitle = "NewVoteStartedNotificationTitle"; // MSG-MTV-NOT-01
        public const string NewVoteStartedNotificationBody = "NewVoteStartedNotificationBody"; // MSG-MTV-NOT-01

        // Meeting Time Vote Messages (User Story 2)
        public const string VoteSubmittedSuccessfully = "VoteSubmittedSuccessfully"; // MSG-VMT-SUC-01
        public const string SelectAtLeastOneOption = "SelectAtLeastOneOption"; // MSG-VMT-ERR-01
        public const string VotingCompletedNotificationTitle = "VotingCompletedNotificationTitle"; // MSG-VMT-NOT-01
        public const string VotingCompletedNotificationBody = "VotingCompletedNotificationBody"; // MSG-VMT-NOT-01
        public const string VotingForProposalComplete = "VotingForProposalComplete"; // MSG-VMT-INF-01
        public const string AlreadyVotedOnProposal = "AlreadyVotedOnProposal"; // MSG-VMT-INF-02

        // Additional validation messages for meetings
        public const string FutureDateRequired = "FutureDateRequired";
        public const string DuplicateSelection = "DuplicateSelection";

        // Meeting Status Descriptions
        public const string MeetingStatusScheduled = "MeetingStatusScheduled";
        public const string MeetingStatusInProgress = "MeetingStatusInProgress";
        public const string MeetingStatusFinished = "MeetingStatusFinished";
        public const string MeetingStatusCancelled = "MeetingStatusCancelled";
        public const string MeetingStatusPostponed = "MeetingStatusPostponed";

        // Meeting State Validation Messages
        public const string MeetingScheduledValidationMessage = "MeetingScheduledValidationMessage";
        public const string MeetingNotifyAttendeesMessage = "MeetingNotifyAttendeesMessage";
        public const string MeetingInProgressValidationMessage = "MeetingInProgressValidationMessage";
        public const string MeetingLiveFeaturesMessage = "MeetingLiveFeaturesMessage";
        public const string MeetingFinishedValidationMessage = "MeetingFinishedValidationMessage";
        public const string MeetingMinutesCanBeCreatedMessage = "MeetingMinutesCanBeCreatedMessage";
        public const string MeetingCancelledValidationMessage = "MeetingCancelledValidationMessage";
        public const string MeetingPostponedValidationMessage = "MeetingPostponedValidationMessage";
        public const string MeetingRescheduleMessage = "MeetingRescheduleMessage";

        // Audit Action Localization Keys for Resolution Status History
        public const string AuditActionResolutionCreation = "AuditActionResolutionCreation";
        public const string AuditActionResolutionEdit = "AuditActionResolutionEdit";
        public const string AuditActionResolutionCompletion = "AuditActionResolutionCompletion";
        public const string AuditActionResolutionDataUpdate = "AuditActionResolutionDataUpdate";
        public const string AuditActionResolutionVoteSuspend = "AuditActionResolutionVoteSuspend";
        public const string AuditActionResolutionConfirmation = "AuditActionResolutionConfirmation";
        public const string AuditActionResolutionRejection = "AuditActionResolutionRejection";
        public const string AuditActionResolutionSentToVote = "AuditActionResolutionSentToVote";
        public const string AuditActionResolutionCancellation = "AuditActionResolutionCancellation";
        public const string AuditActionResolutionApproved = "AuditActionResolutionApproved";
        public const string AuditActionResolutionUnApproved = "AuditActionResolutionUnApproved";
        public const string AuditActionResolutionDeletion = "AuditActionResolutionDeletion";

        // Resolution Status Localization Keys for Audit History Display
        public const string ResolutionStatusDraft = "ResolutionStatusDraft";
        public const string ResolutionStatusPending = "ResolutionStatusPending";
        public const string ResolutionStatusCompletingData = "ResolutionStatusCompletingData";
        public const string ResolutionStatusWaitingForConfirmation = "ResolutionStatusWaitingForConfirmation";
        public const string ResolutionStatusConfirmed = "ResolutionStatusConfirmed";
        public const string ResolutionStatusVotingInProgress = "ResolutionStatusVotingInProgress";
        public const string ResolutionStatusApproved = "ResolutionStatusApproved";
        public const string ResolutionStatusNotApproved = "ResolutionStatusNotApproved";
        public const string ResolutionStatusRejected = "ResolutionStatusRejected";
        public const string ResolutionStatusCancelled = "ResolutionStatusCancelled";



        public const string SystemErrorRetrievingData = "SystemErrorRetrievingData";
        public const string NoRecords = "NoRecords";

        // Sprint 3 User Management Message Codes

        // User Profile Management Messages (MSG-PROFILE-001 to MSG-PROFILE-009)
        public const string ProfileRequiredField = "ProfileRequiredField"; // MSG-PROFILE-001
        public const string ProfileInvalidEmailFormat = "ProfileInvalidEmailFormat"; // MSG-PROFILE-002
        public const string ProfileDuplicateEmail = "ProfileDuplicateEmail"; // MSG-PROFILE-003
        public const string ProfileInvalidCountryCode = "ProfileInvalidCountryCode"; // MSG-PROFILE-004
        public const string ProfileMobileAlreadyInUse = "ProfileMobileAlreadyInUse"; // MSG-PROFILE-005
        public const string ProfileInvalidCVFile = "ProfileInvalidCVFile"; // MSG-PROFILE-006
        public const string ProfileUpdatedSuccessfully = "ProfileUpdatedSuccessfully"; // MSG-PROFILE-007
        public const string ProfileSystemErrorSavingData = "ProfileSystemErrorSavingData"; // MSG-PROFILE-008
        public const string ProfileInvalidPhotoFile = "ProfileInvalidPhotoFile"; // MSG-PROFILE-009
        public const string ProfileFullNameTooLong = "ProfileFullNameTooLong"; // P1-12 BE-1
        public const string ProfileInvalidPhoneFormat = "ProfileInvalidPhoneFormat"; // P1-12 BE-1
        public const string ProfileCountryTooLong = "ProfileCountryTooLong"; // P1-12 BE-1
        public const string ProfileEmailTooLong = "ProfileEmailTooLong"; // QC defect fix — RegisterParent email length bound

        // User Login Messages (MSG-LOGIN-001 to MSG-LOGIN-004)
        public const string LoginUserNotFound = "LoginUserNotFound"; // MSG-LOGIN-001
        public const string LoginIncorrectPassword = "LoginIncorrectPassword"; // MSG-LOGIN-002
        public const string LoginAccountDeactivated = "LoginAccountDeactivated"; // MSG-LOGIN-003
        public const string LoginTooManyFailedAttempts = "LoginTooManyFailedAttempts"; // MSG-LOGIN-004
        public const string LoginInvalidCredentials = "LoginInvalidCredentials"; // MSG-LOGIN-005
        public const string LoginSystemError = "LoginSystemError"; // MSG-LOGIN-006

        // User Logout Messages (MSG-LOGOUT-001 to MSG-LOGOUT-002)
        public const string LogoutSuccessful = "LogoutSuccessful"; // MSG-LOGOUT-001
        public const string LogoutSystemError = "LogoutSystemError"; // MSG-LOGOUT-002

        // Session Timeout Messages (MSG-SESSION-001 to MSG-SESSION-015)
        public const string SessionExpiredWarning = "SessionExpiredWarning"; // MSG-SESSION-001
        public const string SessionExpiredTitle = "SessionExpiredTitle"; // MSG-SESSION-002
        public const string SessionExpiredMessage = "SessionExpiredMessage"; // MSG-SESSION-003
        public const string SessionExtendedSuccessfully = "SessionExtendedSuccessfully"; // MSG-SESSION-004
        public const string SessionExtensionFailed = "SessionExtensionFailed"; // MSG-SESSION-005
        public const string SessionExtensionInvalidToken = "SessionExtensionInvalidToken"; // MSG-SESSION-006
        public const string SessionExtensionSystemError = "SessionExtensionSystemError"; // MSG-SESSION-007
        public const string SessionNotFound = "SessionNotFound"; // MSG-SESSION-008
        public const string SessionStatusInvalidToken = "SessionStatusInvalidToken"; // MSG-SESSION-009
        public const string SessionStatusSystemError = "SessionStatusSystemError"; // MSG-SESSION-010
        public const string SessionWarningExtendButton = "SessionWarningExtendButton"; // MSG-SESSION-011
        public const string SessionWarningLogoutButton = "SessionWarningLogoutButton"; // MSG-SESSION-012
        public const string SessionWarningContinueButton = "SessionWarningContinueButton"; // MSG-SESSION-013
        public const string SessionTimeoutConfigRetrieved = "SessionTimeoutConfigRetrieved"; // MSG-SESSION-014
        public const string SessionActivityUpdated = "SessionActivityUpdated"; // MSG-SESSION-015

        // Session Audit Messages (MSG-AUDIT-001 to MSG-AUDIT-010)
        public const string SessionCreatedAuditNote = "SessionCreatedAuditNote"; // MSG-AUDIT-001
        public const string SessionExtendedAuditNote = "SessionExtendedAuditNote"; // MSG-AUDIT-002
        public const string SessionTerminatedAuditNote = "SessionTerminatedAuditNote"; // MSG-AUDIT-003
        public const string SessionValidationFailedAuditNote = "SessionValidationFailedAuditNote"; // MSG-AUDIT-004
        public const string SecurityViolationAuditNote = "SecurityViolationAuditNote"; // MSG-AUDIT-005
        public const string ConcurrentSessionLimitExceededAuditNote = "ConcurrentSessionLimitExceededAuditNote"; // MSG-AUDIT-006
        public const string SessionActivityAuditNote = "SessionActivityAuditNote"; // MSG-AUDIT-007

        // Assessment Status Localization Keys
        public const string AssessmentStatusDraft = "AssessmentStatusDraft";
        public const string AssessmentStatusWaitingForApproval = "AssessmentStatusWaitingForApproval";
        public const string AssessmentStatusApproved = "AssessmentStatusApproved";
        public const string AssessmentStatusRejected = "AssessmentStatusRejected";
        public const string AssessmentStatusActive = "AssessmentStatusActive";
        public const string AssessmentStatusCompleted = "AssessmentStatusCompleted";

        // Assessment Type Localization Keys
        public const string AssessmentTypeQuestionnaire = "AssessmentTypeQuestionnaire";
        public const string AssessmentTypeAttachment = "AssessmentTypeAttachment";

        // Question Type Localization Keys
        public const string QuestionTypeSingleChoice = "QuestionTypeSingleChoice";
        public const string QuestionTypeText = "QuestionTypeText";

        // Assessment Validation Messages
        public const string AssessmentTitleRequired = "AssessmentTitleRequired";
        public const string AssessmentTypeRequired = "AssessmentTypeRequired";
        public const string AtLeastOneQuestionRequired = "AtLeastOneQuestionRequired";
        public const string AttachmentRequired = "AttachmentRequired";
       
        public const string AllRequiredQuestionsRequired = "AllRequiredQuestionsRequired";
        public const string QuestionTextRequired = "QuestionTextRequired";
        public const string QuestionOptionsRequired = "QuestionOptionsRequired";

        // Assessment Success Messages
        public const string AssessmentSubmittedForApproval = "AssessmentSubmittedForApproval";
        public const string AssessmentSavedAsDraft = "AssessmentSavedAsDraft";
        public const string AssessmentApproved = "AssessmentApproved";
        public const string AssessmentRejected = "AssessmentRejected";
        public const string AssessmentDistributed = "AssessmentDistributed";
        public const string ResponseSubmittedSuccessfully = "ResponseSubmittedSuccessfully";

        // Assessment Notification Messages
        public const string NewAssessmentWaitingApproval = "NewAssessmentWaitingApproval";
        public const string AssessmentApprovedNotification = "AssessmentApprovedNotification";
        public const string AssessmentRejectedNotification = "AssessmentRejectedNotification";
        public const string NewAssessmentReadyForResponse = "NewAssessmentReadyForResponse";
        public const string AssessmentDistributionConfirmation = "AssessmentDistributionConfirmation";

 
        public const string AssessmentNotFound = "AssessmentNotFound";
        public const string UnauthorizedAssessmentAccess = "UnauthorizedAssessmentAccess";
        public const string AssessmentAlreadyResponded = "AssessmentAlreadyResponded";
        public const string AssessmentNotActive = "AssessmentNotActive";
        public const string SessionCleanupAuditNote = "SessionCleanupAuditNote"; // MSG-AUDIT-008
        public const string RoleBasedTimeoutAppliedAuditNote = "RoleBasedTimeoutAppliedAuditNote"; // MSG-AUDIT-009
        public const string RememberMeSessionCreatedAuditNote = "RememberMeSessionCreatedAuditNote"; // MSG-AUDIT-010

        // Password Management Messages (MSG-PROFILE-PW-001 to MSG-PROFILE-PW-006)
        public const string PasswordIncorrectCurrent = "PasswordIncorrectCurrent"; // MSG-PROFILE-PW-001
        public const string PasswordComplexityError = "PasswordComplexityError"; // MSG-PROFILE-PW-002
        public const string PasswordMismatch = "PasswordMismatch"; // MSG-PROFILE-PW-003
        public const string PasswordSameAsCurrent = "PasswordSameAsCurrent"; // MSG-PROFILE-PW-004
        public const string PasswordChangedSuccessfully = "PasswordChangedSuccessfully"; // MSG-PROFILE-PW-005
        public const string PasswordChangeSystemError = "PasswordChangeSystemError"; // MSG-PROFILE-PW-006

        // User Administration Messages
 
        public const string UserPasswordResetSuccessfully = "UserPasswordResetSuccessfully";
        public const string RegistrationMessageSentSuccessfully = "RegistrationMessageSentSuccessfully";
        public const string UserNotEligibleForRegistrationMessage = "UserNotEligibleForRegistrationMessage";
        public const string UserAlreadyActive = "UserAlreadyActive";
        public const string UserAlreadyInactive = "UserAlreadyInactive";

        // User Validation Messages
        public const string InvalidSaudiMobileFormat = "InvalidSaudiMobileFormat";
        public const string InvalidIBANFormat = "InvalidIBANFormat";
        public const string InvalidFileSize = "InvalidFileSize";
        public const string UnauthorizedUserAccess = "UnauthorizedUserAccess";
        public const string UserUpdatedSuccessfully = "UserUpdatedSuccessfully";
        public const string UserAddedSuccessfully = "UserAddedSuccessfully";

        // MinIO File Management Messages
        public const string MinIORequestCannotBeBlank = "MinIORequestCannotBeBlank";
        public const string MinIOFileMissingOrEmpty = "MinIOFileMissingOrEmpty";
        public const string MinIOStorageNotEnabled = "MinIOStorageNotEnabled";
        public const string MinIOInvalidFileNameOrExtension = "MinIOInvalidFileNameOrExtension";
        public const string MinIOFileUploadFailed = "MinIOFileUploadFailed";
        public const string MinIOFileUploadedSuccessfully = "MinIOFileUploadedSuccessfully";
        public const string MinIOFileNotFound = "MinIOFileNotFound";
        public const string MinIOFileNotFoundInStorage = "MinIOFileNotFoundInStorage";
        public const string MinIOPreviewUrlGenerationFailed = "MinIOPreviewUrlGenerationFailed";
        public const string MinIOPreviewUrlGeneratedSuccessfully = "MinIOPreviewUrlGeneratedSuccessfully";
        public const string MinIOFileDeletedSuccessfully = "MinIOFileDeletedSuccessfully";
        public const string MinIOFileDeleteFailed = "MinIOFileDeleteFailed";
        public const string MinIONoFilesProvided = "MinIONoFilesProvided";
        public const string MinIOTooManyFiles = "MinIOTooManyFiles";
        public const string MinIOFileNameCountMismatch = "MinIOFileNameCountMismatch";
        public const string MinIOFileNullOrEmpty = "MinIOFileNullOrEmpty";
        public const string MinIOFileSizeExceedsLimit = "MinIOFileSizeExceedsLimit";
        public const string MinIOInvalidBucketName = "MinIOInvalidBucketName";
        public const string MinIOFileIdRequired = "MinIOFileIdRequired";
        public const string MinIOFileIdMustBeGreaterThanZero = "MinIOFileIdMustBeGreaterThanZero";
        public const string MinIOFileNameTooLong = "MinIOFileNameTooLong";
        public const string MinIOModuleIdMustBeGreaterThanZero = "MinIOModuleIdMustBeGreaterThanZero";
        public const string MinIOMaxFilesExceeded = "MinIOMaxFilesExceeded";
        public const string MinIOExpiryTimeInvalid = "MinIOExpiryTimeInvalid";

        // Mobile Number Validation Messages
        public const string MobileNumberRequired = "MobileNumberRequired";
        public const string InvalidSaudiMobilePattern = "InvalidSaudiMobilePattern";

        // Authentication Validation Messages
        public const string LoginUsernameRequired = "LoginUsernameRequired";
        public const string LoginPasswordRequired = "LoginPasswordRequired";
        public const string UsernameAlreadyInUse = "UsernameAlreadyInUse";

        // Google Social Sign-In Messages (P1-12 BE-5)
        public const string GoogleIdTokenRequired = "GoogleIdTokenRequired";
        public const string GoogleSignInFailed = "GoogleSignInFailed";
        public const string GoogleEmailNotVerified = "GoogleEmailNotVerified";

        // Password Validation Messages
        public const string PasswordMinimumLength = "PasswordMinimumLength";

        // Profile Field Validation Messages
        public const string PassportNumberAlphanumeric = "PassportNumberAlphanumeric";

        // Edit User Messages (MSG-EDIT-001 to MSG-EDIT-014)
        public const string EditUserInvalidRoleSelection = "EditUserInvalidRoleSelection"; // MSG-EDIT-004
        public const string EditUserInvalidCVFile = "EditUserInvalidCVFile"; // MSG-EDIT-008
        public const string EditUserRoleReplacementConfirmation = "EditUserRoleReplacementConfirmation"; // MSG-EDIT-010
        public const string EditUserCannotChangeBoardMemberRole = "EditUserCannotChangeBoardMemberRole"; // MSG-EDIT-011
        public const string EditUserCannotChangeFundManagerRole = "EditUserCannotChangeFundManagerRole"; // MSG-EDIT-012
        public const string EditUserRelieveOfDutiesNotification = "EditUserRelieveOfDutiesNotification"; // MSG-EDIT-013
        public const string EditUserRoleUpdateNotification = "EditUserRoleUpdateNotification"; // MSG-EDIT-014

        // Edit User Notification Titles
        public const string EditUserRelieveOfDutiesNotificationTitle = "EditUserRelieveOfDutiesNotificationTitle";
        public const string EditUserRoleUpdateNotificationTitle = "EditUserRoleUpdateNotificationTitle";

        // Unique Role Validation Messages
        public const string RoleConflictDetected = "RoleConflictDetected";
        public const string RoleConflictReplacePrompt = "RoleConflictReplacePrompt";
        public const string RoleConflictSelectDifferent = "RoleConflictSelectDifferent";
        public const string AtLeastOneRoleRequired = "AtLeastOneRoleRequired";
        public const string UniqueRoleAlreadyAssigned = "UniqueRoleAlreadyAssigned";

        // Role List Messages
        public const string NotFoundRoles = "NotFoundRoles";

 

        // Additional Assessment Validation Messages
        public const string AssessmentTitleMaxLength = "AssessmentTitleMaxLength";
        public const string AssessmentWaitingTooLong = "AssessmentWaitingTooLong";
        public const string AssessmentReadyForDistribution = "AssessmentReadyForDistribution";
        public const string AssessmentCannotBeModified = "AssessmentCannotBeModified";
        public const string AssessmentResponsesReceived = "AssessmentResponsesReceived";
        public const string AssessmentCompletionReady = "AssessmentCompletionReady";

        // Assessment Action Keys
        public const string AssessmentSubmitForApproval = "AssessmentSubmitForApproval";
        public const string AssessmentApprove = "AssessmentApprove";
        public const string AssessmentReject = "AssessmentReject";
        public const string AssessmentDistribute = "AssessmentDistribute";
        public const string AssessmentComplete = "AssessmentComplete";
        public const string AssessmentEdit = "AssessmentEdit";
        public const string AssessmentViewDetails = "AssessmentViewDetails";
        public const string AssessmentDelete = "AssessmentDelete";
        public const string AssessmentSave = "AssessmentSave";
        public const string AssessmentRespond = "AssessmentRespond";

        // Assessment Review Messages
        public const string ReviewerInformationRequired = "ReviewerInformationRequired";
        public const string ReviewDateRequired = "ReviewDateRequired";
        public const string FundBoardMembersRequired = "FundBoardMembersRequired";

        // Additional Validation Messages
        public const string InvalidAttachmentId = "InvalidAttachmentId";
        public const string QuestionsNotAllowedForAttachment = "QuestionsNotAllowedForAttachment";
        public const string QuestionDisplayOrdersUnique = "QuestionDisplayOrdersUnique";

        // Question Validation Messages
        public const string QuestionTextMaxLength = "QuestionTextMaxLength";
        public const string QuestionTypeRequired = "QuestionTypeRequired";
        public const string QuestionDisplayOrderRequired = "QuestionDisplayOrderRequired";
        public const string QuestionOptionsMinimumTwo = "QuestionOptionsMinimumTwo";
        public const string QuestionOptionsNotAllowedForText = "QuestionOptionsNotAllowedForText";

        // Assessment Notification Resource Keys
        public const string AssessmentSubmittedForApprovalNotificationTitle = "AssessmentSubmittedForApprovalNotificationTitle";
        public const string AssessmentSubmittedForApprovalNotificationBody = "AssessmentSubmittedForApprovalNotificationBody";
        public const string AssessmentApprovedNotificationTitle = "AssessmentApprovedNotificationTitle";
        public const string AssessmentApprovedNotificationBody = "AssessmentApprovedNotificationBody";
        public const string AssessmentRejectedNotificationTitle = "AssessmentRejectedNotificationTitle";
        public const string AssessmentRejectedNotificationBody = "AssessmentRejectedNotificationBody";
        public const string AssessmentDistributedNotificationTitle = "AssessmentDistributedNotificationTitle";
        public const string AssessmentDistributedNotificationBody = "AssessmentDistributedNotificationBody";
        public const string AssessmentCompletedNotificationTitle = "AssessmentCompletedNotificationTitle";
        public const string AssessmentCompletedNotificationBody = "AssessmentCompletedNotificationBody";

        // Assessment State Transition Messages
        public const string AssessmentStatusTransitionMessage = "AssessmentStatusTransitionMessage";

        // Assessment State Information Messages
        public const string AssessmentPendingResponsesInfo = "AssessmentPendingResponsesInfo";
        public const string AssessmentAllResponsesReceivedInfo = "AssessmentAllResponsesReceivedInfo";
        public const string AssessmentNoResponsesWarning = "AssessmentNoResponsesWarning";
        public const string AssessmentCompletionStatistics = "AssessmentCompletionStatistics";
        public const string AssessmentNoResponsesReceived = "AssessmentNoResponsesReceived";
        public const string AssessmentNoResponsesExist = "AssessmentNoResponsesExist";

        // Assessment Action Keys (Additional)
        public const string AssessmentViewRejectionReason = "AssessmentViewRejectionReason";
        public const string AssessmentResubmit = "AssessmentResubmit";
        public const string AssessmentViewResponses = "AssessmentViewResponses";
        public const string AssessmentViewResults = "AssessmentViewResults";
        public const string AssessmentCompleteAssessment = "AssessmentCompleteAssessment";
        public const string AssessmentExportResults = "AssessmentExportResults";
        public const string AssessmentExportData = "AssessmentExportData";
        public const string AssessmentArchive = "AssessmentArchive";

        // Parent-Child Linkage Messages (P1-04)
        // Single generic, non-enumerating message for ALL link-child failures (non-existent email,
        // not a student, already linked to another family) — AC-5 / AC-7. Do not split into
        // specific reasons; differing messages would leak whether an email exists.
        public const string CannotLinkChild = "CannotLinkChild";
        public const string ChildLinkedSuccessfully = "ChildLinkedSuccessfully";
        // Returned (HTTP 409 Conflict) when a parent re-links a child that is already in THEIR
        // OWN family. This is not an enumeration risk: the parent knows they own this child.
        public const string ChildAlreadyLinked = "ChildAlreadyLinked";

        // Add-Child Validation Messages (P1-03)
        public const string GradeOutOfRange = "GradeOutOfRange";
        public const string InvalidLanguageCode = "InvalidLanguageCode";

        // Register consent (P1-12 BE-9): registration requires accepting the terms of service.
        public const string TermsConsentRequired = "TermsConsentRequired";

        // Anti-automation CAPTCHA on register (P1-13 BE-4): token missing or failed server-side verification.
        public const string CaptchaVerificationFailed = "CaptchaVerificationFailed";

        // Edit-Child family scope (P1-12 BE-8): parent may only edit a child in their own family.
        public const string CannotEditChildNotInFamily = "CannotEditChildNotInFamily";

        // Unlink-Child last-parent guard (P2-12): cannot remove the only parent linked to a child.
        public const string CannotUnlinkLastParent = "CannotUnlinkLastParent";

        // Avatar upload/remove (P1-12 BE-4).
        public const string AvatarFileRequired = "AvatarFileRequired";
        public const string AvatarFileTooLarge = "AvatarFileTooLarge";
        public const string AvatarFileInvalidType = "AvatarFileInvalidType";
        public const string AvatarUploadFailed = "AvatarUploadFailed";
        public const string AvatarUploadedSuccessfully = "AvatarUploadedSuccessfully";
        public const string AvatarRemovedSuccessfully = "AvatarRemovedSuccessfully";

        // Self-service password reset (P1-12 BE-6). The forgot-password response is intentionally generic
        // (no enumeration); the invalid-link message covers BOTH unknown-account and bad/expired-token.
        public const string ForgotPasswordGenericResponse = "ForgotPasswordGenericResponse";
        public const string ResetPasswordInvalidLink = "ResetPasswordInvalidLink";
        public const string ResetPasswordSuccessful = "ResetPasswordSuccessful";

        // Account settings — notification preferences (P2-12 BE-1).
        public const string NotificationPreferencesUpdatedSuccessfully = "NotificationPreferencesUpdatedSuccessfully";
        public const string NotificationPreferencesRetrievedSuccessfully = "NotificationPreferencesRetrievedSuccessfully";
        public const string NotificationPreferenceInvalidCategory = "NotificationPreferenceInvalidCategory";
        public const string NotificationPreferenceDuplicateCategory = "NotificationPreferenceDuplicateCategory";

        // Account settings — security/sessions (P2-12 BE-3).
        public const string OtherSessionsSignedOutSuccessfully = "OtherSessionsSignedOutSuccessfully";
        public const string SessionsRetrievedSuccessfully = "SessionsRetrievedSuccessfully";

        // Account settings — plan stub (P2-12 PLAN).
        public const string PlanRetrievedSuccessfully = "PlanRetrievedSuccessfully";

        // Browse subjects/lessons — student-facing queries (P2-02).
        public const string GradeNotFound = "GradeNotFound";
        public const string SubjectNotFound = "SubjectNotFound";

        // Quiz/Assessment — start-attempt (P2-06 BE-3/BE-4).
        public const string AttemptStartedSuccessfully = "AttemptStartedSuccessfully";
        public const string AttemptResumedSuccessfully = "AttemptResumedSuccessfully";
        public const string LessonNotFound = "LessonNotFound";
        public const string LessonIdMustBePositive = "LessonIdMustBePositive";

        // Quiz/Assessment — per-type question content validation (P2-06 BE-3).
        public const string McqRequiresAtLeastTwoOptions = "McqRequiresAtLeastTwoOptions";
        public const string McqCorrectAnswerMustBeValidOption = "McqCorrectAnswerMustBeValidOption";
        public const string TrueFalseCorrectAnswerInvalid = "TrueFalseCorrectAnswerInvalid";
        public const string MatchingOptionsMustBePaired = "MatchingOptionsMustBePaired";
        public const string FillInBlankCorrectAnswerRequired = "FillInBlankCorrectAnswerRequired";

        // Quiz/Assessment — submit answer (P2-08 BE-1).
        public const string AttemptNotFound = "AttemptNotFound";
        public const string AttemptNotInProgress = "AttemptNotInProgress";
        public const string QuestionNotFound = "QuestionNotFound";
        public const string QuestionAlreadyAnswered = "QuestionAlreadyAnswered";
        public const string AnswerSubmittedSuccessfully = "AnswerSubmittedSuccessfully";
        public const string AttemptIdMustBePositive = "AttemptIdMustBePositive";
        public const string QuestionIdMustBePositive = "QuestionIdMustBePositive";
        public const string AnswerPayloadRequired = "AnswerPayloadRequired";
        public const string TimeSpentSecondsMustBeNonNegative = "TimeSpentSecondsMustBeNonNegative";
        public const string TimeSpentSecondsExceedsMaximum = "TimeSpentSecondsExceedsMaximum";

        // Quiz/Assessment — complete/abandon attempt (P2-08 BE-2 + BE-3).
        public const string AttemptCompletedSuccessfully = "AttemptCompletedSuccessfully";
        public const string AttemptAbandonedSuccessfully = "AttemptAbandonedSuccessfully";
        public const string AttemptAlreadyAbandoned = "AttemptAlreadyAbandoned";
        public const string AttemptAlreadyCompleted = "AttemptAlreadyCompleted";

        // Quiz/Assessment — read queries (P2-08 BE-4).
        public const string StudentIdMustBePositive = "StudentIdMustBePositive";
        public const string SkillIdMustBePositive = "SkillIdMustBePositive";
        public const string AttemptsRetrievedSuccessfully = "AttemptsRetrievedSuccessfully";
        public const string SkillStatsRetrievedSuccessfully = "SkillStatsRetrievedSuccessfully";

        // Skill dependency graph — node queries (P2-11 BE-5).
        public const string KnowledgeNodeNotFound = "KnowledgeNodeNotFound";

        // Learning Path Engine — P2-04
        public const string LearningPathSubjectNotFound = "LearningPathSubjectNotFound";
        public const string LearningPathUnauthorized = "LearningPathUnauthorized";

        // P4-09 Re-engagement notifications — parent prefs, device tokens, inbox
        public const string ChildPreferencesRetrievedSuccessfully = "ChildPreferencesRetrievedSuccessfully";
        public const string ChildPreferencesUpdatedSuccessfully = "ChildPreferencesUpdatedSuccessfully";
        public const string NotAuthorizedForChild = "NotAuthorizedForChild";
        public const string InvalidTimeZoneId = "InvalidTimeZoneId";
        public const string InvalidDailyCapRange = "InvalidDailyCapRange";
        public const string QuietHoursBothOrNeither = "QuietHoursBothOrNeither";
        public const string QuietHoursStartEndMustDiffer = "QuietHoursStartEndMustDiffer";
        public const string DeviceTokenRegisteredSuccessfully = "DeviceTokenRegisteredSuccessfully";
        public const string DeviceTokenRevokedSuccessfully = "DeviceTokenRevokedSuccessfully";
        public const string DeviceTokenNotFound = "DeviceTokenNotFound";
        public const string DeviceTokenRequired = "DeviceTokenRequired";
        public const string DevicePlatformInvalid = "DevicePlatformInvalid";
        public const string InboxRetrievedSuccessfully = "InboxRetrievedSuccessfully";
        public const string NotificationMarkedReadSuccessfully = "NotificationMarkedReadSuccessfully";
        public const string AllNotificationsMarkedReadSuccessfully = "AllNotificationsMarkedReadSuccessfully";
        public const string NotificationNotFound = "NotificationNotFound";
        public const string NotificationAccessForbidden = "NotificationAccessForbidden";
        public const string NotificationIdRequired = "NotificationIdRequired";

        // P8-03 — learning-language curriculum guard
        /// <summary>
        /// Returned when a student tries to access a lesson whose subject language does not match
        /// the student's resolved effective language for that subject code.
        /// </summary>
        public const string LessonLanguageMismatch = "LessonLanguageMismatch";

        // P8-04 — change a child's learning language (parent-only, fresh start)

        /// <summary>
        /// Returned (HTTP 424 FailedDependency) when the parent calls Change-Learning-Language
        /// without <c>confirmFreshStart = true</c>. No state is changed when this fires.
        /// </summary>
        public const string ConfirmFreshStartRequired = "ConfirmFreshStartRequired";

        /// <summary>Returned on success of Change-Learning-Language.</summary>
        public const string LearningLanguageChangedSuccessfully = "LearningLanguageChangedSuccessfully";

        /// <summary>
        /// Returned when the Identity seam fails to persist the new LearningLanguage value.
        /// </summary>
        public const string LearningLanguageUpdateFailed = "LearningLanguageUpdateFailed";

        // ── P7-01 Subject/Unit admin management ──────────────────────────────────────────────

        /// <summary>Returned when a unit to delete or modify is not found.</summary>
        public const string UnitNotFound = "UnitNotFound";

        /// <summary>
        /// Returned when an admin tries to soft-delete a Unit that still has non-deleted Lessons.
        /// </summary>
        public const string UnitNotEmpty = "UnitNotEmpty";

        /// <summary>
        /// Returned when an admin tries to soft-delete a Subject that still has non-deleted Units.
        /// </summary>
        public const string SubjectNotEmpty = "SubjectNotEmpty";

        /// <summary>
        /// Returned when a Create/Update would produce a duplicate (GradeId, SubjectCode, Language) tree.
        /// </summary>
        public const string SubjectDuplicateTree = "SubjectDuplicateTree";

        /// <summary>
        /// Returned when a soft-deleted tree with the same (GradeId, SubjectCode, Language) natural key
        /// already exists — the admin must restore it instead of creating a new one.
        /// </summary>
        public const string SubjectSoftDeletedTreeExists = "SubjectSoftDeletedTreeExists";

        /// <summary>
        /// Returned when a SubjectCode value is not one of the 4 allowed codes
        /// (MATH, SCIENCE, ARABIC, ENGLISH).
        /// </summary>
        public const string InvalidSubjectCode = "InvalidSubjectCode";

        /// <summary>Returned when a reorder request spans multiple language trees or grades.</summary>
        public const string ReorderCrossTreeForbidden = "ReorderCrossTreeForbidden";

        /// <summary>Returned on successful Subject activation.</summary>
        public const string SubjectActivatedSuccessfully = "SubjectActivatedSuccessfully";

        /// <summary>Returned on successful Subject deactivation.</summary>
        public const string SubjectDeactivatedSuccessfully = "SubjectDeactivatedSuccessfully";

        /// <summary>Returned on successful Unit activation.</summary>
        public const string UnitActivatedSuccessfully = "UnitActivatedSuccessfully";

        /// <summary>Returned on successful Unit deactivation.</summary>
        public const string UnitDeactivatedSuccessfully = "UnitDeactivatedSuccessfully";

        // P7-SEC-5: Reorder list upper-bound guards (subjects: max 12; units: max 200).
        /// <summary>
        /// Returned when the reorder ID list exceeds the allowed upper bound.
        /// (Subjects: 12; Units: 200.)
        /// </summary>
        public const string ReorderListTooLong = "ReorderListTooLong";

        // ── P7-02 Lesson management ────────────────────────────────────────────────

        /// <summary>Returned on successful Lesson activation.</summary>
        public const string LessonActivatedSuccessfully = "LessonActivatedSuccessfully";

        /// <summary>Returned on successful Lesson deactivation.</summary>
        public const string LessonDeactivatedSuccessfully = "LessonDeactivatedSuccessfully";

        // ── P7-02 ContentBlock management ─────────────────────────────────────────

        /// <summary>Returned when a ContentBlock to modify/delete is not found.</summary>
        public const string ContentBlockNotFound = "ContentBlockNotFound";

        /// <summary>Returned when a ContentBlock's payload JSON is empty or missing required fields for its type.</summary>
        public const string ContentBlockPayloadInvalid = "ContentBlockPayloadInvalid";

        /// <summary>Returned when the ContentBlock type is not a valid enum member.</summary>
        public const string ContentBlockTypeInvalid = "ContentBlockTypeInvalid";

        /// <summary>Returned when a Text block is missing the required markdown field.</summary>
        public const string ContentBlockTextPayloadRequired = "ContentBlockTextPayloadRequired";

        /// <summary>Returned when an Image block is missing the required url field.</summary>
        public const string ContentBlockImageUrlRequired = "ContentBlockImageUrlRequired";

        /// <summary>Returned when a Video block is missing the required url field.</summary>
        public const string ContentBlockVideoUrlRequired = "ContentBlockVideoUrlRequired";

        /// <summary>Returned when a Callout block is missing the required variant field.</summary>
        public const string ContentBlockCalloutVariantRequired = "ContentBlockCalloutVariantRequired";

        /// <summary>Returned when a Callout block variant is not one of: info, warning, tip.</summary>
        public const string ContentBlockCalloutVariantInvalid = "ContentBlockCalloutVariantInvalid";

        /// <summary>Returned when a Callout block is missing the required markdown field.</summary>
        public const string ContentBlockCalloutMarkdownRequired = "ContentBlockCalloutMarkdownRequired";

        /// <summary>Returned on successful ContentBlock add.</summary>
        public const string ContentBlockAddedSuccessfully = "ContentBlockAddedSuccessfully";

        /// <summary>Returned on successful ContentBlock update.</summary>
        public const string ContentBlockUpdatedSuccessfully = "ContentBlockUpdatedSuccessfully";

        /// <summary>Returned on successful ContentBlock reorder.</summary>
        public const string ContentBlockReorderedSuccessfully = "ContentBlockReorderedSuccessfully";

        /// <summary>
        /// Returned when a ContentBlock reorder request contains IDs that do not all belong to the same Lesson.
        /// </summary>
        public const string ContentBlockReorderCrossLessonForbidden = "ContentBlockReorderCrossLessonForbidden";

        /// <summary>Returned when EstimatedMinutes is negative.</summary>
        public const string EstimatedMinutesMustBeNonNegative = "EstimatedMinutesMustBeNonNegative";

        // P7-SEC-2 (post security-audit) — ContentBlock payload size + URL safety

        /// <summary>
        /// Returned when the ContentBlock Payload exceeds the maximum allowed length (65536 chars).
        /// </summary>
        public const string ContentBlockPayloadTooLong = "ContentBlockPayloadTooLong";

        /// <summary>
        /// Returned when an Image or Video url field is not a valid absolute HTTPS URI,
        /// uses a disallowed scheme (http/javascript/data/file), or points at a loopback/
        /// link-local/private address. Children's platform — only public HTTPS URLs allowed.
        /// </summary>
        public const string ContentBlockUrlInvalid = "ContentBlockUrlInvalid";

        // ── P7-03 Skill & Knowledge-graph management ──────────────────────────────────────────

        /// <summary>Returned when a Skill to modify or delete is not found.</summary>
        public const string SkillNotFound = "SkillNotFound";

        /// <summary>Returned on successful Skill activation.</summary>
        public const string SkillActivatedSuccessfully = "SkillActivatedSuccessfully";

        /// <summary>Returned on successful Skill deactivation.</summary>
        public const string SkillDeactivatedSuccessfully = "SkillDeactivatedSuccessfully";

        /// <summary>
        /// Returned when auto-creating a KnowledgeNode for a new Skill fails because
        /// the Skill's Concept, Subject, or Grade cannot be resolved.
        /// </summary>
        public const string SkillNodeAutoCreateFailed = "SkillNodeAutoCreateFailed";

        /// <summary>Returned when a KnowledgeEdge to remove is not found.</summary>
        public const string KnowledgeEdgeNotFound = "KnowledgeEdgeNotFound";

        /// <summary>Returned on successful edge add.</summary>
        public const string KnowledgeEdgeAddedSuccessfully = "KnowledgeEdgeAddedSuccessfully";

        /// <summary>Returned on successful edge remove (soft-delete).</summary>
        public const string KnowledgeEdgeRemovedSuccessfully = "KnowledgeEdgeRemovedSuccessfully";

        /// <summary>
        /// Returned when both endpoints of a proposed edge do not belong to the same
        /// language tree (AR ↔ EN cross-language edge rejected).
        /// </summary>
        public const string KnowledgeEdgeCrossLanguageForbidden = "KnowledgeEdgeCrossLanguageForbidden";

        /// <summary>
        /// Returned when a proposed edge would introduce a cycle in the Prerequisite graph.
        /// The message should include the cycle details from SkillGraphValidator.
        /// </summary>
        public const string KnowledgeEdgeWouldCreateCycle = "KnowledgeEdgeWouldCreateCycle";

        /// <summary>
        /// Returned when a (SourceNodeId, TargetNodeId, RelationshipType) triple already exists.
        /// </summary>
        public const string KnowledgeEdgeDuplicate = "KnowledgeEdgeDuplicate";

        /// <summary>
        /// Returned when an edge's source or target node cannot resolve its owning Subject
        /// (dangling SubjectId). Fail-closed guard — never 500.
        /// </summary>
        public const string KnowledgeNodeSubjectNotResolvable = "KnowledgeNodeSubjectNotResolvable";

        /// <summary>Returned when the Strength value is outside the allowed range [0.0, 1.0].</summary>
        public const string KnowledgeEdgeStrengthOutOfRange = "KnowledgeEdgeStrengthOutOfRange";

        /// <summary>Returned on successful graph retrieval.</summary>
        public const string SkillGraphRetrievedSuccessfully = "SkillGraphRetrievedSuccessfully";

        // ── P7-04 Quiz/Question authoring ─────────────────────────────────────────

        /// <summary>Returned when a QuizQuestion to modify/delete is not found.</summary>
        public const string QuizQuestionNotFound = "QuizQuestionNotFound";

        /// <summary>Returned on successful question add.</summary>
        public const string QuizQuestionAddedSuccessfully = "QuizQuestionAddedSuccessfully";

        /// <summary>Returned on successful question update.</summary>
        public const string QuizQuestionUpdatedSuccessfully = "QuizQuestionUpdatedSuccessfully";

        /// <summary>Returned on successful question soft-delete.</summary>
        public const string QuizQuestionDeletedSuccessfully = "QuizQuestionDeletedSuccessfully";

        /// <summary>Returned on successful question reorder.</summary>
        public const string QuizQuestionReorderedSuccessfully = "QuizQuestionReorderedSuccessfully";

        /// <summary>Returned on successful question activation.</summary>
        public const string QuizQuestionActivatedSuccessfully = "QuizQuestionActivatedSuccessfully";

        /// <summary>Returned on successful question deactivation.</summary>
        public const string QuizQuestionDeactivatedSuccessfully = "QuizQuestionDeactivatedSuccessfully";

        /// <summary>Returned when a reorder request spans multiple lessons (cross-lesson reorder forbidden).</summary>
        public const string QuizQuestionReorderCrossLessonForbidden = "QuizQuestionReorderCrossLessonForbidden";

        /// <summary>Returned when QuestionText exceeds the maximum allowed length.</summary>
        public const string QuizQuestionTextTooLong = "QuizQuestionTextTooLong";

        /// <summary>Returned when the Options JSON string exceeds the maximum allowed length.</summary>
        public const string QuizQuestionOptionsTooLong = "QuizQuestionOptionsTooLong";

        /// <summary>Returned when the CorrectAnswer JSON string exceeds the maximum allowed length.</summary>
        public const string QuizQuestionCorrectAnswerTooLong = "QuizQuestionCorrectAnswerTooLong";

        /// <summary>Returned when QuestionText is empty or missing.</summary>
        public const string QuizQuestionTextRequired = "QuizQuestionTextRequired";

        /// <summary>Returned when the QuestionType enum value is invalid.</summary>
        public const string QuizQuestionTypeInvalid = "QuizQuestionTypeInvalid";

        /// <summary>Returned when the DifficultyLevel enum value is invalid.</summary>
        public const string QuizQuestionDifficultyInvalid = "QuizQuestionDifficultyInvalid";

        /// <summary>Returned when the GeneratedBy enum value is invalid.</summary>
        public const string QuizQuestionGeneratedByInvalid = "QuizQuestionGeneratedByInvalid";

        /// <summary>Returned on successful admin questions list retrieval.</summary>
        public const string QuizQuestionsRetrievedSuccessfully = "QuizQuestionsRetrievedSuccessfully";

        /// <summary>
        /// Returned when one or more question IDs in a reorder request do not belong to the supplied LessonId anchor.
        /// </summary>
        public const string QuizQuestionReorderLessonMismatch = "QuizQuestionReorderLessonMismatch";

        // ── P7-05 Content Lifecycle / Versioning ──────────────────────────────────────────

        /// <summary>Returned when a lifecycle transition is illegal (e.g. Archived → Published without going through Draft).</summary>
        public const string IllegalLifecycleTransition = "IllegalLifecycleTransition";

        /// <summary>Returned when a publish operation completes successfully.</summary>
        public const string ContentPublishedSuccessfully = "ContentPublishedSuccessfully";

        /// <summary>Returned when an archive operation completes successfully.</summary>
        public const string ContentArchivedSuccessfully = "ContentArchivedSuccessfully";

        /// <summary>Returned when an unpublish (Published→Draft) operation completes successfully.</summary>
        public const string ContentUnpublishedSuccessfully = "ContentUnpublishedSuccessfully";

        /// <summary>Returned when a rollback operation completes successfully.</summary>
        public const string ContentRolledBackSuccessfully = "ContentRolledBackSuccessfully";

        /// <summary>Returned when the versioned entity (Subject/Unit/Lesson/QuizQuestion) is not found.</summary>
        public const string VersionedEntityNotFound = "VersionedEntityNotFound";

        /// <summary>Returned when the requested ContentVersion (by EntityType+EntityId+VersionNumber) is not found.</summary>
        public const string ContentVersionNotFound = "ContentVersionNotFound";

        /// <summary>Returned when a VersionedEntityType enum value is invalid or unrecognised.</summary>
        public const string InvalidVersionedEntityType = "InvalidVersionedEntityType";

        /// <summary>Returned when version history is retrieved successfully.</summary>
        public const string VersionHistoryRetrievedSuccessfully = "VersionHistoryRetrievedSuccessfully";

        /// <summary>Returned when a preview query is completed successfully.</summary>
        public const string PreviewRetrievedSuccessfully = "PreviewRetrievedSuccessfully";

        /// <summary>Returned when the publication coverage report is retrieved successfully.</summary>
        public const string PublicationCoverageRetrievedSuccessfully = "PublicationCoverageRetrievedSuccessfully";

        /// <summary>Returned when the EntityId is missing or invalid (zero or negative) in a lifecycle command.</summary>
        public const string EntityIdRequired = "EntityIdRequired";

        /// <summary>
        /// Returned by <see cref="RollbackToVersionCommandValidator"/> when VersionNumber is zero or negative.
        /// Replaces the incorrect re-use of <see cref="ContentVersionNotFound"/> on the validation rule.
        /// </summary>
        public const string VersionNumberMustBePositive = "VersionNumberMustBePositive";

        // ── P7-06 Admin User Search & Inspect ────────────────────────────────────────────────

        /// <summary>Returned when the admin user search list is retrieved successfully.</summary>
        public const string AdminUserSearchRetrievedSuccessfully = "AdminUserSearchRetrievedSuccessfully";

        /// <summary>Returned when a single admin user profile is retrieved successfully.</summary>
        public const string AdminUserProfileRetrievedSuccessfully = "AdminUserProfileRetrievedSuccessfully";

        /// <summary>Returned when a user's family linkage is retrieved successfully.</summary>
        public const string AdminUserFamilyRetrievedSuccessfully = "AdminUserFamilyRetrievedSuccessfully";

        /// <summary>Returned when a user's activity summary is retrieved successfully.</summary>
        public const string AdminUserActivityRetrievedSuccessfully = "AdminUserActivityRetrievedSuccessfully";

        /// <summary>Returned when PageNumber is less than 1.</summary>
        public const string AdminUserInvalidPageNumber = "AdminUserInvalidPageNumber";

        /// <summary>Returned when PageSize is outside the allowed range (1–100).</summary>
        public const string AdminUserInvalidPageSize = "AdminUserInvalidPageSize";

        // ── P7-07 Account Lifecycle (suspend / reactivate / delete) ─────────────────

        /// <summary>Returned on successful account suspension.</summary>
        public const string AccountSuspendedSuccessfully = "AccountSuspendedSuccessfully";

        /// <summary>Returned on successful account reactivation.</summary>
        public const string AccountReactivatedSuccessfully = "AccountReactivatedSuccessfully";

        /// <summary>Returned on successful account soft-delete.</summary>
        public const string AccountDeletedSuccessfully = "AccountDeletedSuccessfully";

        /// <summary>
        /// Returned (HTTP 424 FailedDependency) when DeleteAccountCommand is called
        /// without <c>Confirm = true</c>. No state is changed when this fires.
        /// Consistent with P8-04 ConfirmFreshStartRequired pattern.
        /// </summary>
        public const string ConfirmAccountDeletionRequired = "ConfirmAccountDeletionRequired";

        /// <summary>
        /// Returned when a suspend/reactivate/delete operation targets an account that is
        /// already in the Deleted (terminal) state.
        /// </summary>
        public const string AccountAlreadyDeleted = "AccountAlreadyDeleted";

        /// <summary>Returned when an account is already in the Suspended state.</summary>
        public const string AccountAlreadySuspended = "AccountAlreadySuspended";

        /// <summary>
        /// Returned when a reactivate command targets an account that is already Active
        /// (idempotency guard — no mutation or event is emitted for phantom transitions).
        /// </summary>
        public const string AccountAlreadyActive = "AccountAlreadyActive";

        /// <summary>Returned when an admin tries to act on their own account (self-protection guard).</summary>
        public const string CannotActOnOwnAccount = "CannotActOnOwnAccount";

        /// <summary>Returned when the user id in a lifecycle command is missing or not positive.</summary>
        public const string AccountLifecycleUserIdRequired = "AccountLifecycleUserIdRequired";

        /// <summary>Returned when a required reason field is empty.</summary>
        public const string AccountLifecycleReasonRequired = "AccountLifecycleReasonRequired";

        /// <summary>Returned when the reason exceeds the maximum allowed length (500 chars).</summary>
        public const string AccountLifecycleReasonTooLong = "AccountLifecycleReasonTooLong";

        /// <summary>
        /// Returned when a reactivate command targets an account that is in the Deleted state
        /// (deleted is a terminal state — cannot be reactivated via this path).
        /// </summary>
        public const string CannotReactivateDeletedAccount = "CannotReactivateDeletedAccount";

        /// <summary>Generic server-error for account lifecycle operations.</summary>
        public const string AccountLifecycleSystemError = "AccountLifecycleSystemError";

        /// <summary>Returned when a lifecycle operation targets a SuperAdmin account.</summary>
        public const string CannotActOnSuperAdminAccount = "CannotActOnSuperAdminAccount";

        // ── P7-08 Child Profile & Grade Override ─────────────────────────────────────

        /// <summary>Returned on successful child profile update (PreferredLanguage + country).</summary>
        public const string ChildProfileUpdatedSuccessfully = "ChildProfileUpdatedSuccessfully";

        /// <summary>Returned when the target user is not a child (Student role required).</summary>
        public const string TargetUserIsNotAChild = "TargetUserIsNotAChild";

        /// <summary>Returned when the child id in a P7-08 command is missing or not positive.</summary>
        public const string ChildIdRequired = "ChildIdRequired";

        /// <summary>Returned on successful grade override (non-destructive, history preserved).</summary>
        public const string ChildGradeOverriddenSuccessfully = "ChildGradeOverriddenSuccessfully";

        /// <summary>Returned when OverrideChildGradeCommand.Confirm is false (soft UX guard, HTTP 400).</summary>
        public const string ChildGradeOverrideConfirmRequired = "ChildGradeOverrideConfirmRequired";

        /// <summary>Returned when the new grade is the same as the current grade (no-op rejected).</summary>
        public const string ChildGradeUnchanged = "ChildGradeUnchanged";

        /// <summary>Returned when grade value is outside the allowed range (1–6).</summary>
        public const string ChildGradeOutOfRange = "ChildGradeOutOfRange";

        /// <summary>Returned when the grade override reason exceeds max length (500 chars).</summary>
        public const string ChildGradeOverrideReasonTooLong = "ChildGradeOverrideReasonTooLong";

        /// <summary>Generic server-error for child profile/grade/language operations.</summary>
        public const string ChildAdminOperationSystemError = "ChildAdminOperationSystemError";

        // ── P7-12 Audit Log (Moderation module) ─────────────────────────────────────

        /// <summary>Returned when the audit log is retrieved successfully.</summary>
        public const string AuditLogRetrievedSuccessfully = "AuditLogRetrievedSuccessfully";

        /// <summary>Returned when an audit log entry is not found.</summary>
        public const string AuditLogNotFound = "AuditLogNotFound";

        // ── P7-13 Gamification Admin Overrides ──────────────────────────────────────

        /// <summary>
        /// Returned when a destructive gamification admin command is called without <c>Confirm = true</c>.
        /// </summary>
        public const string GamificationConfirmRequired = "GamificationConfirmRequired";

        /// <summary>Returned when the student's XpProfile is not found.</summary>
        public const string GamificationProfileNotFound = "GamificationProfileNotFound";

        /// <summary>
        /// Returned when the requested league-tier override is the same as the student's current tier
        /// (no-op guard — reject so audit trail reflects only real changes).
        /// </summary>
        public const string GamificationLeagueTierNoOp = "GamificationLeagueTierNoOp";

        /// <summary>
        /// Returned when a streak-freeze grant is requested but the student is already at MaxFreezes.
        /// </summary>
        public const string GamificationFreezeBalanceAtMax = "GamificationFreezeBalanceAtMax";

        /// <summary>Returned when a BadgeDefinition with the requested Id is not found.</summary>
        public const string GamificationBadgeNotFound = "GamificationBadgeNotFound";

        /// <summary>Returned when a badge Code is already taken by another BadgeDefinition.</summary>
        public const string GamificationBadgeCodeAlreadyExists = "GamificationBadgeCodeAlreadyExists";

        /// <summary>Returned when a MissionDefinition with the requested Id is not found.</summary>
        public const string GamificationMissionNotFound = "GamificationMissionNotFound";

        /// <summary>Returned when a mission Code is already taken by another MissionDefinition.</summary>
        public const string GamificationMissionCodeAlreadyExists = "GamificationMissionCodeAlreadyExists";

        /// <summary>Returned when a TimedEvent with the requested Id is not found.</summary>
        public const string GamificationTimedEventNotFound = "GamificationTimedEventNotFound";

        /// <summary>Returned when a timed-event Code is already taken by another TimedEvent.</summary>
        public const string GamificationTimedEventCodeAlreadyExists = "GamificationTimedEventCodeAlreadyExists";

        /// <summary>Returned when an admin tries to activate a TimedEvent that is already active.</summary>
        public const string GamificationTimedEventAlreadyActive = "GamificationTimedEventAlreadyActive";

        /// <summary>Returned when an admin tries to expire a TimedEvent that is already inactive.</summary>
        public const string GamificationTimedEventAlreadyInactive = "GamificationTimedEventAlreadyInactive";

        /// <summary>
        /// Returned when <c>SetBadgeDefinitionActive</c> is called but the badge's <c>IsActive</c>
        /// already equals the requested value — no-op guard to suppress spurious audit rows (F6).
        /// </summary>
        public const string GamificationBadgeAlreadyInRequestedState = "GamificationBadgeAlreadyInRequestedState";

        /// <summary>
        /// Returned when <c>SetMissionDefinitionActive</c> is called but the mission's <c>IsActive</c>
        /// already equals the requested value — no-op guard to suppress spurious audit rows (F6).
        /// </summary>
        public const string GamificationMissionAlreadyInRequestedState = "GamificationMissionAlreadyInRequestedState";

    }
}
