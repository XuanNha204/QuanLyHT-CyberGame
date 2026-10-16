using System.Windows;
using System.Windows.Controls;
using CyberGame.MemberClient.ViewModels;

namespace CyberGame.MemberClient.Views;

public partial class ActiveSessionView : UserControl
{
    public ActiveSessionView()
    {
        InitializeComponent();
    }

    private void Tab0_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm)
        {
            vm.SelectedTabIndex = 0;
            vm.ClearMessages();
        }
    }

    private void Tab1_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm)
        {
            vm.SelectedTabIndex = 1;
            vm.ClearMessages();
        }
    }

    private void Tab2_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm)
        {
            vm.SelectedTabIndex = 2;
            vm.ClearMessages();
        }
    }

    private void Tab3_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm)
        {
            vm.SelectedTabIndex = 3;
            vm.ClearMessages();
        }
    }

    private void CurrentPinBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm && sender is PasswordBox box)
        {
            vm.CurrentPin = box.Password;
        }
    }

    private void NewPinBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm && sender is PasswordBox box)
        {
            vm.NewPin = box.Password;
        }
    }

    private void ConfirmNewPinBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm && sender is PasswordBox box)
        {
            vm.ConfirmNewPin = box.Password;
        }
    }

    private async void ChangePin_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm && await vm.ChangePinAsync())
        {
            CurrentPinBox.Clear();
            NewPinBox.Clear();
            ConfirmNewPinBox.Clear();
        }
    }

    private void SwitchToTopUpTab_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ActiveSessionViewModel vm)
        {
            vm.SelectedTabIndex = 2;
            Tab2Radio.IsChecked = true;
            vm.ClearMessages();
        }
    }
}
