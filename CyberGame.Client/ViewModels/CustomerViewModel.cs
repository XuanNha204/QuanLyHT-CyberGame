using System.Collections.ObjectModel;
using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.ViewModels;

public class CustomerViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;

    private string _searchQuery = string.Empty;
    private ObservableCollection<CustomerDto> _customers = new();
    private CustomerDto? _selectedCustomer;
    private ObservableCollection<WalletTransactionDto> _walletTransactions = new();

    // Create Customer Dialog state
    private bool _isCreateCustomerDialogOpen;
    private string _newCode = string.Empty;
    private string _newFullName = string.Empty;
    private string _newPhone = string.Empty;
    private string _newInitialPin = string.Empty;
    private decimal _initialDeposit;

    // Top-up Dialog state
    private bool _isTopUpDialogOpen;
    private decimal _topUpAmount = 50000m;
    private PaymentMethod _topUpMethod = PaymentMethod.Cash;
    private string _topUpNote = "Nạp tiền tại quầy";

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public ObservableCollection<CustomerDto> Customers
    {
        get => _customers;
        set => SetProperty(ref _customers, value);
    }

    public CustomerDto? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                OnPropertyChanged(nameof(SelectedCustomerStatusActionText));
                _ = LoadTransactionsForSelectedAsync();
            }
        }
    }

    public ObservableCollection<WalletTransactionDto> WalletTransactions
    {
        get => _walletTransactions;
        set => SetProperty(ref _walletTransactions, value);
    }

    public bool IsCreateCustomerDialogOpen
    {
        get => _isCreateCustomerDialogOpen;
        set => SetProperty(ref _isCreateCustomerDialogOpen, value);
    }

    public string NewCode
    {
        get => _newCode;
        set => SetProperty(ref _newCode, value);
    }

    public string NewFullName
    {
        get => _newFullName;
        set => SetProperty(ref _newFullName, value);
    }

    public string NewPhone
    {
        get => _newPhone;
        set => SetProperty(ref _newPhone, value);
    }

    public decimal InitialDeposit
    {
        get => _initialDeposit;
        set => SetProperty(ref _initialDeposit, value);
    }

    public string NewInitialPin
    {
        get => _newInitialPin;
        set => SetProperty(ref _newInitialPin, value);
    }

    public bool IsTopUpDialogOpen
    {
        get => _isTopUpDialogOpen;
        set => SetProperty(ref _isTopUpDialogOpen, value);
    }

    public decimal TopUpAmount
    {
        get => _topUpAmount;
        set => SetProperty(ref _topUpAmount, value);
    }

    public PaymentMethod TopUpMethod
    {
        get => _topUpMethod;
        set => SetProperty(ref _topUpMethod, value);
    }

    public string TopUpNote
    {
        get => _topUpNote;
        set => SetProperty(ref _topUpNote, value);
    }

    // Set PIN Dialog state
    private bool _isSetPinDialogOpen;
    private string _pinInput = string.Empty;

    public bool IsSetPinDialogOpen
    {
        get => _isSetPinDialogOpen;
        set => SetProperty(ref _isSetPinDialogOpen, value);
    }

    public string PinInput
    {
        get => _pinInput;
        set => SetProperty(ref _pinInput, value);
    }

    // Pending Top-Up Requests state
    private ObservableCollection<StaffMemberTopUpRequestDto> _pendingTopUpRequests = new();
    private StaffMemberTopUpRequestDto? _selectedTopUpRequest;

    public ObservableCollection<StaffMemberTopUpRequestDto> PendingTopUpRequests
    {
        get => _pendingTopUpRequests;
        set => SetProperty(ref _pendingTopUpRequests, value);
    }

    public StaffMemberTopUpRequestDto? SelectedTopUpRequest
    {
        get => _selectedTopUpRequest;
        set => SetProperty(ref _selectedTopUpRequest, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand OpenCreateCustomerCommand { get; }
    public ICommand ConfirmCreateCustomerCommand { get; }
    public ICommand CancelCreateCustomerCommand { get; }

    public ICommand OpenTopUpCommand { get; }
    public ICommand ConfirmTopUpCommand { get; }
    public ICommand CancelTopUpCommand { get; }

    public ICommand OpenSetPinCommand { get; }
    public ICommand ConfirmSetPinCommand { get; }
    public ICommand CancelSetPinCommand { get; }

    public ICommand RefreshTopUpRequestsCommand { get; }
    public ICommand ApproveTopUpRequestCommand { get; }
    public ICommand RejectTopUpRequestCommand { get; }
    public ICommand ToggleCustomerStatusCommand { get; }

    public string SelectedCustomerStatusActionText => SelectedCustomer?.Status == CustomerStatus.Active
        ? "NGỪNG HOẠT ĐỘNG"
        : "KÍCH HOẠT LẠI";

    public CustomerViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;

        SearchCommand = new AsyncRelayCommand(LoadCustomersAsync);

        OpenCreateCustomerCommand = new RelayCommand(() =>
        {
            ClearMessages();
            NewCode = $"KH{DateTime.Now:fff}";
            NewFullName = string.Empty;
            NewPhone = string.Empty;
            NewInitialPin = string.Empty;
            InitialDeposit = 0m;
            IsCreateCustomerDialogOpen = true;
        });

        ConfirmCreateCustomerCommand = new AsyncRelayCommand(ExecuteCreateCustomerAsync);
        CancelCreateCustomerCommand = new RelayCommand(() => IsCreateCustomerDialogOpen = false);

        OpenTopUpCommand = new RelayCommand(() =>
        {
            if (SelectedCustomer == null)
            {
                ErrorMessage = "Vui lòng chọn một hội viên để nạp tiền.";
                return;
            }
            ClearMessages();
            TopUpAmount = 50000m;
            TopUpMethod = PaymentMethod.Cash;
            TopUpNote = "Nạp tiền ví hội viên tại quầy";
            IsTopUpDialogOpen = true;
        });

        ConfirmTopUpCommand = new AsyncRelayCommand(ExecuteTopUpAsync);
        CancelTopUpCommand = new RelayCommand(() => IsTopUpDialogOpen = false);

        OpenSetPinCommand = new RelayCommand(() =>
        {
            if (SelectedCustomer == null)
            {
                ErrorMessage = "Vui lòng chọn một hội viên để thiết lập mã PIN.";
                return;
            }
            ClearMessages();
            PinInput = string.Empty;
            IsSetPinDialogOpen = true;
        });

        ConfirmSetPinCommand = new AsyncRelayCommand(ExecuteSetPinAsync);
        CancelSetPinCommand = new RelayCommand(() => IsSetPinDialogOpen = false);

        RefreshTopUpRequestsCommand = new AsyncRelayCommand(LoadPendingTopUpRequestsAsync);
        ApproveTopUpRequestCommand = new AsyncRelayCommand(async param => await ExecuteApproveTopUpRequestAsync(param));
        RejectTopUpRequestCommand = new AsyncRelayCommand(async param => await ExecuteRejectTopUpRequestAsync(param));
        ToggleCustomerStatusCommand = new AsyncRelayCommand(ExecuteToggleCustomerStatusAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadCustomersAsync();
        await LoadPendingTopUpRequestsAsync();
    }

    private async Task LoadCustomersAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            var result = await _apiClient.GetCustomersAsync(SearchQuery);
            Customers = new ObservableCollection<CustomerDto>(result.Items);

            if (SelectedCustomer != null)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == SelectedCustomer.Id) ?? Customers.FirstOrDefault();
            }
            else
            {
                SelectedCustomer = Customers.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải danh sách hội viên: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadTransactionsForSelectedAsync()
    {
        WalletTransactions.Clear();
        if (SelectedCustomer == null) return;

        try
        {
            var result = await _apiClient.GetWalletTransactionsAsync(SelectedCustomer.Id);
            WalletTransactions = new ObservableCollection<WalletTransactionDto>(result.Items);
        }
        catch
        {
            // Bỏ qua lỗi ngầm
        }
    }

    private async Task ExecuteCreateCustomerAsync()
    {
        ClearMessages();
        if (string.IsNullOrWhiteSpace(NewCode) || string.IsNullOrWhiteSpace(NewFullName))
        {
            ErrorMessage = "Vui lòng nhập Mã hội viên và Họ tên.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewInitialPin) || NewInitialPin.Trim().Length != 6 || !NewInitialPin.Trim().All(char.IsDigit))
        {
            ErrorMessage = "Mã PIN ban đầu phải gồm đúng 6 chữ số.";
            return;
        }

        try
        {
            IsBusy = true;
            var created = await _apiClient.CreateCustomerAsync(new CreateCustomerRequest(
                null,
                NewCode.Trim(),
                NewFullName.Trim(),
                string.IsNullOrWhiteSpace(NewPhone) ? null : NewPhone.Trim(),
                null,
                null,
                NewInitialPin.Trim(),
                InitialDeposit,
                "Tạo mới qua ứng dụng thu ngân"
            ));

            IsCreateCustomerDialogOpen = false;
            await LoadCustomersAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == created.Id);
            SuccessMessage = $"Đã tạo mới hội viên {created.FullName} ({created.Code}); có thể đăng nhập máy khách ngay bằng PIN vừa đặt.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteToggleCustomerStatusAsync()
    {
        if (SelectedCustomer == null) return;

        ClearMessages();
        var customer = SelectedCustomer;
        var newStatus = customer.Status == CustomerStatus.Active ? CustomerStatus.Inactive : CustomerStatus.Active;
        try
        {
            IsBusy = true;
            var updated = await _apiClient.UpdateCustomerAsync(customer.Id, new UpdateCustomerRequest(
                customer.CustomerGroupId,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.DateOfBirth,
                newStatus,
                customer.Notes));

            await LoadCustomersAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == updated.Id);
            SuccessMessage = newStatus == CustomerStatus.Active
                ? $"Đã kích hoạt lại hội viên {updated.FullName}."
                : $"Đã ngừng hoạt động hội viên {updated.FullName}; lịch sử ví và phiên chơi vẫn được giữ nguyên.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteTopUpAsync()
    {
        if (SelectedCustomer == null) return;
        ClearMessages();

        if (TopUpAmount <= 0)
        {
            ErrorMessage = "Số tiền nạp phải lớn hơn 0.";
            return;
        }

        try
        {
            IsBusy = true;
            var updated = await _apiClient.TopUpWalletAsync(SelectedCustomer.Id, new TopUpWalletRequest(
                TopUpAmount,
                TopUpMethod,
                TopUpNote
            ));

            IsTopUpDialogOpen = false;
            await LoadCustomersAsync();
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == updated.Id);
            await LoadTransactionsForSelectedAsync();
            SuccessMessage = $"Nạp thành công {TopUpAmount:N0} đ cho hội viên {updated.FullName}! Số dư mới: {updated.WalletBalance:N0} đ";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadPendingTopUpRequestsAsync()
    {
        try
        {
            var requests = await _apiClient.GetMemberTopUpRequestsAsync("Pending");
            PendingTopUpRequests = new ObservableCollection<StaffMemberTopUpRequestDto>(requests);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải yêu cầu nạp tiền: {ex.Message}";
        }
    }

    private async Task ExecuteSetPinAsync()
    {
        if (SelectedCustomer == null) return;
        ClearMessages();

        if (string.IsNullOrWhiteSpace(PinInput) || PinInput.Trim().Length != 6 || !PinInput.Trim().All(char.IsDigit))
        {
            ErrorMessage = "Mã PIN phải gồm đúng 6 chữ số.";
            return;
        }

        try
        {
            IsBusy = true;
            await _apiClient.SetCustomerMemberPinAsync(SelectedCustomer.Id, PinInput.Trim());
            IsSetPinDialogOpen = false;
            SuccessMessage = $"Đã cài đặt mã PIN máy khách cho hội viên {SelectedCustomer.FullName} ({SelectedCustomer.Code}) thành công!";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteApproveTopUpRequestAsync(object? parameter)
    {
        Guid requestId = Guid.Empty;
        if (parameter is Guid id) requestId = id;
        else if (parameter is StaffMemberTopUpRequestDto dto) requestId = dto.Id;
        else if (SelectedTopUpRequest != null) requestId = SelectedTopUpRequest.Id;

        if (requestId == Guid.Empty) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            var result = await _apiClient.ApproveMemberTopUpRequestAsync(requestId);
            await LoadPendingTopUpRequestsAsync();
            await LoadCustomersAsync();
            if (SelectedCustomer != null)
            {
                await LoadTransactionsForSelectedAsync();
            }
            SuccessMessage = $"Đã duyệt nạp {result.Amount:N0} đ cho hội viên {result.CustomerName} ({result.CustomerCode})!";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteRejectTopUpRequestAsync(object? parameter)
    {
        Guid requestId = Guid.Empty;
        if (parameter is Guid id) requestId = id;
        else if (parameter is StaffMemberTopUpRequestDto dto) requestId = dto.Id;
        else if (SelectedTopUpRequest != null) requestId = SelectedTopUpRequest.Id;

        if (requestId == Guid.Empty) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            var result = await _apiClient.RejectMemberTopUpRequestAsync(requestId);
            SuccessMessage = $"Đã từ chối yêu cầu nạp {result.Amount:N0} đ của hội viên {result.CustomerName} ({result.CustomerCode}).";
            await LoadPendingTopUpRequestsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
