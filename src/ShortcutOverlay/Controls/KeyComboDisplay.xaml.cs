using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ShortcutOverlay.Controls;

public partial class KeyComboDisplay : UserControl
{
    private readonly ObservableCollection<string> _individualKeys = new();

    public KeyComboDisplay()
    {
        InitializeComponent();
    }

    public string Keys
    {
        get => (string)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    public ObservableCollection<string> IndividualKeys => _individualKeys;

    public static readonly DependencyProperty KeysProperty =
        DependencyProperty.Register(
            nameof(Keys),
            typeof(string),
            typeof(KeyComboDisplay),
            new PropertyMetadata(null, OnKeysChanged));

    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not KeyComboDisplay control)
            return;

        control._individualKeys.Clear();

        if (e.NewValue is string keysString && !string.IsNullOrWhiteSpace(keysString))
        {
            var keys = keysString.Split('+');
            foreach (var key in keys)
            {
                control._individualKeys.Add(key.Trim());
            }
        }
    }
}
