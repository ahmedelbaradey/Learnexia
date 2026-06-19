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
        /// <summary>CO-BE-2: structured (JSON-looking) answer payload that fails to parse → 422.</summary>
        public const string AnswerPayloadMalformedJson = "AnswerPayloadMalformedJson";
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
        // Generic 403 for attempts reads (owning student or linked parent only) — anti-enumeration:
        // same message whether the student id is unknown or simply not linked to the caller.
        public const string AttemptsAccessForbidden = "AttemptsAccessForbidden";

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

        /// <summary>Returned when a Concept referenced by a parent-ID FK is not found (e.g. AddSkill with bad ConceptId).</summary>
        public const string ConceptNotFound = "ConceptNotFound";

        // ── QC-DEFECT fixes (Phase-2 backend QC pass) ─────────────────────────────────────────

        /// <summary>
        /// Generic validation-failed label used by ErrorHandlerMiddleWare for the FluentValidation
        /// exception path (replaces the previous bare string literal "Validation Failed").
        /// </summary>
        public const string ValidationFailed = "ValidationFailed";

        /// <summary>
        /// Returned by ErrorHandlerMiddleWare as defense-in-depth when a DbUpdateException
        /// carries a unique-constraint violation (SqlState 23505) that escaped the handler's
        /// own pre-check (e.g. a race condition). Returns HTTP 422 with this message.
        /// </summary>
        public const string UniqueConstraintViolation = "UniqueConstraintViolation";

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

        // ── P7-09 Moderation Queue ────────────────────────────────────────────────

        /// <summary>Returned when the moderation queue list is retrieved successfully.</summary>
        public const string ModerationQueueRetrievedSuccessfully = "ModerationQueueRetrievedSuccessfully";

        /// <summary>Returned when a single moderation item is retrieved successfully.</summary>
        public const string ModerationItemRetrievedSuccessfully = "ModerationItemRetrievedSuccessfully";

        /// <summary>Returned when a moderation item is not found by id.</summary>
        public const string ModerationItemNotFound = "ModerationItemNotFound";

        /// <summary>Returned when a review action is completed successfully.</summary>
        public const string ModerationItemReviewedSuccessfully = "ModerationItemReviewedSuccessfully";

        /// <summary>Validation: the moderation item id must be a positive integer.</summary>
        public const string ModerationItemIdRequired = "ModerationItemIdRequired";

        /// <summary>
        /// Validation: the review decision must be Approved, Rejected, or Flagged
        /// (Pending is not a valid decision value).
        /// </summary>
        public const string ModerationReviewDecisionInvalid = "ModerationReviewDecisionInvalid";

        /// <summary>Validation: a reason is required when the review decision is Rejected.</summary>
        public const string ModerationReviewReasonRequired = "ModerationReviewReasonRequired";

        /// <summary>Validation: the review reason must not exceed 2000 characters.</summary>
        public const string ModerationReviewReasonTooLong = "ModerationReviewReasonTooLong";

        /// <summary>
        /// Returned (400) when a review is attempted on an item that is already in a
        /// terminal status (Approved or Rejected — cannot be re-reviewed).
        /// </summary>
        public const string ModerationItemAlreadyTerminal = "ModerationItemAlreadyTerminal";

        /// <summary>
        /// Returned (400) when a review attempts to Flag an item that is already Flagged.
        /// </summary>
        public const string ModerationItemAlreadyFlagged = "ModerationItemAlreadyFlagged";

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

        // ── P3-08 Adaptivity Engine ──────────────────────────────────────────────────────────────────────

        /// <summary>Returned when the adaptive difficulty decision is retrieved successfully.</summary>
        public const string AdaptivityDecisionRetrievedSuccessfully = "AdaptivityDecisionRetrievedSuccessfully";

        // ── P3-09 Student Mastery Engine ─────────────────────────────────────────────────────────────

        /// <summary>Returned when all mastery rows for the student are retrieved successfully.</summary>
        public const string MasteryRetrievedSuccessfully = "MasteryRetrievedSuccessfully";

        /// <summary>Returned when a single skill's mastery row is retrieved successfully.</summary>
        public const string SkillMasteryRetrievedSuccessfully = "SkillMasteryRetrievedSuccessfully";

        /// <summary>
        /// Returned when no mastery row exists yet for the requested (student, skill) pair.
        /// The response still returns 200 with NotStarted defaults — never 404.
        /// </summary>
        public const string SkillMasteryNotFound = "SkillMasteryNotFound";

        // ── P3-10 Spaced-Repetition Scheduler ────────────────────────────────────────────────────────

        /// <summary>Returned when the due-reviews list is retrieved successfully for the student.</summary>
        public const string DueReviewsRetrievedSuccessfully = "DueReviewsRetrievedSuccessfully";

        // ── P3-13 Adaptive Student Profile (Behavioral Modeling) ─────────────────────────────────────

        /// <summary>
        /// Returned when the student's behavioral learning profile is retrieved successfully.
        /// Used by <c>GetStudentProfileQueryHandler</c> (AC3 / AC5 inspection endpoint).
        /// </summary>
        public const string StudentProfileRetrievedSuccessfully = "StudentProfileRetrievedSuccessfully";

        // ── P3-02 AI Safety Layer ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Child-safe fallback message returned to the student when AI content is blocked
        /// by the Safety Layer (toxicity / age-appropriateness / hallucination check failed).
        /// Never exposes the unsafe content; tells the student the tutor cannot help right now.
        /// Resolved via string localizer from <c>SafetyOptions.FallbackMessageKey</c>.
        /// </summary>
        public const string AiContentBlocked = "AiContentBlocked";

        /// <summary>
        /// Child-safe fallback message returned to the student when the AI gateway itself
        /// is unavailable or errored. Resolved via string localizer from
        /// <c>SafetyOptions.GatewayErrorFallbackKey</c>.
        /// </summary>
        public const string AiServiceUnavailable = "AiServiceUnavailable";

        // ── P3-04 AI Tutor — Explain Concept ────────────────────────────────────────────────────

        /// <summary>
        /// Validation error when ExplainConceptCommand has none of LessonId/ConceptId/SkillId set.
        /// At least one must be provided to scope the explanation.
        /// </summary>
        public const string ExplainConceptContextRequired = "ExplainConceptContextRequired";

        /// <summary>
        /// Validation error when ExplainConceptCommand.Question exceeds the 500-character maximum.
        /// </summary>
        public const string ExplainConceptQuestionTooLong = "ExplainConceptQuestionTooLong";

        /// <summary>
        /// Refuse-and-redirect copy (Arabic) emitted when ILearningContextProvider returns no approved
        /// context for the active skill. Templated with the skill/lesson name.
        /// Format: "خلينا نكمل تحدي {0} الحالي، أو اختار درس العلوم."
        /// </summary>
        public const string AiTutorRedirectAr = "AiTutorRedirectAr";

        /// <summary>
        /// Refuse-and-redirect copy (English) emitted when ILearningContextProvider returns no approved
        /// context for the active skill. Templated with the skill/lesson name.
        /// Format: "Let's keep working on your current {0} challenge, or pick a Science lesson."
        /// </summary>
        public const string AiTutorRedirectEn = "AiTutorRedirectEn";

        /// <summary>
        /// SSE error code returned when the AI safety layer blocks the generated content.
        /// Surfaced as event: error / data: {"code":"AiContentBlocked","message":"..."}.
        /// </summary>
        public const string ExplainConceptSafetyBlocked = "ExplainConceptSafetyBlocked";

        /// <summary>
        /// SSE error code returned when the subject of the request has no registered prompt template.
        /// </summary>
        public const string ExplainConceptUnsupportedSubject = "ExplainConceptUnsupportedSubject";

        /// <summary>
        /// SSE error code returned when the student JWT is missing required claims (grade/language).
        /// </summary>
        public const string ExplainConceptMissingProfile = "ExplainConceptMissingProfile";

        /// <summary>
        /// Gentle message shown when the explain endpoint rate limit is exceeded (per-student window).
        /// </summary>
        public const string ExplainConceptRateLimitExceeded = "ExplainConceptRateLimitExceeded";

        // ── P3-05 AI Tutor — Hint, WhyWrong, Simplify ────────────────────────────────────────────

        // Validator keys (GetHintCommandValidator)

        /// <summary>
        /// Validation error when GetHintCommand.Intent is not Hint or WhyWrong.
        /// </summary>
        public const string HintIntentInvalid = "HintIntentInvalid";

        /// <summary>
        /// Validation error when GetHintCommand.HintLevel is outside the allowed 1..MaxHintLevels range.
        /// </summary>
        public const string HintLevelOutOfRange = "HintLevelOutOfRange";

        /// <summary>
        /// Validation error when GetHintCommand.WrongAnswer is missing/empty for a WhyWrong intent.
        /// </summary>
        public const string HintWrongAnswerRequired = "HintWrongAnswerRequired";

        /// <summary>
        /// Validation error when GetHintCommand.WrongAnswer exceeds the 500-character maximum.
        /// </summary>
        public const string HintWrongAnswerTooLong = "HintWrongAnswerTooLong";

        // Validator keys (SimplifyExplanationCommandValidator)

        /// <summary>
        /// Validation error when SimplifyExplanationCommand has neither ConceptId nor LessonId set.
        /// At least one must be provided to scope the simplification.
        /// </summary>
        public const string SimplifyContextRequired = "SimplifyContextRequired";

        // Handler keys (GetHintCommandHandler)

        /// <summary>
        /// SSE error code returned when the hint/why-wrong request rate limit is exceeded (per-student window).
        /// </summary>
        public const string HintRateLimitExceeded = "HintRateLimitExceeded";

        /// <summary>
        /// SSE error code returned when the AI safety layer blocks the generated hint content.
        /// Surfaced as event: error. Child-safe message — no unsafe content exposed.
        /// </summary>
        public const string HintSafetyBlocked = "HintSafetyBlocked";

        /// <summary>
        /// SSE error code returned when the hint subject has no registered prompt template.
        /// </summary>
        public const string HintUnsupportedSubject = "HintUnsupportedSubject";

        /// <summary>
        /// SSE error code returned when the generated hint content contains the question's CorrectAnswer
        /// (post-generation no-reveal check, OQ-4). The hint is blocked; the student should try again.
        /// </summary>
        public const string HintNoRevealViolation = "HintNoRevealViolation";

        /// <summary>
        /// SSE error code returned when IQuestionAnswerContract returns null (question or attempt not found).
        /// </summary>
        public const string HintQuestionNotFound = "HintQuestionNotFound";

        /// <summary>
        /// SSE error code returned when the student has used all MaxHintLevels for the current question.
        /// The FE should offer "want a simpler explanation?" (Simplify intent).
        /// </summary>
        public const string HintMaxLevelsReached = "HintMaxLevelsReached";

        // Handler keys (SimplifyExplanationCommandHandler)

        /// <summary>
        /// SSE error code returned when the student JWT is missing required claims (grade/language)
        /// in the Simplify handler. Mirrors ExplainConceptMissingProfile — separate key for per-handler tracking.
        /// </summary>
        public const string SimplifyMissingProfile = "SimplifyMissingProfile";

        /// <summary>
        /// SSE error code returned when the simplify endpoint rate limit is exceeded (per-student window).
        /// </summary>
        public const string SimplifyRateLimitExceeded = "SimplifyRateLimitExceeded";

        /// <summary>
        /// SSE error code returned when the AI safety layer blocks the generated simplify content.
        /// </summary>
        public const string SimplifySafetyBlocked = "SimplifySafetyBlocked";

        /// <summary>
        /// SSE error code returned when the simplify subject has no registered prompt template.
        /// </summary>
        public const string SimplifyUnsupportedSubject = "SimplifyUnsupportedSubject";

        // ── P3-06 Part B — AI Tutor — Similar Example (intent #4) ───────────────────────────

        /// <summary>
        /// Validation error when <c>SimilarExampleCommand.SkillId</c> is zero or negative.
        /// SkillId is required to scope the similar example to the active skill (AC-7 scope guard).
        /// </summary>
        public const string SimilarExampleSkillIdRequired = "SimilarExampleSkillIdRequired";

        /// <summary>
        /// SSE error code returned when the student JWT is missing required claims (grade/language)
        /// in the SimilarExample handler.
        /// </summary>
        public const string SimilarExampleMissingProfile = "SimilarExampleMissingProfile";

        /// <summary>
        /// Gentle message shown when the similar-example endpoint rate limit is exceeded (per-student window).
        /// </summary>
        public const string SimilarExampleRateLimitExceeded = "SimilarExampleRateLimitExceeded";

        /// <summary>
        /// SSE error code returned when the AI safety layer blocks the generated similar-example content.
        /// Surfaced as event: error. Child-safe message — no unsafe content exposed.
        /// </summary>
        public const string SimilarExampleSafetyBlocked = "SimilarExampleSafetyBlocked";

        /// <summary>
        /// SSE error code returned when the similar-example subject has no registered prompt template.
        /// </summary>
        public const string SimilarExampleUnsupportedSubject = "SimilarExampleUnsupportedSubject";

        // ── P3-07 WI-A2: Re-embed curriculum job ─────────────────────────────────────────────────

        /// <summary>
        /// Returned by the admin ReEmbed endpoint when the BGE-M3 TEI embedding endpoint is not
        /// configured (Curriculum:Embedding:BaseUrl or ModelVersion absent).
        /// No job is enqueued; the admin must provision the TEI endpoint first.
        /// </summary>
        public const string ReEmbedEndpointNotConfigured = "ReEmbedEndpointNotConfigured";

        /// <summary>
        /// Returned by the admin ReEmbed endpoint on successful job enqueue.
        /// The job ID is included in the response so the admin can track progress.
        /// </summary>
        public const string ReEmbedJobEnqueued = "ReEmbedJobEnqueued";

        // ── P10-01 Billing / Credit Ledger ───────────────────────────────────────────────────────

        /// <summary>Returned when a debit is attempted but the child's TotalBalance is insufficient.</summary>
        public const string InsufficientBalance = "InsufficientBalance";

        /// <summary>Returned when a credit account is not found for the given childId.</summary>
        public const string CreditAccountNotFound = "CreditAccountNotFound";

        /// <summary>Returned on successful credit debit.</summary>
        public const string CreditDebitSucceeded = "CreditDebitSucceeded";

        /// <summary>Returned on successful credit grant.</summary>
        public const string CreditGrantSucceeded = "CreditGrantSucceeded";

        /// <summary>Returned on successful credit account reconciliation query.</summary>
        public const string CreditReconcileSucceeded = "CreditReconcileSucceeded";

        /// <summary>Returned on duplicate idempotency key (no-op, prior result returned).</summary>
        public const string CreditIdempotentDuplicate = "CreditIdempotentDuplicate";

        // ── P10-12 Global Settings ───────────────────────────────────────────────────────────────

        /// <summary>Returned when a requested GlobalSetting key is not found in the DB.</summary>
        public const string GlobalSettingNotFound = "GlobalSettingNotFound";

        /// <summary>Validation: the Key field is required.</summary>
        public const string GlobalSettingKeyRequired = "GlobalSettingKeyRequired";

        /// <summary>Validation: the Key is not in the managed allowlist.</summary>
        public const string GlobalSettingKeyNotAllowed = "GlobalSettingKeyNotAllowed";

        /// <summary>Validation: the NewValue field is required.</summary>
        public const string GlobalSettingValueRequired = "GlobalSettingValueRequired";

        /// <summary>Validation: the UpdatedBy field is required.</summary>
        public const string GlobalSettingUpdatedByRequired = "GlobalSettingUpdatedByRequired";

        /// <summary>Validation: NewValue cannot be parsed as the key's declared type.</summary>
        public const string GlobalSettingValueTypeMismatch = "GlobalSettingValueTypeMismatch";

        /// <summary>Validation: NewValue is out of the allowed range for the given key.</summary>
        public const string GlobalSettingValueOutOfRange = "GlobalSettingValueOutOfRange";

        /// <summary>Validation: daily cap value exceeds the monthly grant for the same tier.</summary>
        public const string GlobalSettingDailyCapExceedsMonthly = "GlobalSettingDailyCapExceedsMonthly";

        /// <summary>Returned when all global settings are retrieved successfully.</summary>
        public const string GlobalSettingsRetrievedSuccessfully = "GlobalSettingsRetrievedSuccessfully";

        /// <summary>Returned when a global setting is updated successfully.</summary>
        public const string GlobalSettingUpdatedSuccessfully = "GlobalSettingUpdatedSuccessfully";

        // ── P10-05 Subscription / Plan management ────────────────────────────────────────────────

        /// <summary>Returned when the parent's subscription is retrieved successfully.</summary>
        public const string SubscriptionRetrievedSuccessfully = "SubscriptionRetrievedSuccessfully";

        /// <summary>Returned when the plan comparison data is retrieved successfully.</summary>
        public const string PlanComparisonRetrievedSuccessfully = "PlanComparisonRetrievedSuccessfully";

        /// <summary>Returned when an upgrade request is accepted (transitions to PendingPayment).</summary>
        public const string SubscriptionUpgradeRequested = "SubscriptionUpgradeRequested";

        /// <summary>Returned when a downgrade is scheduled at next cycle end.</summary>
        public const string SubscriptionDowngradeScheduled = "SubscriptionDowngradeScheduled";

        /// <summary>Returned when the subscription is canceled successfully.</summary>
        public const string SubscriptionCanceledSuccessfully = "SubscriptionCanceledSuccessfully";

        /// <summary>Returned when no active subscription is found for the parent.</summary>
        public const string SubscriptionNotFound = "SubscriptionNotFound";

        /// <summary>Returned when trying to downgrade an already-Free subscription.</summary>
        public const string SubscriptionAlreadyFree = "SubscriptionAlreadyFree";

        /// <summary>Validation: ParentUserId must be > 0.</summary>
        public const string SubscriptionParentIdRequired = "SubscriptionParentIdRequired";

        /// <summary>Validation: BillingPeriod must be a valid enum value.</summary>
        public const string SubscriptionBillingPeriodInvalid = "SubscriptionBillingPeriodInvalid";

        // ── P10-03 AI Energy spend — insufficient energy decline ─────────────────────────────────

        /// <summary>
        /// Returned by all 4 AI Helper handlers when the child's energy balance is insufficient
        /// (monthly hard limit) or the daily cap is reached with HardStopEnabled=true.
        /// Surfaces via the SSE "error" frame — client shows the low-energy UI.
        /// Never thrown as an exception — always a typed graceful decline.
        /// </summary>
        public const string AiInsufficientEnergy = "AiInsufficientEnergy";

        /// <summary>
        /// Returned when the daily energy cap has been reached (soft-cap, non-blocking by default).
        /// Only blocks when <c>Billing:HardStopEnabled=true</c> in configuration.
        /// </summary>
        public const string AiDailyCapReached = "AiDailyCapReached";

        // ── P10-06 Payments / Webhooks ────────────────────────────────────────────────────────────

        /// <summary>Returned when a checkout session is created successfully (redirect URL in Data).</summary>
        public const string CheckoutSessionCreated = "CheckoutSessionCreated";

        /// <summary>Returned when the payment provider API is unavailable (transient failure).</summary>
        public const string PaymentProviderUnavailable = "PaymentProviderUnavailable";

        /// <summary>
        /// Returned (401 shape) when a webhook request fails HMAC signature verification.
        /// Message is intentionally vague to avoid disclosing verification details.
        /// </summary>
        public const string WebhookSignatureInvalid = "WebhookSignatureInvalid";

        /// <summary>Returned when the webhook body cannot be parsed after signature verification passes.</summary>
        public const string WebhookPayloadInvalid = "WebhookPayloadInvalid";

        /// <summary>Returned when a webhook event is successfully processed.</summary>
        public const string WebhookProcessed = "WebhookProcessed";

        /// <summary>Returned when a duplicate webhook event is received (idempotent no-op).</summary>
        public const string WebhookEventAlreadyProcessed = "WebhookEventAlreadyProcessed";

        // ── P10-07 Energy-pack purchase ───────────────────────────────────────────────────────────

        /// <summary>Returned when an energy-pack checkout session is created successfully (redirect URL in Data).</summary>
        public const string PackCheckoutSessionCreated = "PackCheckoutSessionCreated";

        /// <summary>
        /// Returned (403 shape) when the authenticated parent does not own the requested child.
        /// Intentionally generic (anti-IDOR: caller cannot distinguish "child not found" from "not your child").
        /// </summary>
        public const string PackCheckoutChildNotOwned = "PackCheckoutChildNotOwned";

        /// <summary>Validation: ChildId must be a positive integer.</summary>
        public const string PackCheckoutChildIdRequired = "PackCheckoutChildIdRequired";

        /// <summary>Returned (internally, for logging) when a pack purchase is credited to the child account.</summary>
        public const string PackCreditedSuccessfully = "PackCreditedSuccessfully";

        // ── P10-09 Refunds + Dunning ───────────────────────────────────────────────────────────────

        /// <summary>Returned when an admin refund request is accepted by the provider.</summary>
        public const string RefundInitiatedSuccessfully = "RefundInitiatedSuccessfully";

        /// <summary>Returned when a payment is not found for refund.</summary>
        public const string PaymentNotFound = "PaymentNotFound";

        /// <summary>Returned when a payment cannot be refunded (not in Succeeded state).</summary>
        public const string PaymentNotRefundable = "PaymentNotRefundable";

        // ── P10-08 Billing History + Receipts ────────────────────────────────────────────────────

        /// <summary>Returned when billing history is retrieved successfully.</summary>
        public const string BillingHistoryRetrievedSuccessfully = "BillingHistoryRetrievedSuccessfully";

        /// <summary>Returned when a receipt is retrieved successfully.</summary>
        public const string ReceiptRetrievedSuccessfully = "ReceiptRetrievedSuccessfully";

        /// <summary>Returned when a receipt is not found or not owned by the requesting parent.</summary>
        public const string ReceiptNotFound = "ReceiptNotFound";

        /// <summary>Receipt description for an energy-pack purchase.</summary>
        public const string ReceiptDescriptionEnergyPack = "ReceiptDescriptionEnergyPack";

        /// <summary>Receipt description for a Premium subscription charge.</summary>
        public const string ReceiptDescriptionPremiumSubscription = "ReceiptDescriptionPremiumSubscription";

        // Note: Unauthorized already defined at line 333 (shared usage).

        // ── P10-13 Family Energy Wallet ────────────────────────────────────────────────────────────

        /// <summary>Returned when the family energy wallet overview is retrieved successfully.</summary>
        public const string FamilyEnergyOverviewRetrieved = "FamilyEnergyOverviewRetrieved";

        /// <summary>Returned when the requesting parent does not own the family energy wallet (IDOR guard).</summary>
        public const string FamilyEnergyWalletNotOwned = "FamilyEnergyWalletNotOwned";

        /// <summary>Returned when the family energy wallet is not found for the parent.</summary>
        public const string FamilyEnergyWalletNotFound = "FamilyEnergyWalletNotFound";

        /// <summary>
        /// Returned when a child's AI spend is denied because neither their allocation row
        /// nor the shared family purchased balance has sufficient energy.
        /// </summary>
        public const string FamilyEnergyInsufficientBalance = "FamilyEnergyInsufficientBalance";

        /// <summary>Returned when the monthly subscription grant allocation job processes a family successfully.</summary>
        public const string FamilyEnergyGrantAllocated = "FamilyEnergyGrantAllocated";

        /// <summary>Returned when the monthly subscription grant is already allocated for this family+cycle (idempotent).</summary>
        public const string FamilyEnergyGrantAlreadyAllocated = "FamilyEnergyGrantAlreadyAllocated";

        /// <summary>Returned when the clean-cutover migration completes successfully.</summary>
        public const string FamilyEnergyMigrationCompleted = "FamilyEnergyMigrationCompleted";

        /// <summary>Returned when the pack-credit re-home credits the shared family purchased balance successfully (P10-13-BE-12).</summary>
        public const string FamilyEnergyPackCredited = "FamilyEnergyPackCredited";

        // ── P10-14 Seats & seat-reserved add-child ────────────────────────────────────────────────

        /// <summary>
        /// Returned (HTTP 402/409) when a parent tries to add a child but has no free seat available.
        /// The child account is NOT created. The parent must buy extra seats first.
        /// </summary>
        public const string NoFreeSeatAvailable = "NoFreeSeatAvailable";

        /// <summary>Returned when an extra-seat checkout session is created successfully (redirect URL in Data).</summary>
        public const string SeatCheckoutSessionCreated = "SeatCheckoutSessionCreated";

        /// <summary>Returned when a seat-checkout would exceed the maximum allowed seat count (seats.max).</summary>
        public const string SeatCheckoutExceedsMaxSeats = "SeatCheckoutExceedsMaxSeats";

        /// <summary>Returned when a Free-tier parent tries to buy extra seats (Free plans cannot add extra seats).</summary>
        public const string SeatCheckoutFreePlanNotAllowed = "SeatCheckoutFreePlanNotAllowed";

        /// <summary>Validation: seat quantity must be a positive integer.</summary>
        public const string SeatCheckoutQuantityRequired = "SeatCheckoutQuantityRequired";

        /// <summary>Returned when the seat-status query is retrieved successfully.</summary>
        public const string SeatStatusRetrievedSuccessfully = "SeatStatusRetrievedSuccessfully";

        /// <summary>Returned when the seat cancel grace marker is recorded successfully.</summary>
        public const string SeatCancelledSuccessfully = "SeatCancelledSuccessfully";

        /// <summary>Returned when a seat cancel request fails because the parent has no purchased extra seats.</summary>
        public const string SeatCancelInsufficientExtraSeats = "SeatCancelInsufficientExtraSeats";

        /// <summary>Returned when the webhook confirms extra seats were purchased successfully.</summary>
        public const string SeatPurchaseConfirmed = "SeatPurchaseConfirmed";

        // ── P10-15 Seat lifecycle / enforcement / grace ───────────────────────────────────────────

        /// <summary>
        /// Returned (HTTP 403) when a child whose seat is NoSeatLocked attempts to spend AI energy.
        /// The spend is denied before any balance is touched (P10-15-BE-5).
        /// </summary>
        public const string ChildSeatLockedNoEnergy = "ChildSeatLockedNoEnergy";

        /// <summary>
        /// Validation: the requested count of active children exceeds the subscription's paid-active-seat limit
        /// (P10-15-BE-6 ChooseActiveChildren validator).
        /// </summary>
        public const string SeatChoiceExceedsLimit = "SeatChoiceExceedsLimit";

        /// <summary>Returned when a parent's ChooseActiveChildren selection is applied successfully.</summary>
        public const string SeatChoiceApplied = "SeatChoiceApplied";

        /// <summary>
        /// Returned when a prorated ReactivateChildSeat checkout session is created successfully.
        /// Redirect URL is in the response Data (P10-15-BE-6).
        /// </summary>
        public const string SeatReactivateInitiated = "SeatReactivateInitiated";

        /// <summary>
        /// Returned (informational) when a payment-failure seat grace window is opened on a subscription
        /// (P10-15-BE-2). The parent has a grace window to resolve the payment.
        /// </summary>
        public const string SeatGraceWindowStarted = "SeatGraceWindowStarted";

        /// <summary>
        /// Returned by the enforcement job / service when enforcement is applied to a subscription
        /// (P10-15-BE-4). Logged; not surfaced to the parent unless a relevant portal notification is sent.
        /// </summary>
        public const string SeatEnforcementApplied = "SeatEnforcementApplied";

        /// <summary>
        /// Returned when the GetSeatStatusQuery (P10-15-BE-6 extended) retrieves the full seat-lifecycle
        /// status snapshot successfully (complements the earlier SeatStatusRetrievedSuccessfully key which
        /// is reused here — only add a NEW key if the response shape differs).
        /// </summary>
        public const string SeatStatusLifecycleRetrievedSuccessfully = "SeatStatusLifecycleRetrievedSuccessfully";

        /// <summary>
        /// Validation / authz error: the supplied child id does not belong to the authenticated parent's family
        /// (IDOR guard used on seat endpoints that accept a childId, P10-15-BE-6/BE-9).
        /// </summary>
        public const string ChildNotInFamily = "ChildNotInFamily";

        /// <summary>
        /// Returned when a child seat reactivation webhook payment is confirmed and the seat is flipped
        /// back to Active (P10-15-BE-7).
        /// </summary>
        public const string SeatReactivationConfirmed = "SeatReactivationConfirmed";

        /// <summary>
        /// Validation: a ReactivateChildSeat request is rejected because the child's seat is already
        /// Active (not locked) — no reactivation needed (P10-15-BE-6).
        /// </summary>
        public const string SeatAlreadyActive = "SeatAlreadyActive";

        // ── P10-16 Family allocation redistribution ───────────────────────────────────────────────

        /// <summary>
        /// Success: allocation transfer completed. Source and destination remaining balances updated.
        /// </summary>
        public const string AllocationTransferSucceeded = "AllocationTransferSucceeded";

        /// <summary>
        /// Rejection: one or both of the specified children do not belong to the authenticated parent's
        /// family (cross-family transfer rejected — anti-abuse, anti-resale, by construction).
        /// </summary>
        public const string AllocationTransferCrossFamily = "AllocationTransferCrossFamily";

        /// <summary>
        /// Rejection: the requested transfer amount exceeds the source child's unspent remaining
        /// allowance. Already-spent energy can never be reclaimed or transferred.
        /// </summary>
        public const string AllocationTransferExceedsRemaining = "AllocationTransferExceedsRemaining";

        /// <summary>
        /// Rejection: source child and destination child are the same (self-transfer not allowed).
        /// </summary>
        public const string AllocationTransferSameChild = "AllocationTransferSameChild";

        /// <summary>
        /// Rejection: the destination (or source) child does not hold an active seat
        /// (NoSeatLocked or no seat reservation). Only active-seat children can be transfer targets.
        /// </summary>
        public const string AllocationTransferDestinationNoSeat = "AllocationTransferDestinationNoSeat";

        /// <summary>
        /// Validation: transfer amount must be a positive integer greater than zero.
        /// </summary>
        public const string AllocationTransferAmountRequired = "AllocationTransferAmountRequired";

        /// <summary>
        /// Validation: transfer amount exceeds the maximum supported value (int range).
        /// </summary>
        public const string AllocationTransferAmountTooLarge = "AllocationTransferAmountTooLarge";

        /// <summary>
        /// Conflict: the supplied idempotency key was already used for a transfer with a
        /// DIFFERENT payload (from/to child or amount). Reusing a key with a changed payload is rejected.
        /// </summary>
        public const string AllocationTransferKeyConflict = "AllocationTransferKeyConflict";

        /// <summary>
        /// Rate limit: too many transfer requests from this parent in a short window.
        /// </summary>
        public const string AllocationTransferRateLimited = "AllocationTransferRateLimited";

        // ── P10-17 Purchased-energy refunds ──────────────────────────────────────────────────────────

        /// <summary>Returned when a refundable-energy quote is retrieved successfully (parent or admin path).</summary>
        public const string RefundableQuoteRetrieved = "RefundableQuoteRetrieved";

        /// <summary>
        /// Returned when a purchased-energy refund request is accepted and forwarded to the provider.
        /// The ledger change is pending the <c>refund.succeeded</c> webhook.
        /// </summary>
        public const string RefundRequestAccepted = "RefundRequestAccepted";

        /// <summary>
        /// Returned (HTTP 422) when the refundable amount is zero — all purchased energy has already
        /// been consumed and there is nothing to refund.
        /// </summary>
        public const string RefundableAmountIsZero = "RefundableAmountIsZero";

        /// <summary>
        /// Returned (no-op / duplicate) when the pack payment has already been refunded.
        /// Guards against distinct-event-id over-refund (Finding #1, CRITICAL).
        /// </summary>
        public const string PaymentAlreadyRefunded = "PaymentAlreadyRefunded";

        // ── P10-18 Parent-pause / child access control ─────────────────────────────────────────────

        /// <summary>
        /// Child-facing: returned when a paused child tries to use an AI feature.
        /// Graceful localized decline — no raw error. "Your parent has paused your AI access."
        /// </summary>
        public const string ChildAccessPausedByParent = "ChildAccessPausedByParent";

        /// <summary>
        /// Parent-facing: returned (HTTP 403) when the parent attempts to pause/unpause/query
        /// a child that does not belong to their family (IDOR rejection).
        /// </summary>
        public const string ChildNotOwnedByParent = "ChildNotOwnedByParent";

        /// <summary>
        /// Informational: returned when the parent tries to pause a child whose
        /// <c>ParentPauseState</c> is already <c>Paused</c> (idempotency no-op success).
        /// </summary>
        public const string ChildAlreadyPaused = "ChildAlreadyPaused";

        /// <summary>
        /// Informational: returned when the parent tries to unpause a child whose
        /// <c>ParentPauseState</c> is already <c>Active</c> (idempotency no-op success).
        /// </summary>
        public const string ChildAlreadyActive = "ChildAlreadyActive";

        /// <summary>
        /// Success: returned when a child's access is successfully paused by the parent.
        /// </summary>
        public const string ChildAccessPausedSuccessfully = "ChildAccessPausedSuccessfully";

        /// <summary>
        /// Success: returned when a child's access is successfully unpaused by the parent.
        /// </summary>
        public const string ChildAccessUnpausedSuccessfully = "ChildAccessUnpausedSuccessfully";

        /// <summary>
        /// Success: returned when the child's access status is retrieved successfully.
        /// </summary>
        public const string ChildAccessStatusRetrieved = "ChildAccessStatusRetrieved";

        // ── P5-08 / P5-02 Learning read seams + weak-area detector ──────────────────────────────

        /// <summary>
        /// Success: returned when the student's learning stats are retrieved successfully.
        /// Used by <c>IStudentLearningStatsQuery</c> consumers (P5-08-BE-2).
        /// </summary>
        public const string LearningStatsRetrievedSuccessfully = "LearningStatsRetrievedSuccessfully";

        /// <summary>
        /// Success: returned when the student's subject mastery summary is retrieved successfully.
        /// Used by <c>IStudentMasterySummaryQuery</c> consumers (P5-08-BE-3).
        /// </summary>
        public const string MasterySummaryRetrievedSuccessfully = "MasterySummaryRetrievedSuccessfully";

        /// <summary>
        /// Success: returned when the student's weak areas are retrieved successfully.
        /// Includes the case of an empty list (no weak areas — not an error).
        /// Used by <c>IStudentAllSubjectsWeakAreasQuery</c> consumers (P5-02-BE-2).
        /// </summary>
        public const string WeakAreasRetrievedSuccessfully = "WeakAreasRetrievedSuccessfully";

        /// <summary>
        /// Suggested next action: review the underlying concept (shown for High-severity weak areas).
        /// Resolved by Parent handlers when building the recommended-actions display (P5-02).
        /// </summary>
        public const string WeakAreaActionReviewConcept = "WeakAreaActionReviewConcept";

        /// <summary>
        /// Suggested next action: practice the skill more (shown for Medium/Low-severity weak areas).
        /// Resolved by Parent handlers when building the recommended-actions display (P5-02).
        /// </summary>
        public const string WeakAreaActionPracticeSkill = "WeakAreaActionPracticeSkill";

        // ── P5-08-BE-5..13 Parent analytics endpoints ──────────────────────────────────────────

        /// <summary>Returned when child progress (E1) is retrieved successfully.</summary>
        public const string ChildProgressRetrievedSuccessfully = "ChildProgressRetrievedSuccessfully";

        /// <summary>Returned when the family summary (E2) is retrieved successfully.</summary>
        public const string FamilySummaryRetrievedSuccessfully = "FamilySummaryRetrievedSuccessfully";

        /// <summary>Returned when the child's weekly KPIs + WoW deltas (E3) are retrieved successfully.</summary>
        public const string WeeklyKpisRetrievedSuccessfully = "WeeklyKpisRetrievedSuccessfully";

        /// <summary>Returned when the child's subject mastery (E4) is retrieved successfully.</summary>
        public const string SubjectMasteryRetrievedSuccessfully = "SubjectMasteryRetrievedSuccessfully";

        /// <summary>Returned when the child's report charts (E6) are retrieved successfully.</summary>
        public const string ReportChartsRetrievedSuccessfully = "ReportChartsRetrievedSuccessfully";

        /// <summary>Returned when the child's energy balance (E7) is retrieved successfully.</summary>
        public const string EnergyRetrievedSuccessfully = "EnergyRetrievedSuccessfully";

        /// <summary>Returned when the child's activity feed (E8) is retrieved successfully.</summary>
        public const string ActivityFeedRetrievedSuccessfully = "ActivityFeedRetrievedSuccessfully";

        /// <summary>
        /// Generic 403 returned when the authenticated parent is NOT the parent of the requested child.
        /// Intentionally vague — no distinction between "child not found" and "not your child" (anti-IDOR).
        /// </summary>
        public const string ParentChildNotAuthorized = "ParentChildNotAuthorized";

        /// <summary>Returned when childId route parameter is zero or negative.</summary>
        public const string ChildIdMustBePositive = "ChildIdMustBePositive";

        // ── P5-01-BE-4 Weekly report read endpoint (E9) ──────────────────────────────────────

        /// <summary>
        /// Returned when the child's stored weekly report (E9) is retrieved successfully.
        /// Also covers the "no report yet" case where the DTO has zeroed fields.
        /// </summary>
        public const string WeeklyReportRetrievedSuccessfully = "WeeklyReportRetrievedSuccessfully";

        /// <summary>
        /// Returned as the message field when no weekly report has been generated yet for
        /// the child (first-week / no-activity-week state). The response is 200 OK with a
        /// zeroed DTO — not a 404.
        /// </summary>
        public const string WeeklyReportNotYetGenerated = "WeeklyReportNotYetGenerated";

        // ── P3-14 Lexi recommendation narration ───────────────────────────────────────────────────

        /// <summary>
        /// Returned when the student's account has no student id in the JWT
        /// (mirrors ExplainConceptMissingProfile — separate key for per-handler tracking).
        /// </summary>
        public const string RecommendationNarrationMissingProfile = "RecommendationNarrationMissingProfile";

        /// <summary>Rate-limit exceeded for the Recommendation narration intent.</summary>
        public const string RecommendationNarrationRateLimitExceeded = "RecommendationNarrationRateLimitExceeded";

        /// <summary>
        /// Returned when the child has no persisted recommendations yet and the narration cannot be grounded.
        /// Friendly decline — no debit, no LLM call.
        /// </summary>
        public const string RecommendationNarrationNoData = "RecommendationNarrationNoData";

        /// <summary>Safety layer blocked the recommendation narration.</summary>
        public const string RecommendationNarrationSafetyBlocked = "RecommendationNarrationSafetyBlocked";

        // ── P5-09 Recommendation engine — endpoint + i18n keys ─────────────────────────────────

        /// <summary>Returned when the child's daily recommendations are retrieved successfully.</summary>
        public const string RecommendationsRetrievedSuccessfully = "RecommendationsRetrievedSuccessfully";

        // ── P5-09 Recommendation item i18n keys (persisted as keys; resolved at display time) ──

        /// <summary>
        /// Title key for a "Review the concept" recommendation (High-severity weak area).
        /// Resolver maps this to a localised string: EN "Time to review" / AR equivalent.
        /// </summary>
        public const string RecReviewTitle = "RecReviewTitle";

        /// <summary>Body key for the Review recommendation.</summary>
        public const string RecReviewBody = "RecReviewBody";

        /// <summary>Call-to-action key for the Review recommendation.</summary>
        public const string RecReviewCta = "RecReviewCta";

        /// <summary>
        /// Title key for a "Practice the skill" recommendation (Medium/Low-severity weak area).
        /// </summary>
        public const string RecPracticeTitle = "RecPracticeTitle";

        /// <summary>Body key for the Practice recommendation.</summary>
        public const string RecPracticeBody = "RecPracticeBody";

        /// <summary>Call-to-action key for the Practice recommendation.</summary>
        public const string RecPracticeCta = "RecPracticeCta";

        /// <summary>
        /// Title key for the cold-start / encouraging recommendation (no weak areas yet).
        /// </summary>
        public const string RecColdStartTitle = "RecColdStartTitle";

        /// <summary>Body key for the cold-start recommendation.</summary>
        public const string RecColdStartBody = "RecColdStartBody";

        /// <summary>Call-to-action key for the cold-start recommendation.</summary>
        public const string RecColdStartCta = "RecColdStartCta";

    }
}
