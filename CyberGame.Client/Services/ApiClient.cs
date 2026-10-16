using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberGame.Application.Common;
using CyberGame.Application.DTOs;

namespace CyberGame.Client.Services;

public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _authToken;

    public string? AuthToken => _authToken;

    public ApiClient(HttpClient http)
    {
        _http = http;
        if (_http.BaseAddress == null)
        {
            _http.BaseAddress = new Uri("http://localhost:5227/api/v1/");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public void SetAuthToken(string? token)
    {
        _authToken = token;
        if (string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        string? errorMessage = null;
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("detail", out var detailProp) && !string.IsNullOrWhiteSpace(detailProp.GetString()))
                {
                    errorMessage = detailProp.GetString();
                }
                else if (doc.RootElement.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
                {
                    errorMessage = titleProp.GetString();
                }
            }
        }
        catch
        {
            // fallback to default
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn hoặc không có quyền truy cập.",
                System.Net.HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện chức năng này.",
                System.Net.HttpStatusCode.NotFound => "Không tìm thấy dữ liệu yêu cầu.",
                System.Net.HttpStatusCode.BadRequest => "Dữ liệu gửi lên không hợp lệ.",
                System.Net.HttpStatusCode.InternalServerError => "Máy chủ xảy ra lỗi xử lý.",
                _ => $"Lỗi máy chủ ({response.StatusCode})"
            };
        }

        throw new HttpRequestException(errorMessage, null, response.StatusCode);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        var resp = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
        var result = await resp.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
        return result!;
    }

    private async Task<T> PostCommandAsync<T>(string uri, object? content, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, uri);
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        if (content != null)
        {
            req.Content = JsonContent.Create(content, options: _jsonOptions);
        }
        var resp = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
        var result = await resp.Content.ReadFromJsonAsync<T>(_jsonOptions, ct);
        return result!;
    }

    private async Task<T> PutAsync<T>(string uri, object content, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, uri)
        {
            Content = JsonContent.Create(content, options: _jsonOptions)
        };
        return await SendAsync<T>(req, ct);
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var resp = await _http.PostAsJsonAsync("auth/login", request, _jsonOptions, ct);
        await EnsureSuccessAsync(resp, ct);
        var result = await resp.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions, ct);
        SetAuthToken(result!.AccessToken);
        return result;
    }

    public Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        return SendAsync<DashboardSummaryDto>(new HttpRequestMessage(HttpMethod.Get, "dashboard/summary"), ct);
    }

    public Task<List<MachineGroupDto>> GetMachineGroupsAsync(CancellationToken ct = default)
    {
        return SendAsync<List<MachineGroupDto>>(new HttpRequestMessage(HttpMethod.Get, "machine-groups"), ct);
    }

    public Task<List<MachineDto>> GetMachinesAsync(CancellationToken ct = default)
    {
        return SendAsync<List<MachineDto>>(new HttpRequestMessage(HttpMethod.Get, "machines"), ct);
    }

    public Task<List<PlaySessionDto>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        return SendAsync<List<PlaySessionDto>>(new HttpRequestMessage(HttpMethod.Get, "sessions/active"), ct);
    }

    public Task<PlaySessionDto> PauseSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return PostCommandAsync<PlaySessionDto>($"sessions/{sessionId}/pause", null, ct);
    }

    public Task<PlaySessionDto> ResumeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return PostCommandAsync<PlaySessionDto>($"sessions/{sessionId}/resume", null, ct);
    }

    public Task<PlaySessionDto> TransferSessionAsync(Guid sessionId, TransferSessionRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<PlaySessionDto>($"sessions/{sessionId}/transfer", request, ct);
    }

    public Task<InvoiceDto> CompleteSessionAsync(Guid sessionId, CompleteSessionRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<InvoiceDto>($"sessions/{sessionId}/complete", request, ct);
    }

    public Task<PagedResult<CustomerDto>> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var url = $"customers?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search)}";
        }
        return SendAsync<PagedResult<CustomerDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<CustomerDto>("customers", request, ct);
    }

    public Task<CustomerDto> UpdateCustomerAsync(Guid customerId, UpdateCustomerRequest request, CancellationToken ct = default)
    {
        return PutAsync<CustomerDto>($"customers/{customerId}", request, ct);
    }

    public Task<CustomerDto> TopUpWalletAsync(Guid customerId, TopUpWalletRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<CustomerDto>($"customers/{customerId}/wallet/top-up", request, ct);
    }

    public Task<PagedResult<WalletTransactionDto>> GetWalletTransactionsAsync(Guid customerId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        return SendAsync<PagedResult<WalletTransactionDto>>(new HttpRequestMessage(HttpMethod.Get, $"customers/{customerId}/wallet-transactions?page={page}&pageSize={pageSize}"), ct);
    }

    public Task<List<ProductCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        return SendAsync<List<ProductCategoryDto>>(new HttpRequestMessage(HttpMethod.Get, "categories"), ct);
    }

    public async Task<List<ProductDto>> GetProductsAsync(Guid? categoryId = null, CancellationToken ct = default)
    {
        var url = "products?pageSize=1000";
        if (categoryId.HasValue) url += $"&categoryId={categoryId.Value}";
        var paged = await SendAsync<PagedResult<ProductDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
        return paged?.Items?.ToList() ?? new List<ProductDto>();
    }

    public Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<ProductDto>("products", request, ct);
    }

    public Task<List<InventoryItemDto>> GetInventoryAsync(CancellationToken ct = default)
    {
        return SendAsync<List<InventoryItemDto>>(new HttpRequestMessage(HttpMethod.Get, "inventory"), ct);
    }

    public Task<InventoryItemDto> ReceiveInventoryAsync(ReceiveInventoryRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<InventoryItemDto>("inventory/receive", request, ct);
    }

    public Task<InventoryItemDto> AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<InventoryItemDto>("inventory/adjust", request, ct);
    }

    public Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<OrderDto>("orders", request, ct);
    }

    public Task<PagedResult<OrderDto>> GetOrdersAsync(CyberGame.Domain.Enums.OrderStatus? status = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var url = $"orders?page={page}&pageSize={pageSize}";
        if (status.HasValue) url += $"&status={status.Value}";
        return SendAsync<PagedResult<OrderDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public Task<OrderDto> ConfirmOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        return PostCommandAsync<OrderDto>($"orders/{orderId}/confirm", null, ct);
    }

    public Task<OrderDto> CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        return PostCommandAsync<OrderDto>($"orders/{orderId}/cancel", null, ct);
    }

    public Task<OrderDto> FulfillOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        return PostCommandAsync<OrderDto>($"orders/{orderId}/fulfill", null, ct);
    }

    public Task<PagedResult<InvoiceDto>> GetInvoicesAsync(DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var url = $"invoices?page={page}&pageSize={pageSize}";
        if (fromDate.HasValue) url += $"&fromDate={fromDate.Value:yyyy-MM-dd}";
        if (toDate.HasValue) url += $"&toDate={toDate.Value:yyyy-MM-dd}";
        return SendAsync<PagedResult<InvoiceDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public async Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("shifts/current", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            await EnsureSuccessAsync(resp, ct);
            return await resp.Content.ReadFromJsonAsync<ShiftDto>(_jsonOptions, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<ShiftSummaryDto> GetShiftSummaryAsync(Guid shiftId, CancellationToken ct = default)
    {
        return SendAsync<ShiftSummaryDto>(new HttpRequestMessage(HttpMethod.Get, $"shifts/{shiftId}/summary"), ct);
    }

    public Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<ShiftDto>("shifts/open", request, ct);
    }

    public Task<ShiftDto> CloseShiftAsync(Guid shiftId, CloseShiftRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<ShiftDto>($"shifts/{shiftId}/close", request, ct);
    }

    public Task<CashMovementDto> AddCashMovementAsync(Guid shiftId, CreateCashMovementRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<CashMovementDto>($"shifts/{shiftId}/cash-movements", request, ct);
    }

    public Task<IReadOnlyList<MaintenanceTicketDto>> GetMaintenanceTicketsAsync(CancellationToken ct = default)
    {
        return SendAsync<IReadOnlyList<MaintenanceTicketDto>>(new HttpRequestMessage(HttpMethod.Get, "maintenance-tickets"), ct);
    }

    public Task<MaintenanceTicketDto> CreateMaintenanceTicketAsync(CreateMaintenanceTicketRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<MaintenanceTicketDto>("maintenance-tickets", request, ct);
    }

    public Task<MaintenanceTicketDto> AddMaintenanceLogAsync(Guid ticketId, AddMaintenanceLogRequest request, CancellationToken ct = default)
    {
        return PostCommandAsync<MaintenanceTicketDto>($"maintenance-tickets/{ticketId}/logs", request, ct);
    }

    public Task<IReadOnlyList<RevenueReportDto>> GetRevenueReportAsync(DateTime? fromUtc = null, DateTime? toUtc = null, CancellationToken ct = default)
    {
        var url = "reports/revenue";
        var queryParams = new List<string>();
        if (fromUtc.HasValue) queryParams.Add($"fromUtc={fromUtc.Value:O}");
        if (toUtc.HasValue) queryParams.Add($"toUtc={toUtc.Value:O}");
        if (queryParams.Any()) url += "?" + string.Join("&", queryParams);

        return SendAsync<IReadOnlyList<RevenueReportDto>>(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public Task<IReadOnlyList<TopProductReportDto>> GetTopProductsReportAsync(int limit = 10, CancellationToken ct = default)
    {
        return SendAsync<IReadOnlyList<TopProductReportDto>>(new HttpRequestMessage(HttpMethod.Get, $"reports/top-products?limit={limit}"), ct);
    }

    public Task<CustomerDto> SetCustomerMemberPinAsync(Guid customerId, string pin, CancellationToken ct = default)
    {
        return PostCommandAsync<CustomerDto>($"customers/{customerId}/member-pin", new SetMemberPinRequest(pin), ct);
    }

    public async Task<IReadOnlyList<StaffMemberTopUpRequestDto>> GetMemberTopUpRequestsAsync(string? status = "Pending", CancellationToken ct = default)
    {
        var uri = string.IsNullOrWhiteSpace(status)
            ? "member-top-up-requests"
            : $"member-top-up-requests?status={status}";
        var result = await SendAsync<List<StaffMemberTopUpRequestDto>>(new HttpRequestMessage(HttpMethod.Get, uri), ct);
        return result ?? new List<StaffMemberTopUpRequestDto>();
    }

    public Task<StaffMemberTopUpRequestDto> ApproveMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        return PostCommandAsync<StaffMemberTopUpRequestDto>($"member-top-up-requests/{requestId}/approve", new { }, ct);
    }

    public Task<StaffMemberTopUpRequestDto> RejectMemberTopUpRequestAsync(Guid requestId, CancellationToken ct = default)
    {
        return PostCommandAsync<StaffMemberTopUpRequestDto>($"member-top-up-requests/{requestId}/reject", new { }, ct);
    }
}
