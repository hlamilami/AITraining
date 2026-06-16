using FundTransfer.Application.DTOs;
using FundTransfer.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundTransfer.Api.Controllers;

/// <summary>Manages bank accounts.</summary>
[ApiController]
[Route("v1/accounts")]
public class AccountsController : ControllerBase
{
    private readonly AccountService _accountService;

    public AccountsController(AccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>Creates a new account.</summary>
    /// <response code="201">Account created successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var callerIdentity = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "dev-user-001";
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        var response = await _accountService.CreateAccountAsync(request, callerIdentity, correlationId, ct);
        return CreatedAtAction(nameof(GetAccount), new { accountNumber = response.AccountNumber }, response);
    }

    /// <summary>Retrieves an account by account number.</summary>
    /// <response code="200">Account found.</response>
    /// <response code="404">Account not found.</response>
    [HttpGet("{accountNumber}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(string accountNumber, CancellationToken ct)
    {
        var response = await _accountService.GetAccountAsync(accountNumber, ct);
        return Ok(response);
    }
}
