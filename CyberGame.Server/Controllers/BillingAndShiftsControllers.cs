using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IBillingService _billingService;

    public InvoicesController(IBillingService billingService)
    {
        _billingService = billingService;
    }

    [HttpGet]
    public async Task<ActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _billingService.GetInvoicesAsync(status, date, fromDate, toDate, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceDto>> GetInvoiceById(Guid id, CancellationToken ct)
    {
        var invoice = await _billingService.GetInvoiceByIdAsync(id, ct);
        return Ok(invoice);
    }

    [HttpPost("{id:guid}/payments")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<InvoiceDto>> AddPayment(Guid id, [FromBody] CreatePaymentRequest request, CancellationToken ct)
    {
        var invoice = await _billingService.AddPaymentAsync(id, request, ct);
        return Ok(invoice);
    }

    [HttpPost("{id:guid}/void")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<InvoiceDto>> VoidInvoice(Guid id, [FromBody] VoidInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await _billingService.VoidInvoiceAsync(id, request, ct);
        return Ok(invoice);
    }
}

[ApiController]
[Route("api/v1/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ShiftDto?>> GetCurrentShift(CancellationToken ct)
    {
        var shift = await _shiftService.GetCurrentShiftAsync(ct);
        return Ok(shift);
    }

    [HttpPost("open")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<ShiftDto>> OpenShift([FromBody] OpenShiftRequest request, CancellationToken ct)
    {
        var shift = await _shiftService.OpenShiftAsync(request, ct);
        return CreatedAtAction(nameof(GetCurrentShift), new { id = shift.Id }, shift);
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<ShiftDto>> CloseShift(Guid id, [FromBody] CloseShiftRequest request, CancellationToken ct)
    {
        var shift = await _shiftService.CloseShiftAsync(id, request, ct);
        return Ok(shift);
    }

    [HttpGet("{id:guid}/summary")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<ShiftSummaryDto>> GetShiftSummary(Guid id, CancellationToken ct)
    {
        var summary = await _shiftService.GetShiftSummaryAsync(id, ct);
        return Ok(summary);
    }

    [HttpPost("{id:guid}/cash-movements")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<CashMovementDto>> AddCashMovement(Guid id, [FromBody] CreateCashMovementRequest request, CancellationToken ct)
    {
        var movement = await _shiftService.AddCashMovementAsync(id, request, ct);
        return Ok(movement);
    }

    [HttpGet("{id:guid}/cash-movements")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<IReadOnlyList<CashMovementDto>>> GetCashMovements(Guid id, CancellationToken ct)
    {
        var movements = await _shiftService.GetCashMovementsAsync(id, ct);
        return Ok(movements);
    }
}
