using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/member-top-up-requests")]
[Authorize(Roles = "Owner,Manager,Cashier")]
public class MemberTopUpRequestsController : ControllerBase
{
    private readonly ICustomerWalletService _customerWalletService;

    public MemberTopUpRequestsController(ICustomerWalletService customerWalletService)
    {
        _customerWalletService = customerWalletService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffMemberTopUpRequestDto>>> GetRequests(
        [FromQuery] MemberTopUpRequestStatus? status,
        CancellationToken ct)
    {
        var requests = await _customerWalletService.GetMemberTopUpRequestsAsync(status, ct);
        return Ok(requests);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<StaffMemberTopUpRequestDto>> ApproveRequest(Guid id, CancellationToken ct)
    {
        var result = await _customerWalletService.ApproveMemberTopUpRequestAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<StaffMemberTopUpRequestDto>> RejectRequest(Guid id, CancellationToken ct)
    {
        var result = await _customerWalletService.RejectMemberTopUpRequestAsync(id, ct);
        return Ok(result);
    }
}
