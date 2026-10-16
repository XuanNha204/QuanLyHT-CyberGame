using System.Collections.ObjectModel;
using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;

namespace CyberGame.Client.ViewModels;

public class ReportViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private string _selectedPreset = "7 ngày qua";
    private DateTime _fromDate = DateTime.Today.AddDays(-6);
    private DateTime _toDate = DateTime.Today;

    private ObservableCollection<RevenueReportDto> _dailyRevenues = new();
    private ObservableCollection<TopProductReportDto> _topProducts = new();

    private decimal _totalRevenue;
    private decimal _totalMachineRevenue;
    private decimal _totalServiceRevenue;

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                switch (value)
                {
                    case "Hôm nay":
                        FromDate = DateTime.Today;
                        ToDate = DateTime.Today;
                        break;
                    case "7 ngày qua":
                        FromDate = DateTime.Today.AddDays(-6);
                        ToDate = DateTime.Today;
                        break;
                    case "30 ngày qua":
                        FromDate = DateTime.Today.AddDays(-29);
                        ToDate = DateTime.Today;
                        break;
                }
                _ = LoadReportDataAsync();
            }
        }
    }

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public ObservableCollection<RevenueReportDto> DailyRevenues
    {
        get => _dailyRevenues;
        set => SetProperty(ref _dailyRevenues, value);
    }

    public ObservableCollection<TopProductReportDto> TopProducts
    {
        get => _topProducts;
        set => SetProperty(ref _topProducts, value);
    }

    public decimal TotalRevenue
    {
        get => _totalRevenue;
        set => SetProperty(ref _totalRevenue, value);
    }

    public decimal TotalMachineRevenue
    {
        get => _totalMachineRevenue;
        set => SetProperty(ref _totalMachineRevenue, value);
    }

    public decimal TotalServiceRevenue
    {
        get => _totalServiceRevenue;
        set => SetProperty(ref _totalServiceRevenue, value);
    }

    public ICommand RefreshCommand { get; }

    public ReportViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadReportDataAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadReportDataAsync();
    }

    private async Task LoadReportDataAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            var revs = await _apiClient.GetRevenueReportAsync(FromDate, ToDate.AddDays(1));
            var products = await _apiClient.GetTopProductsReportAsync(15);

            DailyRevenues = new ObservableCollection<RevenueReportDto>(revs);
            TopProducts = new ObservableCollection<TopProductReportDto>(products);

            TotalMachineRevenue = revs.Sum(r => r.MachineRevenue);
            TotalServiceRevenue = revs.Sum(r => r.ServiceRevenue);
            TotalRevenue = revs.Sum(r => r.TotalRevenue);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu báo cáo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
