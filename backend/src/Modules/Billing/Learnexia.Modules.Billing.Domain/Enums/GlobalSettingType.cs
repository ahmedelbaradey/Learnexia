namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Declared type of a <c>GlobalSetting</c> value string — drives parse/validate logic.
/// </summary>
public enum GlobalSettingType
{
    Int     = 1,
    Decimal = 2,
    String  = 3,
    Bool    = 4,
}
