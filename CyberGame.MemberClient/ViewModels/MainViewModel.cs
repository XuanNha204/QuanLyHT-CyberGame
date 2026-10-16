using CyberGame.MemberClient.Common;

namespace CyberGame.MemberClient.ViewModels;

public class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentViewModel;

    public LoginViewModel LoginViewModel { get; }
    public ActiveSessionViewModel ActiveSessionViewModel { get; }

    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public MainViewModel(LoginViewModel loginViewModel, ActiveSessionViewModel activeSessionViewModel)
    {
        LoginViewModel = loginViewModel;
        ActiveSessionViewModel = activeSessionViewModel;

        LoginViewModel.LoginSucceeded += async loginInfo =>
        {
            CurrentViewModel = ActiveSessionViewModel;
            await ActiveSessionViewModel.StartSessionAsync(loginInfo);
        };

        ActiveSessionViewModel.SessionEnded += () =>
        {
            ActiveSessionViewModel.StopTimers();
            LoginViewModel.Reset();
            CurrentViewModel = LoginViewModel;
        };

        _currentViewModel = LoginViewModel;
    }
}
