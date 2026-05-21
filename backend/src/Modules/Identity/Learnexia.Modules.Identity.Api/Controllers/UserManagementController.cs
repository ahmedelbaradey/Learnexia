using Learnexia.Modules.Identity.Api.Bases;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.AddUser;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminResetPassword;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.ChangePassword;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.DeleteUser;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUser;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserPreferredLanguage;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserRoles;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.ResendRegistrationMessage;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.SetNewPassword;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateUserProfile;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.CheckRoleAvailability;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Get;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.GetCurrentCultureLanguage;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUserProfile;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUserRoles;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUsersByRole;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.List;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Learnexia.Modules.Identity.Api.Controllers;

[Route("api/Users/UserManagement/[Action]")]
[ApiController]
public class UserManagementController : AppControllerBase
{
    [HttpGet]
    public async Task<IActionResult> UserList([FromQuery] ListQuery query)
        => NewResult(await Mediator.Send(query));

    [HttpGet]
    public async Task<IActionResult> GetUserById(int id)
        => NewResult(await Mediator.Send(new GetUserQuery { Id = id }));

    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddUser([FromBody] AddUserCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUser([FromBody] EditUserCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut]
    public async Task<IActionResult> UpdateUserLanguage([FromBody] EditUserPreferredLanguageCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpGet]
    public async Task<IActionResult> UserRoles(int id)
        => NewResult(await Mediator.Send(new GetUserRolesQuery { Id = id }));

    [HttpPut]
    public async Task<IActionResult> UpdateUserRoles([FromBody] EditUserRolesCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpDelete]
    public async Task<IActionResult> DeleteUser(int id)
        => NewResult(await Mediator.Send(new DeleteUserCommand { Id = id }));

    [HttpPut]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePasswordForUser([FromBody] ChangePasswordCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPut]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetNewPasswordForUser([FromBody] SetNewPasswordCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<GetUserListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFundManagerUsers()
        => NewResult(await Mediator.Send(new GetUsersByRoleQuery { RoleName = Roles.FundManager.ToString() }));

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<GetUserListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoardSecretaryUsers()
        => NewResult(await Mediator.Send(new GetUsersByRoleQuery { RoleName = Roles.BoardSecretary.ToString() }));

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<GetUserListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLegalCouncilUsers()
        => NewResult(await Mediator.Send(new GetUsersByRoleQuery { RoleName = Roles.LegalCouncil.ToString() }));

    [HttpGet]
    public async Task<IActionResult> GetCurrentCultureLanguage()
        => NewResult(await Mediator.Send(new GetCurrentCultureLanguageQuery()));

    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<UserProfileResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserProfile([FromQuery] int? userId = null)
        => NewResult(await Mediator.Send(new GetUserProfileQuery { UserId = userId }));

    [HttpPut]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateUserProfile([FromForm] UpdateUserProfileCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminResetPassword([FromBody] AdminResetPasswordCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResendRegistrationMessage([FromBody] ResendRegistrationMessageCommand command)
        => NewResult(await Mediator.Send(command));

    [HttpGet]
    [ProducesResponseType(typeof(BaseResponse<SingleHolderRoleAvailabilityResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckRoleAvailability()
        => NewResult(await Mediator.Send(new CheckRoleAvailabilityQuery()));
}
