using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Enums;
using CyberGame.MemberClient.Common;
using CyberGame.MemberClient.Services;

namespace CyberGame.MemberClient.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IMemberApiClient _apiClient;
    private readonly DispatcherTimer _machinePollingTimer;
    private string _memberCode = string.Empty;
    private string _pin = string.Empty;
    private ObservableCollection<MemberMachineSelectionDto> _machines = new();
    private MemberMachineSelectionDto? _selectedMachine;
    private bool _isLoadingMachines;

    public ObservableCollection<MemberMachineSelectionDto> Machines
    {
        get => _machines;
        set
        {
            if (SetProperty(ref _machines, value))
            {
                OnPropertyChanged(nameof(TotalMachineCount));
                OnPropertyChanged(nameof(AvailableMachineCount));
                OnPropertyChanged(nameof(HasMachines));
            }
        }
    }

    public MemberMachineSelectionDto? SelectedMachine
    {
        get => _selectedMachine;
        set
        {
            if (value?.Status != MachineStatus.Available)
            {
                value = null;
            }

            if (SetProperty(ref _selectedMachine, value))
            {
                OnPropertyChanged(nameof(HasSelectedMachine));
                OnPropertyChanged(nameof(SelectedMachineText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public int TotalMachineCount => Machines.Count;
    public int AvailableMachineCount => Machines.Count(m => m.Status == MachineStatus.Available);
    public bool HasMachines => Machines.Count > 0;
    public bool HasSelectedMachine => SelectedMachine != null;
    public string SelectedMachineText => SelectedMachine == null
        ? "Hãy chọn một máy đang trống"
        : $"Đã chọn {SelectedMachine.Code} — {SelectedMachine.DisplayName}";

    public string MemberCode
    {
        get => _memberCode;
        set => SetProperty(ref _memberCode, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand RefreshMachinesCommand { get; }

    public event Action<MemberLoginResponse>? LoginSucceeded;

    public LoginViewModel(IMemberApiClient apiClient)
    {
        _apiClient = apiClient;
        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync);
        RefreshMachinesCommand = new AsyncRelayCommand(() => LoadMachinesAsync(showBusy: true));

        _machinePollingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _machinePollingTimer.Tick += async (_, _) => await LoadMachinesAsync(showBusy: false);
    }

    public override async Task InitializeAsync()
    {
        await LoadMachinesAsync(showBusy: true);
        _machinePollingTimer.Start();
    }

    private async Task LoadMachinesAsync(bool showBusy)
    {
        if (_isLoadingMachines) return;
        _isLoadingMachines = true;
        var selectedId = SelectedMachine?.Id;

        try
        {
            if (showBusy) IsBusy = true;
            var machines = await _apiClient.GetMachinesAsync();
            Machines = new ObservableCollection<MemberMachineSelectionDto>(machines);
            SelectedMachine = selectedId.HasValue
                ? Machines.FirstOrDefault(m => m.Id == selectedId.Value && m.Status == MachineStatus.Available)
                : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải trạng thái phòng máy: {ex.Message}";
        }
        finally
        {
            if (showBusy) IsBusy = false;
            _isLoadingMachines = false;
        }
    }

    private async Task ExecuteLoginAsync()
    {
        ClearMessages();

        if (string.IsNullOrWhiteSpace(MemberCode))
        {
            ErrorMessage = "Vui lòng nhập mã hội viên.";
            return;
        }

        var trimmedPin = Pin?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedPin))
        {
            ErrorMessage = "Vui lòng nhập mã PIN.";
            return;
        }

        if (trimmedPin.Length != 6 || !trimmedPin.All(char.IsDigit))
        {
            ErrorMessage = "Mã PIN phải gồm đúng 6 chữ số.";
            return;
        }

        if (SelectedMachine == null)
        {
            ErrorMessage = "Vui lòng chọn một máy đang trống trước khi đăng nhập.";
            return;
        }

        try
        {
            IsBusy = true;
            var response = await _apiClient.LoginAsync(
                MemberCode.Trim(),
                trimmedPin,
                SelectedMachine.Id,
                SelectedMachine.Code);

            var session = await _apiClient.GetActiveSessionAsync();
            if (session == null)
            {
                _apiClient.SetAuthSession(null);
                ErrorMessage = $"Không thể khởi tạo phiên chơi cho máy {SelectedMachine.Code}. Vui lòng thử lại.";
                await LoadMachinesAsync(showBusy: false);
                return;
            }

            Pin = string.Empty;
            _machinePollingTimer.Stop();
            LoginSucceeded?.Invoke(response);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await LoadMachinesAsync(showBusy: false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Reset()
    {
        ClearMessages();
        MemberCode = string.Empty;
        Pin = string.Empty;
        SelectedMachine = null;
        _apiClient.SetAuthSession(null);
    }

    public void StopMachinePolling() => _machinePollingTimer.Stop();
}
