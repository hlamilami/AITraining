using FundTransfer.Application.Constants;
using FundTransfer.Application.DTOs;
using FundTransfer.Application.Exceptions;
using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;

namespace FundTransfer.Application.Services;

public class ExchangeRateService
{
    private readonly IExchangeRateRepository _repo;
    private readonly IAuditLogRepository _audit;

    public ExchangeRateService(IExchangeRateRepository repo, IAuditLogRepository audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<ExchangeRateResponse> SetRateAsync(SetExchangeRateRequest request, string callerIdentity, string correlationId, CancellationToken ct = default)
    {
        if (!DomainConstants.SupportedCurrencies.Contains(request.SourceCurrency))
            throw new ValidationException($"Unsupported source currency: {request.SourceCurrency}");
        if (!DomainConstants.SupportedCurrencies.Contains(request.TargetCurrency))
            throw new ValidationException($"Unsupported target currency: {request.TargetCurrency}");
        if (request.SourceCurrency == request.TargetCurrency)
            throw new ValidationException("Source and target currencies must differ.");
        if (request.Rate <= 0)
            throw new ValidationException("Rate must be greater than zero.");

        var existing = await _repo.GetActiveRateAsync(request.SourceCurrency, request.TargetCurrency, ct);
        string? beforeState = null;
        if (existing != null)
        {
            beforeState = System.Text.Json.JsonSerializer.Serialize(new { existing.Id, existing.Rate, existing.EffectiveFrom });
            await _repo.DeactivateAsync(existing.Id, ct);
        }

        var rate = new ExchangeRate
        {
            SourceCurrency = request.SourceCurrency,
            TargetCurrency = request.TargetCurrency,
            Rate = Math.Round(request.Rate, 6),
            CreatedBy = callerIdentity,
            EffectiveFrom = DateTimeOffset.UtcNow,
            IsActive = true
        };
        await _repo.AddAsync(rate, ct);

        await _audit.AddAsync(new AuditLogEntry
        {
            EntityType = "ExchangeRate",
            EntityId = rate.Id.ToString(),
            Actor = callerIdentity,
            Operation = existing == null ? "ExchangeRateCreated" : "ExchangeRateUpdated",
            CorrelationId = correlationId,
            BeforeState = beforeState,
            AfterState = System.Text.Json.JsonSerializer.Serialize(new { rate.Id, rate.SourceCurrency, rate.TargetCurrency, rate.Rate })
        }, ct);

        return MapToResponse(rate);
    }

    public async Task<ExchangeRateResponse> GetRateAsync(string sourceCurrency, string targetCurrency, CancellationToken ct = default)
    {
        var rate = await _repo.GetActiveRateAsync(sourceCurrency, targetCurrency, ct);
        if (rate == null)
            throw new NotFoundException($"No exchange rate found for {sourceCurrency} → {targetCurrency}.");
        return MapToResponse(rate);
    }

    private static ExchangeRateResponse MapToResponse(ExchangeRate r) => new()
    {
        Id = r.Id,
        SourceCurrency = r.SourceCurrency,
        TargetCurrency = r.TargetCurrency,
        Rate = r.Rate,
        EffectiveFrom = r.EffectiveFrom,
        CreatedBy = r.CreatedBy
    };
}
