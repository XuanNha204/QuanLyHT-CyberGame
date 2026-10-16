using System.Collections.ObjectModel;
using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;

namespace CyberGame.Client.ViewModels;

public class InvoiceViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;

    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private ObservableCollection<InvoiceDto> _invoices = new();
    private InvoiceDto? _selectedInvoice;

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

    public ObservableCollection<InvoiceDto> Invoices
    {
        get => _invoices;
        set => SetProperty(ref _invoices, value);
    }

    public InvoiceDto? SelectedInvoice
    {
        get => _selectedInvoice;
        set => SetProperty(ref _selectedInvoice, value);
    }

    public ICommand RefreshCommand { get; }

    public InvoiceViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshCommand = new AsyncRelayCommand(LoadInvoicesAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadInvoicesAsync();
    }

    private async Task LoadInvoicesAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            var result = await _apiClient.GetInvoicesAsync(FromDate, ToDate);
            Invoices = new ObservableCollection<InvoiceDto>(result.Items);

            if (SelectedInvoice != null)
            {
                SelectedInvoice = Invoices.FirstOrDefault(i => i.Id == SelectedInvoice.Id) ?? Invoices.FirstOrDefault();
            }
            else
            {
                SelectedInvoice = Invoices.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải danh sách hóa đơn: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
