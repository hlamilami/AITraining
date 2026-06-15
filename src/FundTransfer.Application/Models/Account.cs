namespace FundTransfer.Application.Models;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public long Balance { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[]? RowVersion { get; set; }
}
