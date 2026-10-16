using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.ViewModels;

public class CartItemModel : ViewModelBase
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }

    private decimal _quantity = 1;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(LineTotal));
            }
        }
    }

    private string? _note;
    public string? Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public decimal LineTotal => UnitPrice * Quantity;
}

public class PosViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;
    private readonly DispatcherTimer _serviceOrdersTimer;
    private bool _isRefreshingServiceOrders;

    private ObservableCollection<ProductCategoryDto> _categories = new();
    private ProductCategoryDto? _selectedCategory;
    private ObservableCollection<ProductDto> _allProducts = new();
    private ObservableCollection<ProductDto> _filteredProducts = new();

    private ObservableCollection<CartItemModel> _cartItems = new();
    private decimal _cartTotal;

    private ObservableCollection<PlaySessionDto> _activeSessions = new();
    private PlaySessionDto? _selectedSessionTarget;
    private ObservableCollection<OrderDto> _pendingMemberOrders = new();
    private ObservableCollection<OrderDto> _confirmedMemberOrders = new();

    public ObservableCollection<ProductCategoryDto> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    public ProductCategoryDto? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                ApplyCategoryFilter();
            }
        }
    }

    public ObservableCollection<ProductDto> FilteredProducts
    {
        get => _filteredProducts;
        set => SetProperty(ref _filteredProducts, value);
    }

    public ObservableCollection<CartItemModel> CartItems
    {
        get => _cartItems;
        set => SetProperty(ref _cartItems, value);
    }

    public decimal CartTotal
    {
        get => _cartTotal;
        set => SetProperty(ref _cartTotal, value);
    }

    public ObservableCollection<PlaySessionDto> ActiveSessions
    {
        get => _activeSessions;
        set => SetProperty(ref _activeSessions, value);
    }

    public PlaySessionDto? SelectedSessionTarget
    {
        get => _selectedSessionTarget;
        set => SetProperty(ref _selectedSessionTarget, value);
    }

    public ObservableCollection<OrderDto> PendingMemberOrders
    {
        get => _pendingMemberOrders;
        set => SetProperty(ref _pendingMemberOrders, value);
    }

    public ObservableCollection<OrderDto> ConfirmedMemberOrders
    {
        get => _confirmedMemberOrders;
        set => SetProperty(ref _confirmedMemberOrders, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand RemoveFromCartCommand { get; }
    public ICommand IncreaseQtyCommand { get; }
    public ICommand DecreaseQtyCommand { get; }
    public ICommand ClearCartCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand RefreshServiceOrdersCommand { get; }
    public ICommand ApproveOrderCommand { get; }
    public ICommand RejectOrderCommand { get; }
    public ICommand FulfillOrderCommand { get; }

    public PosViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;
        _serviceOrdersTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _serviceOrdersTimer.Tick += async (_, _) => await LoadServiceOrdersAsync(clearMessages: false);

        RefreshCommand = new AsyncRelayCommand(LoadPosDataAsync);

        AddToCartCommand = new RelayCommand(param =>
        {
            if (param is ProductDto product)
            {
                var existing = CartItems.FirstOrDefault(i => i.ProductId == product.Id);
                if (existing != null)
                {
                    existing.Quantity += 1;
                }
                else
                {
                    CartItems.Add(new CartItemModel
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Sku = product.Sku,
                        Unit = product.Unit,
                        UnitPrice = product.SalePrice,
                        Quantity = 1
                    });
                }
                RecalculateTotal();
            }
        });

        RemoveFromCartCommand = new RelayCommand(param =>
        {
            if (param is CartItemModel item)
            {
                CartItems.Remove(item);
                RecalculateTotal();
            }
        });

        IncreaseQtyCommand = new RelayCommand(param =>
        {
            if (param is CartItemModel item)
            {
                item.Quantity += 1;
                RecalculateTotal();
            }
        });

        DecreaseQtyCommand = new RelayCommand(param =>
        {
            if (param is CartItemModel item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity -= 1;
                }
                else
                {
                    CartItems.Remove(item);
                }
                RecalculateTotal();
            }
        });

        ClearCartCommand = new RelayCommand(() =>
        {
            CartItems.Clear();
            RecalculateTotal();
        });

        CheckoutCommand = new AsyncRelayCommand(ExecuteCheckoutAsync);
        RefreshServiceOrdersCommand = new AsyncRelayCommand(LoadServiceOrdersAsync);
        ApproveOrderCommand = new AsyncRelayCommand(ExecuteApproveOrderAsync);
        RejectOrderCommand = new AsyncRelayCommand(ExecuteRejectOrderAsync);
        FulfillOrderCommand = new AsyncRelayCommand(ExecuteFulfillOrderAsync);
    }

    public override async Task InitializeAsync()
    {
        await LoadPosDataAsync();
        _serviceOrdersTimer.Start();
    }

    private async Task LoadPosDataAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            var cats = await _apiClient.GetCategoriesAsync();
            var products = await _apiClient.GetProductsAsync();
            var sessions = await _apiClient.GetActiveSessionsAsync();

            Categories = new ObservableCollection<ProductCategoryDto>(cats);
            _allProducts = new ObservableCollection<ProductDto>(products);
            ActiveSessions = new ObservableCollection<PlaySessionDto>(sessions);

            ApplyCategoryFilter();
            await LoadServiceOrdersAsync(clearMessages: false);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải dữ liệu bán hàng: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadServiceOrdersAsync() => await LoadServiceOrdersAsync(clearMessages: true);

    private async Task LoadServiceOrdersAsync(bool clearMessages)
    {
        if (_isRefreshingServiceOrders) return;
        _isRefreshingServiceOrders = true;
        if (clearMessages) ClearMessages();
        try
        {
            var pending = await _apiClient.GetOrdersAsync(OrderStatus.Draft, pageSize: 100);
            var confirmed = await _apiClient.GetOrdersAsync(OrderStatus.Confirmed, pageSize: 100);
            PendingMemberOrders = new ObservableCollection<OrderDto>(pending.Items.Where(o => o.PlaySessionId.HasValue));
            ConfirmedMemberOrders = new ObservableCollection<OrderDto>(confirmed.Items.Where(o => o.PlaySessionId.HasValue));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải hàng đợi món từ máy khách: {ex.Message}";
        }
        finally
        {
            _isRefreshingServiceOrders = false;
        }
    }

    private async Task ExecuteApproveOrderAsync(object? parameter)
    {
        if (parameter is not OrderDto order) return;
        ClearMessages();
        try
        {
            IsBusy = true;
            await _apiClient.ConfirmOrderAsync(order.Id);
            SuccessMessage = $"Đã ghi nhận thu tiền mặt và duyệt đơn {order.OrderNumber}; tồn kho đã được cập nhật.";
            await LoadServiceOrdersAsync(clearMessages: false);
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

    private async Task ExecuteRejectOrderAsync(object? parameter)
    {
        if (parameter is not OrderDto order) return;
        ClearMessages();
        try
        {
            IsBusy = true;
            await _apiClient.CancelOrderAsync(order.Id);
            SuccessMessage = order.PaymentStatus == PaymentStatus.Completed
                ? $"Đã hủy đơn {order.OrderNumber}, hoàn tiền và hoàn lại tồn kho."
                : $"Đã từ chối đơn {order.OrderNumber}.";
            await LoadServiceOrdersAsync(clearMessages: false);
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

    private async Task ExecuteFulfillOrderAsync(object? parameter)
    {
        if (parameter is not OrderDto order) return;
        ClearMessages();
        try
        {
            IsBusy = true;
            await _apiClient.FulfillOrderAsync(order.Id);
            SuccessMessage = $"Đơn {order.OrderNumber} đã được đánh dấu phục vụ xong.";
            await LoadServiceOrdersAsync(clearMessages: false);
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

    private void ApplyCategoryFilter()
    {
        if (SelectedCategory == null)
        {
            FilteredProducts = new ObservableCollection<ProductDto>(_allProducts);
        }
        else
        {
            FilteredProducts = new ObservableCollection<ProductDto>(_allProducts.Where(p => p.CategoryId == SelectedCategory.Id));
        }
    }

    private void RecalculateTotal()
    {
        CartTotal = CartItems.Sum(i => i.LineTotal);
    }

    private async Task ExecuteCheckoutAsync()
    {
        ClearMessages();
        if (!CartItems.Any())
        {
            ErrorMessage = "Giỏ hàng đang trống. Vui lòng chọn sản phẩm.";
            return;
        }

        try
        {
            IsBusy = true;
            var items = CartItems.Select(i => new CreateOrderItemRequest(i.ProductId, i.Quantity, i.Note)).ToList();
            var order = await _apiClient.CreateOrderAsync(new CreateOrderRequest(
                SelectedSessionTarget?.Id,
                SelectedSessionTarget?.CustomerId,
                items
            ));

            CartItems.Clear();
            RecalculateTotal();
            await LoadPosDataAsync();
            SuccessMessage = $"Tạo đơn {order.OrderNumber} thành công! Tổng tiền: {order.TotalAmount:N0} đ";
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
