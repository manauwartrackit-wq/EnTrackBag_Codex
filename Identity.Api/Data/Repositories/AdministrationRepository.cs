using Identity.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Data.Repositories;

public class AdministrationRepository : IAdministrationRepository
{
    private readonly IdentityDbContext _identityDbContext;

    public AdministrationRepository(IdentityDbContext identityDbContext)
    {
        _identityDbContext = identityDbContext;
    }

    public Task<bool> RoleHasSystemAccountAsync(int roleId, CancellationToken ct) =>
        _identityDbContext.UserRoles.AnyAsync(x => x.RoleId == roleId && x.User.UserName.ToLower() == "admin", ct);

    public Task<RoleEntity[]> GetRolesAsync(CancellationToken ct) =>
        _identityDbContext.Roles
            .AsNoTracking()
            .Include(x => x.RolePermissions)
            .OrderBy(x => x.Name)
            .ToArrayAsync(ct);

    public Task<RoleEntity?> GetRoleAsync(int id, CancellationToken ct) =>
        _identityDbContext.Roles
            .Include(x => x.RolePermissions)
            .SingleOrDefaultAsync(x => x.Id == id, ct);

    public Task<PermissionEntity[]> GetPermissionsAsync(CancellationToken ct) =>
        _identityDbContext.Permissions.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToArrayAsync(ct);

    public Task<AccessTypeEntity[]> GetAccessTypesAsync(CancellationToken ct) =>
        _identityDbContext.AccessTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).ToArrayAsync(ct);

    public Task<UserSessionEntity[]> GetSessionsAsync(int take, CancellationToken ct) =>
        _identityDbContext.UserSessions
            .AsNoTracking()
            .Include(x => x.User)
            .OrderByDescending(x => x.LoginAt)
            .Take(take)
            .ToArrayAsync(ct);

    public Task<int> GetActiveSessionCountAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddMinutes(-5);
        return _identityDbContext.UserSessions.CountAsync(x => x.IsActive && x.LogoutAt == null &&
            x.TokenExpiresAt > now && x.LastActivityAt > cutoff && x.User.IsActive, ct);
    }

    public Task<AuditEventEntity[]> GetAuditEventsAsync(int take, CancellationToken ct) =>
        _identityDbContext.AuditEvents
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .ToArrayAsync(ct);

    public Task AddAuditEventAsync(AuditEventEntity auditEvent, CancellationToken ct) =>
        _identityDbContext.AuditEvents.AddAsync(auditEvent, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct) => _identityDbContext.SaveChangesAsync(ct);
}
