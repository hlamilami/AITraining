namespace FundTransfer.Application.Models;

public class ExchangeRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SourceCurrency { get; set; } = string.Empty;  // ISO 4217
    public string TargetCurrency { get; set; } = string.Empty;  // ISO 4217
    public decimal Rate { get; set; }  // positive, up to 6 decimal places
    public DateTimeOffset EffectiveFrom { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;  // false when superseded
}
