namespace FundTransfer.Application.Constants;

public static class DomainConstants
{
    public static readonly IReadOnlyList<string> SupportedCurrencies =
        new[] { "USD", "EUR", "GBP", "SAR", "AED" };

    public static class FailureReasonCodes
    {
        public const string InsufficientFunds = "InsufficientFunds";
        public const string CurrencyMismatch = "CurrencyMismatch";
        public const string SameAccountTransfer = "SameAccountTransfer";
        public const string SourceAccountNotFound = "SourceAccountNotFound";
        public const string DestinationAccountNotFound = "DestinationAccountNotFound";
        public const string InvalidAmount = "InvalidAmount";
        public const string NoExchangeRateAvailable = "NoExchangeRateAvailable";
        public const string ConversionResultsInZero = "ConversionResultsInZero";
    }
}

public enum TransferStatus
{
    Pending = 0,
    Completed = 1,
    Rejected = 2
}
