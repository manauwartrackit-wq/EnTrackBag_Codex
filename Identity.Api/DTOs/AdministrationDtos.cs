namespace Identity.Api.DTOs;

public class UserListItemDto
{
    public bool IsProtectedSystemAccount { get; init; }
    public int Id { get; init; }
    public string? EmpCode { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PassportMasked { get; init; }
    public string? Nationality { get; init; }
    public string? Designation { get; init; }
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public DateTime? LastLogoutAt { get; init; }
    public int FailedLoginCount { get; init; }
    public DateTime? LockedUntil { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int? CreatedBy { get; init; }
    public int? UpdatedBy { get; init; }
    public int[] RoleIds { get; init; } = [];
    public string[] Roles { get; init; } = [];
}

public class CreateUserRequestDto
{
    public string EmpCode { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PassportNumber { get; init; } = string.Empty;
    public string Nationality { get; init; } = string.Empty;
    public string? Designation { get; init; }
    public string Password { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public bool MustChangePassword { get; init; } = false;
    public int[] RoleIds { get; init; } = [];
}

public class UpdateUserRequestDto
{
    public string EmpCode { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PassportNumber { get; init; }
    public string Nationality { get; init; } = string.Empty;
    public string? Designation { get; init; }
    public bool IsActive { get; init; }
    public bool MustChangePassword { get; init; }
    public int[] RoleIds { get; init; } = [];
}

public class ResetUserPasswordRequestDto
{
    public string TemporaryPassword { get; init; } = string.Empty;
    public bool MustChangePassword { get; init; } = true;
}

public class PassportDetailDto
{
    public int UserId { get; init; }
    public string EmpCode { get; init; } = string.Empty;
    public string PassportNumber { get; init; } = string.Empty;
    public string PassportMasked { get; init; } = string.Empty;
}

public sealed record PermissionAssignmentDto(int PermissionId, int AccessTypeId);

public sealed record RoleListItemDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    PermissionAssignmentDto[] Permissions, bool IsProtected = false);

public sealed record PermissionOptionDto(int Id, string Code, string Name, string? Description);
public sealed record AccessTypeOptionDto(int Id, string Code, string Name);
public sealed record UpdateRolePermissionsRequestDto(PermissionAssignmentDto[] Permissions);

public sealed record SessionListItemDto(
    long SessionId,
    int UserId,
    string UserName,
    string DisplayName,
    DateTime LoginAt,
    DateTime? LogoutAt,
    string? RemoteIp,
    string? UserAgent,
    DateTime? TokenExpiresAt,
    bool IsActive,
    DateTime LastActivityAt,
    DateTime IdleExpiresAt,
    string Status);

public sealed record AuditEventListItemDto(
    long Id,
    DateTime OccurredAt,
    string? UserName,
    string Action,
    string? EntityType,
    string? EntityId,
    string? Description,
    bool Success,
    string? CorrelationId);
