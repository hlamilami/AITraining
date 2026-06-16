using FundTransfer.Application.DTOs;
using FundTransfer.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace FundTransfer.Api.Controllers;

/// <summary>Manages fund transfers between accounts.</summary>
[ApiController]
[Route("v1/transfers")]
public class TransfersController : ControllerBase
{
    private readonly TransferService _transferService;

    public TransfersController(TransferService transferService)
    {
        _transferService = transferService;
    }

    /// <summary>Initiates a fund transfer between two accounts.</summary>
    /// <param name="request">Transfer details.</param>
    /// <param name="idempotencyKey">Unique UUID to prevent duplicate submissions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Transfer initiated.</response>
    /// <response code="200">Idempotent replay — same transfer returned.</response>
    /// <response code="400">Invalid request or missing Idempotency-Key.</response>
    /// <response code="403">Caller does not own the source account.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateTransferRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || !Guid.TryParse(idempotencyKey, out _))
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Bad Request",
                Detail = "Idempotency-Key header is required and must be a valid UUID."
            });

        var callerIdentity = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "dev-user-001";
        var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        var (response, isReplay) = await _transferService.ExecuteTransferAsync(
            request, idempotencyKey, callerIdentity, correlationId, ct);

        return isReplay ? Ok(response) : StatusCode(StatusCodes.Status201Created, response);
    }
}
