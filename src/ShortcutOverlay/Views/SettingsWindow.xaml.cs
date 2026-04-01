using System.Windows;
using ShortcutOverlay.ViewModels;

namespace ShortcutOverlay.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        var vm = new SettingsViewModel();
        vm.SettingsSaved += () => DialogResult = true;

        DataContext = vm;
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DialogResult = false;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            await vm.SaveCommand.ExecuteAsync(null);
    }
}
