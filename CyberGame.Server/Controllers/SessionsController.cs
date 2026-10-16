using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/sessions")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessionService;

    public SessionsController(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<PlaySessionDto>>> GetActiveSessions(CancellationToken ct)
    {
        var sessions = await _sessionService.GetActiveSessionsAsync(ct);
        return Ok(sessions);
    }

    [HttpGet]
    public async Task<ActionResult> GetSessions(
        [FromQuery] Guid? machineId,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? date,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sessionService.GetSessionsAsync(machineId, customerId, date, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlaySessionDto>> GetSessionById(Guid id, CancellationToken ct)
    {
        var session = await _sessionService.GetSessionByIdAsync(id, ct);
        return Ok(session);
    }

    [HttpPost("{id:guid}/pause")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<PlaySessionDto>> PauseSession(Guid id, CancellationToken ct)
    {
        var session = await _sessionService.PauseSessionAsync(id, ct);
        return Ok(session);
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<PlaySessionDto>> ResumeSession(Guid id, CancellationToken ct)
    {
        var session = await _sessionService.ResumeSessionAsync(id, ct);
        return Ok(session);
    }

    [HttpPost("{id:guid}/transfer")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<PlaySessionDto>> TransferSession(Guid id, [FromBody] TransferSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionService.TransferSessionAsync(id, request, ct);
        return Ok(session);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<InvoiceDto>> CompleteSession(Guid id, [FromBody] CompleteSessionRequest request, CancellationToken ct)
    {
        var invoice = await _sessionService.CompleteSessionAsync(id, request, ct);
        return Ok(invoice);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<PlaySessionDto>> CancelSession(Guid id, [FromBody] CancelSessionRequest request, CancellationToken ct)
    {
        var session = await _sessionService.CancelSessionAsync(id, request, ct);
        return Ok(session);
    }

    [HttpGet("{id:guid}/quote")]
    public async Task<ActionResult<SessionQuoteDto>> GetSessionQuote(Guid id, CancellationToken ct)
    {
        var quote = await _sessionService.GetSessionQuoteAsync(id, ct);
        return Ok(quote);
    }

    [HttpGet("{id:guid}/events")]
    public async Task<ActionResult<IReadOnlyList<SessionEventDto>>> GetSessionEvents(Guid id, CancellationToken ct)
    {
        var events = await _sessionService.GetSessionEventsAsync(id, ct);
        return Ok(events);
    }
}
