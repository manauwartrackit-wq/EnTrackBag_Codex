using Identity.Api.Data.Entities;

namespace Identity.Api.Data.Repositories;

public interface IAdministrationRepository
{
    Task<bool> RoleHasSystemAccountAsync(int roleId, CancellationToken ct);
    Task<RoleEntity[]> GetRolesAsync(CancellationToken ct);
    Task<RoleEntity?> GetRoleAsync(int id, CancellationToken ct);
    Task<PermissionEntity[]> GetPermissionsAsync(CancellationToken ct);
    Task<AccessTypeEntity[]> GetAccessTypesAsync(CancellationToken ct);
    Task<UserSessionEntity[]> GetSessionsAsync(int take, CancellationToken ct);
    Task<int> GetActiveSessionCountAsync(CancellationToken ct);
    Task<AuditEventEntity[]> GetAuditEventsAsync(int take, CancellationToken ct);
    Task AddAuditEventAsync(AuditEventEntity auditEvent, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
