using Identity.Api.Data.Entities;

namespace Identity.Api.Security;

public static class SystemAccount
{
    public const string UserName = "admin";
    public static bool IsProtected(UserEntity user) => IsReservedName(user.UserName);
    public static bool IsReservedName(string name) =>
        string.Equals(name.Trim(), UserName, StringComparison.OrdinalIgnoreCase);
    public static void RejectModification(UserEntity user)
    {
        if (IsProtected(user))
            throw new ProtectedAccountException("The system Administrator can only be viewed or have its password changed.");
    }
}

public sealed class ProtectedAccountException(string message) : Exception(message);
