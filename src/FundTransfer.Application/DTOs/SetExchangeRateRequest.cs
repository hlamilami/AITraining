namespace FundTransfer.Application.DTOs;

public class SetExchangeRateRequest
{
    public string SourceCurrency { get; set; } = string.Empty;
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}
