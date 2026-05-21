namespace Learnexia.Modules.Identity.Domain.Enums;

public enum SessionTerminationReason
{
    UserLogout,
    InactivityTimeout,
    SecurityRevocation,
    AdminTermination,
    ConcurrentLoginLimit,
}
