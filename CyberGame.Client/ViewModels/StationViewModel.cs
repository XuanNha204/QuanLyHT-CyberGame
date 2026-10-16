using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.ViewModels;

public class StationViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;
    private readonly DispatcherTimer _stationPollingTimer;
    private bool _isLoadingStations;

    private ObservableCollection<MachineDto> _allMachines = new();
    private ObservableCollection<MachineDto> _filteredMachines = new();
    private ObservableCollection<MachineGroupDto> _groups = new();
    private MachineGroupDto? _selectedGroupFilter;
    private string _selectedStatusFilter = "Tất cả";
    private MachineDto? _selectedMachine;
    private PlaySessionDto? _activeSessionForSelected;

    // Transfer Session Dialog state
    private bool _isTransferDialogOpen;
    private MachineDto? _transferTargetMachine;

    // Complete Session Dialog state
    private bool _isCompleteSessionDialogOpen;
    private InvoiceDto? _completedInvoice;

    public ObservableCollection<MachineDto> FilteredMachines
    {
        get => _filteredMachines;
        set => SetProperty(ref _filteredMachines, value);
    }

    public ObservableCollection<MachineGroupDto> Groups
    {
        get => _groups;
        set => SetProperty(ref _groups, value);
    }

    public MachineGroupDto? SelectedGroupFilter
    {
        get => _selectedGroupFilter;
        set
        {
            if (SetProperty(ref _selectedGroupFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (SetProperty(ref _selectedStatusFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public MachineDto? SelectedMachine
    {
        get => _selectedMachine;
        set
        {
            if (SetProperty(ref _selectedMachine, value))
            {
                _ = LoadActiveSessionForSelectedAsync();
            }
        }
    }

    public PlaySessionDto? ActiveSessionForSelected
    {
        get => _activeSessionForSelected;
        set
        {
            if (SetProperty(ref _activeSessionForSelected, value))
            {
                OnPropertyChanged(nameof(HasActiveSessionForSelected));
            }
        }
    }

    public bool HasActiveSessionForSelected => ActiveSessionForSelected != null;

    public bool IsTransferDialogOpen
    {
        get => _isTransferDialogOpen;
        set => SetProperty(ref _isTransferDialogOpen, value);
    }

    public MachineDto? TransferTargetMachine
    {
        get => _transferTargetMachine;
        set => SetProperty(ref _transferTargetMachine, value);
    }

    public bool IsCompleteSessionDialogOpen
    {
        get => _isCompleteSessionDialogOpen;
        set => SetProperty(ref _isCompleteSessionDialogOpen, value);
    }

    public InvoiceDto? CompletedInvoice
    {
        get => _completedInvoice;
        set => SetProperty(ref _completedInvoice, value);
    }

    public List<MachineDto> AvailableMachinesForTransfer =>
        _allMachines.Where(m => m.Status == MachineStatus.Available && m.Id != SelectedMachine?.Id).ToList();

    public ICommand RefreshCommand { get; }
    public ICommand PauseSessionCommand { get; }
    public ICommand ResumeSessionCommand { get; }

    public ICommand OpenTransferCommand { get; }
    public ICommand ConfirmTransferCommand { get; }
    public ICommand CancelTransferCommand { get; }

    public ICommand OpenCompleteSessionCommand { get; }
    public ICommand ConfirmCompleteSessionCommand { get; }
    public ICommand CloseCompleteSessionDialogCommand { get; }

    public StationViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;
        _stationPollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _stationPollingTimer.Tick += async (_, _) => await LoadStationsAsync(clearMessages: false, showBusy: false);

        RefreshCommand = new AsyncRelayCommand(LoadStationsAsync);

        PauseSessionCommand = new AsyncRelayCommand(ExecutePauseSessionAsync);
        ResumeSessionCommand = new AsyncRelayCommand(ExecuteResumeSessionAsync);

        OpenTransferCommand = new RelayCommand(() =>
        {
            if (ActiveSessionForSelected == null)
            {
                ErrorMessage = "Máy hiện không có phiên chơi để chuyển.";
                return;
            }
            ClearMessages();
            OnPropertyChanged(nameof(AvailableMachinesForTransfer));
            TransferTargetMachine = AvailableMachinesForTransfer.FirstOrDefault();
            IsTransferDialogOpen = true;
        });

        ConfirmTransferCommand = new AsyncRelayCommand(ExecuteTransferSessionAsync);
        CancelTransferCommand = new RelayCommand(() => IsTransferDialogOpen = false);

        OpenCompleteSessionCommand = new RelayCommand(() =>
        {
            if (ActiveSessionForSelected == null)
            {
                ErrorMessage = "Máy hiện không có phiên chơi đang mở.";
                return;
            }
            ClearMessages();
            IsCompleteSessionDialogOpen = true;
        });

        ConfirmCompleteSessionCommand = new AsyncRelayCommand(ExecuteCompleteSessionAsync);
        CloseCompleteSessionDialogCommand = new RelayCommand(() =>
        {
            IsCompleteSessionDialogOpen = false;
            CompletedInvoice = null;
        });
    }

    public override async Task InitializeAsync()
    {
        await LoadStationsAsync();
        _stationPollingTimer.Start();
    }

    private Task LoadStationsAsync() => LoadStationsAsync(clearMessages: true, showBusy: true);

    private async Task LoadStationsAsync(bool clearMessages, bool showBusy)
    {
        if (_isLoadingStations) return;
        _isLoadingStations = true;
        if (clearMessages) ClearMessages();
        try
        {
            if (showBusy) IsBusy = true;
            var machines = await _apiClient.GetMachinesAsync();
            var groups = await _apiClient.GetMachineGroupsAsync();

            _allMachines = new ObservableCollection<MachineDto>(machines);
            Groups = new ObservableCollection<MachineGroupDto>(groups);

            ApplyFilter();

            if (SelectedMachine != null)
            {
                SelectedMachine = _allMachines.FirstOrDefault(m => m.Id == SelectedMachine.Id);
            }
            else
            {
                SelectedMachine = FilteredMachines.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải danh sách máy: {ex.Message}";
        }
        finally
        {
            if (showBusy) IsBusy = false;
            _isLoadingStations = false;
        }
    }

    private void ApplyFilter()
    {
        var query = _allMachines.AsEnumerable();

        if (SelectedGroupFilter != null)
        {
            query = query.Where(m => m.MachineGroupId == SelectedGroupFilter.Id);
        }

        if (SelectedStatusFilter != "Tất cả")
        {
            query = SelectedStatusFilter switch
            {
                "Sẵn sàng" => query.Where(m => m.Status == MachineStatus.Available),
                "Đang sử dụng" => query.Where(m => m.Status == MachineStatus.InUse),
                "Bảo trì" => query.Where(m => m.Status == MachineStatus.Maintenance),
                "Ngoại tuyến" => query.Where(m => m.Status == MachineStatus.Offline),
                _ => query
            };
        }

        FilteredMachines = new ObservableCollection<MachineDto>(query.ToList());
    }

    private async Task LoadActiveSessionForSelectedAsync()
    {
        ActiveSessionForSelected = null;
        if (SelectedMachine == null || SelectedMachine.Status != MachineStatus.InUse) return;

        try
        {
            var activeSessions = await _apiClient.GetActiveSessionsAsync();
            ActiveSessionForSelected = activeSessions.FirstOrDefault(s => s.MachineId == SelectedMachine.Id);
        }
        catch
        {
            // Bỏ qua lỗi ngầm
        }
    }

    private async Task ExecutePauseSessionAsync()
    {
        if (ActiveSessionForSelected == null) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            await _apiClient.PauseSessionAsync(ActiveSessionForSelected.Id);
            SuccessMessage = "Đã tạm dừng phiên chơi.";
            await LoadStationsAsync();
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

    private async Task ExecuteResumeSessionAsync()
    {
        if (ActiveSessionForSelected == null) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            await _apiClient.ResumeSessionAsync(ActiveSessionForSelected.Id);
            SuccessMessage = "Đã tiếp tục phiên chơi.";
            await LoadStationsAsync();
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

    private async Task ExecuteTransferSessionAsync()
    {
        if (ActiveSessionForSelected == null || TransferTargetMachine == null) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            await _apiClient.TransferSessionAsync(ActiveSessionForSelected.Id, new TransferSessionRequest(TransferTargetMachine.Id));
            IsTransferDialogOpen = false;
            SuccessMessage = $"Đã chuyển phiên từ máy {SelectedMachine?.Code} sang máy {TransferTargetMachine.Code}.";
            await LoadStationsAsync();
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

    private async Task ExecuteCompleteSessionAsync()
    {
        if (ActiveSessionForSelected == null) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            var invoice = await _apiClient.CompleteSessionAsync(ActiveSessionForSelected.Id, new CompleteSessionRequest(true));
            CompletedInvoice = invoice;
            SuccessMessage = $"Đã kết thúc phiên chơi! Hóa đơn: {invoice.InvoiceNumber} - {invoice.TotalAmount:N0} đ";
            await LoadStationsAsync();
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
