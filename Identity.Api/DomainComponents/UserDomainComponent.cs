using Identity.Api.Data.Entities;
using Identity.Api.Data.Repositories;
using Identity.Api.DTOs;
using Identity.Api.Security;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.DomainComponents;

public class UserDomainComponent : IUserDomainComponent
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;
    private readonly IPassportProtector _passportProtector;

    public UserDomainComponent(
        IUserRepository userRepository,
        IPasswordHasher<UserEntity> passwordHasher,
        IPassportProtector passportProtector)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passportProtector = passportProtector;
    }

    public async Task<UserListItemDto[]> GetUsersAsync(string? search, int? roleId, bool? isActive, CancellationToken ct) =>
        (await _userRepository.GetUsersAsync(search, roleId, isActive, ct)).Select(user => MapUser(user)).ToArray();

    public async Task<PassportDetailDto?> GetPassportAsync(int id, int actorUserId, string actorUserName, CancellationToken ct)
    {
        var user = await _userRepository.GetUserAsync(id, ct);
        if (user?.PassportNumberEncrypted is null) return null;
        var passportNumber = _passportProtector.Unprotect(user.PassportNumberEncrypted);
        await AddAuditAsync(actorUserId, actorUserName, "UserPassportViewed", user.Id.ToString(), ct);
        await _userRepository.SaveChangesAsync(ct);
        return new PassportDetailDto { UserId = user.Id, EmpCode = user.EmpCode ?? string.Empty,
            PassportNumber = passportNumber, PassportMasked = MaskPassport(user.PassportLast4) ?? string.Empty };
    }

    public async Task<UserListItemDto> CreateUserAsync(
        CreateUserRequestDto request,
        int actorUserId,
        string actorUserName,
        CancellationToken ct)
    {
        var values = ValidateProfile(request.EmpCode, request.UserName, request.FirstName, request.LastName, request.Email);
        if (SystemAccount.IsReservedName(values.UserName)) throw new ProtectedAccountException("The admin user name is reserved for the system account.");
        var employment = ValidateEmployment(request.Nationality, request.Designation);
        var passportNumber = NormalizePassport(request.PassportNumber, false);
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        if (request.Password != request.ConfirmPassword) throw new ArgumentException("Passwords do not match.");
        await EnsureUniqueAsync(values.EmpCode, values.UserName, values.Email, null, ct);

        var roles = await _userRepository.GetRolesAsync(ct);
        var selectedRoleIds = request.RoleIds.Distinct().ToHashSet();
        if (selectedRoleIds.Except(roles.Select(x => x.Id)).Any())
            throw new ArgumentException("One or more selected roles are invalid.");

        var now = DateTime.UtcNow;
        var user = new UserEntity
        {
            EmpCode = values.EmpCode, UserName = values.UserName, FirstName = values.FirstName,
            LastName = values.LastName, DisplayName = values.DisplayName, Email = values.Email,
            PassportNumberEncrypted = passportNumber is null ? null : _passportProtector.Protect(passportNumber), PassportLast4 = passportNumber is null ? null : passportNumber[^4..],
            Nationality = employment.Nationality, Designation = employment.Designation,
            IsActive = request.IsActive, MustChangePassword = false,
            CreatedAt = now, CreatedBy = actorUserId, FailedLoginCount = 0
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        foreach (var roleId in selectedRoleIds)
            user.UserRoles.Add(new UserRoleEntity { RoleId = roleId, AssignedAt = now, AssignedBy = actorUserId });

        await _userRepository.AddUserAsync(user, ct);
        await AddAuditAsync(actorUserId, actorUserName, "UserCreated", user.UserName, ct);
        await _userRepository.SaveChangesAsync(ct);
        return MapUser(user, roles);
    }

    public async Task<UserListItemDto?> UpdateUserAsync(
        int id,
        UpdateUserRequestDto request,
        int actorUserId,
        string actorUserName,
        CancellationToken ct)
    {
        var user = await _userRepository.GetUserAsync(id, ct);
        if (user is null) return null;
        SystemAccount.RejectModification(user);
        if (id == actorUserId && !request.IsActive)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        var values = ValidateProfile(request.EmpCode, request.UserName, request.FirstName, request.LastName, request.Email);
        var employment = ValidateEmployment(request.Nationality, request.Designation);
        var passportNumber = NormalizePassport(request.PassportNumber, false);
        await EnsureUniqueAsync(values.EmpCode, values.UserName, values.Email, id, ct);
        var roles = await _userRepository.GetRolesAsync(ct);
        var selectedRoleIds = request.RoleIds.Distinct().ToHashSet();
        if (selectedRoleIds.Except(roles.Select(x => x.Id)).Any())
            throw new ArgumentException("One or more selected roles are invalid.");
        if (SystemAccount.IsReservedName(values.UserName)) throw new ProtectedAccountException("The admin user name is reserved for the system account.");
        user.EmpCode = values.EmpCode; user.UserName = values.UserName; user.FirstName = values.FirstName;
        user.LastName = values.LastName; user.DisplayName = values.DisplayName; user.Email = values.Email;
        user.Nationality = employment.Nationality; user.Designation = employment.Designation;
        if (passportNumber is not null)
        {
            user.PassportNumberEncrypted = _passportProtector.Protect(passportNumber);
            user.PassportLast4 = passportNumber[^4..];
        }
        user.IsActive = request.IsActive;
        user.MustChangePassword = request.MustChangePassword;
        user.UpdatedAt = DateTime.UtcNow; user.UpdatedBy = actorUserId;

        var removed = user.UserRoles.Where(x => !selectedRoleIds.Contains(x.RoleId)).ToArray();
        foreach (var role in removed) user.UserRoles.Remove(role);
        foreach (var roleId in selectedRoleIds.Except(user.UserRoles.Select(x => x.RoleId)))
            user.UserRoles.Add(new UserRoleEntity { UserId = id, RoleId = roleId, AssignedAt = DateTime.UtcNow, AssignedBy = actorUserId });

        await AddAuditAsync(actorUserId, actorUserName, "UserUpdated", user.UserName, ct);
        await _userRepository.SaveChangesAsync(ct);
        return MapUser(user, roles);
    }

    public async Task<bool> ResetPasswordAsync(int id, ResetUserPasswordRequestDto request, int actorUserId, string actorUserName, CancellationToken ct)
    {
        if (request.TemporaryPassword.Length < 8) throw new ArgumentException("Password must be at least 8 characters.");
        var user = await _userRepository.GetUserAsync(id, ct);
        if (user is null) return false;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.TemporaryPassword);
        user.MustChangePassword = SystemAccount.IsProtected(user) ? false : request.MustChangePassword;
        user.FailedLoginCount = 0; user.LockedUntil = null; user.UpdatedAt = DateTime.UtcNow; user.UpdatedBy = actorUserId;
        await AddAuditAsync(actorUserId, actorUserName, "UserPasswordReset", user.UserName, ct);
        await _userRepository.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveUserAsync(
        int id,
        int actorUserId,
        string actorUserName,
        CancellationToken ct)
    {
        if (id == actorUserId)
            throw new InvalidOperationException("You cannot remove your own account.");

        var user = await _userRepository.GetUserAsync(id, ct);
        if (user is null) return false;

        SystemAccount.RejectModification(user);
        var userName = user.UserName;
        if (await _userRepository.UserHasHistoryAsync(id, ct))
        {
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = actorUserId;
        }
        else
        {
            _userRepository.RemoveUser(user);
        }

        await AddAuditAsync(actorUserId, actorUserName, "UserRemoved", userName, ct);
        await _userRepository.SaveChangesAsync(ct);
        return true;
    }

    private Task AddAuditAsync(int actorUserId, string actorUserName, string action, string target, CancellationToken ct) =>
        _userRepository.AddAuditEventAsync(new AuditEventEntity
        {
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            UserName = actorUserName,
            Action = action,
            EntityType = "User",
            EntityId = target,
            Description = $"{action}: {target}",
            Success = true
        }, ct);

    private static UserListItemDto MapUser(UserEntity user, RoleEntity[]? knownRoles = null)
    {
        var roleIds = user.UserRoles.Select(x => x.RoleId).OrderBy(x => x).ToArray();
        var roleNames = knownRoles is null
            ? user.UserRoles.Select(x => x.Role.Name).OrderBy(x => x).ToArray()
            : knownRoles.Where(x => roleIds.Contains(x.Id)).Select(x => x.Name).OrderBy(x => x).ToArray();
        return new UserListItemDto { IsProtectedSystemAccount=SystemAccount.IsProtected(user), Id=user.Id, EmpCode=user.EmpCode, UserName=user.UserName, FirstName=user.FirstName,
            LastName=user.LastName, DisplayName=user.DisplayName ?? user.UserName, Email=user.Email,
            PassportMasked=MaskPassport(user.PassportLast4), Nationality=user.Nationality, Designation=user.Designation, IsActive=user.IsActive,
            MustChangePassword=user.MustChangePassword, LastLoginAt=user.LastLoginAt, LastLogoutAt=user.LastLogoutAt,
            FailedLoginCount=user.FailedLoginCount, LockedUntil=user.LockedUntil, CreatedAt=user.CreatedAt,
            UpdatedAt=user.UpdatedAt, CreatedBy=user.CreatedBy, UpdatedBy=user.UpdatedBy, RoleIds=roleIds, Roles=roleNames };
    }

    private async Task EnsureUniqueAsync(string? empCode, string userName, string email, int? exceptId, CancellationToken ct)
    {
        if (empCode is not null && await _userRepository.EmpCodeExistsAsync(empCode, exceptId, ct)) throw new InvalidOperationException("Employee code already exists.");
        if (await _userRepository.UserNameExistsAsync(userName, exceptId, ct)) throw new InvalidOperationException("User name already exists.");
        if (await _userRepository.EmailExistsAsync(email, exceptId, ct)) throw new InvalidOperationException("Email address already exists.");
    }

    private static (string? EmpCode,string UserName,string FirstName,string LastName,string DisplayName,string Email) ValidateProfile(string empCode,string userName,string firstName,string lastName,string email)
    {
        var values=(EmpCode:string.IsNullOrWhiteSpace(empCode) ? null : empCode.Trim(),UserName:userName.Trim(),FirstName:firstName.Trim(),LastName:lastName.Trim(),DisplayName:$"{firstName.Trim()} {lastName.Trim()}".Trim(),Email:email.Trim().ToLowerInvariant());
        if (string.IsNullOrWhiteSpace(values.UserName)||string.IsNullOrWhiteSpace(values.FirstName)||string.IsNullOrWhiteSpace(values.LastName)||string.IsNullOrWhiteSpace(values.Email)) throw new ArgumentException("User name, first name, last name and email are required.");
        try { _ = new System.Net.Mail.MailAddress(values.Email); } catch (FormatException) { throw new ArgumentException("Enter a valid email address."); }
        return values;
    }

    private static (string Nationality, string? Designation) ValidateEmployment(string nationality, string? designation)
    {
        var values = (Nationality: nationality.Trim(), Designation: string.IsNullOrWhiteSpace(designation) ? null : designation.Trim());
        return values;
    }

    private static string? NormalizePassport(string? passportNumber, bool required)
    {
        var normalized = passportNumber?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            if (required) throw new ArgumentException("Passport number is required.");
            return null;
        }
        if (normalized.Length is < 4 or > 30 || normalized.Any(char.IsControl))
            throw new ArgumentException("Passport number must contain between 4 and 30 printable characters.");
        return normalized;
    }

    private static string? MaskPassport(string? last4) => string.IsNullOrWhiteSpace(last4) ? null : $"********{last4}";
}
