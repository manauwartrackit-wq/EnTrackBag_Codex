using Identity.Api.Data.Entities;
using Identity.Api.Data.Repositories;
using Identity.Api.DTOs;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Api.DomainComponents;

public class IdentityDomainComponent : IIdentityDomainComponent
{
    private readonly IIdentityRepository _identityRepository;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly IPasswordHasher<UserEntity> _passwordHasher;

    // MANDATORY REQUIREMENT: Standard constructor injection layout only (No primary constructors)
    public IdentityDomainComponent(IIdentityRepository identityRepository, IConfiguration configuration, IPasswordHasher<UserEntity> passwordHasher)
    {
        _identityRepository = identityRepository;

        // OPTIMIZATION: Read and cache configuration items at setup initialization phase
        _jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
        _jwtIssuer = configuration["Jwt:Issuer"] ?? string.Empty;
        _jwtAudience = configuration["Jwt:Audience"] ?? string.Empty;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, string? ip, string? userAgent, string? machineName, CancellationToken ct)
    {
        var user = await _identityRepository.GetUserForLoginAsync(request.UserName, ct);
        if (user is null || !user.IsActive)
            return null;
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            return null;

        var verification = VerifyPasswordHash(user, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            user.UpdatedAt = DateTime.UtcNow;
            await _identityRepository.SaveChangesAsync(ct);
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        var roles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        var permissionAccess = await _identityRepository.GetPermissionAccessAsync(user.Id, ct);
        var now = DateTime.UtcNow;
        var expires = now.AddHours(8);

        // Stage 1: Add Session and Save changes immediately to acquire the Database-generated SessionId
        var session = new UserSessionEntity
        {
            UserId = user.Id,
            LoginAt = now,
            LastActivityAt = now,
            RemoteIp = ip,
            UserAgent = userAgent,
            TokenIssuedAt = now,
            TokenExpiresAt = expires,
            IsActive = true
        };
        user.LastLoginAt = now;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        await _identityRepository.AddSessionAsync(session, ct);
        await _identityRepository.SaveChangesAsync(ct);

        // Stage 2: Append the Audit Event record now that session.SessionId is safely generated
        var auditEvent = new AuditEventEntity
        {
            UserId = user.Id,
            UserName = user.UserName,
            SessionId = session.SessionId,
            Action = "Login",
            Description = "User login",
            RemoteIp = ip,
            UserAgent = userAgent,
            Success = true
        };

        await _identityRepository.AddAuditEventAsync(auditEvent, ct);
        await _identityRepository.SaveChangesAsync(ct);

        // OPTIMIZATION: Pre-calculate allocation metrics capacity limit boundaries to speed up runtime execution loops
        int expectedClaimsCapacity = 4 + roles.Length + (permissionAccess.Count() * 2);

        var claims = new List<Claim>(expectedClaimsCapacity)
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName ?? user.UserName),
            new("session_id", session.SessionId.ToString())
        };

        foreach (var r in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
        }

        foreach (var p in permissionAccess)
        {
            claims.Add(new Claim("permission", p.Code));
            // OPTIMIZATION: Avoid multi-string concatenation heap leaks by using string interpolation
            claims.Add(new Claim("permission_access", $"{p.Code}:{p.AccessType}"));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var token = new JwtSecurityToken(
            _jwtIssuer,
            _jwtAudience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        var permissionsDto = permissionAccess.Select(x => new PermissionAccessDto(x.Code, x.AccessType)).ToArray();

        return new LoginResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            user.UserName,
            user.DisplayName ?? user.UserName,
            roles,
            permissionsDto,
            session.SessionId
        );
    }

    private PasswordVerificationResult VerifyPasswordHash(UserEntity user, string password)
    {
        try
        {
            return _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }

}
