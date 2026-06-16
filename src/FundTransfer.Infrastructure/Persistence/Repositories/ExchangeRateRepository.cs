using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;
using FundTransfer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FundTransfer.Infrastructure.Persistence.Repositories;

public class ExchangeRateRepository : IExchangeRateRepository
{
    private readonly AppDbContext _db;

    public ExchangeRateRepository(AppDbContext db) { _db = db; }

    public async Task<ExchangeRate?> GetActiveRateAsync(string src, string tgt, CancellationToken ct)
        => await _db.ExchangeRates.FirstOrDefaultAsync(r => r.SourceCurrency == src && r.TargetCurrency == tgt && r.IsActive, ct);

    public async Task AddAsync(ExchangeRate rate, CancellationToken ct)
    {
        await _db.ExchangeRates.AddAsync(rate, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.ExchangeRates.FindAsync(new object[] { id }, ct);
        if (r != null) { r.IsActive = false; await _db.SaveChangesAsync(ct); }
    }
}
