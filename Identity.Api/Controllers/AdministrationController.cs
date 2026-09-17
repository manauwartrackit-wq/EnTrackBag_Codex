using System.Security.Claims;
using Identity.Api.DomainComponents;
using Identity.Api.DTOs;
using Identity.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/administration")]
[Authorize(Policy = "Administration")]
public class AdministrationController : ControllerBase
{
    private readonly IUserDomainComponent _userDomainComponent;
    private readonly IRoleDomainComponent _roleDomainComponent;
    private readonly ISessionDomainComponent _sessionDomainComponent;

    public AdministrationController(
        IUserDomainComponent userDomainComponent,
        IRoleDomainComponent roleDomainComponent,
        ISessionDomainComponent sessionDomainComponent)
    {
        _userDomainComponent = userDomainComponent;
        _roleDomainComponent = roleDomainComponent;
        _sessionDomainComponent = sessionDomainComponent;
    }

    [HttpGet("users")]
    [Authorize(Policy = "Users")]
    public async Task<ActionResult<UserListItemDto[]>> GetUsers(
        [FromQuery] string? search, [FromQuery] int? roleId, [FromQuery] bool? isActive, CancellationToken ct) =>
        Ok(await _userDomainComponent.GetUsersAsync(search, roleId, isActive, ct));

    [HttpGet("users/{id:int}/passport")]
    [Authorize(Policy = "Users.Sensitive")]
    public async Task<ActionResult<PassportDetailDto>> GetPassport(int id, CancellationToken ct)
    {
        var result = await _userDomainComponent.GetPassportAsync(id, CurrentUserId(), CurrentUserName(), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("users")]
    [Authorize(Policy = "Users:CREATE")]
    public async Task<ActionResult<UserListItemDto>> CreateUser(CreateUserRequestDto request, CancellationToken ct)
    {
        try
        {
            var created = await _userDomainComponent.CreateUserAsync(request, CurrentUserId(), CurrentUserName(), ct);
            return CreatedAtAction(nameof(GetUsers), new { search = created.UserName }, created);
        }
        catch (ProtectedAccountException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("users/{id:int}/reset-password")]
    [Authorize(Policy = "Users:EDIT")]
    public async Task<IActionResult> ResetPassword(int id, ResetUserPasswordRequestDto request, CancellationToken ct)
    {
        try
        {
            return await _userDomainComponent.ResetPasswordAsync(id, request, CurrentUserId(), CurrentUserName(), ct)
                ? NoContent() : NotFound();
        }
        catch (ProtectedAccountException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("users/{id:int}")]
    [Authorize(Policy = "Users:EDIT")]
    public async Task<ActionResult<UserListItemDto>> UpdateUser(int id, UpdateUserRequestDto request, CancellationToken ct)
    {
        try
        {
            var updated = await _userDomainComponent.UpdateUserAsync(id, request, CurrentUserId(), CurrentUserName(), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ProtectedAccountException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("users/{id:int}")]
    [Authorize(Policy = "Users:DELETE")]
    public async Task<IActionResult> RemoveUser(int id, CancellationToken ct)
    {
        try
        {
            return await _userDomainComponent.RemoveUserAsync(id, CurrentUserId(), CurrentUserName(), ct)
                ? NoContent()
                : NotFound();
        }
        catch (ProtectedAccountException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpGet("roles")]
    [Authorize(Policy = "Roles")]
    public async Task<ActionResult<RoleListItemDto[]>> GetRoles(CancellationToken ct) =>
        Ok(await _roleDomainComponent.GetRolesAsync(ct));

    [HttpGet("permissions")]
    [Authorize(Policy = "Roles")]
    public async Task<ActionResult<PermissionOptionDto[]>> GetPermissions(CancellationToken ct) =>
        Ok(await _roleDomainComponent.GetPermissionsAsync(ct));

    [HttpGet("access-types")]
    [Authorize(Policy = "Roles")]
    public async Task<ActionResult<AccessTypeOptionDto[]>> GetAccessTypes(CancellationToken ct) =>
        Ok(await _roleDomainComponent.GetAccessTypesAsync(ct));

    [HttpPut("roles/{id:int}/permissions")]
    [Authorize(Policy = "Roles:EDIT")]
    public async Task<ActionResult<RoleListItemDto>> UpdateRolePermissions(
        int id,
        UpdateRolePermissionsRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var updated = await _roleDomainComponent.UpdatePermissionsAsync(id, request, CurrentUserId(), CurrentUserName(), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ProtectedAccountException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("sessions")]
    [Authorize(Policy = "Sessions")]
    public async Task<ActionResult<SessionListItemDto[]>> GetSessions([FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await _sessionDomainComponent.GetSessionsAsync(take, ct));

    [HttpGet("sessions/active-count")]
    [Authorize(Policy = "Sessions")]
    public async Task<ActionResult<int>> GetActiveSessionCount(CancellationToken ct) =>
        Ok(await _sessionDomainComponent.GetActiveSessionCountAsync(ct));

    [HttpGet("audit-events")]
    [Authorize(Policy = "AuditLog")]
    public async Task<ActionResult<AuditEventListItemDto[]>> GetAuditEvents([FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await _sessionDomainComponent.GetAuditEventsAsync(take, ct));

    private int CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : throw new UnauthorizedAccessException("User identifier is missing.");
    }

    private string CurrentUserName() => User.Identity?.Name ?? "Unknown";
}
