using System.Windows;
using ShortcutOverlay.ViewModels;

namespace ShortcutOverlay.Views;

public partial class ShortcutEditorWindow : Window
{
    public ShortcutEditorWindow()
    {
        DataContext = new ShortcutEditorViewModel();
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Close_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Close();
    }
}
