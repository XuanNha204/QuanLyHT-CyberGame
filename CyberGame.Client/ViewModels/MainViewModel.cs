using CyberGame.Client.Common;
using CyberGame.Client.Services;

namespace CyberGame.Client.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IApiClient _apiClient;
    private readonly IStateService _stateService;

    private ViewModelBase _currentView;

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public MainViewModel(IApiClient apiClient, IStateService stateService)
    {
        _apiClient = apiClient;
        _stateService = stateService;

        _currentView = CreateLoginViewModel();
    }

    private LoginViewModel CreateLoginViewModel()
    {
        return new LoginViewModel(_apiClient, _stateService, OnLoginSuccess);
    }

    private ShellViewModel CreateShellViewModel()
    {
        return new ShellViewModel(_apiClient, _stateService, OnLogout);
    }

    private void OnLoginSuccess()
    {
        var shellVM = CreateShellViewModel();
        CurrentView = shellVM;
        _ = shellVM.InitializeAsync();
    }

    private void OnLogout()
    {
        CurrentView = CreateLoginViewModel();
    }
}
