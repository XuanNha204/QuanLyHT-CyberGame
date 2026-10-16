using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private DashboardSummaryDto? _summary;
    private IReadOnlyList<MachineDto> _machines = new List<MachineDto>();

    public DashboardSummaryDto? Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public IReadOnlyList<MachineDto> Machines
    {
        get => _machines;
        set => SetProperty(ref _machines, value);
    }

    public int AvailableCount => Summary?.AvailableMachines ?? 0;
    public int InUseCount => Summary?.InUseMachines ?? 0;
    public int MaintenanceCount => Summary?.MaintenanceMachines ?? 0;
    public int OfflineCount => Summary?.OfflineMachines ?? 0;

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            Summary = await _apiClient.GetDashboardSummaryAsync();
            Machines = Summary.Machines;

            OnPropertyChanged(nameof(AvailableCount));
            OnPropertyChanged(nameof(InUseCount));
            OnPropertyChanged(nameof(MaintenanceCount));
            OnPropertyChanged(nameof(OfflineCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu tổng quan: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
