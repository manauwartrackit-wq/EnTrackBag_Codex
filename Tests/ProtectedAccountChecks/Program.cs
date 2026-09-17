using Identity.Api.Data.Entities;
using Identity.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Buffers.Binary;
using Identity.Api.Data.Repositories;
using Identity.Api.DomainComponents;
using Identity.Api.DTOs;

var checks = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var admin = new UserEntity { UserName = "admin", DisplayName = "Administrator" };
Check(SystemAccount.IsProtected(admin), "admin is protected");
Check(SystemAccount.IsReservedName(" ADMIN "), "reserved name cannot be bypassed with casing/space");
foreach (var name in new[] { "gopi", "murli", "rishi" })
{
    var normal = new UserEntity { UserName = name, DisplayName = "Administrator" };
    SystemAccount.RejectModification(normal);
    Check(!SystemAccount.IsProtected(normal), name + " remains manageable irrespective of display name");
}
try { SystemAccount.RejectModification(admin); throw new Exception("Administrator modification allowed"); }
catch (ProtectedAccountException) { Check(true, "Administrator modification rejected"); }
var hasher = new Pbkdf2Sha512PasswordHasher();
const string password = "test-only-password-not-an-account";
var hash = hasher.HashPassword(admin, password);
var bytes = Convert.FromBase64String(hash);
Check(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)) == 2, "hash PRF is HMAC-SHA512");
Check(BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5)) == 210000, "210000 iterations");
Check(hash != hasher.HashPassword(admin, password), "random salt per hash");
Check(hasher.VerifyHashedPassword(admin, hash, password) == PasswordVerificationResult.Success, "correct password verifies");
Check(hasher.VerifyHashedPassword(admin, hash, "wrong") == PasswordVerificationResult.Failed, "wrong password rejected");
Check(hasher.VerifyHashedPassword(admin, "not-base64", password) == PasswordVerificationResult.Failed, "malformed hash rejected");
Check(hasher.VerifyHashedPassword(admin, Convert.ToBase64String(new byte[32]), password) == PasswordVerificationResult.Failed, "legacy/non-PBKDF2 payload rejected");
var legacyV3 = new PasswordHasher<UserEntity>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3, IterationCount = 100000 }));
Check(hasher.VerifyHashedPassword(admin, legacyV3.HashPassword(admin, password), password) == PasswordVerificationResult.SuccessRehashNeeded, "existing Identity V3 SHA512 hash verifies and upgrades");
Check(legacyV3.VerifyHashedPassword(admin, hash, password) != PasswordVerificationResult.Failed, "generated envelope is Identity V3 compatible");
admin.Id = 1;
var repository = new UserRepositoryFake(admin);
var domain = new UserDomainComponent(repository, hasher, null!);
try { await domain.UpdateUserAsync(1, new UpdateUserRequestDto { IsActive = false }, 2, "operator", default); throw new Exception("Update allowed"); }
catch (ProtectedAccountException) { Check(repository.Saves == 0, "API domain blocks Administrator edits before saving"); }
try { await domain.RemoveUserAsync(1, 2, "operator", default); throw new Exception("Delete allowed"); }
catch (ProtectedAccountException) { Check(repository.Saves == 0, "API domain blocks Administrator deletion before saving"); }
await domain.ResetPasswordAsync(1, new ResetUserPasswordRequestDto { TemporaryPassword = password }, 1, "admin", default);
Check(repository.Saves == 1 && hasher.VerifyHashedPassword(admin, admin.PasswordHash, password) == PasswordVerificationResult.Success, "Administrator password change remains allowed");
Check(!admin.MustChangePassword, "protected password change does not impose temporary-password flag");
Console.WriteLine($"{checks} checks passed. No database accessed.");

sealed class UserRepositoryFake(UserEntity user) : IUserRepository
{
    public int Saves { get; private set; }
    public Task<UserEntity?> GetUserAsync(int id, CancellationToken ct) => Task.FromResult<UserEntity?>(user);
    public Task<UserEntity[]> GetUsersAsync(string? search, int? roleId, bool? isActive, CancellationToken ct) => Task.FromResult(new[] { user });
    public Task<bool> UserNameExistsAsync(string value, int? id, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> EmpCodeExistsAsync(string value, int? id, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> EmailExistsAsync(string value, int? id, CancellationToken ct) => Task.FromResult(false);
    public Task<RoleEntity[]> GetRolesAsync(CancellationToken ct) => Task.FromResult(Array.Empty<RoleEntity>());
    public Task<bool> UserHasHistoryAsync(int id, CancellationToken ct) => Task.FromResult(false);
    public Task AddUserAsync(UserEntity value, CancellationToken ct) => Task.CompletedTask;
    public void RemoveUser(UserEntity value) => throw new Exception("Protected account reached delete repository");
    public Task AddAuditEventAsync(AuditEventEntity value, CancellationToken ct) => Task.CompletedTask;
    public Task SaveChangesAsync(CancellationToken ct) { Saves++; return Task.CompletedTask; }
}
