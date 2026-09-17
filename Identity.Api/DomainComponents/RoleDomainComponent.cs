using Identity.Api.Data.Entities;
using Identity.Api.Data.Repositories;
using Identity.Api.DTOs;
using Identity.Api.Security;

namespace Identity.Api.DomainComponents;

public sealed class RoleDomainComponent : IRoleDomainComponent
{
    private readonly IAdministrationRepository _administrationRepository;

    public RoleDomainComponent(IAdministrationRepository administrationRepository)
    {
        _administrationRepository = administrationRepository;
    }

    public async Task<RoleListItemDto[]> GetRolesAsync(CancellationToken ct)
    {
        var result = new List<RoleListItemDto>();
        foreach (var role in await _administrationRepository.GetRolesAsync(ct))
            result.Add(MapRole(role, await _administrationRepository.RoleHasSystemAccountAsync(role.Id, ct)));
        return result.ToArray();
    }

    public async Task<PermissionOptionDto[]> GetPermissionsAsync(CancellationToken ct) =>
        (await _administrationRepository.GetPermissionsAsync(ct))
            .Select(x => new PermissionOptionDto(x.Id, x.Code, x.Name, x.Description))
            .ToArray();

    public async Task<AccessTypeOptionDto[]> GetAccessTypesAsync(CancellationToken ct) =>
        (await _administrationRepository.GetAccessTypesAsync(ct))
            .Select(x => new AccessTypeOptionDto(x.Id, x.Code, x.Name))
            .ToArray();

    public async Task<RoleListItemDto?> UpdatePermissionsAsync(
        int roleId,
        UpdateRolePermissionsRequestDto request,
        int actorUserId,
        string actorUserName,
        CancellationToken ct)
    {
        var role = await _administrationRepository.GetRoleAsync(roleId, ct);
        if (role is null) return null;
        if (await _administrationRepository.RoleHasSystemAccountAsync(roleId, ct))
            throw new ProtectedAccountException("Permissions of a role assigned to the system Administrator cannot be changed.");

        var permissions = await _administrationRepository.GetPermissionsAsync(ct);
        var accessTypes = await _administrationRepository.GetAccessTypesAsync(ct);
        var validPermissionIds = permissions.Select(x => x.Id).ToHashSet();
        var validAccessTypeIds = accessTypes.Select(x => x.Id).ToHashSet();
        var requested = request.Permissions
            .DistinctBy(x => new { x.PermissionId, x.AccessTypeId })
            .ToArray();

        if (requested.Any(x => !validPermissionIds.Contains(x.PermissionId) || !validAccessTypeIds.Contains(x.AccessTypeId)))
            throw new ArgumentException("One or more permission assignments are invalid.");

        var requestedKeys = requested.Select(x => (x.PermissionId, x.AccessTypeId)).ToHashSet();
        var removedAssignments = role.RolePermissions
            .Where(x => !requestedKeys.Contains((x.PermissionId, x.AccessTypeId)))
            .ToArray();
        foreach (var removed in removedAssignments)
            role.RolePermissions.Remove(removed);

        var existingKeys = role.RolePermissions
            .Select(x => (x.PermissionId, x.AccessTypeId))
            .ToHashSet();
        foreach (var assignment in requested.Where(x => !existingKeys.Contains((x.PermissionId, x.AccessTypeId))))
        {
            role.RolePermissions.Add(new RolePermissionEntity
            {
                RoleId = roleId,
                PermissionId = assignment.PermissionId,
                AccessTypeId = assignment.AccessTypeId,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = actorUserId
            });
        }

        await _administrationRepository.AddAuditEventAsync(new AuditEventEntity
        {
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            UserName = actorUserName,
            Action = "RolePermissionsUpdated",
            EntityType = "Role",
            EntityId = role.Id.ToString(),
            Description = $"Permissions updated for role {role.Name}",
            Success = true
        }, ct);
        await _administrationRepository.SaveChangesAsync(ct);
        return MapRole(role);
    }

    private static RoleListItemDto MapRole(RoleEntity role, bool isProtected = false) => new(
        role.Id,
        role.Name,
        role.Description,
        role.IsActive,
        role.RolePermissions
            .Select(x => new PermissionAssignmentDto(x.PermissionId, x.AccessTypeId))
            .ToArray(), isProtected);
}
