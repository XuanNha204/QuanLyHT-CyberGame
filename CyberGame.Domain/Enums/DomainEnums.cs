namespace CyberGame.Domain.Enums;

public enum MachineStatus
{
    Available,
    InUse,
    Maintenance,
    Offline
}

public enum BillingMode
{
    PrepaidWallet,
    Postpaid
}

public enum PlaySessionStatus
{
    Open,
    Paused,
    Completed,
    Cancelled
}

public enum OrderStatus
{
    Draft,
    Confirmed,
    Fulfilled,
    Cancelled
}

public enum InvoiceStatus
{
    Draft,
    Issued,
    Paid,
    Voided
}

public enum InvoiceLineType
{
    ComputerUsage,
    Product,
    Discount,
    Adjustment
}

public enum PaymentType
{
    Payment,
    Refund,
    WalletTopUp
}

public enum PaymentMethod
{
    Cash,
    BankTransfer,
    Wallet
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Voided
}

public enum WalletTransactionType
{
    TopUp,
    Usage,
    Refund,
    Adjustment,
    Transfer
}

public enum ShiftStatus
{
    Open,
    Closed
}

public enum CashMovementType
{
    Opening,
    Sale,
    TopUp,
    Refund,
    Expense,
    CashIn,
    CashOut,
    Adjustment
}

public enum ExpenseStatus
{
    Pending,
    Approved,
    Rejected
}

public enum MaintenancePriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum MaintenanceStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum CustomerStatus
{
    Active,
    Locked,
    Inactive
}

public enum InventoryTransactionType
{
    Opening,
    Receipt,
    Sale,
    Return,
    Adjustment,
    Waste
}

public enum MemberTopUpRequestStatus
{
    Pending,
    Approved,
    Rejected
}
