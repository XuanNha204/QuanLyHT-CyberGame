using System.Windows;
using System.Windows.Controls;
using CyberGame.Client.ViewModels;

namespace CyberGame.Client.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        Loaded += LoginView_Loaded;
    }

    private void LoginView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            TxtPassword.Password = vm.Password;
        }
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Password = TxtPassword.Password;
        }
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            TxtPassword.Password = vm.Password;
        }
    }
}
