using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FundTransfer.Application.DTOs;

namespace FundTransfer.IntegrationTests.Controllers;

public class TransfersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public TransfersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        CustomWebApplicationFactory.CurrentUserId = "dev-user-001";
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
    }

    private async Task<AccountResponse> CreateAccountAsync(string owner, string currency, long balance)
    {
        var req = new CreateAccountRequest { Owner = owner, Currency = currency, InitialBalance = balance };
        var resp = await _client.PostAsJsonAsync("/v1/accounts", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AccountResponse>())!;
    }

    private HttpRequestMessage MakeTransferRequest(CreateTransferRequest body, string idempotencyKey)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/v1/transfers")
        {
            Content = JsonContent.Create(body)
        };
        msg.Headers.Add("Idempotency-Key", idempotencyKey);
        msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        return msg;
    }

    [Fact]
    public async Task PostTransfer_ValidTransfer_Returns201WithCorrectBalances()
    {
        var source = await CreateAccountAsync("dev-user-001", "USD", 10000L);
        var dest = await CreateAccountAsync("dev-user-001", "USD", 5000L);

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = source.AccountNumber,
            DestinationAccountNumber = dest.AccountNumber,
            Amount = 3000L
        };
        var response = await _client.SendAsync(MakeTransferRequest(req, Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
        body!.Status.Should().Be("Completed");

        var srcGet = await _client.GetFromJsonAsync<AccountResponse>($"/v1/accounts/{source.AccountNumber}");
        srcGet!.Balance.Should().Be(7000L);
        var dstGet = await _client.GetFromJsonAsync<AccountResponse>($"/v1/accounts/{dest.AccountNumber}");
        dstGet!.Balance.Should().Be(8000L);
    }

    [Fact]
    public async Task PostTransfer_IdempotentReplay_Returns200WithSameTransferId()
    {
        var source = await CreateAccountAsync("dev-user-001", "USD", 20000L);
        var dest = await CreateAccountAsync("dev-user-001", "USD", 1000L);
        var key = Guid.NewGuid().ToString();

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = source.AccountNumber,
            DestinationAccountNumber = dest.AccountNumber,
            Amount = 1000L
        };
        var first = await _client.SendAsync(MakeTransferRequest(req, key));
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var firstBody = await first.Content.ReadFromJsonAsync<TransferResponse>();

        var second = await _client.SendAsync(MakeTransferRequest(req, key));
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<TransferResponse>();

        secondBody!.TransferId.Should().Be(firstBody!.TransferId);
    }

    [Fact]
    public async Task PostTransfer_InsufficientFunds_ReturnsRejectedStatus()
    {
        var source = await CreateAccountAsync("dev-user-001", "USD", 100L);
        var dest = await CreateAccountAsync("dev-user-001", "USD", 0L);

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = source.AccountNumber,
            DestinationAccountNumber = dest.AccountNumber,
            Amount = 9999L
        };
        var response = await _client.SendAsync(MakeTransferRequest(req, Guid.NewGuid().ToString()));

        var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
        body!.Status.Should().Be("Rejected");
        body.FailureReason.Should().Be("InsufficientFunds");
    }

    [Fact]
    public async Task PostTransfer_SameSourceAndDest_Returns400()
    {
        var account = await CreateAccountAsync("dev-user-001", "USD", 10000L);

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = account.AccountNumber,
            DestinationAccountNumber = account.AccountNumber,
            Amount = 100L
        };
        var response = await _client.SendAsync(MakeTransferRequest(req, Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostTransfer_CurrencyMismatch_ReturnsRejectedStatus()
    {
        var source = await CreateAccountAsync("dev-user-001", "USD", 10000L);
        var dest = await CreateAccountAsync("dev-user-001", "EUR", 5000L);

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = source.AccountNumber,
            DestinationAccountNumber = dest.AccountNumber,
            Amount = 500L
        };
        var response = await _client.SendAsync(MakeTransferRequest(req, Guid.NewGuid().ToString()));

        var body = await response.Content.ReadFromJsonAsync<TransferResponse>();
        body!.Status.Should().Be("Rejected");
        body.FailureReason.Should().Be("CurrencyMismatch");
    }

    [Fact]
    public async Task PostTransfer_MissingIdempotencyKey_Returns400()
    {
        var req = new CreateTransferRequest { SourceAccountNumber = "A", DestinationAccountNumber = "B", Amount = 100L };
        var msg = new HttpRequestMessage(HttpMethod.Post, "/v1/transfers")
        {
            Content = JsonContent.Create(req)
        };
        msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");

        var response = await _client.SendAsync(msg);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAccount_AfterTransfer_ShowsReducedBalance()
    {
        var source = await CreateAccountAsync("dev-user-001", "USD", 5000L);
        var dest = await CreateAccountAsync("dev-user-001", "USD", 0L);

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = source.AccountNumber,
            DestinationAccountNumber = dest.AccountNumber,
            Amount = 2000L
        };
        await _client.SendAsync(MakeTransferRequest(req, Guid.NewGuid().ToString()));

        var getResp = await _client.GetFromJsonAsync<AccountResponse>($"/v1/accounts/{source.AccountNumber}");
        getResp!.Balance.Should().Be(3000L);
    }

    [Fact]
    public async Task PostTransfer_CallerNotOwner_Returns403()
    {
        CustomWebApplicationFactory.CurrentUserId = "dev-user-001";
        var source = await CreateAccountAsync("dev-user-001", "USD", 10000L);
        var dest = await CreateAccountAsync("dev-user-001", "USD", 0L);

        CustomWebApplicationFactory.CurrentUserId = "other-user";
        var otherClient = _factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");

        var req = new CreateTransferRequest
        {
            SourceAccountNumber = source.AccountNumber,
            DestinationAccountNumber = dest.AccountNumber,
            Amount = 100L
        };
        var msg = new HttpRequestMessage(HttpMethod.Post, "/v1/transfers")
        {
            Content = JsonContent.Create(req)
        };
        msg.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        msg.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");

        var response = await otherClient.SendAsync(msg);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
