using CyberGame.Application.DTOs;
using CyberGame.Application.Services;
using CyberGame.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberGame.Server.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogInventoryService _catalogService;

    public CategoriesController(ICatalogInventoryService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductCategoryDto>>> GetCategories(CancellationToken ct)
    {
        var categories = await _catalogService.GetCategoriesAsync(ct);
        return Ok(categories);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductCategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var category = await _catalogService.CreateCategoryAsync(request, ct);
        return CreatedAtAction(nameof(GetCategories), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductCategoryDto>> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        var category = await _catalogService.UpdateCategoryAsync(id, request, ct);
        return Ok(category);
    }
}

[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ICatalogInventoryService _catalogService;

    public ProductsController(ICatalogInventoryService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult> GetProducts(
        [FromQuery] Guid? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _catalogService.GetProductsAsync(categoryId, search, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetProductById(Guid id, CancellationToken ct)
    {
        var product = await _catalogService.GetProductByIdAsync(id, ct);
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var product = await _catalogService.CreateProductAsync(request, ct);
        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var product = await _catalogService.UpdateProductAsync(id, request, ct);
        return Ok(product);
    }
}

[ApiController]
[Route("api/v1/inventory")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly ICatalogInventoryService _catalogService;

    public InventoryController(ICatalogInventoryService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InventoryItemDto>>> GetInventory([FromQuery] bool lowStockOnly = false, CancellationToken ct = default)
    {
        var items = await _catalogService.GetInventoryAsync(lowStockOnly, ct);
        return Ok(items);
    }

    [HttpPost("receive")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<InventoryItemDto>> Receive([FromBody] ReceiveInventoryRequest request, CancellationToken ct)
    {
        var item = await _catalogService.ReceiveInventoryAsync(request, ct);
        return Ok(item);
    }

    [HttpPost("adjust")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<ActionResult<InventoryItemDto>> Adjust([FromBody] AdjustInventoryRequest request, CancellationToken ct)
    {
        var item = await _catalogService.AdjustInventoryAsync(request, ct);
        return Ok(item);
    }

    [HttpGet("transactions")]
    public async Task<ActionResult> GetTransactions([FromQuery] Guid? productId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _catalogService.GetInventoryTransactionsAsync(productId, page, pageSize, ct);
        return Ok(result);
    }
}

[ApiController]
[Route("api/v1/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult> GetOrders(
        [FromQuery] OrderStatus? status,
        [FromQuery] Guid? sessionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _orderService.GetOrdersAsync(status, sessionId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> GetOrderById(Guid id, CancellationToken ct)
    {
        var order = await _orderService.GetOrderByIdAsync(id, ct);
        return Ok(order);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request, [FromQuery] bool autoConfirm = true, CancellationToken ct = default)
    {
        var order = await _orderService.CreateOrderAsync(request, autoConfirm, ct);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, order);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> UpdateOrder(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken ct)
    {
        var order = await _orderService.UpdateOrderAsync(id, request, ct);
        return Ok(order);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> ConfirmOrder(Guid id, CancellationToken ct)
    {
        var order = await _orderService.ConfirmOrderAsync(id, ct);
        return Ok(order);
    }

    [HttpPost("{id:guid}/fulfill")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> FulfillOrder(Guid id, CancellationToken ct)
    {
        var order = await _orderService.FulfillOrderAsync(id, ct);
        return Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Owner,Manager,Cashier")]
    public async Task<ActionResult<OrderDto>> CancelOrder(Guid id, CancellationToken ct)
    {
        var order = await _orderService.CancelOrderAsync(id, ct);
        return Ok(order);
    }
}
