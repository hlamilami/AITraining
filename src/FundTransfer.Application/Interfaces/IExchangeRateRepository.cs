using FundTransfer.Application.Models;

namespace FundTransfer.Application.Interfaces;

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetActiveRateAsync(string sourceCurrency, string targetCurrency, CancellationToken ct = default);
    Task AddAsync(ExchangeRate rate, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);  // sets IsActive = false
}
