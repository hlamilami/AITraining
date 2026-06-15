namespace FundTransfer.Application.DTOs;

/// <summary>Account details response.</summary>
public class AccountResponse
{
    /// <summary>System-assigned account number.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>Account owner name.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>ISO 4217 currency code.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Current balance in minor currency units.</summary>
    public long Balance { get; set; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
