using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/member")]
[Authorize(Policy = "MemberOnly")]
public class MemberController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MemberController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    [HttpGet("machines")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MemberMachineSelectionDto>>> GetMachines(CancellationToken ct)
    {
        var machines = await _memberService.GetMachinesAsync(ct);
        return Ok(machines);
    }

    [HttpPost("auth/login")]
    [AllowAnonymous]
    public async Task<ActionResult<MemberLoginResponse>> Login([FromBody] MemberLoginRequest request, CancellationToken ct)
    {
        var response = await _memberService.LoginAsync(request, ct);
        return Ok(response);
    }

    [HttpGet("me")]
    public async Task<ActionResult<MemberMeResponse>> GetMe(CancellationToken ct)
    {
        var response = await _memberService.GetMeAsync(ct);
        return Ok(response);
    }

    [HttpPost("change-pin")]
    public async Task<ActionResult> ChangePin([FromBody] ChangeMemberPinRequest request, CancellationToken ct)
    {
        await _memberService.ChangePinAsync(request, ct);
        return NoContent();
    }

    [HttpGet("session")]
    public async Task<ActionResult<MemberSessionDto>> GetActiveSession(CancellationToken ct)
    {
        var session = await _memberService.GetActiveSessionAsync(ct);
        if (session == null)
        {
            return NoContent();
        }
        return Ok(session);
    }

    [HttpGet("session/quote")]
    public async Task<ActionResult<MemberSessionQuoteDto>> GetSessionQuote(CancellationToken ct)
    {
        var quote = await _memberService.GetSessionQuoteAsync(ct);
        return Ok(quote);
    }

    [HttpPost("session/complete")]
    public async Task<ActionResult<MemberSessionSummaryDto>> CompleteSession(CancellationToken ct)
    {
        var summary = await _memberService.CompleteSessionAsync(ct);
        return Ok(summary);
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<IReadOnlyList<MemberProductDto>>> GetCatalog(CancellationToken ct)
    {
        var catalog = await _memberService.GetCatalogAsync(ct);
        return Ok(catalog);
    }

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<MemberOrderDto>>> GetOrders(CancellationToken ct)
    {
        var orders = await _memberService.GetOrdersAsync(ct);
        return Ok(orders);
    }

    [HttpPost("orders")]
    public async Task<ActionResult<MemberOrderDto>> CreateOrder([FromBody] CreateMemberOrderRequest request, CancellationToken ct)
    {
        var order = await _memberService.CreateOrderAsync(request, ct);
        return Ok(order);
    }

    [HttpGet("top-up-requests")]
    public async Task<ActionResult<IReadOnlyList<MemberTopUpRequestDto>>> GetTopUpRequests(CancellationToken ct)
    {
        var requests = await _memberService.GetTopUpRequestsAsync(ct);
        return Ok(requests);
    }

    [HttpPost("top-up-requests")]
    public async Task<ActionResult<MemberTopUpRequestDto>> CreateTopUpRequest([FromBody] CreateMemberTopUpRequest request, CancellationToken ct)
    {
        var result = await _memberService.CreateTopUpRequestAsync(request, ct);
        return CreatedAtAction(nameof(GetTopUpRequests), new { id = result.Id }, result);
    }
}
