using System.Collections.ObjectModel;
using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.ViewModels;

public class MaintenanceViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;

    private ObservableCollection<MaintenanceTicketDto> _tickets = new();
    private MaintenanceTicketDto? _selectedTicket;
    private ObservableCollection<MachineDto> _machines = new();

    // Create Ticket Dialog state
    private bool _isCreateTicketDialogOpen;
    private MachineDto? _createMachine;
    private string _createTitle = string.Empty;
    private string _createDescription = string.Empty;
    private MaintenancePriority _createPriority = MaintenancePriority.Medium;

    // Update Ticket Dialog state
    private bool _isUpdateTicketDialogOpen;
    private MaintenanceStatus _updateStatus = MaintenanceStatus.InProgress;
    private string _updateLogNote = string.Empty;
    private decimal? _updateCost;

    public ObservableCollection<MaintenanceTicketDto> Tickets
    {
        get => _tickets;
        set => SetProperty(ref _tickets, value);
    }

    public MaintenanceTicketDto? SelectedTicket
    {
        get => _selectedTicket;
        set => SetProperty(ref _selectedTicket, value);
    }

    public ObservableCollection<MachineDto> Machines
    {
        get => _machines;
        set => SetProperty(ref _machines, value);
    }

    public bool IsCreateTicketDialogOpen
    {
        get => _isCreateTicketDialogOpen;
        set => SetProperty(ref _isCreateTicketDialogOpen, value);
    }

    public MachineDto? CreateMachine
    {
        get => _createMachine;
        set => SetProperty(ref _createMachine, value);
    }

    public string CreateTitle
    {
        get => _createTitle;
        set => SetProperty(ref _createTitle, value);
    }

    public string CreateDescription
    {
        get => _createDescription;
        set => SetProperty(ref _createDescription, value);
    }

    public MaintenancePriority CreatePriority
    {
        get => _createPriority;
        set => SetProperty(ref _createPriority, value);
    }

    public bool IsUpdateTicketDialogOpen
    {
        get => _isUpdateTicketDialogOpen;
        set => SetProperty(ref _isUpdateTicketDialogOpen, value);
    }

    public MaintenanceStatus UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    public string UpdateLogNote
    {
        get => _updateLogNote;
        set => SetProperty(ref _updateLogNote, value);
    }

    public decimal? UpdateCost
    {
        get => _updateCost;
        set => SetProperty(ref _updateCost, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenCreateTicketCommand { get; }
    public ICommand ConfirmCreateTicketCommand { get; }
    public ICommand CancelCreateTicketCommand { get; }

    public ICommand OpenUpdateTicketCommand { get; }
    public ICommand ConfirmUpdateTicketCommand { get; }
    public ICommand CancelUpdateTicketCommand { get; }

    public MaintenanceViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;

        RefreshCommand = new AsyncRelayCommand(LoadMaintenanceDataAsync);

        OpenCreateTicketCommand = new RelayCommand(() =>
        {
            ClearMessages();
            CreateMachine = Machines.FirstOrDefault();
            CreateTitle = "Báo sự cố máy trạm";
            CreateDescription = string.Empty;
            CreatePriority = MaintenancePriority.Medium;
            IsCreateTicketDialogOpen = true;
        });

        ConfirmCreateTicketCommand = new AsyncRelayCommand(ExecuteCreateTicketAsync);
        CancelCreateTicketCommand = new RelayCommand(() => IsCreateTicketDialogOpen = false);

        OpenUpdateTicketCommand = new RelayCommand(() =>
        {
            if (SelectedTicket == null)
            {
                ErrorMessage = "Vui lòng chọn phiếu sự cố cần cập nhật.";
                return;
            }
            ClearMessages();
            UpdateStatus = SelectedTicket.Status == MaintenanceStatus.Open ? MaintenanceStatus.InProgress : MaintenanceStatus.Resolved;
            UpdateLogNote = "Đã kiểm tra và xử lý";
            UpdateCost = 0m;
            IsUpdateTicketDialogOpen = true;
        });

        ConfirmUpdateTicketCommand = new AsyncRelayCommand(ExecuteUpdateTicketAsync);
        CancelUpdateTicketCommand = new RelayCommand(() => IsUpdateTicketDialogOpen = false);
    }

    public override async Task InitializeAsync()
    {
        await LoadMaintenanceDataAsync();
    }

    private async Task LoadMaintenanceDataAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            var tickets = await _apiClient.GetMaintenanceTicketsAsync();
            var machines = await _apiClient.GetMachinesAsync();

            Tickets = new ObservableCollection<MaintenanceTicketDto>(tickets);
            Machines = new ObservableCollection<MachineDto>(machines);

            if (SelectedTicket != null)
            {
                SelectedTicket = Tickets.FirstOrDefault(t => t.Id == SelectedTicket.Id) ?? Tickets.FirstOrDefault();
            }
            else
            {
                SelectedTicket = Tickets.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu bảo trì: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteCreateTicketAsync()
    {
        if (CreateMachine == null) return;
        ClearMessages();

        if (string.IsNullOrWhiteSpace(CreateTitle))
        {
            ErrorMessage = "Vui lòng nhập tiêu đề sự cố.";
            return;
        }

        try
        {
            IsBusy = true;
            var ticket = await _apiClient.CreateMaintenanceTicketAsync(new CreateMaintenanceTicketRequest(
                CreateMachine.Id,
                CreatePriority,
                CreateTitle.Trim(),
                CreateDescription.Trim()
            ));

            IsCreateTicketDialogOpen = false;
            SuccessMessage = $"Tạo phiếu bảo trì #{ticket.TicketNumber} cho máy {CreateMachine.Code} thành công!";
            await LoadMaintenanceDataAsync();
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

    private async Task ExecuteUpdateTicketAsync()
    {
        if (SelectedTicket == null) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            await _apiClient.AddMaintenanceLogAsync(SelectedTicket.Id, new AddMaintenanceLogRequest(
                UpdateStatus.ToString(),
                UpdateLogNote.Trim(),
                UpdateCost ?? 0m
            ));

            IsUpdateTicketDialogOpen = false;
            SuccessMessage = $"Cập nhật phiếu #{SelectedTicket.TicketNumber} thành công!";
            await LoadMaintenanceDataAsync();
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
