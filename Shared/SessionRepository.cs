using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;

namespace EnTrackBag.Sessions;

// Shared by both APIs: the database, not JWT lifetime or browser state, is authoritative.
public sealed class SessionRepository
{
    private readonly string _connectionString;
    public SessionRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BLTSMFT")
            ?? throw new InvalidOperationException("BLTSMFT connection missing.");
    }

    public static async Task ValidateTokenAsync(TokenValidatedContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Principal?.FindFirstValue("sub");
        if (!int.TryParse(userId, out var uid) ||
            !long.TryParse(context.Principal?.FindFirstValue("session_id"), out var sid) ||
            !await context.HttpContext.RequestServices.GetRequiredService<SessionRepository>()
                .ValidateAsync(sid, uid, context.HttpContext.RequestAborted))
            context.Fail("Session expired, logged out or revoked.");
    }

    public async Task<bool> ValidateAsync(long sessionId, int userId, CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.UserSessions SET IsActive=0, LogoutAt=SYSUTCDATETIME(), LogoutReason='Expired'
            WHERE SessionId=@sid AND UserId=@uid AND IsActive=1
              AND (LastActivityAt<=DATEADD(minute,-5,SYSUTCDATETIME())
                   OR TokenExpiresAt IS NULL OR TokenExpiresAt<=SYSUTCDATETIME());
            SELECT COUNT(*) FROM dbo.UserSessions s INNER JOIN dbo.Users u ON u.Id=s.UserId
            WHERE s.SessionId=@sid AND s.UserId=@uid AND s.IsActive=1 AND s.LogoutAt IS NULL
              AND s.LastActivityAt>DATEADD(minute,-5,SYSUTCDATETIME())
              AND s.TokenExpiresAt>SYSUTCDATETIME() AND u.IsActive=1;
            """;
        command.Parameters.Add("@sid", SqlDbType.BigInt).Value = sessionId;
        command.Parameters.Add("@uid", SqlDbType.Int).Value = userId;
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1;
    }

    public async Task<bool> TouchAsync(long sessionId, int userId, CancellationToken ct, int activityAgeMs = 0)
    {
        // Never revive an expired session, even if expiry raced token validation.
        return await ExecuteAsync("""
            UPDATE dbo.UserSessions SET LastActivityAt=CASE WHEN LastActivityAt>DATEADD(millisecond,-@age,SYSUTCDATETIME()) THEN LastActivityAt ELSE DATEADD(millisecond,-@age,SYSUTCDATETIME()) END
            WHERE SessionId=@sid AND UserId=@uid AND IsActive=1 AND LogoutAt IS NULL
              AND LastActivityAt>DATEADD(minute,-5,SYSUTCDATETIME()) AND TokenExpiresAt>SYSUTCDATETIME();
            """, sessionId, userId, ct, Math.Clamp(activityAgeMs, 0, 60000)) == 1;
    }

    public Task LogoutAsync(long sessionId, int userId, CancellationToken ct) => ExecuteAsync("""
        UPDATE dbo.UserSessions SET IsActive=0, LogoutAt=SYSUTCDATETIME(), LogoutReason='Logged out'
        WHERE SessionId=@sid AND UserId=@uid AND IsActive=1;
        """, sessionId, userId, ct);

    public Task ExpireAsync(CancellationToken ct) => ExecuteAsync("""
        UPDATE dbo.UserSessions SET IsActive=0, LogoutAt=SYSUTCDATETIME(), LogoutReason='Expired'
        WHERE IsActive=1 AND (LogoutAt IS NOT NULL OR LastActivityAt<=DATEADD(minute,-5,SYSUTCDATETIME())
            OR TokenExpiresAt IS NULL OR TokenExpiresAt<=SYSUTCDATETIME());
        """, 0, 0, ct);

    private async Task<int> ExecuteAsync(string sql, long sessionId, int userId, CancellationToken ct, int activityAgeMs = 0)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add("@age", SqlDbType.Int).Value = activityAgeMs;
        command.Parameters.Add("@sid", SqlDbType.BigInt).Value = sessionId;
        command.Parameters.Add("@uid", SqlDbType.Int).Value = userId;
        return await command.ExecuteNonQueryAsync(ct);
    }
}

public sealed class SessionExpiryWorker : BackgroundService
{
    private readonly SessionRepository _sessions;
    private readonly ILogger<SessionExpiryWorker> _logger;
    public SessionExpiryWorker(SessionRepository sessions, ILogger<SessionExpiryWorker> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        do
        {
            try
            {
                await _sessions.ExpireAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { _logger.LogError(ex, "Session expiry sweep failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

// Only explicit, batched interaction reports extend a session. Ordinary API requests, hubs, logout,
// status checks and refresh endpoints cannot extend it, even with the header.
public sealed class SessionActivityMiddleware
{
    private readonly RequestDelegate _next;
    public SessionActivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }
    public async Task InvokeAsync(HttpContext context, SessionRepository sessions)
    {
        var path = context.Request.Path;
        var foreground = context.Request.Headers["X-User-Activity"] == "1";
        if (context.User.Identity?.IsAuthenticated == true && foreground &&
            path == "/api/auth/activity")
        {
            var sid = long.Parse(context.User.FindFirstValue("session_id")!);
            var uid = int.Parse(context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub")!);
            var age = int.TryParse(context.Request.Headers["X-Activity-Age-Ms"], out var elapsed) ? Math.Clamp(elapsed, 0, 60000) : 0;
            if (!await sessions.TouchAsync(sid, uid, context.RequestAborted, age))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }
        await _next(context);
    }
}
