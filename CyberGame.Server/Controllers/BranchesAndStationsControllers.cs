using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IBranchMachineService _branchService;

    public BranchesController(IBranchMachineService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet("current")]
    public async Task<ActionResult<BranchDto>> GetCurrent(CancellationToken ct)
    {
        var branch = await _branchService.GetCurrentBranchAsync(ct);
        return Ok(branch);
    }

    [HttpPut("current")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<BranchDto>> UpdateCurrent([FromBody] UpdateBranchRequest request, CancellationToken ct)
    {
        var branch = await _branchService.UpdateCurrentBranchAsync(request, ct);
        return Ok(branch);
    }
}

[ApiController]
[Route("api/v1/machine-groups")]
[Authorize]
public class MachineGroupsController : ControllerBase
{
    private readonly IBranchMachineService _branchService;

    public MachineGroupsController(IBranchMachineService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MachineGroupDto>>> GetMachineGroups(CancellationToken ct)
    {
        var groups = await _branchService.GetMachineGroupsAsync(ct);
        return Ok(groups);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<MachineGroupDto>> CreateMachineGroup([FromBody] CreateMachineGroupRequest request, CancellationToken ct)
    {
        var group = await _branchService.CreateMachineGroupAsync(request, ct);
        return CreatedAtAction(nameof(GetMachineGroups), new { id = group.Id }, group);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<MachineGroupDto>> UpdateMachineGroup(Guid id, [FromBody] UpdateMachineGroupRequest request, CancellationToken ct)
    {
        var group = await _branchService.UpdateMachineGroupAsync(id, request, ct);
        return Ok(group);
    }
}

[ApiController]
[Route("api/v1/machines")]
[Authorize]
public class MachinesController : ControllerBase
{
    private readonly IBranchMachineService _branchService;

    public MachinesController(IBranchMachineService branchService)
    {
        _branchService = branchService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MachineDto>>> GetMachines([FromQuery] Guid? groupId, [FromQuery] MachineStatus? status, CancellationToken ct)
    {
        var machines = await _branchService.GetMachinesAsync(groupId, status, ct);
        return Ok(machines);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MachineDto>> GetMachineById(Guid id, CancellationToken ct)
    {
        var machine = await _branchService.GetMachineByIdAsync(id, ct);
        return Ok(machine);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<MachineDto>> CreateMachine([FromBody] CreateMachineRequest request, CancellationToken ct)
    {
        var machine = await _branchService.CreateMachineAsync(request, ct);
        return CreatedAtAction(nameof(GetMachineById), new { id = machine.Id }, machine);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<MachineDto>> UpdateMachine(Guid id, [FromBody] UpdateMachineRequest request, CancellationToken ct)
    {
        var machine = await _branchService.UpdateMachineAsync(id, request, ct);
        return Ok(machine);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Owner,Manager,Technician")]
    public async Task<ActionResult<MachineDto>> UpdateStatus(Guid id, [FromBody] UpdateMachineStatusRequest request, CancellationToken ct)
    {
        var machine = await _branchService.UpdateMachineStatusAsync(id, request, ct);
        return Ok(machine);
    }
}

[ApiController]
[Route("api/v1/pricing-plans")]
[Authorize]
public class PricingPlansController : ControllerBase
{
    private readonly IPricingService _pricingService;

    public PricingPlansController(IPricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PricingPlanDto>>> GetPricingPlans(CancellationToken ct)
    {
        var plans = await _pricingService.GetPricingPlansAsync(ct);
        return Ok(plans);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PricingPlanDto>> GetPricingPlanById(Guid id, CancellationToken ct)
    {
        var plan = await _pricingService.GetPricingPlanByIdAsync(id, ct);
        return Ok(plan);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<PricingPlanDto>> CreatePricingPlan([FromBody] CreatePricingPlanRequest request, CancellationToken ct)
    {
        var plan = await _pricingService.CreatePricingPlanAsync(request, ct);
        return CreatedAtAction(nameof(GetPricingPlanById), new { id = plan.Id }, plan);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<PricingPlanDto>> UpdatePricingPlan(Guid id, [FromBody] CreatePricingPlanRequest request, CancellationToken ct)
    {
        var plan = await _pricingService.UpdatePricingPlanAsync(id, request, ct);
        return Ok(plan);
    }
}
