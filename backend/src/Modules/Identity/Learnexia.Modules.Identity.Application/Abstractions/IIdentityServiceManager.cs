namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface IIdentityServiceManager
{
    IAuthenticationService AuthenticationService { get; }
    IAuthorizationService AuthorizationService { get; }
    IUserManagmentService UserManagmentService { get; }
}
