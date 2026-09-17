using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using EnTrackBag.Sessions;
using EnTrackBag.Api.DomainComponents;
using System.Text.Json;

namespace EnTrackBag.Api.Hubs;

[Authorize]
public class MonitoringHub : Hub
{
    private readonly SessionRepository _sessions;
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<MonitoringHub> _hub;
    public MonitoringHub(SessionRepository sessions, IServiceScopeFactory scopes, IHubContext<MonitoringHub> hub) { _sessions = sessions; _scopes = scopes; _hub = hub; }

    public void SubscribePage(string channel)
    {
        var permission = channel == "Dashboard.SLA" ? EnTrackBag.Authorization.PermissionCodes.DashboardSla : channel;
        if (channel != "" && (channel is not ("Dashboard" or "Dashboard.SLA" or "DeviceStatus") ||
            !Context.User!.HasClaim("permission_access", permission + ":VIEW")))
            throw new HubException("Access denied.");
        Context.Items["channel"] = channel;
    }

    public override async Task OnConnectedAsync()
    {
        var context = Context;
        var sid = long.Parse(context.User!.FindFirstValue("session_id")!);
        var uid = int.Parse(context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);
        // An established WebSocket doesn't pass through JWT validation again.
        // Check its session without treating keep-alives as user activity.
        _ = WatchSessionAsync(context, sid, uid);
        await base.OnConnectedAsync();
    }

    private async Task WatchSessionAsync(HubCallerContext context, long sid, int uid)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            var ticks = 0;
            string? previous = null;
            string? previousChannel = null;
            while (await timer.WaitForNextTickAsync(context.ConnectionAborted))
            {
                if (!await _sessions.ValidateAsync(sid, uid, context.ConnectionAborted))
                { context.Abort(); return; }
                // Reuse the session watcher; read only the subscribed page every 15 seconds.
                if (++ticks % 3 != 0) continue;
                var channel = context.Items.TryGetValue("channel", out var selected) ? selected as string : "";
                if (string.IsNullOrEmpty(channel)) continue;
                try
                {
                    using var scope = _scopes.CreateScope();
                    object payload;
                    string eventName;
                    if (channel == "Dashboard")
                    {
                        payload = await scope.ServiceProvider.GetRequiredService<IDashboardDomainComponent>().GetKpisAsync(context.ConnectionAborted);
                        eventName = "DashboardUpdated";
                    }
                    else if (channel == "Dashboard.SLA")
                    {
                        payload = await scope.ServiceProvider.GetRequiredService<ISlaDomainComponent>().GetSlaAsync(context.ConnectionAborted);
                        eventName = "SlaUpdated";
                    }
                    else
                    {
                        var devices = scope.ServiceProvider.GetRequiredService<IDeviceStatusDomainComponent>();
                        payload = new { summary = await devices.GetSummaryAsync(context.ConnectionAborted),
                            details = await devices.GetDetailsAsync(null, context.ConnectionAborted) };
                        eventName = "SystemUpdated";
                    }
                    var serialized = JsonSerializer.Serialize(payload);
                    if (previousChannel == channel && previous == serialized) continue;
                    previousChannel = channel; previous = serialized;
                    await _hub.Clients.Client(context.ConnectionId).SendAsync(eventName, payload, context.ConnectionAborted);
                    if (channel == "Dashboard.SLA")
                    {
                        var json = JsonSerializer.SerializeToElement(payload);
                        var count = json.EnumerateArray().Count(x => x.TryGetProperty("SlaBreach", out var value) && value.GetBoolean());
                        await _hub.Clients.Client(context.ConnectionId).SendAsync("AlarmUpdated", count, context.ConnectionAborted);
                    }
                }
                catch (OperationCanceledException) when (context.ConnectionAborted.IsCancellationRequested) { return; }
                catch { /* Operational data failure must not interrupt session validation. */ }
            }
        }
        catch (OperationCanceledException) when (context.ConnectionAborted.IsCancellationRequested) { }
        catch { context.Abort(); } // Fail closed if session validation is unavailable.
    }
}
