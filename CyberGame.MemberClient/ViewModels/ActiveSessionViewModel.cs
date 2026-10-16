using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Threading;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Enums;
using CyberGame.MemberClient.Common;
using CyberGame.MemberClient.Services;

namespace CyberGame.MemberClient.ViewModels;

public class CartItemViewModel : ViewModelBase
{
    public MemberProductDto Product { get; }
    private int _quantity;
    private string _note = string.Empty;

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(Subtotal));
            }
        }
    }

    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public decimal Subtotal => Product.SalePrice * Quantity;

    public CartItemViewModel(MemberProductDto product, int quantity = 1)
    {
        Product = product;
        _quantity = quantity;
    }
}

public class ActiveSessionViewModel : ViewModelBase
{
    private readonly IMemberApiClient _apiClient;
    private readonly DispatcherTimer _secondTimer;
    private CancellationTokenSource? _pollingCts;

    // Header info
    private string _customerName = string.Empty;
    private string _customerCode = string.Empty;
    private string _machineCode = string.Empty;
    private bool _isConnected = true;
    private string _connectionStatusText = "Đã kết nối";

    // 5 Key metrics
    private decimal _walletBalance;
    private long _elapsedSeconds;
    private decimal _usageCharge;
    private decimal _serviceCharge;
    private decimal _availableBalance;
    private decimal _hourlyRate;
    private PlaySessionStatus _sessionStatus = PlaySessionStatus.Open;

    // Warning
    private bool _isLowBalance;
    private string? _lowBalanceWarning;

    // Navigation
    private int _selectedTabIndex;

    // End Session Dialog & Summary
    private bool _isEndSessionConfirmOpen;
    private bool _isSessionSummaryOpen;
    private MemberSessionSummaryDto? _sessionSummary;
    private int _autoCompleteTriggered;

    // Catalog & Order
    private ObservableCollection<MemberProductDto> _catalog = new();
    private ObservableCollection<CartItemViewModel> _cart = new();
    private ObservableCollection<MemberOrderDto> _orders = new();
    private bool _isSubmittingOrder;
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    // Top-Up
    private decimal _topUpAmount = 50000m;
    private string _topUpNote = string.Empty;
    private bool _isSubmittingTopUp;
    private ObservableCollection<MemberTopUpRequestDto> _topUpRequests = new();

    // Change PIN
    private string _currentPin = string.Empty;
    private string _newPin = string.Empty;
    private string _confirmNewPin = string.Empty;
    private bool _isChangingPin;

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value);
    }

    public string CustomerCode
    {
        get => _customerCode;
        set => SetProperty(ref _customerCode, value);
    }

    public string MachineCode
    {
        get => _machineCode;
        set => SetProperty(ref _machineCode, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        set => SetProperty(ref _connectionStatusText, value);
    }

    public decimal WalletBalance
    {
        get => _walletBalance;
        set => SetProperty(ref _walletBalance, value);
    }

    public long ElapsedSeconds
    {
        get => _elapsedSeconds;
        set => SetProperty(ref _elapsedSeconds, value);
    }

    public decimal UsageCharge
    {
        get => _usageCharge;
        set => SetProperty(ref _usageCharge, value);
    }

    public decimal ServiceCharge
    {
        get => _serviceCharge;
        set => SetProperty(ref _serviceCharge, value);
    }

    public decimal AvailableBalance
    {
        get => _availableBalance;
        set => SetProperty(ref _availableBalance, value);
    }

    public decimal HourlyRate
    {
        get => _hourlyRate;
        set => SetProperty(ref _hourlyRate, value);
    }

    public PlaySessionStatus SessionStatus
    {
        get => _sessionStatus;
        set => SetProperty(ref _sessionStatus, value);
    }

    public bool IsLowBalance
    {
        get => _isLowBalance;
        set => SetProperty(ref _isLowBalance, value);
    }

    public string? LowBalanceWarning
    {
        get => _lowBalanceWarning;
        set => SetProperty(ref _lowBalanceWarning, value);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public bool IsEndSessionConfirmOpen
    {
        get => _isEndSessionConfirmOpen;
        set => SetProperty(ref _isEndSessionConfirmOpen, value);
    }

    public bool IsSessionSummaryOpen
    {
        get => _isSessionSummaryOpen;
        set => SetProperty(ref _isSessionSummaryOpen, value);
    }

    public MemberSessionSummaryDto? SessionSummary
    {
        get => _sessionSummary;
        set => SetProperty(ref _sessionSummary, value);
    }

    public ObservableCollection<MemberProductDto> Catalog
    {
        get => _catalog;
        set => SetProperty(ref _catalog, value);
    }

    public ObservableCollection<CartItemViewModel> Cart
    {
        get => _cart;
        set => SetProperty(ref _cart, value);
    }

    public ObservableCollection<MemberOrderDto> Orders
    {
        get => _orders;
        set
        {
            if (SetProperty(ref _orders, value))
            {
                OnPropertyChanged(nameof(HasOrders));
                OnPropertyChanged(nameof(OrderCount));
                OnPropertyChanged(nameof(OrderedItemCount));
            }
        }
    }

    public bool HasOrders => Orders.Count > 0;

    public int OrderCount => Orders.Count;

    public decimal OrderedItemCount => Orders.Sum(order => order.Items.Sum(item => item.Quantity));

    public decimal CartTotal => Cart.Sum(c => c.Subtotal);

    public int CartItemCount => Cart.Sum(c => c.Quantity);

    public bool HasCartItems => Cart.Count > 0;

    public bool IsSubmittingOrder
    {
        get => _isSubmittingOrder;
        set => SetProperty(ref _isSubmittingOrder, value);
    }

    public PaymentMethod SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                OnPropertyChanged(nameof(IsCashPayment));
                OnPropertyChanged(nameof(IsWalletPayment));
                OnPropertyChanged(nameof(PaymentHelpText));
                OnPropertyChanged(nameof(SubmitOrderButtonText));
            }
        }
    }

    public bool IsCashPayment
    {
        get => SelectedPaymentMethod == PaymentMethod.Cash;
        set
        {
            if (value) SelectedPaymentMethod = PaymentMethod.Cash;
        }
    }

    public bool IsWalletPayment
    {
        get => SelectedPaymentMethod == PaymentMethod.Wallet;
        set
        {
            if (value) SelectedPaymentMethod = PaymentMethod.Wallet;
        }
    }

    public string PaymentHelpText => SelectedPaymentMethod == PaymentMethod.Wallet
        ? "Trừ ví ngay; đơn tự động chuyển sang chuẩn bị."
        : "Trả tại quầy; nhân viên duyệt sau khi nhận tiền.";

    public string SubmitOrderButtonText => SelectedPaymentMethod == PaymentMethod.Wallet
        ? "THANH TOÁN VÍ & ĐẶT MÓN"
        : "GỬI ĐƠN THANH TOÁN TIỀN MẶT";

    public decimal TopUpAmount
    {
        get => _topUpAmount;
        set => SetProperty(ref _topUpAmount, value);
    }

    public string TopUpNote
    {
        get => _topUpNote;
        set => SetProperty(ref _topUpNote, value);
    }

    public bool IsSubmittingTopUp
    {
        get => _isSubmittingTopUp;
        set => SetProperty(ref _isSubmittingTopUp, value);
    }

    public ObservableCollection<MemberTopUpRequestDto> TopUpRequests
    {
        get => _topUpRequests;
        set => SetProperty(ref _topUpRequests, value);
    }

    public bool HasPendingTopUpRequest => TopUpRequests.Any(r => r.Status == MemberTopUpRequestStatus.Pending);

    public string CurrentPin { get => _currentPin; set => SetProperty(ref _currentPin, value); }
    public string NewPin { get => _newPin; set => SetProperty(ref _newPin, value); }
    public string ConfirmNewPin { get => _confirmNewPin; set => SetProperty(ref _confirmNewPin, value); }
    public bool IsChangingPin { get => _isChangingPin; set => SetProperty(ref _isChangingPin, value); }

    // Commands
    public ICommand AddToCartCommand { get; }
    public ICommand RemoveFromCartCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand ClearCartCommand { get; }
    public ICommand SubmitOrderCommand { get; }

    public ICommand SelectPresetTopUpCommand { get; }
    public ICommand SubmitTopUpCommand { get; }
    public ICommand ChangePinCommand { get; }

    public ICommand OpenEndSessionConfirmCommand { get; }
    public ICommand CancelEndSessionConfirmCommand { get; }
    public ICommand ConfirmEndSessionCommand { get; }
    public ICommand CloseSessionSummaryCommand { get; }

    public event Action? SessionEnded;

    public ActiveSessionViewModel(IMemberApiClient apiClient)
    {
        _apiClient = apiClient;
        MachineCode = _apiClient.MachineCode;

        // Local 1-second timer for interpolation
        _secondTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _secondTimer.Tick += OnSecondTimerTick;

        // Cart commands
        AddToCartCommand = new RelayCommand(param =>
        {
            if (param is MemberProductDto prod)
            {
                var existing = Cart.FirstOrDefault(c => c.Product.Id == prod.Id);
                if (existing != null)
                {
                    existing.Quantity++;
                }
                else
                {
                    Cart.Add(new CartItemViewModel(prod, 1));
                }
                NotifyCartChanged();
            }
        });

        RemoveFromCartCommand = new RelayCommand(param =>
        {
            if (param is CartItemViewModel item)
            {
                Cart.Remove(item);
                NotifyCartChanged();
            }
        });

        IncreaseQuantityCommand = new RelayCommand(param =>
        {
            if (param is CartItemViewModel item)
            {
                item.Quantity++;
                NotifyCartChanged();
            }
        });

        DecreaseQuantityCommand = new RelayCommand(param =>
        {
            if (param is CartItemViewModel item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    Cart.Remove(item);
                }
                NotifyCartChanged();
            }
        });

        ClearCartCommand = new RelayCommand(() =>
        {
            Cart.Clear();
            NotifyCartChanged();
        });

        SubmitOrderCommand = new AsyncRelayCommand(ExecuteSubmitOrderAsync);

        // Top-up commands
        SelectPresetTopUpCommand = new RelayCommand(param =>
        {
            if (param is string s && decimal.TryParse(s, out var amt))
            {
                TopUpAmount = amt;
            }
            else if (param is decimal d)
            {
                TopUpAmount = d;
            }
        });

        SubmitTopUpCommand = new AsyncRelayCommand(ExecuteSubmitTopUpAsync);
        ChangePinCommand = new AsyncRelayCommand(async () => await ChangePinAsync());

        // End session commands
        OpenEndSessionConfirmCommand = new RelayCommand(() => IsEndSessionConfirmOpen = true);
        CancelEndSessionConfirmCommand = new RelayCommand(() => IsEndSessionConfirmOpen = false);
        ConfirmEndSessionCommand = new AsyncRelayCommand(ExecuteEndSessionAsync);
        CloseSessionSummaryCommand = new RelayCommand(() =>
        {
            IsSessionSummaryOpen = false;
            _apiClient.SetAuthSession(null);
            SessionEnded?.Invoke();
        });
    }

    private void NotifyCartChanged()
    {
        OnPropertyChanged(nameof(CartTotal));
        OnPropertyChanged(nameof(CartItemCount));
        OnPropertyChanged(nameof(HasCartItems));
    }

    public async Task StartSessionAsync(MemberLoginResponse loginInfo)
    {
        _autoCompleteTriggered = 0;
        CustomerName = loginInfo.Member.FullName;
        CustomerCode = loginInfo.Member.Code;
        WalletBalance = loginInfo.Member.WalletBalance;
        MachineCode = loginInfo.Machine.Code;
        SelectedPaymentMethod = PaymentMethod.Cash;
        CurrentPin = string.Empty;
        NewPin = string.Empty;
        ConfirmNewPin = string.Empty;

        ClearMessages();
        _secondTimer.Start();

        // Start background polling
        _pollingCts?.Cancel();
        _pollingCts = new CancellationTokenSource();
        _ = RunPollingLoopAsync(_pollingCts.Token);

        // Initial fetch
        await RefreshQuoteAsync();
        await RefreshCatalogAsync();
        await RefreshOrdersAsync();
        await RefreshTopUpRequestsAsync();
    }

    private void OnSecondTimerTick(object? sender, EventArgs e)
    {
        if (SessionStatus == PlaySessionStatus.Open)
        {
            ElapsedSeconds++;

            // Local interpolation of available balance
            if (HourlyRate > 0)
            {
                var addedCharge = HourlyRate / 3600m;
                UsageCharge += addedCharge;
                AvailableBalance = Math.Max(0, AvailableBalance - addedCharge);
            }
        }
    }

    public void TickForTesting() => OnSecondTimerTick(this, EventArgs.Empty);

    private async Task RunPollingLoopAsync(CancellationToken ct)
    {
        int count = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(2000, ct);
                if (ct.IsCancellationRequested) break;

                await RefreshQuoteAsync();

                count++;
                if (count % 3 == 0) // Every ~6s
                {
                    await RefreshCatalogAsync();
                    await RefreshOrdersAsync();
                    await RefreshTopUpRequestsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                IsConnected = false;
                ConnectionStatusText = "Mất kết nối máy chủ";
            }
        }
    }

    private async Task RefreshQuoteAsync()
    {
        try
        {
            var quote = await _apiClient.GetSessionQuoteAsync();
            ElapsedSeconds = quote.TotalBillableSeconds;
            HourlyRate = quote.HourlyRate;
            UsageCharge = quote.UsageAmount;
            ServiceCharge = quote.ServiceAmount;
            WalletBalance = quote.WalletBalance;
            AvailableBalance = quote.AvailableBalance;
            SessionStatus = quote.Status;

            // Low balance check
            if (AvailableBalance < 10000m || (HourlyRate > 0 && AvailableBalance / HourlyRate * 3600m < 900))
            {
                IsLowBalance = true;
                LowBalanceWarning = "CẢNH BÁO: Số dư khả dụng sắp hết! Vui lòng nạp thêm tiền tại quầy để duy trì phiên chơi.";
            }
            else
            {
                IsLowBalance = false;
                LowBalanceWarning = null;
            }

            IsConnected = true;
            ConnectionStatusText = "Đã kết nối";

            // When available balance reaches zero from an authoritative quote, automatically complete session once
            if (AvailableBalance <= 0 && SessionStatus == PlaySessionStatus.Open)
            {
                await TriggerAutoCompleteSessionAsync();
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            IsConnected = false;
            ConnectionStatusText = "Phiên đăng nhập hết hạn";
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            IsConnected = false;
            ConnectionStatusText = "Mất kết nối máy chủ";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatusText = "Không thể đồng bộ dữ liệu";
            ErrorMessage = ex.Message;
        }
    }

    public async Task TriggerAutoCompleteSessionAsync()
    {
        if (Interlocked.CompareExchange(ref _autoCompleteTriggered, 1, 0) != 0)
        {
            return;
        }

        ClearMessages();
        try
        {
            StopTimers();
            IsBusy = true;
            var summary = await _apiClient.CompleteSessionAsync();
            SessionSummary = summary;
            IsEndSessionConfirmOpen = false;
            IsSessionSummaryOpen = true;
            SessionStatus = PlaySessionStatus.Completed;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Tự động kết thúc phiên thất bại: {ex.Message}";
            // A transient network failure must not permanently disable the
            // authoritative zero-balance safeguard. Resume polling and allow
            // the next server quote to retry completion.
            _autoCompleteTriggered = 0;
            _secondTimer.Start();
            _pollingCts?.Cancel();
            _pollingCts = new CancellationTokenSource();
            _ = RunPollingLoopAsync(_pollingCts.Token);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshCatalogAsync()
    {
        try
        {
            var items = await _apiClient.GetCatalogAsync();
            Catalog = new ObservableCollection<MemberProductDto>(items);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải thực đơn thật từ máy chủ: {ex.Message}";
        }
    }

    private async Task RefreshOrdersAsync()
    {
        try
        {
            var orders = await _apiClient.GetOrdersAsync();
            Orders = new ObservableCollection<MemberOrderDto>(orders);
        }
        catch
        {
            // Ignore orders fetch transient error
        }
    }

    private async Task RefreshTopUpRequestsAsync()
    {
        try
        {
            var requests = await _apiClient.GetTopUpRequestsAsync();
            TopUpRequests = new ObservableCollection<MemberTopUpRequestDto>(requests);
            OnPropertyChanged(nameof(HasPendingTopUpRequest));
        }
        catch
        {
            // Ignore top-up requests fetch transient error
        }
    }

    private async Task ExecuteSubmitOrderAsync()
    {
        ClearMessages();

        if (!Cart.Any())
        {
            ErrorMessage = "Giỏ hàng đang trống. Vui lòng chọn món trước khi đặt.";
            return;
        }

        if (SelectedPaymentMethod == PaymentMethod.Wallet && AvailableBalance < CartTotal)
        {
            ErrorMessage = $"Số dư khả dụng ({AvailableBalance:N0} đ) không đủ để thanh toán đơn ({CartTotal:N0} đ).";
            return;
        }

        try
        {
            IsSubmittingOrder = true;
            var items = Cart.Select(c => new MemberOrderItemRequest(c.Product.Id, c.Quantity, c.Note)).ToList();
            var order = await _apiClient.CreateOrderAsync(items, SelectedPaymentMethod);

            Cart.Clear();
            NotifyCartChanged();
            SuccessMessage = SelectedPaymentMethod == PaymentMethod.Wallet
                ? $"Đã thanh toán đơn {order.OrderNumber} bằng ví. Đơn được tự động duyệt và đang chuẩn bị."
                : $"Đã gửi đơn {order.OrderNumber}. Vui lòng thanh toán tiền mặt tại quầy; nhân viên sẽ duyệt sau khi nhận tiền.";

            await RefreshQuoteAsync();
            await RefreshOrdersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Đặt món thất bại: {ex.Message}";
        }
        finally
        {
            IsSubmittingOrder = false;
        }
    }

    private async Task ExecuteSubmitTopUpAsync()
    {
        ClearMessages();

        if (TopUpAmount < 10000m)
        {
            ErrorMessage = "Số tiền nạp tối thiểu là 10,000 đ.";
            return;
        }

        if (HasPendingTopUpRequest)
        {
            ErrorMessage = "Bạn đã có một yêu cầu nạp tiền đang chờ thu ngân xử lý.";
            return;
        }

        try
        {
            IsSubmittingTopUp = true;
            await _apiClient.CreateTopUpRequestAsync(TopUpAmount, string.IsNullOrWhiteSpace(TopUpNote) ? null : TopUpNote.Trim());
            SuccessMessage = "Đã gửi yêu cầu nạp tiền! Vui lòng đến quầy thu ngân thanh toán tiền mặt để được cộng tiền.";
            TopUpNote = string.Empty;

            await RefreshTopUpRequestsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Gửi yêu cầu nạp tiền thất bại: {ex.Message}";
        }
        finally
        {
            IsSubmittingTopUp = false;
        }
    }

    public async Task<bool> ChangePinAsync()
    {
        ClearMessages();
        if (CurrentPin.Length != 6 || !CurrentPin.All(char.IsDigit) ||
            NewPin.Length != 6 || !NewPin.All(char.IsDigit))
        {
            ErrorMessage = "PIN hiện tại và PIN mới phải gồm đúng 6 chữ số.";
            return false;
        }

        if (NewPin != ConfirmNewPin)
        {
            ErrorMessage = "Xác nhận PIN mới không khớp.";
            return false;
        }

        if (CurrentPin == NewPin)
        {
            ErrorMessage = "PIN mới phải khác PIN hiện tại.";
            return false;
        }

        try
        {
            IsChangingPin = true;
            await _apiClient.ChangePinAsync(CurrentPin, NewPin);
            CurrentPin = string.Empty;
            NewPin = string.Empty;
            ConfirmNewPin = string.Empty;
            SuccessMessage = "Đổi mã PIN thành công. PIN mới sẽ được dùng cho lần đăng nhập tiếp theo.";
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Đổi PIN thất bại: {ex.Message}";
            return false;
        }
        finally
        {
            IsChangingPin = false;
        }
    }

    private async Task ExecuteEndSessionAsync()
    {
        if (Interlocked.CompareExchange(ref _autoCompleteTriggered, 1, 0) != 0)
        {
            return;
        }

        ClearMessages();
        try
        {
            IsBusy = true;
            StopTimers();

            var summary = await _apiClient.CompleteSessionAsync();
            SessionSummary = summary;
            IsEndSessionConfirmOpen = false;
            IsSessionSummaryOpen = true;
            SessionStatus = PlaySessionStatus.Completed;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kết thúc phiên thất bại: {ex.Message}";
            _autoCompleteTriggered = 0;
            _secondTimer.Start();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void StopTimers()
    {
        _secondTimer.Stop();
        _pollingCts?.Cancel();
        _pollingCts = null;
    }
}
