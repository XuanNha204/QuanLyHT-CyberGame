using CyberGame.Domain.Enums;

namespace CyberGame.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public string InvoiceNumber { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? PlaySessionId { get; set; }
    public PlaySession? PlaySession { get; set; }
    public Guid ShiftId { get; set; }
    public Shift Shift { get; set; } = null!;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ChangeAmount { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAtUtc { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class InvoiceLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public InvoiceLineType LineType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public PaymentType Type { get; set; } = PaymentType.Payment;
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Completed;
    public string? ExternalReference { get; set; }
    public DateTime PaidAtUtc { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public Guid CreatedByUserId { get; set; }
    public AppUser CreatedByUser { get; set; } = null!;
}
