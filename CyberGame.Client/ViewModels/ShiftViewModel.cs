using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;
using CyberGame.Domain.Enums;

namespace CyberGame.Client.ViewModels;

public class ShiftViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;

    private ShiftDto? _currentShift;
    private ShiftSummaryDto? _shiftSummary;

    // Open Shift Dialog
    private bool _isOpenShiftDialogOpen;
    private decimal _openingCashInput = 500000m;

    // Close Shift Dialog
    private bool _isCloseShiftDialogOpen;
    private decimal _actualCashInput;
    private string _closingNote = "Chốt ca đúng chuẩn";

    // Cash Movement Dialog
    private bool _isCashMovementDialogOpen;
    private CashMovementType _movementType = CashMovementType.Expense;
    private decimal _movementAmount = 50000m;
    private string _movementReason = "Chi mua đá lạnh";

    public ShiftDto? CurrentShift
    {
        get => _currentShift;
        set => SetProperty(ref _currentShift, value);
    }

    public ShiftSummaryDto? ShiftSummary
    {
        get => _shiftSummary;
        set => SetProperty(ref _shiftSummary, value);
    }

    public bool HasActiveShift => CurrentShift != null && CurrentShift.Status == ShiftStatus.Open;

    public bool IsOpenShiftDialogOpen
    {
        get => _isOpenShiftDialogOpen;
        set => SetProperty(ref _isOpenShiftDialogOpen, value);
    }

    public decimal OpeningCashInput
    {
        get => _openingCashInput;
        set => SetProperty(ref _openingCashInput, value);
    }

    public bool IsCloseShiftDialogOpen
    {
        get => _isCloseShiftDialogOpen;
        set => SetProperty(ref _isCloseShiftDialogOpen, value);
    }

    public decimal ActualCashInput
    {
        get => _actualCashInput;
        set
        {
            if (SetProperty(ref _actualCashInput, value))
            {
                OnPropertyChanged(nameof(CashDifference));
            }
        }
    }

    public decimal CashDifference => ActualCashInput - (ShiftSummary?.ExpectedCash ?? 0m);

    public string ClosingNote
    {
        get => _closingNote;
        set => SetProperty(ref _closingNote, value);
    }

    public bool IsCashMovementDialogOpen
    {
        get => _isCashMovementDialogOpen;
        set => SetProperty(ref _isCashMovementDialogOpen, value);
    }

    public CashMovementType MovementType
    {
        get => _movementType;
        set => SetProperty(ref _movementType, value);
    }

    public decimal MovementAmount
    {
        get => _movementAmount;
        set => SetProperty(ref _movementAmount, value);
    }

    public string MovementReason
    {
        get => _movementReason;
        set => SetProperty(ref _movementReason, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenShiftCommand { get; }
    public ICommand ConfirmOpenShiftCommand { get; }
    public ICommand CancelOpenShiftCommand { get; }

    public ICommand CloseShiftCommand { get; }
    public ICommand ConfirmCloseShiftCommand { get; }
    public ICommand CancelCloseShiftCommand { get; }

    public ICommand OpenCashMovementCommand { get; }
    public ICommand ConfirmCashMovementCommand { get; }
    public ICommand CancelCashMovementCommand { get; }

    public ShiftViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;

        RefreshCommand = new AsyncRelayCommand(LoadShiftDataAsync);

        OpenShiftCommand = new RelayCommand(() =>
        {
            ClearMessages();
            OpeningCashInput = 500000m;
            IsOpenShiftDialogOpen = true;
        });

        ConfirmOpenShiftCommand = new AsyncRelayCommand(ExecuteOpenShiftAsync);
        CancelOpenShiftCommand = new RelayCommand(() => IsOpenShiftDialogOpen = false);

        CloseShiftCommand = new AsyncRelayCommand(async () =>
        {
            ClearMessages();
            if (CurrentShift == null) return;
            try
            {
                IsBusy = true;
                ShiftSummary = await _apiClient.GetShiftSummaryAsync(CurrentShift.Id);
                ActualCashInput = ShiftSummary.ExpectedCash;
                ClosingNote = "Chốt ca thu ngân";
                IsCloseShiftDialogOpen = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        });

        ConfirmCloseShiftCommand = new AsyncRelayCommand(ExecuteCloseShiftAsync);
        CancelCloseShiftCommand = new RelayCommand(() => IsCloseShiftDialogOpen = false);

        OpenCashMovementCommand = new RelayCommand(() =>
        {
            ClearMessages();
            MovementType = CashMovementType.Expense;
            MovementAmount = 50000m;
            MovementReason = "Chi mua vật dụng quầy";
            IsCashMovementDialogOpen = true;
        });

        ConfirmCashMovementCommand = new AsyncRelayCommand(ExecuteCashMovementAsync);
        CancelCashMovementCommand = new RelayCommand(() => IsCashMovementDialogOpen = false);
    }

    public override async Task InitializeAsync()
    {
        await LoadShiftDataAsync();
    }

    public async Task LoadShiftDataAsync()
    {
        ClearMessages();
        try
        {
            IsBusy = true;
            CurrentShift = await _apiClient.GetCurrentShiftAsync();
            _stateService.CurrentShift = CurrentShift;
            OnPropertyChanged(nameof(HasActiveShift));

            if (CurrentShift != null)
            {
                ShiftSummary = await _apiClient.GetShiftSummaryAsync(CurrentShift.Id);
            }
            else
            {
                ShiftSummary = null;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Không thể tải thông tin ca: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteOpenShiftAsync()
    {
        ClearMessages();
        if (OpeningCashInput < 0)
        {
            ErrorMessage = "Tiền đầu ca không được âm.";
            return;
        }

        try
        {
            IsBusy = true;
            var shift = await _apiClient.OpenShiftAsync(new OpenShiftRequest(OpeningCashInput));
            _stateService.CurrentShift = shift;
            IsOpenShiftDialogOpen = false;
            SuccessMessage = $"Mở ca làm việc #{shift.ShiftNumber} thành công với số tiền đầu ca {shift.OpeningCash:N0} đ!";
            await LoadShiftDataAsync();
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

    private async Task ExecuteCloseShiftAsync()
    {
        if (CurrentShift == null) return;
        ClearMessages();

        try
        {
            IsBusy = true;
            var closed = await _apiClient.CloseShiftAsync(CurrentShift.Id, new CloseShiftRequest(ActualCashInput, ClosingNote));
            _stateService.CurrentShift = null;
            IsCloseShiftDialogOpen = false;
            SuccessMessage = $"Đã chốt ca #{closed.ShiftNumber}! Tiền thực tế: {closed.ActualCash:N0} đ, Chênh lệch: {closed.Difference:N0} đ";
            await LoadShiftDataAsync();
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

    private async Task ExecuteCashMovementAsync()
    {
        if (CurrentShift == null) return;
        ClearMessages();

        if (MovementAmount <= 0)
        {
            ErrorMessage = "Số tiền phải lớn hơn 0.";
            return;
        }

        try
        {
            IsBusy = true;
            await _apiClient.AddCashMovementAsync(CurrentShift.Id, new CreateCashMovementRequest(
                MovementType,
                MovementAmount,
                MovementReason
            ));

            IsCashMovementDialogOpen = false;
            SuccessMessage = $"Ghi nhận {(MovementType == CashMovementType.Expense ? "chi" : "thu")} tiền mặt {MovementAmount:N0} đ thành công!";
            await LoadShiftDataAsync();
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
