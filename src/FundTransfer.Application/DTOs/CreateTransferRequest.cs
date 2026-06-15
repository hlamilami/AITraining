namespace FundTransfer.Application.DTOs;

/// <summary>Request to transfer funds between two accounts.</summary>
public class CreateTransferRequest
{
    /// <summary>Source account number.</summary>
    public string SourceAccountNumber { get; set; } = string.Empty;

    /// <summary>Destination account number.</summary>
    public string DestinationAccountNumber { get; set; } = string.Empty;

    /// <summary>Amount in minor currency units. Must be > 0.</summary>
    public long Amount { get; set; }
}
