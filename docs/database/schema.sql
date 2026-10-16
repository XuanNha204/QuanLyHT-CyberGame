-- Canonical logical schema for CyberGame Manager (SQL Server)
-- Runtime schema is produced by EF Core migrations and must remain semantically aligned.

CREATE TABLE Branches (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    Code nvarchar(32) NOT NULL UNIQUE,
    Name nvarchar(160) NOT NULL,
    Address nvarchar(400) NULL,
    TimeZoneId nvarchar(100) NOT NULL,
    Phone nvarchar(30) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL
);

CREATE TABLE Roles (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    Code nvarchar(40) NOT NULL UNIQUE,
    Name nvarchar(100) NOT NULL
);

CREATE TABLE AppUsers (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NULL REFERENCES Branches(Id),
    Username nvarchar(64) NOT NULL,
    NormalizedUsername nvarchar(64) NOT NULL UNIQUE,
    FullName nvarchar(160) NOT NULL,
    PasswordHash nvarchar(500) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    FailedLoginCount int NOT NULL DEFAULT 0,
    LockoutEndUtc datetime2(7) NULL,
    LastLoginAtUtc datetime2(7) NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL
);

CREATE TABLE UserRoles (
    UserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    RoleId uniqueidentifier NOT NULL REFERENCES Roles(Id),
    PRIMARY KEY (UserId, RoleId)
);

CREATE TABLE RefreshTokens (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    TokenHash char(64) NOT NULL UNIQUE,
    ExpiresAtUtc datetime2(7) NOT NULL,
    RevokedAtUtc datetime2(7) NULL,
    ReplacedByTokenId uniqueidentifier NULL REFERENCES RefreshTokens(Id),
    DeviceName nvarchar(200) NULL,
    CreatedAtUtc datetime2(7) NOT NULL
);

CREATE TABLE MachineGroups (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    Code nvarchar(32) NOT NULL,
    Name nvarchar(120) NOT NULL,
    SortOrder int NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    CONSTRAINT UQ_MachineGroups_Branch_Code UNIQUE (BranchId, Code)
);

CREATE TABLE Machines (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    MachineGroupId uniqueidentifier NOT NULL REFERENCES MachineGroups(Id),
    Code nvarchar(32) NOT NULL,
    DisplayName nvarchar(120) NOT NULL,
    IpAddress varchar(45) NULL,
    MacAddress varchar(17) NULL,
    Status nvarchar(24) NOT NULL CHECK (Status IN ('Available','InUse','Maintenance','Offline')),
    LastHeartbeatAtUtc datetime2(7) NULL,
    Notes nvarchar(1000) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_Machines_Branch_Code UNIQUE (BranchId, Code)
);

CREATE TABLE PricingPlans (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    MachineGroupId uniqueidentifier NULL REFERENCES MachineGroups(Id),
    Name nvarchar(160) NOT NULL,
    EffectiveFromUtc datetime2(7) NOT NULL,
    EffectiveToUtc datetime2(7) NULL,
    IsDefault bit NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    CHECK (EffectiveToUtc IS NULL OR EffectiveToUtc > EffectiveFromUtc)
);

CREATE TABLE PricingRules (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    PricingPlanId uniqueidentifier NOT NULL REFERENCES PricingPlans(Id),
    DayOfWeekMask tinyint NOT NULL CHECK (DayOfWeekMask BETWEEN 1 AND 127),
    StartTime time(0) NOT NULL,
    EndTime time(0) NOT NULL,
    HourlyRate decimal(18,2) NOT NULL CHECK (HourlyRate > 0),
    Priority int NOT NULL DEFAULT 0,
    CreatedAtUtc datetime2(7) NOT NULL
);

CREATE TABLE CustomerGroups (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    Name nvarchar(120) NOT NULL,
    DiscountPercent decimal(5,2) NOT NULL DEFAULT 0 CHECK (DiscountPercent BETWEEN 0 AND 100),
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    CONSTRAINT UQ_CustomerGroups_Branch_Name UNIQUE (BranchId, Name)
);

CREATE TABLE Customers (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    HomeBranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    CustomerGroupId uniqueidentifier NULL REFERENCES CustomerGroups(Id),
    Code nvarchar(32) NOT NULL UNIQUE,
    FullName nvarchar(160) NOT NULL,
    Phone nvarchar(30) NULL,
    Email nvarchar(254) NULL,
    DateOfBirth date NULL,
    Status nvarchar(20) NOT NULL CHECK (Status IN ('Active','Locked','Inactive')),
    MemberPinHash nvarchar(500) NULL,
    MemberFailedLoginCount int NOT NULL DEFAULT 0,
    MemberLockoutEndUtc datetime2(7) NULL,
    MemberLastLoginAtUtc datetime2(7) NULL,
    JoinedAtUtc datetime2(7) NOT NULL,
    Notes nvarchar(1000) NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL
);

CREATE TABLE Wallets (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    CustomerId uniqueidentifier NOT NULL UNIQUE REFERENCES Customers(Id),
    Balance decimal(18,2) NOT NULL DEFAULT 0 CHECK (Balance >= 0),
    UpdatedAtUtc datetime2(7) NOT NULL,
    RowVersion rowversion NOT NULL
);

CREATE TABLE Shifts (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    UserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    ShiftNumber nvarchar(40) NOT NULL,
    Status nvarchar(16) NOT NULL CHECK (Status IN ('Open','Closed')),
    OpenedAtUtc datetime2(7) NOT NULL,
    ClosedAtUtc datetime2(7) NULL,
    OpeningCash decimal(18,2) NOT NULL CHECK (OpeningCash >= 0),
    ExpectedCash decimal(18,2) NULL,
    ActualCash decimal(18,2) NULL,
    Difference decimal(18,2) NULL,
    ClosingNote nvarchar(1000) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_Shifts_Branch_Number UNIQUE (BranchId, ShiftNumber)
);

CREATE TABLE PlaySessions (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    MachineId uniqueidentifier NOT NULL REFERENCES Machines(Id),
    CustomerId uniqueidentifier NULL REFERENCES Customers(Id),
    ShiftId uniqueidentifier NOT NULL REFERENCES Shifts(Id),
    SessionNumber nvarchar(40) NOT NULL,
    BillingMode nvarchar(24) NOT NULL CHECK (BillingMode IN ('PrepaidWallet','Postpaid')),
    Status nvarchar(16) NOT NULL CHECK (Status IN ('Open','Paused','Completed','Cancelled')),
    StartedAtUtc datetime2(7) NOT NULL,
    PausedAtUtc datetime2(7) NULL,
    EndedAtUtc datetime2(7) NULL,
    TotalPausedSeconds int NOT NULL DEFAULT 0 CHECK (TotalPausedSeconds >= 0),
    UsageAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (UsageAmount >= 0),
    ServiceAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (ServiceAmount >= 0),
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (DiscountAmount >= 0),
    TotalAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (TotalAmount >= 0),
    InvoiceId uniqueidentifier NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_PlaySessions_Branch_Number UNIQUE (BranchId, SessionNumber)
);

CREATE UNIQUE INDEX UX_PlaySessions_OneActivePerMachine
ON PlaySessions(MachineId)
WHERE Status IN ('Open','Paused');

CREATE TABLE SessionChargeSegments (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    PlaySessionId uniqueidentifier NOT NULL REFERENCES PlaySessions(Id),
    PricingRuleId uniqueidentifier NULL REFERENCES PricingRules(Id),
    StartedAtUtc datetime2(7) NOT NULL,
    EndedAtUtc datetime2(7) NULL,
    RatePerHour decimal(18,2) NOT NULL CHECK (RatePerHour > 0),
    BillableSeconds int NOT NULL DEFAULT 0 CHECK (BillableSeconds >= 0),
    Amount decimal(18,2) NOT NULL DEFAULT 0 CHECK (Amount >= 0),
    CHECK (EndedAtUtc IS NULL OR EndedAtUtc >= StartedAtUtc)
);

CREATE TABLE SessionEvents (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    PlaySessionId uniqueidentifier NOT NULL REFERENCES PlaySessions(Id),
    EventType nvarchar(32) NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL,
    UserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    DataJson nvarchar(max) NULL,
    Note nvarchar(1000) NULL
);

CREATE TABLE ProductCategories (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    Name nvarchar(120) NOT NULL,
    SortOrder int NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    CONSTRAINT UQ_ProductCategories_Branch_Name UNIQUE (BranchId, Name)
);

CREATE TABLE Products (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    CategoryId uniqueidentifier NOT NULL REFERENCES ProductCategories(Id),
    Sku nvarchar(48) NOT NULL,
    Name nvarchar(160) NOT NULL,
    Unit nvarchar(32) NOT NULL,
    SalePrice decimal(18,2) NOT NULL CHECK (SalePrice >= 0),
    CostPrice decimal(18,2) NOT NULL CHECK (CostPrice >= 0),
    TrackInventory bit NOT NULL DEFAULT 1,
    ReorderLevel decimal(18,3) NOT NULL DEFAULT 0 CHECK (ReorderLevel >= 0),
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_Products_Branch_Sku UNIQUE (BranchId, Sku)
);

CREATE TABLE InventoryBalances (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    ProductId uniqueidentifier NOT NULL REFERENCES Products(Id),
    QuantityOnHand decimal(18,3) NOT NULL DEFAULT 0,
    UpdatedAtUtc datetime2(7) NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_InventoryBalances_Branch_Product UNIQUE (BranchId, ProductId)
);

CREATE TABLE Orders (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    OrderNumber nvarchar(40) NOT NULL,
    PlaySessionId uniqueidentifier NULL REFERENCES PlaySessions(Id),
    CustomerId uniqueidentifier NULL REFERENCES Customers(Id),
    ShiftId uniqueidentifier NOT NULL REFERENCES Shifts(Id),
    Status nvarchar(16) NOT NULL CHECK (Status IN ('Draft','Confirmed','Fulfilled','Cancelled')),
    Subtotal decimal(18,2) NOT NULL DEFAULT 0 CHECK (Subtotal >= 0),
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (DiscountAmount >= 0),
    TotalAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (TotalAmount >= 0),
    ConfirmedAtUtc datetime2(7) NULL,
    CancelledAtUtc datetime2(7) NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    UpdatedAtUtc datetime2(7) NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_Orders_Branch_Number UNIQUE (BranchId, OrderNumber)
);

CREATE TABLE OrderItems (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    OrderId uniqueidentifier NOT NULL REFERENCES Orders(Id),
    ProductId uniqueidentifier NULL REFERENCES Products(Id),
    ProductName nvarchar(160) NOT NULL,
    Sku nvarchar(48) NOT NULL,
    Unit nvarchar(32) NOT NULL,
    Quantity decimal(18,3) NOT NULL CHECK (Quantity > 0),
    UnitPrice decimal(18,2) NOT NULL CHECK (UnitPrice >= 0),
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (DiscountAmount >= 0),
    LineTotal decimal(18,2) NOT NULL CHECK (LineTotal >= 0),
    Note nvarchar(500) NULL
);

CREATE TABLE InventoryTransactions (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    ProductId uniqueidentifier NOT NULL REFERENCES Products(Id),
    Type nvarchar(24) NOT NULL CHECK (Type IN ('Opening','Receipt','Sale','Return','Adjustment','Waste')),
    Quantity decimal(18,3) NOT NULL CHECK (Quantity <> 0),
    QuantityBefore decimal(18,3) NOT NULL,
    QuantityAfter decimal(18,3) NOT NULL,
    UnitCost decimal(18,2) NULL,
    ReferenceType nvarchar(40) NULL,
    ReferenceId uniqueidentifier NULL,
    Note nvarchar(1000) NULL,
    OccurredAtUtc datetime2(7) NOT NULL,
    CreatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id)
);

CREATE TABLE Invoices (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    InvoiceNumber nvarchar(40) NOT NULL,
    CustomerId uniqueidentifier NULL REFERENCES Customers(Id),
    PlaySessionId uniqueidentifier NULL REFERENCES PlaySessions(Id),
    ShiftId uniqueidentifier NOT NULL REFERENCES Shifts(Id),
    Status nvarchar(16) NOT NULL CHECK (Status IN ('Draft','Issued','Paid','Voided')),
    Subtotal decimal(18,2) NOT NULL CHECK (Subtotal >= 0),
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (DiscountAmount >= 0),
    TotalAmount decimal(18,2) NOT NULL CHECK (TotalAmount >= 0),
    PaidAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (PaidAmount >= 0),
    ChangeAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (ChangeAmount >= 0),
    IssuedAtUtc datetime2(7) NOT NULL,
    PaidAtUtc datetime2(7) NULL,
    VoidedAtUtc datetime2(7) NULL,
    VoidReason nvarchar(1000) NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_Invoices_Branch_Number UNIQUE (BranchId, InvoiceNumber)
);

ALTER TABLE PlaySessions
ADD CONSTRAINT FK_PlaySessions_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id);

CREATE TABLE InvoiceLines (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    InvoiceId uniqueidentifier NOT NULL REFERENCES Invoices(Id),
    LineType nvarchar(24) NOT NULL CHECK (LineType IN ('ComputerUsage','Product','Discount','Adjustment')),
    ReferenceId uniqueidentifier NULL,
    Description nvarchar(300) NOT NULL,
    Quantity decimal(18,3) NOT NULL CHECK (Quantity > 0),
    UnitPrice decimal(18,2) NOT NULL,
    DiscountAmount decimal(18,2) NOT NULL DEFAULT 0 CHECK (DiscountAmount >= 0),
    LineTotal decimal(18,2) NOT NULL
);

CREATE TABLE Payments (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    InvoiceId uniqueidentifier NULL REFERENCES Invoices(Id),
    CustomerId uniqueidentifier NULL REFERENCES Customers(Id),
    ShiftId uniqueidentifier NULL REFERENCES Shifts(Id),
    PaymentNumber nvarchar(40) NOT NULL,
    Type nvarchar(16) NOT NULL CHECK (Type IN ('Payment','Refund','WalletTopUp')),
    Method nvarchar(24) NOT NULL CHECK (Method IN ('Cash','BankTransfer','Wallet')),
    Amount decimal(18,2) NOT NULL CHECK (Amount > 0),
    Status nvarchar(16) NOT NULL CHECK (Status IN ('Pending','Completed','Failed','Voided')),
    ExternalReference nvarchar(160) NULL,
    PaidAtUtc datetime2(7) NOT NULL,
    Note nvarchar(1000) NULL,
    CreatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    CONSTRAINT UQ_Payments_Branch_Number UNIQUE (BranchId, PaymentNumber)
);

CREATE TABLE WalletTransactions (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    WalletId uniqueidentifier NOT NULL REFERENCES Wallets(Id),
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    Type nvarchar(24) NOT NULL CHECK (Type IN ('TopUp','Usage','Refund','Adjustment','Transfer')),
    Amount decimal(18,2) NOT NULL CHECK (Amount <> 0),
    BalanceBefore decimal(18,2) NOT NULL CHECK (BalanceBefore >= 0),
    BalanceAfter decimal(18,2) NOT NULL CHECK (BalanceAfter >= 0),
    ReferenceType nvarchar(40) NULL,
    ReferenceId uniqueidentifier NULL,
    Description nvarchar(500) NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL,
    ShiftId uniqueidentifier NULL REFERENCES Shifts(Id),
    CreatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    CHECK (BalanceAfter = BalanceBefore + Amount)
);

CREATE TABLE CashMovements (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    ShiftId uniqueidentifier NOT NULL REFERENCES Shifts(Id),
    Type nvarchar(24) NOT NULL CHECK (Type IN ('Opening','Sale','TopUp','Refund','Expense','CashIn','CashOut','Adjustment')),
    Amount decimal(18,2) NOT NULL CHECK (Amount <> 0),
    ReferenceType nvarchar(40) NULL,
    ReferenceId uniqueidentifier NULL,
    Description nvarchar(500) NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL,
    CreatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id)
);

CREATE TABLE MemberTopUpRequests (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    CustomerId uniqueidentifier NOT NULL REFERENCES Customers(Id),
    Amount decimal(18,2) NOT NULL CHECK (Amount > 0),
    Status nvarchar(24) NOT NULL CHECK (Status IN ('Pending','Approved','Rejected')),
    Note nvarchar(500) NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    ProcessedAtUtc datetime2(7) NULL,
    ProcessedByUserId uniqueidentifier NULL REFERENCES AppUsers(Id),
    WalletTransactionId uniqueidentifier NULL REFERENCES WalletTransactions(Id),
    RowVersion rowversion NOT NULL
);

CREATE UNIQUE INDEX UX_MemberTopUpRequests_OnePendingPerCustomer
ON MemberTopUpRequests(CustomerId) WHERE Status = 'Pending';

CREATE UNIQUE INDEX UX_MemberTopUpRequests_WalletTransaction
ON MemberTopUpRequests(WalletTransactionId) WHERE WalletTransactionId IS NOT NULL;

CREATE TABLE ExpenseCategories (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    Name nvarchar(120) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAtUtc datetime2(7) NOT NULL,
    CONSTRAINT UQ_ExpenseCategories_Branch_Name UNIQUE (BranchId, Name)
);

CREATE TABLE Expenses (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    ShiftId uniqueidentifier NULL REFERENCES Shifts(Id),
    CategoryId uniqueidentifier NOT NULL REFERENCES ExpenseCategories(Id),
    Amount decimal(18,2) NOT NULL CHECK (Amount > 0),
    SpentAtUtc datetime2(7) NOT NULL,
    Description nvarchar(1000) NOT NULL,
    ReceiptNumber nvarchar(100) NULL,
    Status nvarchar(16) NOT NULL CHECK (Status IN ('Pending','Approved','Rejected')),
    ApprovedByUserId uniqueidentifier NULL REFERENCES AppUsers(Id),
    CreatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    CreatedAtUtc datetime2(7) NOT NULL,
    RowVersion rowversion NOT NULL
);

CREATE TABLE MaintenanceTickets (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    MachineId uniqueidentifier NOT NULL REFERENCES Machines(Id),
    TicketNumber nvarchar(40) NOT NULL,
    Priority nvarchar(16) NOT NULL CHECK (Priority IN ('Low','Medium','High','Critical')),
    Status nvarchar(20) NOT NULL CHECK (Status IN ('Open','InProgress','Resolved','Closed')),
    Title nvarchar(200) NOT NULL,
    Description nvarchar(2000) NOT NULL,
    AssignedToUserId uniqueidentifier NULL REFERENCES AppUsers(Id),
    OpenedAtUtc datetime2(7) NOT NULL,
    ResolvedAtUtc datetime2(7) NULL,
    ClosedAtUtc datetime2(7) NULL,
    Resolution nvarchar(2000) NULL,
    Cost decimal(18,2) NOT NULL DEFAULT 0 CHECK (Cost >= 0),
    CreatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    RowVersion rowversion NOT NULL,
    CONSTRAINT UQ_MaintenanceTickets_Branch_Number UNIQUE (BranchId, TicketNumber)
);

CREATE TABLE MaintenanceLogs (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    TicketId uniqueidentifier NOT NULL REFERENCES MaintenanceTickets(Id),
    StatusFrom nvarchar(20) NULL,
    StatusTo nvarchar(20) NULL,
    Note nvarchar(2000) NOT NULL,
    CostDelta decimal(18,2) NOT NULL DEFAULT 0,
    OccurredAtUtc datetime2(7) NOT NULL,
    UserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id)
);

CREATE TABLE BranchSettings (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NOT NULL REFERENCES Branches(Id),
    [Key] nvarchar(120) NOT NULL,
    ValueJson nvarchar(max) NOT NULL,
    UpdatedAtUtc datetime2(7) NOT NULL,
    UpdatedByUserId uniqueidentifier NOT NULL REFERENCES AppUsers(Id),
    CONSTRAINT UQ_BranchSettings_Branch_Key UNIQUE (BranchId, [Key])
);

CREATE TABLE AuditLogs (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    BranchId uniqueidentifier NULL REFERENCES Branches(Id),
    UserId uniqueidentifier NULL REFERENCES AppUsers(Id),
    Action nvarchar(100) NOT NULL,
    EntityType nvarchar(120) NOT NULL,
    EntityId uniqueidentifier NULL,
    OldValuesJson nvarchar(max) NULL,
    NewValuesJson nvarchar(max) NULL,
    IpAddress varchar(45) NULL,
    CorrelationId nvarchar(100) NOT NULL,
    OccurredAtUtc datetime2(7) NOT NULL
);

CREATE TABLE IdempotencyRecords (
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    Scope nvarchar(256) NOT NULL,
    [Key] nvarchar(100) NOT NULL,
    RequestHash char(64) NOT NULL,
    ResponseStatusCode int NOT NULL,
    ResponseBody nvarchar(max) NULL,
    ExpiresAtUtc datetime2(7) NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL,
    CONSTRAINT UQ_IdempotencyRecords_Scope_Key UNIQUE (Scope, [Key])
);

CREATE INDEX IX_Machines_Branch_Status_Group ON Machines(BranchId, Status, MachineGroupId);
CREATE INDEX IX_PlaySessions_Branch_StartedAt ON PlaySessions(BranchId, StartedAtUtc DESC);
CREATE INDEX IX_PlaySessions_Customer_StartedAt ON PlaySessions(CustomerId, StartedAtUtc DESC);
CREATE INDEX IX_WalletTransactions_Wallet_Time ON WalletTransactions(WalletId, OccurredAtUtc DESC);
CREATE INDEX IX_InventoryTransactions_Product_Time ON InventoryTransactions(BranchId, ProductId, OccurredAtUtc DESC);
CREATE INDEX IX_Invoices_Branch_IssuedAt ON Invoices(BranchId, IssuedAtUtc DESC);
CREATE INDEX IX_Payments_Branch_PaidAt ON Payments(BranchId, PaidAtUtc DESC);
CREATE INDEX IX_AuditLogs_Time ON AuditLogs(OccurredAtUtc DESC);
CREATE INDEX IX_AuditLogs_Entity ON AuditLogs(EntityType, EntityId);
