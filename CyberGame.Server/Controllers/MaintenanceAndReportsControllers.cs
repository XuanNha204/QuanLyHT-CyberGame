using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/maintenance-tickets")]
[Authorize]
public class MaintenanceTicketsController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceTicketsController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenanceTicketDto>>> GetTickets([FromQuery] MaintenanceStatus? status, [FromQuery] Guid? machineId, CancellationToken ct)
    {
        var tickets = await _maintenanceService.GetTicketsAsync(status, machineId, ct);
        return Ok(tickets);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MaintenanceTicketDto>> GetTicketById(Guid id, CancellationToken ct)
    {
        var ticket = await _maintenanceService.GetTicketByIdAsync(id, ct);
        return Ok(ticket);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager,Technician")]
    public async Task<ActionResult<MaintenanceTicketDto>> CreateTicket([FromBody] CreateMaintenanceTicketRequest request, CancellationToken ct)
    {
        var ticket = await _maintenanceService.CreateTicketAsync(request, ct);
        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
    }

    [HttpPost("{id:guid}/logs")]
    [Authorize(Roles = "Owner,Manager,Technician")]
    public async Task<ActionResult<MaintenanceTicketDto>> AddLog(Guid id, [FromBody] AddMaintenanceLogRequest request, CancellationToken ct)
    {
        var ticket = await _maintenanceService.AddLogAsync(id, request, ct);
        return Ok(ticket);
    }
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardReportService _reportService;

    public DashboardController(IDashboardReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken ct)
    {
        var summary = await _reportService.GetDashboardSummaryAsync(ct);
        return Ok(summary);
    }
}

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = "Owner,Manager")]
public class ReportsController : ControllerBase
{
    private readonly IDashboardReportService _reportService;

    public ReportsController(IDashboardReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("revenue")]
    public async Task<ActionResult<IReadOnlyList<RevenueReportDto>>> GetRevenueReport([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
    {
        var report = await _reportService.GetRevenueReportAsync(fromUtc, toUtc, ct);
        return Ok(report);
    }

    [HttpGet("machine-utilization")]
    public async Task<ActionResult<IReadOnlyList<MachineUtilizationDto>>> GetMachineUtilizationReport([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken ct)
    {
        var report = await _reportService.GetMachineUtilizationReportAsync(fromUtc, toUtc, ct);
        return Ok(report);
    }

    [HttpGet("top-products")]
    public async Task<ActionResult<IReadOnlyList<TopProductReportDto>>> GetTopProductsReport([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        var report = await _reportService.GetTopProductsReportAsync(limit, ct);
        return Ok(report);
    }

    [HttpGet("wallet-liability")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<WalletLiabilityReportDto>> GetWalletLiabilityReport(CancellationToken ct)
    {
        var report = await _reportService.GetWalletLiabilityReportAsync(ct);
        return Ok(report);
    }
}

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly CyberGame.Infrastructure.Data.CyberGameDbContext _db;

    public HealthController(CyberGame.Infrastructure.Data.CyberGameDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        try
        {
            bool canConnect = await _db.Database.CanConnectAsync(ct);
            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Unhealthy",
                    database = "Disconnected",
                    timestamp = DateTime.UtcNow
                });
            }

            return Ok(new
            {
                status = "Healthy",
                database = "Connected",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                database = "Error",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
