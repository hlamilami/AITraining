using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundTransfer.Infrastructure.Persistence.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly AppDbContext _context;

    public TransferRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Transfer?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await _context.Transfers.FirstOrDefaultAsync(t => t.IdempotencyKey == key, ct);

    public async Task AddAsync(Transfer transfer, CancellationToken ct = default)
    {
        await _context.Transfers.AddAsync(transfer, ct);
        await _context.SaveChangesAsync(ct);
    }
}
