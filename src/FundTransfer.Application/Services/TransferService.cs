using System.Text.Json;
using FundTransfer.Application.Constants;
using FundTransfer.Application.DTOs;
using FundTransfer.Application.Exceptions;
using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;

namespace FundTransfer.Application.Services;

public class TransferService
{
    private readonly IAccountRepository _accountRepository;
    private readonly ITransferRepository _transferRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IExchangeRateRepository _exchangeRateRepository;

    public TransferService(
        IAccountRepository accountRepository,
        ITransferRepository transferRepository,
        IAuditLogRepository auditLogRepository,
        IExchangeRateRepository exchangeRateRepository)
    {
        _accountRepository = accountRepository;
        _transferRepository = transferRepository;
        _auditLogRepository = auditLogRepository;
        _exchangeRateRepository = exchangeRateRepository;
    }

    public async Task<(TransferResponse Response, bool IsReplay)> ExecuteTransferAsync(
        CreateTransferRequest request,
        string idempotencyKey,
        string callerIdentity,
        string correlationId,
        CancellationToken ct = default)
    {
        // 1. Idempotency check
        var existing = await _transferRepository.GetByIdempotencyKeyAsync(idempotencyKey, ct);
        if (existing != null)
            return (MapToResponse(existing), true);

        // 2. Load source account
        var sourceAccount = await _accountRepository.GetByAccountNumberAsync(request.SourceAccountNumber, ct);
        if (sourceAccount == null)
            throw new NotFoundException(DomainConstants.FailureReasonCodes.SourceAccountNotFound);

        // 3. Load destination account
        var destAccount = await _accountRepository.GetByAccountNumberAsync(request.DestinationAccountNumber, ct);
        if (destAccount == null)
            throw new NotFoundException(DomainConstants.FailureReasonCodes.DestinationAccountNotFound);

        // 4. Validate source != dest
        if (request.SourceAccountNumber == request.DestinationAccountNumber)
            throw new ValidationException(DomainConstants.FailureReasonCodes.SameAccountTransfer, "Source and destination accounts must differ.");

        // 5. Validate amount > 0
        if (request.Amount <= 0)
            throw new ValidationException(DomainConstants.FailureReasonCodes.InvalidAmount, "Amount must be greater than zero.");

        // 6. Cross-currency support
        Guid? appliedExchangeRateId = null;
        decimal? appliedRate = null;
        long? destinationAmount = null;

        if (sourceAccount.Currency != destAccount.Currency)
        {
            var rate = await _exchangeRateRepository.GetActiveRateAsync(sourceAccount.Currency, destAccount.Currency, ct);
            if (rate == null)
            {
                var rejectedTransfer = new Transfer
                {
                    IdempotencyKey = idempotencyKey,
                    SourceAccountNumber = request.SourceAccountNumber,
                    DestinationAccountNumber = request.DestinationAccountNumber,
                    Amount = request.Amount,
                    Currency = sourceAccount.Currency,
                    Status = TransferStatus.Rejected,
                    FailureReason = DomainConstants.FailureReasonCodes.NoExchangeRateAvailable,
                    InitiatedBy = callerIdentity,
                    Timestamp = DateTimeOffset.UtcNow
                };
                await _transferRepository.AddAsync(rejectedTransfer, ct);
                await _auditLogRepository.AddAsync(new AuditLogEntry
                {
                    EntityType = "Transfer",
                    EntityId = rejectedTransfer.Id.ToString(),
                    Actor = callerIdentity,
                    Operation = "TransferRejected",
                    CorrelationId = correlationId,
                    AfterState = JsonSerializer.Serialize(rejectedTransfer)
                }, ct);
                return (MapToResponse(rejectedTransfer), false);
            }

            var calcDestAmount = (long)Math.Floor((double)request.Amount * (double)rate.Rate);
            if (calcDestAmount < 1)
            {
                var rejectedTransfer = new Transfer
                {
                    IdempotencyKey = idempotencyKey,
                    SourceAccountNumber = request.SourceAccountNumber,
                    DestinationAccountNumber = request.DestinationAccountNumber,
                    Amount = request.Amount,
                    Currency = sourceAccount.Currency,
                    Status = TransferStatus.Rejected,
                    FailureReason = DomainConstants.FailureReasonCodes.ConversionResultsInZero,
                    InitiatedBy = callerIdentity,
                    Timestamp = DateTimeOffset.UtcNow
                };
                await _transferRepository.AddAsync(rejectedTransfer, ct);
                await _auditLogRepository.AddAsync(new AuditLogEntry
                {
                    EntityType = "Transfer",
                    EntityId = rejectedTransfer.Id.ToString(),
                    Actor = callerIdentity,
                    Operation = "TransferRejected",
                    CorrelationId = correlationId,
                    AfterState = JsonSerializer.Serialize(rejectedTransfer)
                }, ct);
                return (MapToResponse(rejectedTransfer), false);
            }

            appliedExchangeRateId = rate.Id;
            appliedRate = rate.Rate;
            destinationAmount = calcDestAmount;
        }

        // 7. Authorization: verify callerIdentity == sourceAccount.Owner or has transfer:admin
        if (callerIdentity != sourceAccount.Owner && callerIdentity != "transfer:admin")
            throw new ForbiddenException($"Caller '{callerIdentity}' is not authorized to transfer from account '{request.SourceAccountNumber}'.");

        // 8. Validate balance >= amount
        if (sourceAccount.Balance < request.Amount)
        {
            var rejectedTransfer = new Transfer
            {
                IdempotencyKey = idempotencyKey,
                SourceAccountNumber = request.SourceAccountNumber,
                DestinationAccountNumber = request.DestinationAccountNumber,
                Amount = request.Amount,
                Currency = sourceAccount.Currency,
                Status = TransferStatus.Rejected,
                FailureReason = DomainConstants.FailureReasonCodes.InsufficientFunds,
                InitiatedBy = callerIdentity,
                Timestamp = DateTimeOffset.UtcNow
            };
            await _transferRepository.AddAsync(rejectedTransfer, ct);
            await _auditLogRepository.AddAsync(new AuditLogEntry
            {
                EntityType = "Transfer",
                EntityId = rejectedTransfer.Id.ToString(),
                Actor = callerIdentity,
                Operation = "TransferRejected",
                CorrelationId = correlationId,
                AfterState = JsonSerializer.Serialize(rejectedTransfer)
            }, ct);
            return (MapToResponse(rejectedTransfer), false);
        }

        // 9. Atomic: debit + credit
        var beforeSource = JsonSerializer.Serialize(sourceAccount);
        var beforeDest = JsonSerializer.Serialize(destAccount);

        sourceAccount.Balance -= request.Amount;
        destAccount.Balance += destinationAmount ?? request.Amount;

        await _accountRepository.UpdateAsync(sourceAccount, ct);
        await _accountRepository.UpdateAsync(destAccount, ct);

        // 10. Persist completed transfer + audit log
        var transfer = new Transfer
        {
            IdempotencyKey = idempotencyKey,
            SourceAccountNumber = request.SourceAccountNumber,
            DestinationAccountNumber = request.DestinationAccountNumber,
            Amount = request.Amount,
            Currency = sourceAccount.Currency,
            Status = TransferStatus.Completed,
            InitiatedBy = callerIdentity,
            Timestamp = DateTimeOffset.UtcNow,
            AppliedExchangeRateId = appliedExchangeRateId,
            DestinationAmount = destinationAmount,
            AppliedRate = appliedRate
        };

        await _transferRepository.AddAsync(transfer, ct);

        await _auditLogRepository.AddAsync(new AuditLogEntry
        {
            EntityType = "Account",
            EntityId = sourceAccount.AccountNumber,
            Actor = callerIdentity,
            Operation = "TransferDebit",
            CorrelationId = correlationId,
            BeforeState = beforeSource,
            AfterState = JsonSerializer.Serialize(sourceAccount)
        }, ct);

        await _auditLogRepository.AddAsync(new AuditLogEntry
        {
            EntityType = "Account",
            EntityId = destAccount.AccountNumber,
            Actor = callerIdentity,
            Operation = "TransferCredit",
            CorrelationId = correlationId,
            BeforeState = beforeDest,
            AfterState = JsonSerializer.Serialize(destAccount)
        }, ct);

        return (MapToResponse(transfer), false);
    }

    private static TransferResponse MapToResponse(Transfer transfer) => new()
    {
        TransferId = transfer.Id,
        IdempotencyKey = transfer.IdempotencyKey,
        SourceAccountNumber = transfer.SourceAccountNumber,
        DestinationAccountNumber = transfer.DestinationAccountNumber,
        Amount = transfer.Amount,
        Currency = transfer.Currency,
        Status = transfer.Status.ToString(),
        FailureReason = transfer.FailureReason,
        InitiatedBy = transfer.InitiatedBy,
        Timestamp = transfer.Timestamp,
        AppliedExchangeRateId = transfer.AppliedExchangeRateId,
        AppliedRate = transfer.AppliedRate,
        DestinationAmount = transfer.DestinationAmount
    };
}
