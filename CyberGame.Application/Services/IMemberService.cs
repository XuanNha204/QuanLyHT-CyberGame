using CyberGame.Application.DTOs;

namespace CyberGame.Application.Services;

public interface IMemberService
{
    Task<IReadOnlyList<MemberMachineSelectionDto>> GetMachinesAsync(CancellationToken ct = default);
    Task<MemberLoginResponse> LoginAsync(MemberLoginRequest request, CancellationToken ct = default);
    Task ChangePinAsync(ChangeMemberPinRequest request, CancellationToken ct = default);
    Task<MemberMeResponse> GetMeAsync(CancellationToken ct = default);
    Task<MemberSessionDto?> GetActiveSessionAsync(CancellationToken ct = default);
    Task<MemberSessionQuoteDto> GetSessionQuoteAsync(CancellationToken ct = default);
    Task<MemberSessionSummaryDto> CompleteSessionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MemberProductDto>> GetCatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MemberOrderDto>> GetOrdersAsync(CancellationToken ct = default);
    Task<MemberOrderDto> CreateOrderAsync(CreateMemberOrderRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<MemberTopUpRequestDto>> GetTopUpRequestsAsync(CancellationToken ct = default);
    Task<MemberTopUpRequestDto> CreateTopUpRequestAsync(CreateMemberTopUpRequest request, CancellationToken ct = default);
}
