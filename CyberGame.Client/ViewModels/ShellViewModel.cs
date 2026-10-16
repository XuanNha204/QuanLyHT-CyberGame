using System.Windows.Input;
using System.Windows.Threading;
using CyberGame.Client.Common;
using CyberGame.Client.Services;

namespace CyberGame.Client.ViewModels;

public class ShellViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;
    private readonly Action _onLogout;
    private readonly DispatcherTimer _clockTimer;

    private ViewModelBase _currentViewModel;
    private string _currentTitle = "Tổng quan";
    private string _currentTimeString = DateTime.Now.ToString("HH:mm:ss - dd/MM/yyyy");

    public DashboardViewModel DashboardVM { get; }
    public StationViewModel StationVM { get; }
    public CustomerViewModel CustomerVM { get; }
    public PosViewModel PosVM { get; }
    public InventoryViewModel InventoryVM { get; }
    public ShiftViewModel ShiftVM { get; }
    public InvoiceViewModel InvoiceVM { get; }
    public MaintenanceViewModel MaintenanceVM { get; }
    public ReportViewModel ReportVM { get; }

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        set => SetProperty(ref _currentTitle, value);
    }

    public string CurrentTimeString
    {
        get => _currentTimeString;
        set => SetProperty(ref _currentTimeString, value);
    }

    public string BranchName => _stateService.BranchName ?? "CyberGame";
    public string UserFullName => _stateService.CurrentUser?.FullName ?? "Nhân viên";
    public string Username => _stateService.CurrentUser?.Username ?? "";
    public string UserRole => _stateService.CurrentUser?.Roles.FirstOrDefault() switch
    {
        "Owner" => "Chủ phòng máy",
        "Manager" => "Quản lý",
        "Cashier" => "Thu ngân",
        "Technician" => "Kỹ thuật viên",
        _ => _stateService.CurrentUser?.Roles.FirstOrDefault() ?? "Người dùng"
    };

    public bool HasActiveShift => _stateService.HasActiveShift;
    public string ShiftBadgeText => HasActiveShift
        ? $"Đang trong ca #{_stateService.CurrentShift?.ShiftNumber}"
        : "Chưa mở ca";

    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigateStationsCommand { get; }
    public ICommand NavigateCustomersCommand { get; }
    public ICommand NavigatePosCommand { get; }
    public ICommand NavigateInventoryCommand { get; }
    public ICommand NavigateShiftsCommand { get; }
    public ICommand NavigateInvoicesCommand { get; }
    public ICommand NavigateMaintenanceCommand { get; }
    public ICommand NavigateReportsCommand { get; }
    public ICommand LogoutCommand { get; }

    public ShellViewModel(
        IApiClient apiClient,
        IStateService stateService,
        Action onLogout)
    {
        _apiClient = apiClient;
        _stateService = stateService;
        _onLogout = onLogout;

        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) =>
        {
            CurrentTimeString = DateTime.Now.ToString("HH:mm:ss - dd/MM/yyyy");
        };
        _clockTimer.Start();

        DashboardVM = new DashboardViewModel(apiClient);
        StationVM = new StationViewModel(apiClient, stateService);
        CustomerVM = new CustomerViewModel(apiClient, stateService);
        PosVM = new PosViewModel(apiClient, stateService);
        InventoryVM = new InventoryViewModel(apiClient, stateService);
        ShiftVM = new ShiftViewModel(apiClient, stateService);
        InvoiceVM = new InvoiceViewModel(apiClient);
        MaintenanceVM = new MaintenanceViewModel(apiClient, stateService);
        ReportVM = new ReportViewModel(apiClient);

        _currentViewModel = DashboardVM;

        NavigateDashboardCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Tổng quan hệ thống";
            CurrentViewModel = DashboardVM;
            await DashboardVM.InitializeAsync();
        });

        NavigateStationsCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Sơ đồ & Trạng thái phòng máy";
            CurrentViewModel = StationVM;
            await StationVM.InitializeAsync();
        });

        NavigateCustomersCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Quản lý hội viên & Số dư ví";
            CurrentViewModel = CustomerVM;
            await CustomerVM.InitializeAsync();
        });

        NavigatePosCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Bán hàng & Phục vụ dịch vụ (POS)";
            CurrentViewModel = PosVM;
            await PosVM.InitializeAsync();
        });

        NavigateInventoryCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Quản lý kho hàng & Tồn kho";
            CurrentViewModel = InventoryVM;
            await InventoryVM.InitializeAsync();
        });

        NavigateShiftsCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Quản lý ca làm việc & Thu chi";
            CurrentViewModel = ShiftVM;
            await ShiftVM.InitializeAsync();
        });

        NavigateInvoicesCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Tra cứu hóa đơn & Lịch sử thanh toán";
            CurrentViewModel = InvoiceVM;
            await InvoiceVM.InitializeAsync();
        });

        NavigateMaintenanceCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Quản lý bảo trì & Sự cố thiết bị";
            CurrentViewModel = MaintenanceVM;
            await MaintenanceVM.InitializeAsync();
        });

        NavigateReportsCommand = new RelayCommand(async () =>
        {
            CurrentTitle = "Báo cáo doanh thu & Thống kê";
            CurrentViewModel = ReportVM;
            await ReportVM.InitializeAsync();
        });

        LogoutCommand = new RelayCommand(() =>
        {
            _clockTimer.Stop();
            _apiClient.SetAuthToken(null);
            _stateService.Clear();
            _onLogout();
        });

        _stateService.StateChanged += () =>
        {
            OnPropertyChanged(nameof(BranchName));
            OnPropertyChanged(nameof(UserFullName));
            OnPropertyChanged(nameof(Username));
            OnPropertyChanged(nameof(UserRole));
            OnPropertyChanged(nameof(HasActiveShift));
            OnPropertyChanged(nameof(ShiftBadgeText));
        };

    }

    public override async Task InitializeAsync()
    {
        await DashboardVM.InitializeAsync();
    }
}
