using System.Windows;
using System.Windows.Controls;

namespace ShortcutOverlay.Controls;

public partial class ShortcutListControl : UserControl
{
    public ShortcutListControl()
    {
        InitializeComponent();
    }

    public IEnumerable Categories
    {
        get => (IEnumerable)GetValue(CategoriesProperty);
        set => SetValue(CategoriesProperty, value);
    }

    public static readonly DependencyProperty CategoriesProperty =
        DependencyProperty.Register(
            nameof(Categories),
            typeof(IEnumerable),
            typeof(ShortcutListControl),
            new PropertyMetadata(null));
}
