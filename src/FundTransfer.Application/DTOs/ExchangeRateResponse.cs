namespace FundTransfer.Application.DTOs;

public class ExchangeRateResponse
{
    public Guid Id { get; set; }
    public string SourceCurrency { get; set; } = string.Empty;
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
