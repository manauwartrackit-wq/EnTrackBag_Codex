using System.Buffers.Binary;
using System.Security.Cryptography;
using Identity.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Security;

// ASP.NET Identity V3 envelope: marker, PRF (2 = SHA512), iterations, salt length,
// random salt and derived key. Compatible existing SHA512 hashes remain usable.
public sealed class Pbkdf2Sha512PasswordHasher : IPasswordHasher<UserEntity>
{
    private const int Iterations = 210_000;
    public string HashPassword(UserEntity user, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, 32);
        var bytes = new byte[61];
        bytes[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(1), 2);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(5), Iterations);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(9), 16);
        salt.CopyTo(bytes, 13);
        key.CopyTo(bytes, 29);
        return Convert.ToBase64String(bytes);
    }

    public PasswordVerificationResult VerifyHashedPassword(UserEntity user, string hashedPassword, string providedPassword)
    {
        try
        {
            var bytes = Convert.FromBase64String(hashedPassword);
            if (bytes.Length < 61 || bytes[0] != 1 || BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1)) != 2)
                return PasswordVerificationResult.Failed;
            var iterations = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5));
            var saltLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(9));
            if (iterations is < 1 or > 1_000_000 || saltLength is < 16 or > 64 || bytes.Length < 13 + (int)saltLength + 32)
                return PasswordVerificationResult.Failed;
            var salt = bytes.AsSpan(13, (int)saltLength);
            var expected = bytes.AsSpan(13 + (int)saltLength);
            if (expected.Length > 64) return PasswordVerificationResult.Failed;
            var actual = Rfc2898DeriveBytes.Pbkdf2(providedPassword, salt.ToArray(), (int)iterations, HashAlgorithmName.SHA512, expected.Length);
            if (!CryptographicOperations.FixedTimeEquals(actual, expected)) return PasswordVerificationResult.Failed;
            return iterations < Iterations ? PasswordVerificationResult.SuccessRehashNeeded : PasswordVerificationResult.Success;
        }
        catch (FormatException) { return PasswordVerificationResult.Failed; }
    }
}
