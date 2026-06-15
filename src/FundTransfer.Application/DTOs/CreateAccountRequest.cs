namespace FundTransfer.Application.DTOs;

/// <summary>Request to create a new account.</summary>
public class CreateAccountRequest
{
    /// <summary>Account owner name.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code (USD, EUR, GBP, SAR, AED).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Initial balance in minor currency units (e.g. cents). Must be >= 0.</summary>
    public long InitialBalance { get; set; }
}
