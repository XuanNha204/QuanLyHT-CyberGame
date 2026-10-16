using System.Collections.ObjectModel;
using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;

namespace CyberGame.Client.ViewModels;

public class InventoryViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;

    private ObservableCollection<InventoryItemDto> _allItems = new();
    private ObservableCollection<InventoryItemDto> _filteredItems = new();
    private InventoryItemDto? _selectedItem;
    private bool _filterLowStockOnly;

    // Create Product Dialog state
    private bool _isCreateProductDialogOpen;
    private ObservableCollection<ProductCategoryDto> _categories = new();
    private ProductCategoryDto? _selectedNewCategory;
    private string _newSku = string.Empty;
    private string _newProductName = string.Empty;
    private string _newUnit = "chai";
    private decimal _newSalePrice;
    private decimal _newCostPrice;
    private bool _newTrackInventory = true;
    private decimal _newReorderLevel = 5;
    private decimal _newInitialStock;

    // Receive Stock Dialog state
    private bool _isReceiveStockDialogOpen;
    private decimal _receiveQuantity = 10;
    private decimal _receiveUnitCost;
    private string _receiveNote = "Nhập thêm hàng";

    // Adjust Stock Dialog state
    private bool _isAdjustStockDialogOpen;
    private decimal _adjustActualQuantity;
    private string _adjustReason = "Kiểm kê định kỳ";

    public ObservableCollection<InventoryItemDto> FilteredItems
    {
        get => _filteredItems;
        set => SetProperty(ref _filteredItems, value);
    }

    public InventoryItemDto? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public bool FilterLowStockOnly
    {
        get => _filterLowStockOnly;
        set
        {
            if (SetProperty(ref _filterLowStockOnly, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsCreateProductDialogOpen { get => _isCreateProductDialogOpen; set => SetProperty(ref _isCreateProductDialogOpen, value); }
    public ObservableCollection<ProductCategoryDto> Categories { get => _categories; set => SetProperty(ref _categories, value); }
    public ProductCategoryDto? SelectedNewCategory { get => _selectedNewCategory; set => SetProperty(ref _selectedNewCategory, value); }
    public string NewSku { get => _newSku; set => SetProperty(ref _newSku, value); }
    public string NewProductName { get => _newProductName; set => SetProperty(ref _newProductName, value); }
    public string NewUnit { get => _newUnit; set => SetProperty(ref _newUnit, value); }
    public decimal NewSalePrice { get => _newSalePrice; set => SetProperty(ref _newSalePrice, value); }
    public decimal NewCostPrice { get => _newCostPrice; set => SetProperty(ref _newCostPrice, value); }
    public bool NewTrackInventory { get => _newTrackInventory; set => SetProperty(ref _newTrackInventory, value); }
    public decimal NewReorderLevel { get => _newReorderLevel; set => SetProperty(ref _newReorderLevel, value); }
    public decimal NewInitialStock { get => _newInitialStock; set => SetProperty(ref _newInitialStock, value); }

    public bool IsReceiveStockDialogOpen
    {
        get => _isReceiveStockDialogOpen;
        set => SetProperty(ref _isReceiveStockDialogOpen, value);
    }

    public decimal ReceiveQuantity
    {
        get => _receiveQuantity;
        set => SetProperty(ref _receiveQuantity, value);
    }

    public decimal ReceiveUnitCost
    {
        get => _receiveUnitCost;
        set => SetProperty(ref _receiveUnitCost, value);
    }

    public string ReceiveNote
    {
        get => _receiveNote;
        set => SetProperty(ref _receiveNote, value);
    }

    public bool IsAdjustStockDialogOpen
    {
        get => _isAdjustStockDialogOpen;
        set => SetProperty(ref _isAdjustStockDialogOpen, value);
    }

    public decimal AdjustActualQuantity
    {
        get => _adjustActualQuantity;
        set => SetProperty(ref _adjustActualQuantity, value);
    }

    public string AdjustReason
    {
        get => _adjustReason;
        set => SetProperty(ref _adjustReason, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenCreateProductCommand { get; }
    public ICommand ConfirmCreateProductCommand { get; }
    public ICommand CancelCreateProductCommand { get; }
    public ICommand OpenReceiveStockCommand { get; }
    public ICommand ConfirmReceiveStockCommand { get; }
    public ICommand CancelReceiveStockCommand { get; }

    public ICommand OpenAdjustStockCommand { get; }
    public ICommand ConfirmAdjustStockCommand { get; }
    public ICommand CancelAdjustStockCommand { get; }

    public InventoryViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;

        RefreshCommand = new AsyncRelayCommand(LoadInventoryAsync);

        OpenCreateProductCommand = new RelayCommand(() =>
        {
            ClearMessages();
            NewSku = string.Empty;
            NewProductName = string.Empty;
            NewUnit = "chai";
            NewSalePrice = 0;
            NewCostPrice = 0;
            NewTrackInventory = true;
            NewReorderLevel = 5;
            NewInitialStock = 0;
            SelectedNewCategory = Categories.FirstOrDefault();
            IsCreateProductDialogOpen = true;
        });
        ConfirmCreateProductCommand = new AsyncRelayCommand(ExecuteCreateProductAsync);
        CancelCreateProductCommand = new RelayCommand(() => IsCreateProductDialogOpen = false);

        OpenReceiveStockCommand = new RelayCommand(() =>
        {
            if (SelectedItem == null)
            {
                ErrorMessage = "Vui lòng chọn sản phẩm cần nhập kho.";
                return;
            }
            ClearMessages();
            ReceiveQuantity = 10;
            ReceiveUnitCost = SelectedItem.CostPrice;
            ReceiveNote = "Nhập thêm hàng hóa vào kho";
            IsReceiveStockDialogOpen = true;
        });

        ConfirmReceiveStockCommand = new AsyncRelayCommand(ExecuteReceiveStockAsync);
        CancelReceiveStockCommand = new RelayCommand(() => IsReceiveStockDialogOpen = false);

        OpenAdjustStockCommand = new RelayCommand(() =>
        {
            if (SelectedItem == null)
            {
                ErrorMessage = "Vui lòng chọn sản phẩm cần điều chỉnh tồn kho.";
                return;
            }
            ClearMessages();
            AdjustActualQuantity = SelectedItem.QuantityOnHand;
            AdjustReason = "Kiểm kê định kỳ phát hiện chênh lệch";
            IsAdjustStockDialogOpen = true;
        });

        ConfirmAdjustStockCommand = new AsyncRelayCommand(ExecuteAdjustStockAsync);
        CancelAdjustStockCommand = new RelayCommand(() => IsAdjustStockDialogOpen = false);
    }

    public override async Task InitializeAsync()
    {
        await LoadInventoryAsync();
    }

    private async Task LoadInventoryAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            var items = await _apiClient.GetInventoryAsync();
            var categories = await _apiClient.GetCategoriesAsync();
            _allItems = new ObservableCollection<InventoryItemDto>(items);
            Categories = new ObservableCollection<ProductCategoryDto>(categories.Where(c => c.IsActive));
            ApplyFilter();

            if (SelectedItem != null)
            {
                SelectedItem = _allItems.FirstOrDefault(i => i.ProductId == SelectedItem.ProductId) ?? FilteredItems.FirstOrDefault();
            }
            else
            {
                SelectedItem = FilteredItems.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải danh sách tồn kho: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteCreateProductAsync()
    {
        ClearMessages();
        if (SelectedNewCategory == null || string.IsNullOrWhiteSpace(NewSku) || string.IsNullOrWhiteSpace(NewProductName) || string.IsNullOrWhiteSpace(NewUnit))
        {
            ErrorMessage = "Vui lòng nhập đủ nhóm món, SKU, tên mặt hàng và đơn vị.";
            return;
        }

        if (NewSalePrice <= 0 || NewCostPrice < 0 || NewInitialStock < 0 || NewReorderLevel < 0)
        {
            ErrorMessage = "Giá bán phải lớn hơn 0; giá vốn, tồn đầu và mức cảnh báo không được âm.";
            return;
        }

        try
        {
            IsBusy = true;
            var product = await _apiClient.CreateProductAsync(new CreateProductRequest(
                SelectedNewCategory.Id,
                NewSku.Trim(),
                NewProductName.Trim(),
                NewUnit.Trim(),
                NewSalePrice,
                NewCostPrice,
                NewTrackInventory,
                NewReorderLevel,
                NewTrackInventory ? NewInitialStock : 0));
            IsCreateProductDialogOpen = false;
            await LoadInventoryAsync();
            SelectedItem = FilteredItems.FirstOrDefault(i => i.ProductId == product.Id);
            SuccessMessage = $"Đã thêm mặt hàng '{product.Name}'. Món sẽ xuất hiện trên máy hội viên khi tải lại thực đơn.";
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

    private void ApplyFilter()
    {
        if (FilterLowStockOnly)
        {
            FilteredItems = new ObservableCollection<InventoryItemDto>(_allItems.Where(i => i.IsLowStock));
        }
        else
        {
            FilteredItems = new ObservableCollection<InventoryItemDto>(_allItems);
        }
    }

    private async Task ExecuteReceiveStockAsync()
    {
        if (SelectedItem == null) return;
        ClearMessages();

        if (ReceiveQuantity <= 0)
        {
            ErrorMessage = "Số lượng nhập phải lớn hơn 0.";
            return;
        }

        try
        {
            IsBusy = true;
            await _apiClient.ReceiveInventoryAsync(new ReceiveInventoryRequest(
                SelectedItem.ProductId,
                ReceiveQuantity,
                ReceiveUnitCost,
                ReceiveNote
            ));

            IsReceiveStockDialogOpen = false;
            var message = $"Nhập kho {ReceiveQuantity} {SelectedItem.Unit} cho '{SelectedItem.Name}' thành công!";
            await LoadInventoryAsync();
            SuccessMessage = message;
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

    private async Task ExecuteAdjustStockAsync()
    {
        if (SelectedItem == null) return;
        ClearMessages();

        if (AdjustActualQuantity < 0)
        {
            ErrorMessage = "Số lượng tồn thực tế không được âm.";
            return;
        }

        try
        {
            IsBusy = true;
            await _apiClient.AdjustInventoryAsync(new AdjustInventoryRequest(
                SelectedItem.ProductId,
                AdjustActualQuantity,
                AdjustReason
            ));

            IsAdjustStockDialogOpen = false;
            var message = $"Điều chỉnh tồn kho '{SelectedItem.Name}' về {AdjustActualQuantity} {SelectedItem.Unit} thành công!";
            await LoadInventoryAsync();
            SuccessMessage = message;
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
