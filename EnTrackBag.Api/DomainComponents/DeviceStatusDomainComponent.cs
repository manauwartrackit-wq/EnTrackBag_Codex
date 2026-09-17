using EnTrackBag.Api.Data.Repositories;
using EnTrackBag.Api.DTOs;
namespace EnTrackBag.Api.DomainComponents;

public class DeviceStatusDomainComponent : IDeviceStatusDomainComponent
{
    private readonly IReaderRepository _readerRepository;

    public DeviceStatusDomainComponent(IReaderRepository readerRepository)
    {
        _readerRepository = readerRepository;
    }

    public async Task<DeviceSummaryDto> GetSummaryAsync(CancellationToken ct)
    {
        var readers = await _readerRepository.GetReadersAsync(ct);
        var ants = await _readerRepository.GetAntennasAsync(ct);
        var ctrls = await _readerRepository.GetControllersAsync(ct);
        return new DeviceSummaryDto(new DeviceCountDto(0, 0, 0),
            Count(readers.Select(x => x.Status),
            readers.Select(x => x.LastError)),
            Count(ants.Select(x => x.Status), ants.Select(x => x.LastError)),
            Count(ctrls.Select(x => x.Status), ctrls.Select(x => x.LastError))
        );
    }

    public async Task<IReadOnlyList<DeviceDetailDto>> GetDetailsAsync(string? category, CancellationToken ct)
    {
        var result = new List<DeviceDetailDto>();
        if (category is null or "Reader")
        {
            foreach (var x in await _readerRepository.GetReadersAsync(ct))
                result.Add(new DeviceDetailDto("Reader",
                    x.ReaderCode ?? $"Reader {x.ID}",
                    x.ReaderLocation,
                    Normalize(x.Status, x.LastError),
                    x.ReaderIP,
                    x.LastError,
                    x.LastConnected,
                    x.LastDisconnected)
                );
        }
        if (category is null or "Antenna")
        {
            foreach (var x in await _readerRepository.GetAntennasAsync(ct))
                result.Add(new DeviceDetailDto("Antenna",
                    x.AntennaCode ?? $"Antenna {x.ID}",
                    x.AntennaLocation,
                    Normalize(x.Status, x.LastError),
                    null, x.LastError,
                    x.LastConnected,
                    x.LastDisconnected)
                );
        }
        if (category is null or "Controller")
        {
            foreach (var x in await _readerRepository.GetControllersAsync(ct))
                result.Add(new DeviceDetailDto("Controller",
                    x.ControllerName ?? $"Controller {x.ID}",
                    null,
                    Normalize(x.Status, x.LastError),
                    x.ControllerIP,
                    x.LastError,
                    x.LastConnected,
                    x.LastDisconnected)
                );
        }
        return result;
    }

    private static DeviceCountDto Count(IEnumerable<string?> statuses, IEnumerable<string?> errors)
    {
        var items = statuses.Zip(errors, (status, error) => new { status, error }).ToList();
        return new DeviceCountDto(items.Count,
            items.Count(x => Normalize(x.status, x.error) == "Online"),
            items.Count(x => Normalize(x.status, x.error) == "Offline"));
    }

    private static string Normalize(string? status, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return "Warning";
        if (status is "Connected" or "Online")
            return "Online";
        if (status is "Disconnected" or "Offline")
            return "Offline";
        return "Idle/Unknown";
    }
}
