using CyberGame.Domain.Enums;

namespace CyberGame.Application.DTOs;

public record InvoiceDto(
    Guid Id,
    Guid BranchId,
    string InvoiceNumber,
    Guid? CustomerId,
    string? CustomerName,
    Guid? PlaySessionId,
    string? MachineCode,
    Guid ShiftId,
    InvoiceStatus Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal ChangeAmount,
    DateTime IssuedAtUtc,
    DateTime? PaidAtUtc,
    IReadOnlyList<InvoiceLineDto> Lines,
    IReadOnlyList<PaymentDto> Payments
);

public record InvoiceLineDto(
    Guid Id,
    InvoiceLineType LineType,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal LineTotal
);

public record PaymentDto(
    Guid Id,
    string PaymentNumber,
    PaymentType Type,
    PaymentMethod Method,
    decimal Amount,
    PaymentStatus Status,
    DateTime PaidAtUtc,
    string? Note,
    string CreatedByUsername
);

public record CreatePaymentRequest(
    decimal Amount,
    PaymentMethod Method,
    string? Note = null
);

public record VoidInvoiceRequest(
    string Reason
);
