using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Enums;
using CyberGame.MemberClient.Models;

namespace CyberGame.MemberClient.Services;

public class MemberApiClient : IMemberApiClient
{
    private readonly HttpClient _httpClient;
    private readonly MemberClientConfig _config;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string MachineCode => CurrentUser?.Machine.Code ?? _config.MachineCode;
    public string? CurrentToken { get; private set; }
    public MemberLoginResponse? CurrentUser { get; private set; }

    public MemberApiClient(HttpClient httpClient, MemberClientConfig config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    public void SetAuthSession(MemberLoginResponse? response)
    {
        CurrentUser = response;
        CurrentToken = response?.Token;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? content = null, bool requireIdempotency = false)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(CurrentToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CurrentToken);
        }

        if (requireIdempotency && method == HttpMethod.Post)
        {
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        }

        if (content != null)
        {
            request.Content = JsonContent.Create(content, options: JsonOptions);
        }

        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? errorMessage = null;
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("detail", out var detailElem) && !string.IsNullOrWhiteSpace(detailElem.GetString()))
                {
                    errorMessage = detailElem.GetString();
                }
                else if (root.TryGetProperty("message", out var msgElem) && !string.IsNullOrWhiteSpace(msgElem.GetString()))
                {
                    errorMessage = msgElem.GetString();
                }
                else if (root.TryGetProperty("title", out var titleElem) && !string.IsNullOrWhiteSpace(titleElem.GetString()))
                {
                    errorMessage = titleElem.GetString();
                }
            }
        }
        catch
        {
            // Fall back to a status-specific message below.
        }

        errorMessage ??= response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            HttpStatusCode.Forbidden => "Tài khoản không có quyền thực hiện chức năng này.",
            HttpStatusCode.NotFound => "Không tìm thấy dữ liệu yêu cầu trên máy chủ.",
            HttpStatusCode.BadRequest => "Dữ liệu gửi lên máy chủ không hợp lệ.",
            _ => $"Máy chủ trả về lỗi {(int)response.StatusCode}: {response.ReasonPhrase}"
        };

        throw new HttpRequestException(errorMessage, null, response.StatusCode);
    }

    public async Task<IReadOnlyList<MemberMachineSelectionDto>> GetMachinesAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/machines");
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<MemberMachineSelectionDto>>(JsonOptions)
               ?? new List<MemberMachineSelectionDto>();
    }

    public async Task<MemberLoginResponse> LoginAsync(string memberCode, string pin, Guid machineId, string machineCode)
    {
        var req = new MemberLoginRequest(memberCode, pin, machineCode, machineId);
        var request = CreateRequest(HttpMethod.Post, "member/auth/login", req);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<MemberLoginResponse>(JsonOptions)
                     ?? throw new InvalidOperationException("Không thể đọc phản hồi đăng nhập từ máy chủ.");

        SetAuthSession(result);
        return result;
    }

    public async Task<MemberMeResponse> GetProfileAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/me");
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<MemberMeResponse>(JsonOptions)
               ?? throw new InvalidOperationException("Không thể tải thông tin hội viên.");
    }

    public async Task ChangePinAsync(string currentPin, string newPin)
    {
        var request = CreateRequest(HttpMethod.Post, "member/change-pin", new ChangeMemberPinRequest(currentPin, newPin));
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task<MemberSessionDto?> GetActiveSessionAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/session");
        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<MemberSessionDto>(JsonOptions);
    }

    public async Task<MemberSessionQuoteDto> GetSessionQuoteAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/session/quote");
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<MemberSessionQuoteDto>(JsonOptions)
               ?? throw new InvalidOperationException("Không thể tải báo giá phiên chơi.");
    }

    public async Task<MemberSessionSummaryDto> CompleteSessionAsync()
    {
        var request = CreateRequest(HttpMethod.Post, "member/session/complete", requireIdempotency: true);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<MemberSessionSummaryDto>(JsonOptions)
               ?? throw new InvalidOperationException("Không thể hoàn tất phiên chơi.");
    }

    public async Task<IReadOnlyList<MemberProductDto>> GetCatalogAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/catalog");
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<MemberProductDto>>(JsonOptions)
               ?? new List<MemberProductDto>();
    }

    public async Task<MemberOrderDto> CreateOrderAsync(List<MemberOrderItemRequest> items, PaymentMethod paymentMethod)
    {
        var req = new CreateMemberOrderRequest(items, paymentMethod);
        var request = CreateRequest(HttpMethod.Post, "member/orders", req, requireIdempotency: true);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<MemberOrderDto>(JsonOptions)
               ?? throw new InvalidOperationException("Không thể tạo đơn hàng dịch vụ.");
    }

    public async Task<IReadOnlyList<MemberOrderDto>> GetOrdersAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/orders");
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<MemberOrderDto>>(JsonOptions)
               ?? new List<MemberOrderDto>();
    }

    public async Task<MemberTopUpRequestDto> CreateTopUpRequestAsync(decimal amount, string? note)
    {
        var req = new CreateMemberTopUpRequest(amount, note);
        var request = CreateRequest(HttpMethod.Post, "member/top-up-requests", req, requireIdempotency: true);
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<MemberTopUpRequestDto>(JsonOptions)
               ?? throw new InvalidOperationException("Không thể gửi yêu cầu nạp tiền.");
    }

    public async Task<IReadOnlyList<MemberTopUpRequestDto>> GetTopUpRequestsAsync()
    {
        var request = CreateRequest(HttpMethod.Get, "member/top-up-requests");
        var response = await _httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);

        return await response.Content.ReadFromJsonAsync<List<MemberTopUpRequestDto>>(JsonOptions)
               ?? new List<MemberTopUpRequestDto>();
    }
}
