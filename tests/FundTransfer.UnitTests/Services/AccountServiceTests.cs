using FluentAssertions;
using FundTransfer.Application.DTOs;
using FundTransfer.Application.Exceptions;
using FundTransfer.Application.Interfaces;
using FundTransfer.Application.Models;
using FundTransfer.Application.Services;
using Moq;

namespace FundTransfer.UnitTests.Services;

public class AccountServiceTests
{
    private readonly Mock<IAccountRepository> _accountRepoMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();
    private readonly AccountService _sut;

    public AccountServiceTests()
    {
        _sut = new AccountService(_accountRepoMock.Object, _auditLogRepoMock.Object);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidRequest_ReturnsAccountResponseWithAssignedNumber()
    {
        var request = new CreateAccountRequest { Owner = "Alice", Currency = "USD", InitialBalance = 10000L };

        var response = await _sut.CreateAccountAsync(request, "alice-id", "corr-1");

        response.AccountNumber.Should().StartWith("ACC-");
        response.Owner.Should().Be("Alice");
        response.Currency.Should().Be("USD");
        response.Balance.Should().Be(10000L);
        _accountRepoMock.Verify(r => r.AddAsync(It.IsAny<Account>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditLogRepoMock.Verify(r => r.AddAsync(It.IsAny<AuditLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAccountAsync_NegativeBalance_ThrowsValidationException()
    {
        var request = new CreateAccountRequest { Owner = "Alice", Currency = "USD", InitialBalance = -1L };

        var act = () => _sut.CreateAccountAsync(request, "alice-id", "corr-1");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CreateAccountAsync_UnsupportedCurrency_ThrowsValidationException()
    {
        var request = new CreateAccountRequest { Owner = "Alice", Currency = "XYZ", InitialBalance = 0L };

        var act = () => _sut.CreateAccountAsync(request, "alice-id", "corr-1");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetAccountAsync_ExistingAccount_ReturnsAccountResponse()
    {
        var account = new Account { AccountNumber = "ACC-20240101-ABCD1234", Owner = "Bob", Currency = "EUR", Balance = 5000L };
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("ACC-20240101-ABCD1234", default)).ReturnsAsync(account);

        var response = await _sut.GetAccountAsync("ACC-20240101-ABCD1234");

        response.AccountNumber.Should().Be("ACC-20240101-ABCD1234");
        response.Owner.Should().Be("Bob");
        response.Currency.Should().Be("EUR");
        response.Balance.Should().Be(5000L);
    }

    [Fact]
    public async Task GetAccountAsync_NonExistingAccount_ThrowsNotFoundException()
    {
        _accountRepoMock.Setup(r => r.GetByAccountNumberAsync("NONEXISTENT", default)).ReturnsAsync((Account?)null);

        var act = () => _sut.GetAccountAsync("NONEXISTENT");

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
