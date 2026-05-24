using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;

namespace Learnexia.Modules.Identity.Infrastructure.Services;

public class IdentityServiceManager : IIdentityServiceManager
{
    private readonly Lazy<IAuthenticationService> _authenticationService;
    private readonly Lazy<IAuthorizationService> _authorizationService;
    private readonly Lazy<IUserManagmentService> _userManagmentService;

    public IdentityServiceManager(ILoggerManager logger, RoleManager<Role> roleManager, UserManager<User> userManager, JwtSettings jwtSettings, IDistributedCache distributedCache, IdentityModuleDbContext dbContext, ICurrentUserService currentUserService)
    {
        _authenticationService = new Lazy<IAuthenticationService>(() =>
            new AuthenticationIdentityService(userManager, roleManager, jwtSettings, logger, distributedCache));
        _authorizationService = new Lazy<IAuthorizationService>(() =>
            new AuthorizationIdentityService(roleManager, userManager, logger));
        _userManagmentService = new Lazy<IUserManagmentService>(() =>
            new UserManagmentIdentityService(userManager, logger));
    }

    public IAuthenticationService AuthenticationService => _authenticationService.Value;
    public IAuthorizationService AuthorizationService => _authorizationService.Value;
    public IUserManagmentService UserManagmentService => _userManagmentService.Value;
}
