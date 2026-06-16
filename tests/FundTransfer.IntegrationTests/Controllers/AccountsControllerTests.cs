using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using FundTransfer.Application.DTOs;

namespace FundTransfer.IntegrationTests.Controllers;

public class AccountsControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AccountsControllerTests(CustomWebApplicationFactory factory)
    {
        CustomWebApplicationFactory.CurrentUserId = "dev-user-001";
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
    }

    [Fact]
    public async Task PostAccount_ValidPayload_Returns201WithCorrectBody()
    {
        var request = new CreateAccountRequest { Owner = "Alice", Currency = "USD", InitialBalance = 50000L };

        var response = await _client.PostAsJsonAsync("/v1/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AccountResponse>();
        body.Should().NotBeNull();
        body!.AccountNumber.Should().StartWith("ACC-");
        body.Owner.Should().Be("Alice");
        body.Currency.Should().Be("USD");
        body.Balance.Should().Be(50000L);
    }

    [Fact]
    public async Task PostAccount_NegativeBalance_Returns400()
    {
        var request = new CreateAccountRequest { Owner = "Bob", Currency = "USD", InitialBalance = -100L };

        var response = await _client.PostAsJsonAsync("/v1/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostAccount_InvalidCurrency_Returns400()
    {
        var request = new CreateAccountRequest { Owner = "Charlie", Currency = "XYZ", InitialBalance = 0L };

        var response = await _client.PostAsJsonAsync("/v1/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAccount_AfterCreation_Returns200WithCorrectBody()
    {
        var createRequest = new CreateAccountRequest { Owner = "Dave", Currency = "EUR", InitialBalance = 1000L };
        var createResponse = await _client.PostAsJsonAsync("/v1/accounts", createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();

        var getResponse = await _client.GetAsync($"/v1/accounts/{created!.AccountNumber}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<AccountResponse>();
        body!.AccountNumber.Should().Be(created.AccountNumber);
        body.Owner.Should().Be("Dave");
        body.Balance.Should().Be(1000L);
    }

    [Fact]
    public async Task GetAccount_NonExistent_Returns404()
    {
        var response = await _client.GetAsync("/v1/accounts/NONEXISTENT-ACCOUNT");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
