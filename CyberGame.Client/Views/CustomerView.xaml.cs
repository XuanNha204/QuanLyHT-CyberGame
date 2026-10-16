using System.Windows.Controls;
using System.Windows;
using CyberGame.Client.ViewModels;

namespace CyberGame.Client.Views;

public partial class CustomerView : UserControl
{
    public CustomerView()
    {
        InitializeComponent();
    }

    private void OpenCreateMember_Click(object sender, RoutedEventArgs e) => CreatePinBox.Clear();

    private void OpenSetPin_Click(object sender, RoutedEventArgs e) => SetPinBox.Clear();

    private void CreatePinBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomerViewModel vm && sender is PasswordBox box)
        {
            vm.NewInitialPin = box.Password;
        }
    }

    private void SetPinBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is CustomerViewModel vm && sender is PasswordBox box)
        {
            vm.PinInput = box.Password;
        }
    }
}
