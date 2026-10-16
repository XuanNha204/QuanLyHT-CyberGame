using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerWalletService _customerService;

    public CustomersController(ICustomerWalletService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult> GetCustomers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _customerService.GetCustomersAsync(search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetCustomerById(Guid id, CancellationToken ct)
    {
        var customer = await _customerService.GetCustomerByIdAsync(id, ct);
        return Ok(customer);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customerService.CreateCustomerAsync(request, ct);
        return CreatedAtAction(nameof(GetCustomerById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customerService.UpdateCustomerAsync(id, request, ct);
        return Ok(customer);
    }

    [HttpGet("{id:guid}/wallet-transactions")]
    public async Task<ActionResult> GetWalletTransactions(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _customerService.GetWalletTransactionsAsync(id, page, pageSize, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/wallet/top-up")]
    [HttpPost("{id:guid}/wallet/topup")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<CustomerDto>> TopUpWallet(Guid id, [FromBody] TopUpWalletRequest request, CancellationToken ct)
    {
        var customer = await _customerService.TopUpWalletAsync(id, request, ct);
        return Ok(customer);
    }

    [HttpPost("{id:guid}/wallet/adjust")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<CustomerDto>> AdjustWallet(Guid id, [FromBody] AdjustWalletRequest request, CancellationToken ct)
    {
        var customer = await _customerService.AdjustWalletAsync(id, request, ct);
        return Ok(customer);
    }

    [HttpPost("{id:guid}/member-pin")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<CustomerDto>> SetMemberPin(Guid id, [FromBody] SetMemberPinRequest request, CancellationToken ct)
    {
        var customer = await _customerService.SetMemberPinAsync(id, request.Pin, ct);
        return Ok(customer);
    }
}
