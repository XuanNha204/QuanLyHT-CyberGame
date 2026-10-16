using CyberGame.Application.DTOs;
using CyberGame.Domain.Enums;

namespace CyberGame.MemberClient.Services;

public interface IMemberApiClient
{
    string MachineCode { get; }
    string? CurrentToken { get; }
    MemberLoginResponse? CurrentUser { get; }
    void SetAuthSession(MemberLoginResponse? response);

    Task<IReadOnlyList<MemberMachineSelectionDto>> GetMachinesAsync();
    Task<MemberLoginResponse> LoginAsync(string memberCode, string pin, Guid machineId, string machineCode);
    Task<MemberMeResponse> GetProfileAsync();
    Task ChangePinAsync(string currentPin, string newPin);
    Task<MemberSessionDto?> GetActiveSessionAsync();
    Task<MemberSessionQuoteDto> GetSessionQuoteAsync();
    Task<MemberSessionSummaryDto> CompleteSessionAsync();
    Task<IReadOnlyList<MemberProductDto>> GetCatalogAsync();
    Task<MemberOrderDto> CreateOrderAsync(List<MemberOrderItemRequest> items, PaymentMethod paymentMethod);
    Task<IReadOnlyList<MemberOrderDto>> GetOrdersAsync();
    Task<MemberTopUpRequestDto> CreateTopUpRequestAsync(decimal amount, string? note);
    Task<IReadOnlyList<MemberTopUpRequestDto>> GetTopUpRequestsAsync();
}
