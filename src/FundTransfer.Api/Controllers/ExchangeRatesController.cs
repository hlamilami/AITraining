using FundTransfer.Application.DTOs;
using FundTransfer.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FundTransfer.Api.Controllers;

/// <summary>Manages currency exchange rates.</summary>
[ApiController]
[Route("v1/exchange-rates")]
public class ExchangeRatesController : ControllerBase
{
    private readonly ExchangeRateService _service;

    public ExchangeRatesController(ExchangeRateService service)
    {
        _service = service;
    }

    /// <summary>Creates or updates the exchange rate for a currency pair.</summary>
    /// <response code="200">Rate set successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ExchangeRateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetRate([FromBody] SetExchangeRateRequest request, CancellationToken ct)
    {
        var callerIdentity = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "dev-user-001";
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
        var response = await _service.SetRateAsync(request, callerIdentity, correlationId, ct);
        return Ok(response);
    }

    /// <summary>Retrieves the current exchange rate for a currency pair.</summary>
    /// <response code="200">Rate found.</response>
    /// <response code="404">No rate found for this pair.</response>
    [HttpGet("{sourceCurrency}/{targetCurrency}")]
    [ProducesResponseType(typeof(ExchangeRateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRate(string sourceCurrency, string targetCurrency, CancellationToken ct)
    {
        var response = await _service.GetRateAsync(sourceCurrency, targetCurrency, ct);
        return Ok(response);
    }
}
