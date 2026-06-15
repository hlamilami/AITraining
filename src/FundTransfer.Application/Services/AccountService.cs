using System.Text.Json;
using FundTransfer.Application.Constants;
using FundTransfer.Application.DTOs;
using FundTransfer.Application.Exceptions;
using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;

namespace FundTransfer.Application.Services;

public class AccountService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public AccountService(IAccountRepository accountRepository, IAuditLogRepository auditLogRepository)
    {
        _accountRepository = accountRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        string callerIdentity,
        string correlationId,
        CancellationToken ct = default)
    {
        if (!DomainConstants.SupportedCurrencies.Contains(request.Currency))
            throw new ValidationException("UnsupportedCurrency", $"Currency '{request.Currency}' is not supported.");

        if (request.InitialBalance < 0)
            throw new ValidationException("InvalidBalance", "InitialBalance must be >= 0.");

        var accountNumber = $"ACC-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        var account = new Account
        {
            AccountNumber = accountNumber,
            Owner = request.Owner,
            Currency = request.Currency,
            Balance = request.InitialBalance,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _accountRepository.AddAsync(account, ct);

        var auditEntry = new AuditLogEntry
        {
            EntityType = "Account",
            EntityId = accountNumber,
            Actor = callerIdentity,
            Operation = "AccountCreated",
            CorrelationId = correlationId,
            BeforeState = null,
            AfterState = JsonSerializer.Serialize(account)
        };

        await _auditLogRepository.AddAsync(auditEntry, ct);

        return MapToResponse(account);
    }

    public async Task<AccountResponse> GetAccountAsync(string accountNumber, CancellationToken ct = default)
    {
        var account = await _accountRepository.GetByAccountNumberAsync(accountNumber, ct);
        if (account == null)
            throw new NotFoundException($"Account '{accountNumber}' not found.");

        return MapToResponse(account);
    }

    private static AccountResponse MapToResponse(Account account) => new()
    {
        AccountNumber = account.AccountNumber,
        Owner = account.Owner,
        Currency = account.Currency,
        Balance = account.Balance,
        CreatedAt = account.CreatedAt
    };
}
