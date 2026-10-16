using CyberGame.Application.Common;
using CyberGame.Application.DTOs;

namespace CyberGame.Client.Services;

public interface IApiClient
{
    string? AuthToken { get; }
    void SetAuthToken(string? token);

    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default);
    Task<List<MachineGroupDto>> GetMachineGroupsAsync(CancellationToken ct = default);
    Task<List<MachineDto>> GetMachinesAsync(CancellationToken ct = default);
    Task<List<PlaySessionDto>> GetActiveSessionsAsync(CancellationToken ct = default);
    Task<PlaySessionDto> PauseSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<PlaySessionDto> ResumeSessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<PlaySessionDto> TransferSessionAsync(Guid sessionId, TransferSessionRequest request, CancellationToken ct = default);
    Task<InvoiceDto> CompleteSessionAsync(Guid sessionId, CompleteSessionRequest request, CancellationToken ct = default);

    Task<PagedResult<CustomerDto>> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken ct = default);
    Task<CustomerDto> TopUpWalletAsync(Guid customerId, TopUpWalletRequest request, CancellationToken ct = default);
    Task<PagedResult<WalletTransactionDto>> GetWalletTransactionsAsync(Guid customerId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    Task<List<ProductCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<List<ProductDto>> GetProductsAsync(Guid? categoryId = null, CancellationToken ct = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<List<InventoryItemDto>> GetInventoryAsync(CancellationToken ct = default);
    Task<InventoryItemDto> ReceiveInventoryAsync(ReceiveInventoryRequest request, CancellationToken ct = default);
    Task<InventoryItemDto> AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken ct = default);

    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);
    Task<PagedResult<OrderDto>> GetOrdersAsync(CyberGame.Domain.Enums.OrderStatus? status = null, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<OrderDto> ConfirmOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<OrderDto> CancelOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<OrderDto> FulfillOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<PagedResult<InvoiceDto>> GetInvoicesAsync(DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50, CancellationToken ct = default);

    Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default);
    Task<ShiftSummaryDto> GetShiftSummaryAsync(Guid shiftId, CancellationToken ct = default);
    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default);
    Task<ShiftDto> CloseShiftAsync(Guid shiftId, CloseShiftRequest request, CancellationToken ct = default);
    Task<CashMovementDto> AddCashMovementAsync(Guid shiftId, CreateCashMovementRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<MaintenanceTicketDto>> GetMaintenanceTicketsAsync(CancellationToken ct = default);
    Task<MaintenanceTicketDto> CreateMaintenanceTicketAsync(CreateMaintenanceTicketRequest request, CancellationToken ct = default);
    Task<MaintenanceTicketDto> AddMaintenanceLogAsync(Guid ticketId, AddMaintenanceLogRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<RevenueReportDto>> GetRevenueReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default);
    Task<IReadOnlyList<TopProductReportDto>> GetTopProductsReportAsync(int limit = 10, CancellationToken ct = default);

    Task<CustomerDto> SetCustomerMemberPinAsync(Guid customerId, string pin, CancellationToken ct = default);
    Task<IReadOnlyList<StaffMemberTopUpRequestDto>> GetMemberTopUpRequestsAsync(string? status = "Pending", CancellationToken ct = default);
    Task<StaffMemberTopUpRequestDto> ApproveMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default);
    Task<StaffMemberTopUpRequestDto> RejectMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default);
}
