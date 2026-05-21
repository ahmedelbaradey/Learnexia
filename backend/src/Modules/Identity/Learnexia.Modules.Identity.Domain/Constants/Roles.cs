using System.ComponentModel;

namespace Learnexia.Modules.Identity.Domain.Constants;

public enum Roles
{
    [Description("None")] None,
    [Description("مشرف عام")] SuperAdmin,
    [Description("مشرف")] Admin,
    [Description("مستخدم أساسي")] Basic,
    [Description("مستخدم")] User,
    [Description("مدير صندوق")] FundManager,
    [Description("المستشار القانوني")] LegalCouncil,
    [Description("سكرتير المجلس")] BoardSecretary,
    [Description("عضو مجلس الإدارة")] BoardMember,
    [Description("مدير المالية")] FinanceController,
    [Description("مدير الادارة")] ComplianceLegalManagingDirector,
    [Description("مدير العقارات")] HeadOfRealEstate,
    [Description("مدير صندوق")] AssociatedFundManager,
}

public static class RoleHelper
{
    public const string FundManager = "fundmanager";
    public const string LegalCouncil = "legalcouncil";
    public const string BoardSecretary = "boardsecretary";
    public const string BoardMember = "boardmember";
    public const string SuperAdmin = "superadmin";
    public const string Admin = "admin";
    public const string Basic = "basic";
    public const string User = "user";
    public const string FinanceController = "financecontroller";
    public const string ComplianceLegalManagingDirector = "compliancelegalmanagingdirector";
    public const string HeadOfRealEstate = "headofrealestate";
    public const string AssociateFundManager = "associatefundmanager";

    public static bool IsFundManager(IList<string> userRoles)
        => userRoles.Any(role => string.Equals(role, FundManager, StringComparison.OrdinalIgnoreCase));

    public static bool IsLegalCouncil(IList<string> userRoles)
        => userRoles.Any(role => string.Equals(role, LegalCouncil, StringComparison.OrdinalIgnoreCase));

    public static bool IsBoardSecretary(IList<string> userRoles)
        => userRoles.Any(role => string.Equals(role, BoardSecretary, StringComparison.OrdinalIgnoreCase));

    public static bool IsBoardMember(IList<string> userRoles)
        => userRoles.Any(role => string.Equals(role, BoardMember, StringComparison.OrdinalIgnoreCase));

    public static bool HasAuthorizedRole(IList<string> userRoles)
        => IsFundManager(userRoles) || IsLegalCouncil(userRoles) || IsBoardSecretary(userRoles);
}
