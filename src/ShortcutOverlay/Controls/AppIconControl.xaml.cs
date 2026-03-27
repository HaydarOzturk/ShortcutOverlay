using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShortcutOverlay.Controls;

/// <summary>
/// Adaptive Hotglass app icon that smoothly transitions its gradient colors
/// with the current theme. Listens for DynamicResource changes on
/// IconGradientStart/IconGradientEnd and updates the gradient stops.
/// </summary>
public partial class AppIconControl : UserControl
{
    public AppIconControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initial color sync
        UpdateGradientColors();

        // Subscribe to resource changes so we update when ThemeAnimator
        // replaces the SolidColorBrush resources during transitions.
        // We use a DispatcherTimer to poll at 60fps during transitions,
        // matching ThemeAnimator's frame rate.
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        timer.Tick += (_, _) => UpdateGradientColors();
        timer.Start();

        // Clean up timer when unloaded
        Unloaded += (_, _) => timer.Stop();
    }

    private void UpdateGradientColors()
    {
        var res = Application.Current.Resources;

        if (res["IconGradientStart"] is SolidColorBrush startBrush)
            GradStop1.Color = startBrush.Color;

        if (res["IconGradientEnd"] is SolidColorBrush endBrush)
            GradStop2.Color = endBrush.Color;
    }
}
