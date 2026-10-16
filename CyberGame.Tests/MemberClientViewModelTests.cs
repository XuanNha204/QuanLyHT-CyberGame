using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Enums;
using CyberGame.MemberClient.Models;
using CyberGame.MemberClient.Services;
using CyberGame.MemberClient.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CyberGame.Tests;

public class MockMemberApiClient : IMemberApiClient
{
    public string MachineCode { get; set; } = "PC-01";
    public string? CurrentToken { get; private set; }
    public MemberLoginResponse? CurrentUser { get; private set; }

    public bool LoginThrows { get; set; }
    public string? LoginErrorMessage { get; set; }
    public MemberSessionDto? ActiveSessionResult { get; set; }
    public MemberSessionQuoteDto? QuoteResult { get; set; }
    public MemberSessionSummaryDto? CompleteSessionResult { get; set; }
    public MemberOrderDto? CreateOrderResult { get; set; }
    public MemberTopUpRequestDto? CreateTopUpResult { get; set; }
    public int CompleteSessionCallCount { get; set; }
    public int CompleteSessionFailuresRemaining { get; set; }
    public IReadOnlyList<MemberMachineSelectionDto> MachinesResult { get; set; } = Array.Empty<MemberMachineSelectionDto>();

    public void SetAuthSession(MemberLoginResponse? response)
    {
        CurrentUser = response;
        CurrentToken = response?.Token;
    }

    public Task<IReadOnlyList<MemberMachineSelectionDto>> GetMachinesAsync() => Task.FromResult(MachinesResult);

    public Task<MemberLoginResponse> LoginAsync(string memberCode, string pin, Guid machineId, string machineCode)
    {
        if (LoginThrows)
        {
            throw new InvalidOperationException(LoginErrorMessage ?? "Đăng nhập thất bại.");
        }

        var res = new MemberLoginResponse(
            "mock_token",
            new MemberProfileDto(Guid.NewGuid(), memberCode, "Member Test", null, null, 100000m),
            new MemberMachineDto(machineId, machineCode, "Máy Test", Guid.NewGuid(), "Chi nhánh Test")
        );
        SetAuthSession(res);
        return Task.FromResult(res);
    }

    public Task<MemberMeResponse> GetProfileAsync() => Task.FromResult(new MemberMeResponse(CurrentUser!.Member, CurrentUser.Machine));

    public Task ChangePinAsync(string currentPin, string newPin) => Task.CompletedTask;

    public Task<MemberSessionDto?> GetActiveSessionAsync() => Task.FromResult(ActiveSessionResult);

    public Task<MemberSessionQuoteDto> GetSessionQuoteAsync() => Task.FromResult(QuoteResult ?? new MemberSessionQuoteDto(
        Guid.NewGuid(), 15000m, 600, 2500m, 0m, 2500m, 100000m, 97500m, PlaySessionStatus.Open, DateTime.UtcNow
    ));

    public Task<MemberSessionSummaryDto> CompleteSessionAsync()
    {
        CompleteSessionCallCount++;
        if (CompleteSessionFailuresRemaining > 0)
        {
            CompleteSessionFailuresRemaining--;
            throw new HttpRequestException("Mất kết nối tạm thời.");
        }

        return Task.FromResult(CompleteSessionResult ?? new MemberSessionSummaryDto(
            Guid.NewGuid(), "PS-001", MachineCode, DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, 1800, 7500m, 0m, 7500m, 100000m, 92500m, "HD-001"
        ));
    }

    public Task<IReadOnlyList<MemberProductDto>> GetCatalogAsync() => Task.FromResult<IReadOnlyList<MemberProductDto>>(new List<MemberProductDto>
    {
        new(Guid.NewGuid(), "Nước ngọt", "NN-01", "Nước", 15000m, "Lon", null, true, 20)
    });

    public Task<MemberOrderDto> CreateOrderAsync(List<MemberOrderItemRequest> items, PaymentMethod paymentMethod) => Task.FromResult(CreateOrderResult ?? new MemberOrderDto(
        Guid.NewGuid(), "ORD-001", OrderStatus.Confirmed, 15000m, DateTime.UtcNow, new List<MemberOrderItemDto>()
    ));

    public Task<IReadOnlyList<MemberOrderDto>> GetOrdersAsync() => Task.FromResult<IReadOnlyList<MemberOrderDto>>(new List<MemberOrderDto>());

    public Task<MemberTopUpRequestDto> CreateTopUpRequestAsync(decimal amount, string? note) => Task.FromResult(CreateTopUpResult ?? new MemberTopUpRequestDto(
        Guid.NewGuid(), amount, MemberTopUpRequestStatus.Pending, note, DateTime.UtcNow, null
    ));

    public Task<IReadOnlyList<MemberTopUpRequestDto>> GetTopUpRequestsAsync() => Task.FromResult<IReadOnlyList<MemberTopUpRequestDto>>(new List<MemberTopUpRequestDto>());
}

public class MemberClientViewModelTests
{
    [Fact]
    public void LoginViewModel_EmptyMemberCode_ShowsErrorMessage()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new LoginViewModel(mockApi)
        {
            MemberCode = "",
            Pin = "123456"
        };

        vm.LoginCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Contains("nhập mã hội viên", vm.ErrorMessage);
    }

    [Fact]
    public void LoginViewModel_EmptyPin_ShowsErrorMessage()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new LoginViewModel(mockApi)
        {
            MemberCode = "KH-001",
            Pin = ""
        };

        vm.LoginCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Contains("nhập mã PIN", vm.ErrorMessage);
    }

    [Fact]
    public void LoginViewModel_InvalidPinLength_ShowsErrorMessage()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new LoginViewModel(mockApi)
        {
            MemberCode = "KH-001",
            Pin = "1234" // Only 4 digits, must be exactly 6
        };

        vm.LoginCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Contains("đúng 6 chữ số", vm.ErrorMessage);
    }

    [Fact]
    public async Task LoginViewModel_LoadsAllMachines_ButOnlyAcceptsAvailableSelection()
    {
        var available = new MemberMachineSelectionDto(
            Guid.NewGuid(), "PC-01", "Máy 01", "Khu thường", Guid.NewGuid(), "Chi nhánh Test", MachineStatus.Available);
        var inUse = new MemberMachineSelectionDto(
            Guid.NewGuid(), "PC-02", "Máy 02", "Khu thường", available.BranchId, "Chi nhánh Test", MachineStatus.InUse);
        var mockApi = new MockMemberApiClient { MachinesResult = new[] { available, inUse } };
        var vm = new LoginViewModel(mockApi);

        await vm.InitializeAsync();

        Assert.Equal(2, vm.TotalMachineCount);
        Assert.Equal(1, vm.AvailableMachineCount);
        vm.SelectedMachine = inUse;
        Assert.Null(vm.SelectedMachine);
        vm.SelectedMachine = available;
        Assert.Equal(available.Id, vm.SelectedMachine?.Id);
        vm.StopMachinePolling();
    }

    [Fact]
    public void LoginViewModel_NoMachineSelected_AsksUserToSelectMachine()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new LoginViewModel(mockApi)
        {
            MemberCode = "KH-001",
            Pin = "123456"
        };

        vm.LoginCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Contains("chọn một máy", vm.ErrorMessage);
    }

    [Fact]
    public void LoginViewModel_ValidSession_TriggersLoginSucceeded()
    {
        var machine = new MemberMachineSelectionDto(
            Guid.NewGuid(), "PC-01", "Máy 01", "Khu thường", Guid.NewGuid(), "Chi nhánh Test", MachineStatus.Available);
        var mockApi = new MockMemberApiClient
        {
            ActiveSessionResult = new MemberSessionDto(
                Guid.NewGuid(), "PS-001", PlaySessionStatus.Open, BillingMode.PrepaidWallet,
                "PC-01", "Máy 01", DateTime.UtcNow, null, 0, 15000m, 0m, 0m, 0m, 100000m, 100000m
            )
        };

        var vm = new LoginViewModel(mockApi)
        {
            MemberCode = "KH-001",
            Pin = "123456",
            SelectedMachine = machine
        };

        MemberLoginResponse? loggedInResponse = null;
        vm.LoginSucceeded += res => loggedInResponse = res;

        vm.LoginCommand.Execute(null);

        Assert.NotNull(loggedInResponse);
        Assert.Equal("KH-001", loggedInResponse.Member.Code);
    }

    [Fact]
    public void ActiveSessionViewModel_CartOperations_CalculateSubtotalAndTotalAccurately()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new ActiveSessionViewModel(mockApi);

        var productA = new MemberProductDto(Guid.NewGuid(), "Trà ô long", "TRA-01", "Nước", 20000m, "Chai", null, true, 10);
        var productB = new MemberProductDto(Guid.NewGuid(), "Mì xào bò", "MI-01", "Món ăn", 35000m, "Dĩa", null, true, 5);

        vm.AddToCartCommand.Execute(productA);
        vm.AddToCartCommand.Execute(productA); // Quantity = 2
        vm.AddToCartCommand.Execute(productB); // Quantity = 1

        Assert.Equal(2, vm.Cart.Count);
        Assert.Equal(40000m, vm.Cart[0].Subtotal);
        Assert.Equal(35000m, vm.Cart[1].Subtotal);
        Assert.Equal(75000m, vm.CartTotal);
        Assert.Equal(3, vm.CartItemCount);
        Assert.True(vm.HasCartItems);

        // Decrease quantity
        vm.DecreaseQuantityCommand.Execute(vm.Cart[0]);
        Assert.Equal(1, vm.Cart[0].Quantity);
        Assert.Equal(20000m, vm.Cart[0].Subtotal);
        Assert.Equal(55000m, vm.CartTotal);
        Assert.Equal(2, vm.CartItemCount);

        // Remove item
        vm.RemoveFromCartCommand.Execute(vm.Cart[1]);
        Assert.Single(vm.Cart);
        Assert.Equal(20000m, vm.CartTotal);

        vm.ClearCartCommand.Execute(null);
        Assert.Empty(vm.Cart);
        Assert.Equal(0, vm.CartItemCount);
        Assert.False(vm.HasCartItems);
    }

    [Fact]
    public void ActiveSessionViewModel_OrderSummary_TracksOrdersAndOrderedQuantities()
    {
        var vm = new ActiveSessionViewModel(new MockMemberApiClient());
        var orderItems = new List<MemberOrderItemDto>
        {
            new(Guid.NewGuid(), "Nước Suối Aquafina 500ml", 1m, 8000m, 8000m, null),
            new(Guid.NewGuid(), "Sting Dâu Đỏ 330ml", 2m, 12000m, 24000m, "Ít đá")
        };
        var order = new MemberOrderDto(
            Guid.NewGuid(),
            "ORD-TEST-001",
            OrderStatus.Fulfilled,
            32000m,
            DateTime.UtcNow,
            orderItems,
            PaymentMethod.Cash,
            PaymentStatus.Completed,
            DateTime.UtcNow);

        vm.Orders = new ObservableCollection<MemberOrderDto> { order };

        Assert.True(vm.HasOrders);
        Assert.Equal(1, vm.OrderCount);
        Assert.Equal(3m, vm.OrderedItemCount);

        vm.Orders = new ObservableCollection<MemberOrderDto>();

        Assert.False(vm.HasOrders);
        Assert.Equal(0, vm.OrderCount);
        Assert.Equal(0m, vm.OrderedItemCount);
    }

    [Fact]
    public void ActiveSessionViewModel_OrderSubmission_GuardsAgainstInsufficientAvailableBalance()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new ActiveSessionViewModel(mockApi)
        {
            AvailableBalance = 10000m, // Only 10,000 VND available
            SelectedPaymentMethod = PaymentMethod.Wallet
        };

        var product = new MemberProductDto(Guid.NewGuid(), "Mì xào", "MI-01", "Món ăn", 35000m, "Dĩa", null, true, 5);
        vm.AddToCartCommand.Execute(product);

        vm.SubmitOrderCommand.Execute(null);

        Assert.True(vm.HasError);
        Assert.Contains("không đủ để thanh toán", vm.ErrorMessage);
    }

    [Fact]
    public void ActiveSessionViewModel_TopUpRequest_GuardsAgainstMinimumAmountAndPendingRequest()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new ActiveSessionViewModel(mockApi)
        {
            TopUpAmount = 5000m // Less than 10k
        };

        vm.SubmitTopUpCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Contains("tối thiểu là 10,000", vm.ErrorMessage);

        // Test already pending request
        vm.ClearMessages();
        vm.TopUpAmount = 50000m;
        vm.TopUpRequests.Add(new MemberTopUpRequestDto(Guid.NewGuid(), 50000m, MemberTopUpRequestStatus.Pending, null, DateTime.UtcNow, null));

        vm.SubmitTopUpCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Contains("đang chờ thu ngân xử lý", vm.ErrorMessage);
    }

    [Fact]
    public void ActiveSessionViewModel_LocalInterpolation_CalculatesAvailableBalanceWithServiceCharge()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new ActiveSessionViewModel(mockApi)
        {
            SessionStatus = PlaySessionStatus.Open,
            WalletBalance = 50000m,
            UsageCharge = 10000m,
            ServiceCharge = 15000m,
            AvailableBalance = 25000m,
            HourlyRate = 36000m // 10 VND per second
        };

        vm.TickForTesting();

        // 10 added to usage charge -> 10010
        Assert.Equal(10010m, vm.UsageCharge);
        // Server-provided available balance is interpolated by the new usage only.
        Assert.Equal(24990m, vm.AvailableBalance);
    }

    [Fact]
    public async Task ActiveSessionViewModel_AutoCompleteSession_GuardsAgainstDuplicateCalls()
    {
        var mockApi = new MockMemberApiClient();
        var vm = new ActiveSessionViewModel(mockApi);

        // Call duplicate times concurrently
        var task1 = vm.TriggerAutoCompleteSessionAsync();
        var task2 = vm.TriggerAutoCompleteSessionAsync();
        var task3 = vm.TriggerAutoCompleteSessionAsync();

        await Task.WhenAll(task1, task2, task3);

        // CompleteSessionAsync must have been invoked exactly once
        Assert.Equal(1, mockApi.CompleteSessionCallCount);
        Assert.True(vm.IsSessionSummaryOpen);
        Assert.Equal(PlaySessionStatus.Completed, vm.SessionStatus);
    }

    [Fact]
    public async Task ActiveSessionViewModel_AutoCompleteSession_AllowsRetryAfterTransientFailure()
    {
        var mockApi = new MockMemberApiClient { CompleteSessionFailuresRemaining = 1 };
        var vm = new ActiveSessionViewModel(mockApi);

        await vm.TriggerAutoCompleteSessionAsync();
        Assert.Equal(1, mockApi.CompleteSessionCallCount);
        Assert.True(vm.HasError);

        await vm.TriggerAutoCompleteSessionAsync();
        vm.StopTimers();

        Assert.Equal(2, mockApi.CompleteSessionCallCount);
        Assert.True(vm.IsSessionSummaryOpen);
        Assert.Equal(PlaySessionStatus.Completed, vm.SessionStatus);
    }

    [Fact]
    public void MemberClient_ClearsAuthSession_WhenActiveSessionMissing_AndOnSummaryClosed()
    {
        var machine = new MemberMachineSelectionDto(
            Guid.NewGuid(), "PC-01", "Máy 01", "Khu thường", Guid.NewGuid(), "Chi nhánh Test", MachineStatus.Available);
        var mockApi = new MockMemberApiClient
        {
            ActiveSessionResult = null
        };
        var loginVm = new LoginViewModel(mockApi)
        {
            MemberCode = "KH-001",
            Pin = "123456",
            SelectedMachine = machine
        };

        // When login succeeds but active session is null, auth session is wiped
        loginVm.LoginCommand.Execute(null);
        Assert.Null(mockApi.CurrentToken);
        Assert.Null(mockApi.CurrentUser);

        // When session summary is closed, auth session is wiped
        mockApi.SetAuthSession(new MemberLoginResponse("token_xyz",
            new MemberProfileDto(Guid.NewGuid(), "KH-001", "Member", null, null, 10000m),
            new MemberMachineDto(Guid.NewGuid(), "PC-01", "PC 01", Guid.NewGuid(), "Branch")));
        Assert.NotNull(mockApi.CurrentToken);

        var activeVm = new ActiveSessionViewModel(mockApi);
        activeVm.CloseSessionSummaryCommand.Execute(null);
        Assert.Null(mockApi.CurrentToken);
        Assert.Null(mockApi.CurrentUser);
    }

    [Fact]
    public void MemberClientConfig_Validate_FailsFastOnMissingOrInvalidValues()
    {
        // MachineCode is selected in the UI and is no longer required in configuration.
        var machineCodeOptional = new MemberClientConfig
        {
            ApiBaseUrl = "http://localhost:5227/api/v1/",
            MachineCode = ""
        };
        CyberGame.MemberClient.App.ValidateConfig(machineCodeOptional);

        // Invalid ApiBaseUrl
        var invalidConfig2 = new MemberClientConfig
        {
            ApiBaseUrl = "not-a-valid-url",
            MachineCode = "PC-01"
        };
        var ex2 = Assert.Throws<InvalidOperationException>(() => CyberGame.MemberClient.App.ValidateConfig(invalidConfig2));
        Assert.Contains("ApiBaseUrl", ex2.Message);

        // Valid configuration passes without exception
        var validConfig = new MemberClientConfig
        {
            ApiBaseUrl = "http://localhost:5227/api/v1/",
            MachineCode = "PC-01"
        };
        CyberGame.MemberClient.App.ValidateConfig(validConfig);
    }

    [Fact]
    public void ClientRegistrations_ReuseOneAuthenticatedApiInstancePerApplication()
    {
        var managementServices = new ServiceCollection();
        CyberGame.Client.App.ConfigureServices(managementServices);
        using var managementProvider = managementServices.BuildServiceProvider();

        var managementApi1 = managementProvider.GetRequiredService<CyberGame.Client.Services.IApiClient>();
        var managementApi2 = managementProvider.GetRequiredService<CyberGame.Client.Services.IApiClient>();
        managementApi1.SetAuthToken("management_token");

        Assert.Same(managementApi1, managementApi2);
        Assert.Equal("management_token", managementApi2.AuthToken);

        var memberServices = new ServiceCollection();
        var config = new MemberClientConfig
        {
            ApiBaseUrl = "http://localhost:5227/api/v1/",
            MachineCode = "PC-01"
        };
        CyberGame.MemberClient.App.ConfigureServices(memberServices, config);
        using var memberProvider = memberServices.BuildServiceProvider();

        var memberApi1 = memberProvider.GetRequiredService<IMemberApiClient>();
        var memberApi2 = memberProvider.GetRequiredService<IMemberApiClient>();
        memberApi1.SetAuthSession(new MemberLoginResponse(
            "member_token",
            new MemberProfileDto(Guid.NewGuid(), "KH-001", "Test", null, null, 100000m),
            new MemberMachineDto(Guid.NewGuid(), "PC-01", "PC 01", Guid.NewGuid(), "Branch")));

        Assert.Same(memberApi1, memberApi2);
        Assert.Equal("member_token", memberApi2.CurrentToken);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;
        private readonly HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }

        public TestHttpMessageHandler(string responseJson = "{}", HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _responseJson = responseJson;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task MemberApiClient_AttachesBearerTokenAndIdempotencyKeyOnTransactionalPost()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5227/api/v1/")
        };
        var config = new MemberClientConfig { MachineCode = "PC-01" };
        var apiClient = new MemberApiClient(httpClient, config);

        apiClient.SetAuthSession(new MemberLoginResponse(
            "test_jwt_bearer_token",
            new MemberProfileDto(Guid.NewGuid(), "KH-001", "Test", null, null, 100000m),
            new MemberMachineDto(Guid.NewGuid(), "PC-01", "PC 01", Guid.NewGuid(), "Branch")
        ));

        // When creating order, should attach Authorization and Idempotency-Key
        try
        {
            await apiClient.CreateOrderAsync(new List<MemberOrderItemRequest>(), PaymentMethod.Cash);
        }
        catch
        {
            // Json parsing of empty object {} is expected to fail or return default
        }

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test_jwt_bearer_token", handler.LastRequest.Headers.Authorization?.Parameter);
        Assert.True(handler.LastRequest.Headers.Contains("Idempotency-Key"));
        var key = handler.LastRequest.Headers.GetValues("Idempotency-Key").FirstOrDefault();
        Assert.True(Guid.TryParse(key, out _));
    }

    [Fact]
    public async Task MemberApiClient_DeserializesStringEnumsCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var json = $$"""
        {
            "id": "{{sessionId}}",
            "sessionNumber": "PS-001",
            "status": "Open",
            "billingMode": "PrepaidWallet",
            "machineCode": "PC-01",
            "machineDisplayName": "May 01",
            "startedAt": "2026-09-18T00:00:00Z",
            "endedAt": null,
            "playedSeconds": 1800,
            "hourlyRate": 15000,
            "usageCharge": 7500,
            "serviceCharge": 0,
            "totalCharge": 7500,
            "walletBalance": 100000,
            "availableBalance": 92500
        }
        """;

        var handler = new TestHttpMessageHandler(json);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5227/api/v1/")
        };
        var config = new MemberClientConfig { MachineCode = "PC-01" };
        var apiClient = new MemberApiClient(httpClient, config);

        var session = await apiClient.GetActiveSessionAsync();

        Assert.NotNull(session);
        Assert.Equal(PlaySessionStatus.Open, session.Status);
        Assert.Equal(BillingMode.PrepaidWallet, session.BillingMode);
    }

    [Fact]
    public async Task MemberApiClient_UnauthorizedResponseExplainsHowToRecover()
    {
        var handler = new TestHttpMessageHandler(string.Empty, HttpStatusCode.Unauthorized);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5227/api/v1/")
        };
        var apiClient = new MemberApiClient(httpClient, new MemberClientConfig { MachineCode = "PC-01" });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => apiClient.GetProfileAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("đăng nhập lại", exception.Message);
    }
}
