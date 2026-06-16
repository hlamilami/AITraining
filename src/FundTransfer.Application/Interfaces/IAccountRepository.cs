using FundTransfer.Application.Models;

namespace FundTransfer.Application.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByAccountNumberAsync(string accountNumber, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
}
