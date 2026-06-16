using FluentAssertions;
using FundTransfer.Application.Constants;
using FundTransfer.Application.DTOs;
using FundTransfer.Application.Exceptions;
using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;
using FundTransfer.Application.Services;
using Moq;

namespace FundTransfer.UnitTests.Services;

public class TransferServiceTests
{
    private readonly Mock<IAccountRepository> _accountRepoMock = new();
    private readonly Mock<ITransferRepository> _transferRepoMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly Mock<IExchangeRateRepository> _exchangeRateRepoMock = new();
    private readonly TransferService _sut;

    public TransferServiceTests()
    {
        _sut = new TransferService(_accountRepoMock.Object, _transferRepoMock.Object, _auditLogRepoMock.Object, _exchangeRateRepoMock.Object);
    }

    private Account MakeAccount(string number, string owner, string currency, long balance) =>
        new() { AccountNumber = number, Owner = owner, Currency = currency, Balance = balance };

    [Fact]
    public async Task ExecuteTransferAsync_ValidTransfer_ReturnsCompleted()
    {
        var source = MakeAccount("ACC-SRC", "alice", "USD", 10000L);
        var dest = MakeAccount("ACC-DST", "bob", "USD", 5000L);
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-1", default)).ReturnsAsync((Transfer?)null);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-SRC", default)).ReturnsAsync(source);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-DST", default)).ReturnsAsync(dest);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SRC", DestinationAccountNumber = "ACC-DST", Amount = 3000L };
        var (response, isReplay) = await _sut.ExecuteTransferAsync(request, "key-1", "alice", "corr-1");

        response.Status.Should().Be(TransferStatus.Completed.ToString());
        isReplay.Should().BeFalse();
        source.Balance.Should().Be(7000L);
        dest.Balance.Should().Be(8000L);
        _accountRepoMock.Verify(r => r.UpdateAsync(source, It.IsAny<CancellationToken>()), Times.Once);
        _accountRepoMock.Verify(r => r.UpdateAsync(dest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteTransferAsync_InsufficientFunds_ReturnsRejected()
    {
        var source = MakeAccount("ACC-SRC", "alice", "USD", 100L);
        var dest = MakeAccount("ACC-DST", "bob", "USD", 5000L);
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-2", default)).ReturnsAsync((Transfer?)null);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-SRC", default)).ReturnsAsync(source);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-DST", default)).ReturnsAsync(dest);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SRC", DestinationAccountNumber = "ACC-DST", Amount = 3000L };
        var (response, isReplay) = await _sut.ExecuteTransferAsync(request, "key-2", "alice", "corr-1");

        response.Status.Should().Be(TransferStatus.Rejected.ToString());
        response.FailureReason.Should().Be(DomainConstants.FailureReasonCodes.InsufficientFunds);
        isReplay.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteTransferAsync_SameAccount_ThrowsValidationException()
    {
        var account = MakeAccount("ACC-SAME", "alice", "USD", 10000L);
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-3", default)).ReturnsAsync((Transfer?)null);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-SAME", default)).ReturnsAsync(account);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SAME", DestinationAccountNumber = "ACC-SAME", Amount = 1000L };
        var act = () => _sut.ExecuteTransferAsync(request, "key-3", "alice", "corr-1");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ExecuteTransferAsync_CrossCurrency_NoRate_ReturnsNoExchangeRateAvailable()
    {
        var source = MakeAccount("ACC-SRC", "alice", "USD", 10000L);
        var dest = MakeAccount("ACC-DST", "bob", "EUR", 5000L);
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-4", default)).ReturnsAsync((Transfer?)null);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-SRC", default)).ReturnsAsync(source);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-DST", default)).ReturnsAsync(dest);
        _exchangeRateRepoMock.Setup(r => r.GetActiveRateAsync("USD", "EUR", default)).ReturnsAsync((ExchangeRate?)null);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SRC", DestinationAccountNumber = "ACC-DST", Amount = 1000L };
        var (response, isReplay) = await _sut.ExecuteTransferAsync(request, "key-4", "alice", "corr-1");

        response.Status.Should().Be(TransferStatus.Rejected.ToString());
        response.FailureReason.Should().Be(DomainConstants.FailureReasonCodes.NoExchangeRateAvailable);
    }

    [Fact]
    public async Task ExecuteTransferAsync_CrossCurrency_WithRate_ReturnsCompleted()
    {
        var source = MakeAccount("ACC-SRC", "alice", "USD", 10000L);
        var dest = MakeAccount("ACC-DST", "bob", "EUR", 5000L);
        var rate = new ExchangeRate { Id = Guid.NewGuid(), SourceCurrency = "USD", TargetCurrency = "EUR", Rate = 0.92m, IsActive = true };
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-x", default)).ReturnsAsync((Transfer?)null);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-SRC", default)).ReturnsAsync(source);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-DST", default)).ReturnsAsync(dest);
        _exchangeRateRepoMock.Setup(r => r.GetActiveRateAsync("USD", "EUR", default)).ReturnsAsync(rate);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SRC", DestinationAccountNumber = "ACC-DST", Amount = 1000L };
        var (response, isReplay) = await _sut.ExecuteTransferAsync(request, "key-x", "alice", "corr-1");

        response.Status.Should().Be(TransferStatus.Completed.ToString());
        response.AppliedExchangeRateId.Should().Be(rate.Id);
        response.AppliedRate.Should().Be(0.92m);
        response.DestinationAmount.Should().Be(920L);  // floor(1000 * 0.92)
        source.Balance.Should().Be(9000L);
        dest.Balance.Should().Be(5920L);
    }

    [Fact]
    public async Task ExecuteTransferAsync_IdempotentReplay_ReturnsExistingRecord()
    {
        var existingTransfer = new Transfer
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = "key-5",
            SourceAccountNumber = "ACC-SRC",
            DestinationAccountNumber = "ACC-DST",
            Amount = 500L,
            Currency = "USD",
            Status = TransferStatus.Completed,
            InitiatedBy = "alice"
        };
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-5", default)).ReturnsAsync(existingTransfer);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SRC", DestinationAccountNumber = "ACC-DST", Amount = 500L };
        var (response, isReplay) = await _sut.ExecuteTransferAsync(request, "key-5", "alice", "corr-1");

        isReplay.Should().BeTrue();
        response.TransferId.Should().Be(existingTransfer.Id);
        _accountRepoMock.Verify(r => r.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteTransferAsync_CallerNotOwner_ThrowsForbiddenException()
    {
        var source = MakeAccount("ACC-SRC", "alice", "USD", 10000L);
        var dest = MakeAccount("ACC-DST", "bob", "USD", 5000L);
        _transferRepoMock.Setup(r => r.GetByIdempotencyKeyAsync("key-6", default)).ReturnsAsync((Transfer?)null);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-SRC", default)).ReturnsAsync(source);
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-DST", default)).ReturnsAsync(dest);

        var request = new CreateTransferRequest { SourceAccountNumber = "ACC-SRC", DestinationAccountNumber = "ACC-DST", Amount = 1000L };
        var act = () => _sut.ExecuteTransferAsync(request, "key-6", "charlie", "corr-1");

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
