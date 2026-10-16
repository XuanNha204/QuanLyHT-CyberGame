# Thiết kế database — CyberGame Manager

## 1. Quy ước chung

- Hệ quản trị production: SQL Server.
- Khóa chính: `uniqueidentifier`, tạo ở ứng dụng để hỗ trợ xử lý phân tán.
- Mỗi bảng nghiệp vụ có `CreatedAtUtc`, `CreatedByUserId`; bảng thay đổi có thêm `UpdatedAtUtc`, `UpdatedByUserId`.
- Các master data có `IsActive`; không xóa vật lý bản ghi đã được tham chiếu.
- Các aggregate dễ tranh chấp có `RowVersion rowversion`.
- Tiền: `decimal(18,2)`, không âm nếu không phải ledger điều chỉnh.
- Thời gian: UTC; timezone IANA/Windows được cấu hình trên `Branches`.
- Ledger (`WalletTransactions`, `InventoryTransactions`, `CashMovements`) là append-only.

## 2. Danh mục bảng

### Identity và tổ chức

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| Branches | Id, Code, Name, Address, TimeZoneId, Phone, IsActive | Code unique |
| Roles | Id, Code, Name | Code unique; seed Owner/Manager/Cashier/Technician |
| AppUsers | Id, BranchId?, Username, NormalizedUsername, FullName, PasswordHash, IsActive, FailedLoginCount, LockoutEndUtc, LastLoginAtUtc | Username unique; Owner có thể không khóa vào một branch |
| UserRoles | UserId, RoleId | PK kép |
| RefreshTokens | Id, UserId, TokenHash, ExpiresAtUtc, RevokedAtUtc, ReplacedByTokenId, DeviceName | Chỉ lưu hash; index UserId + ExpiresAtUtc |

### Máy trạm và giá

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| MachineGroups | Id, BranchId, Code, Name, SortOrder, IsActive | (BranchId, Code) unique |
| Machines | Id, BranchId, MachineGroupId, Code, DisplayName, IpAddress, MacAddress, Status, LastHeartbeatAtUtc, Notes, RowVersion | (BranchId, Code) unique; status enum |
| PricingPlans | Id, BranchId, MachineGroupId?, Name, EffectiveFromUtc, EffectiveToUtc?, IsDefault, IsActive | Khoảng hiệu lực hợp lệ |
| PricingRules | Id, PricingPlanId, DayOfWeekMask, StartTime, EndTime, HourlyRate, Priority | HourlyRate > 0; hỗ trợ khung giờ qua nửa đêm bằng engine |

### Khách hàng và ví

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| CustomerGroups | Id, BranchId, Name, DiscountPercent, IsActive | Discount 0..100 |
| Customers | Id, HomeBranchId, CustomerGroupId?, Code, FullName, Phone, Email, DateOfBirth?, Status, MemberPinHash?, MemberFailedLoginCount, MemberLockoutEndUtc?, MemberLastLoginAtUtc?, JoinedAtUtc, Notes, RowVersion | Code unique; PIN chỉ lưu hash; khóa 15 phút sau 5 lần sai |
| Wallets | Id, CustomerId, Balance, RowVersion | CustomerId unique; Balance >= 0 |
| WalletTransactions | Id, WalletId, BranchId, Type, Amount, BalanceBefore, BalanceAfter, ReferenceType, ReferenceId?, Description, OccurredAtUtc, ShiftId?, CreatedByUserId | Append-only; After = Before + Amount |
| MemberTopUpRequests | Id, BranchId, CustomerId, Amount, Status, Note?, CreatedAtUtc, ProcessedAtUtc?, ProcessedByUserId?, WalletTransactionId?, RowVersion | Một Pending/customer; duyệt mới sinh wallet transaction, payment và cash movement |

### Phiên chơi

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| PlaySessions | Id, BranchId, MachineId, CustomerId?, ShiftId, SessionNumber, BillingMode, Status, StartedAtUtc, PausedAtUtc?, EndedAtUtc?, TotalPausedSeconds, UsageAmount, ServiceAmount, DiscountAmount, TotalAmount, InvoiceId?, RowVersion | Filtered unique MachineId khi status Open/Paused; tổng tiền không âm |
| SessionChargeSegments | Id, PlaySessionId, PricingRuleId?, StartedAtUtc, EndedAtUtc?, RatePerHour, BillableSeconds, Amount | Snapshot RatePerHour; thời gian không chồng lấn |
| SessionEvents | Id, PlaySessionId, EventType, OccurredAtUtc, UserId, DataJson, Note | Append-only timeline |

### Danh mục, kho và bán hàng

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| ProductCategories | Id, BranchId, Name, SortOrder, IsActive | (BranchId, Name) unique |
| Products | Id, BranchId, CategoryId, Sku, Name, Unit, SalePrice, CostPrice, TrackInventory, ReorderLevel, IsActive, RowVersion | (BranchId, Sku) unique; giá >= 0 |
| InventoryBalances | Id, BranchId, ProductId, QuantityOnHand, RowVersion | (BranchId, ProductId) unique |
| InventoryTransactions | Id, BranchId, ProductId, Type, Quantity, QuantityBefore, QuantityAfter, UnitCost?, ReferenceType, ReferenceId?, Note, OccurredAtUtc, CreatedByUserId | Append-only; Quantity khác 0 |
| Orders | Id, BranchId, OrderNumber, PlaySessionId?, CustomerId?, ShiftId, Status, Subtotal, DiscountAmount, TotalAmount, ConfirmedAtUtc?, CancelledAtUtc?, RowVersion | (BranchId, OrderNumber) unique |
| OrderItems | Id, OrderId, ProductId?, ProductName, Sku, Unit, Quantity, UnitPrice, DiscountAmount, LineTotal, Note | Snapshot mô tả/giá; Quantity > 0 |

### Hóa đơn và thanh toán

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| Invoices | Id, BranchId, InvoiceNumber, CustomerId?, PlaySessionId?, ShiftId, Status, Subtotal, DiscountAmount, TotalAmount, PaidAmount, ChangeAmount, IssuedAtUtc, PaidAtUtc?, VoidedAtUtc?, VoidReason?, RowVersion | (BranchId, InvoiceNumber) unique; Paid <= Total + tiền thừa cho cash |
| InvoiceLines | Id, InvoiceId, LineType, ReferenceId?, Description, Quantity, UnitPrice, DiscountAmount, LineTotal | Snapshot đầy đủ |
| Payments | Id, BranchId, InvoiceId?, CustomerId?, ShiftId?, PaymentNumber, Type, Method, Amount, Status, ExternalReference?, PaidAtUtc, Note, CreatedByUserId | (BranchId, PaymentNumber) unique; refund là bản ghi đối ứng |

### Ca, chi phí, bảo trì và hệ thống

| Bảng | Cột trọng yếu | Ràng buộc/nghiệp vụ |
|---|---|---|
| Shifts | Id, BranchId, UserId, ShiftNumber, Status, OpenedAtUtc, ClosedAtUtc?, OpeningCash, ExpectedCash?, ActualCash?, Difference?, ClosingNote?, RowVersion | Filtered unique UserId khi Open |
| CashMovements | Id, ShiftId, Type, Amount, ReferenceType, ReferenceId?, Description, OccurredAtUtc, CreatedByUserId | Append-only |
| ExpenseCategories | Id, BranchId, Name, IsActive | (BranchId, Name) unique |
| Expenses | Id, BranchId, ShiftId?, CategoryId, Amount, SpentAtUtc, Description, ReceiptNumber?, Status, ApprovedByUserId?, CreatedByUserId | Amount > 0 |
| MaintenanceTickets | Id, BranchId, MachineId, TicketNumber, Priority, Status, Title, Description, AssignedToUserId?, OpenedAtUtc, ResolvedAtUtc?, ClosedAtUtc?, Resolution?, Cost, RowVersion | (BranchId, TicketNumber) unique |
| MaintenanceLogs | Id, TicketId, StatusFrom?, StatusTo?, Note, CostDelta, OccurredAtUtc, UserId | Append-only |
| BranchSettings | Id, BranchId, Key, ValueJson, UpdatedAtUtc, UpdatedByUserId | (BranchId, Key) unique |
| AuditLogs | Id, BranchId?, UserId?, Action, EntityType, EntityId?, OldValuesJson?, NewValuesJson?, IpAddress?, CorrelationId, OccurredAtUtc | Append-only; index thời gian/entity/user |
| IdempotencyRecords | Id, Scope, Key, RequestHash, ResponseStatusCode, ResponseBody, ExpiresAtUtc, CreatedAtUtc | (Scope, Key) unique |

## 3. Quan hệ chính

- Branch 1–n Users, MachineGroups, Machines, PricingPlans, Products, Shifts và mọi giao dịch vận hành.
- MachineGroup 1–n Machines; PricingPlan có thể áp dụng branch hoặc một MachineGroup.
- Customer 1–1 Wallet và 1–n PlaySessions/Orders/Invoices/MemberTopUpRequests.
- Machine 1–n PlaySessions nhưng chỉ một session mở tại một thời điểm.
- PlaySession 1–n ChargeSegments, SessionEvents, Orders; tối đa một invoice cuối phiên.
- Order 1–n OrderItems; sản phẩm có thể bị ngừng/xóa mềm nhưng snapshot order item vẫn nguyên vẹn.
- Invoice 1–n InvoiceLines và Payments.
- Shift 1–n Sessions, Orders, Payments, WalletTransactions, CashMovements.
- Machine 1–n MaintenanceTickets; ticket 1–n MaintenanceLogs.

## 4. Trạng thái và chuyển đổi hợp lệ

### MachineStatus

`Available → InUse → Available`; `Available ↔ Maintenance`; `Available ↔ Offline`; không chuyển `InUse → Maintenance` trước khi kết phiên.

### PlaySessionStatus

`Open → Paused → Open`; `Open|Paused → Completed`; `Open|Paused → Cancelled` chỉ khi chưa phát sinh phí/order hoặc có quyền Manager và audit.

### OrderStatus

`Draft → Confirmed → Fulfilled`; `Draft|Confirmed → Cancelled`; hủy sau xác nhận tạo hoàn kho.

### InvoiceStatus

`Draft → Issued → Paid`; `Issued|Paid → Voided` chỉ bằng quyền Manager/Owner và sinh refund/ledger đối ứng khi cần.

### ShiftStatus

`Open → Closed`; ca đã đóng không mở lại, sai lệch xử lý bằng adjustment có audit.

### MaintenanceStatus

`Open → InProgress → Resolved → Closed`; `Resolved → InProgress` khi kiểm tra lại không đạt.

## 5. Chỉ mục quan trọng

- Machines `(BranchId, Status, MachineGroupId)`.
- PlaySessions `(BranchId, StartedAtUtc DESC)`, `(CustomerId, StartedAtUtc DESC)`, filtered unique `(MachineId)` cho trạng thái mở.
- WalletTransactions `(WalletId, OccurredAtUtc DESC)`.
- MemberTopUpRequests filtered unique `(CustomerId)` khi `Status = Pending`; `WalletTransactionId` unique khi có.
- InventoryTransactions `(BranchId, ProductId, OccurredAtUtc DESC)`.
- Orders/Invoices/Payments `(BranchId, Created/Issued/PaidAtUtc DESC)`.
- Shifts `(BranchId, OpenedAtUtc DESC)`.
- AuditLogs `(OccurredAtUtc DESC)`, `(EntityType, EntityId)`, `(UserId, OccurredAtUtc DESC)`.
- Full-text không bắt buộc MVP; dùng normalized code/name/phone và index phù hợp.

## 6. Quy tắc transaction và concurrency

- Mở phiên: khóa logic máy bằng rowversion + filtered index.
- Ghi ví: đọc rowversion, ghi ledger và cập nhật balance cùng transaction; retry có giới hạn khi conflict.
- Xác nhận/hủy order: cập nhật order, inventory ledger và balance cùng transaction.
- Thanh toán: cập nhật payment, invoice, wallet/cash movement cùng transaction.
- Close shift: snapshot các tổng giao dịch trong transaction isolation phù hợp.
- Mọi endpoint tài chính nhận `Idempotency-Key` và lưu response để trả lại khi request bị gửi lặp.

## 7. Xóa và lưu trữ

- User, customer, machine, product và bảng giá dùng deactivate/soft-delete.
- Không cascade delete từ master sang ledger/giao dịch.
- Refresh token hết hạn và idempotency record có thể purge bằng job.
- Audit/financial ledger giữ theo chính sách pháp lý của đơn vị vận hành; mặc định không xóa.
