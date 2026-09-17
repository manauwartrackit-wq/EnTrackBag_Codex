using EnTrackBag.Api.DomainComponents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace EnTrackBag.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardDomainComponent _dashboardDomainComponent;
    private readonly ISlaDomainComponent _slaDomainComponent;

    public DashboardController(IDashboardDomainComponent dashboardDomainComponent, ISlaDomainComponent slaDomainComponent)
    {
        _dashboardDomainComponent = dashboardDomainComponent;
        _slaDomainComponent = slaDomainComponent;
    }

    [HttpGet("kpis")]
    [Authorize(Policy = "Dashboard")]
    public async Task<IActionResult> GetKpis(CancellationToken ct) => Ok(await _dashboardDomainComponent.GetKpisAsync(ct));

    [HttpGet("sla")]
    [Authorize(Policy = EnTrackBag.Authorization.PermissionCodes.DashboardSla)]
    public async Task<IActionResult> GetSla(CancellationToken ct) => Ok(await _slaDomainComponent.GetSlaAsync(ct));

    [HttpGet("bags/{bagId}/history")]
    [Authorize(Policy = "BagHistory")]
    public async Task<IActionResult> GetHistory(string bagId, CancellationToken ct)
    {
        var result = await _dashboardDomainComponent.GetHistoryAsync(bagId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
