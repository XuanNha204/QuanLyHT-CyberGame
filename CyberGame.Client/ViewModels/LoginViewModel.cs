using System.Windows.Input;
using CyberGame.Application.DTOs;
using CyberGame.Client.Common;
using CyberGame.Client.Services;

namespace CyberGame.Client.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;
    private readonly Action _onLoginSuccess;

    private string _username = "owner";
    private string _password = string.Empty;
    private bool _rememberMe = true;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool RememberMe
    {
        get => _rememberMe;
        set => SetProperty(ref _rememberMe, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand FillOwnerCommand { get; }
    public ICommand FillCashierCommand { get; }
    public ICommand FillTechCommand { get; }

    public LoginViewModel(IApiClient apiClient, IStateService stateService, Action onLoginSuccess)
    {
        _apiClient = apiClient;
        _stateService = stateService;
        _onLoginSuccess = onLoginSuccess;

        LoginCommand = new AsyncRelayCommand(ExecuteLoginAsync, () => !IsBusy);
        FillOwnerCommand = new RelayCommand(() =>
        {
            Username = "owner";
            Password = string.Empty;
            ClearMessages();
        });
        FillCashierCommand = new RelayCommand(() =>
        {
            Username = "cashier";
            Password = string.Empty;
            ClearMessages();
        });
        FillTechCommand = new RelayCommand(() =>
        {
            Username = "tech";
            Password = string.Empty;
            ClearMessages();
        });
    }

    private async Task ExecuteLoginAsync()
    {
        ClearMessages();

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Vui lòng nhập tên đăng nhập và mật khẩu.";
            return;
        }

        try
        {
            IsBusy = true;
            var response = await _apiClient.LoginAsync(new LoginRequest(Username.Trim(), Password));
            _stateService.CurrentUser = response.User;

            // Kiểm tra ca làm việc hiện tại
            try
            {
                var currentShift = await _apiClient.GetCurrentShiftAsync();
                _stateService.CurrentShift = currentShift;
            }
            catch
            {
                // Bỏ qua nếu chưa có ca
            }

            _onLoginSuccess();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("Unauthorized")
                ? "Tên đăng nhập hoặc mật khẩu không chính xác."
                : ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
