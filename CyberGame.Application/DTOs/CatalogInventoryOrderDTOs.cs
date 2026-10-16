using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record ProductCategoryDto(
    Guid Id,
    Guid BranchId,
    string Name,
    int SortOrder,
    bool IsActive,
    int TotalProducts
);

public record CreateCategoryRequest(string Name, int SortOrder = 0);
public record UpdateCategoryRequest(string Name, int SortOrder, bool IsActive);

public record ProductDto(
    Guid Id,
    Guid BranchId,
    Guid CategoryId,
    string CategoryName,
    string Sku,
    string Name,
    string Unit,
    decimal SalePrice,
    decimal CostPrice,
    bool TrackInventory,
    decimal ReorderLevel,
    decimal QuantityOnHand,
    bool IsActive,
    bool IsLowStock
);

public record CreateProductRequest(
    Guid CategoryId,
    string Sku,
    string Name,
    string Unit,
    decimal SalePrice,
    decimal CostPrice,
    bool TrackInventory,
    decimal ReorderLevel,
    decimal InitialStock = 0
);

public record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string Unit,
    decimal SalePrice,
    decimal CostPrice,
    bool TrackInventory,
    decimal ReorderLevel,
    bool IsActive
);

public record InventoryItemDto(
    Guid ProductId,
    string Sku,
    string Name,
    string Unit,
    string CategoryName,
    decimal QuantityOnHand,
    decimal ReorderLevel,
    bool IsLowStock,
    decimal CostPrice,
    decimal SalePrice
);

public record ReceiveInventoryRequest(
    Guid ProductId,
    decimal Quantity,
    decimal UnitCost,
    string? Note
);

public record AdjustInventoryRequest(
    Guid ProductId,
    decimal NewQuantity,
    string Reason
);

public record InventoryTransactionDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Sku,
    InventoryTransactionType Type,
    decimal Quantity,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal? UnitCost,
    string? Note,
    DateTime OccurredAtUtc,
    string CreatedByUsername
);

public record OrderDto(
    Guid Id,
    Guid BranchId,
    string OrderNumber,
    Guid? PlaySessionId,
    string? MachineCode,
    Guid? CustomerId,
    string? CustomerName,
    Guid ShiftId,
    OrderStatus Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    DateTime? ConfirmedAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemDto> Items,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    PaymentStatus PaymentStatus = PaymentStatus.Pending,
    DateTime? PaidAtUtc = null
);

public record OrderItemDto(
    Guid Id,
    Guid? ProductId,
    string ProductName,
    string Sku,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal,
    string? Note
);

public record CreateOrderItemRequest(
    Guid ProductId,
    decimal Quantity,
    string? Note = null
);

public record CreateOrderRequest(
    Guid? PlaySessionId,
    Guid? CustomerId,
    List<CreateOrderItemRequest> Items
);

public record UpdateOrderRequest(
    List<CreateOrderItemRequest> Items
);
