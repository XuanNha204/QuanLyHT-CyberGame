using System.Windows;
using System.Windows.Controls;
using CyberGame.MemberClient.ViewModels;

namespace CyberGame.MemberClient.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private async void LoginView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
        {
            vm.Pin = pb.Password;
        }
    }
}
