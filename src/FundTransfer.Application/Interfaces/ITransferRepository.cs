using FundTransfer.Application.Models;

namespace FundTransfer.Application.Interfaces;

public interface ITransferRepository
{
    Task<Transfer?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default);
    Task AddAsync(Transfer transfer, CancellationToken ct = default);
}
