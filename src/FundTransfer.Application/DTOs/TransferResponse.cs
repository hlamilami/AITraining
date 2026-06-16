namespace FundTransfer.Application.DTOs;

/// <summary>Transfer result response.</summary>
public class TransferResponse
{
    /// <summary>Unique transfer ID.</summary>
    public Guid TransferId { get; set; }

    /// <summary>Client-supplied idempotency key.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Source account number.</summary>
    public string SourceAccountNumber { get; set; } = string.Empty;

    /// <summary>Destination account number.</summary>
    public string DestinationAccountNumber { get; set; } = string.Empty;

    /// <summary>Amount in minor currency units.</summary>
    public long Amount { get; set; }

    /// <summary>Currency code of the transfer.</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Transfer status: Pending, Completed, or Rejected.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Reason for rejection, if applicable.</summary>
    public string? FailureReason { get; set; }

    /// <summary>Identity of the user who initiated the transfer.</summary>
    public string InitiatedBy { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the transfer.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Exchange rate ID used for cross-currency transfers.</summary>
    public Guid? AppliedExchangeRateId { get; set; }

    /// <summary>Exchange rate value applied, if cross-currency.</summary>
    public decimal? AppliedRate { get; set; }

    /// <summary>Amount credited to the destination account in destination currency minor units.</summary>
    public long? DestinationAmount { get; set; }
}
