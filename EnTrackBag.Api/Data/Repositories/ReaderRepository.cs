using EnTrackBag.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
namespace EnTrackBag.Api.Data.Repositories;

public class ReaderRepository : IReaderRepository
{
    private readonly BltsmftDbContext _db; public ReaderRepository(BltsmftDbContext db)
    {
        _db = db;
    }
    public Task<List<ReaderEntity>> GetReadersAsync(CancellationToken ct) =>
        _db.Readers.AsNoTracking().Where(x => x.IsActive != false).ToListAsync(ct);

    public Task<List<AntennaEntity>> GetAntennasAsync(CancellationToken ct) =>
        _db.Antennas.AsNoTracking().Where(x => x.IsActive != false).ToListAsync(ct);

    public Task<List<ControllerEntity>> GetControllersAsync(CancellationToken ct) =>
        _db.Controllers.AsNoTracking().Where(x => x.IsActive != false).ToListAsync(ct);
}
