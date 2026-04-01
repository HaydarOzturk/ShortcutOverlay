using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ShortcutOverlay.Models;
using ShortcutOverlay.NativeInterop;
using ShortcutOverlay.Services;
using ShortcutOverlay.ViewModels;
using ShortcutOverlay.Views;

namespace ShortcutOverlay.Controls;

public partial class ShortcutListControl : UserControl
{
    private const string DragFormat = "ShortcutCategory";

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

    /// <summary>
    /// When true, clicking a shortcut row executes the shortcut in the background app.
    /// Bound from the parent window based on pin state.
    /// </summary>
    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    public static readonly DependencyProperty IsPinnedProperty =
        DependencyProperty.Register(
            nameof(IsPinned),
            typeof(bool),
            typeof(ShortcutListControl),
            new PropertyMetadata(false));

    // ── Drag & Drop for category reordering ──

    private void CategoryDragGrip_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        if (element.DataContext is not ShortcutCategory category) return;

        e.Handled = true;

        var data = new DataObject(DragFormat, category);
        // Reduce opacity on source during drag
        var parent = FindParentStackPanel(element);
        if (parent != null) parent.Opacity = 0.5;

        DragDrop.DoDragDrop(element, data, DragDropEffects.Move);

        // Restore opacity after drop
        if (parent != null) parent.Opacity = 1.0;
    }

    private const double AutoScrollEdge = 40.0;  // pixels from edge to trigger scroll
    private const double AutoScrollStep = 8.0;   // pixels per DragOver tick

    private void Categories_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragFormat))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        // Auto-scroll when dragging near top/bottom edges of the ScrollViewer
        var mousePos = e.GetPosition(CategoryScrollViewer);
        if (mousePos.Y < AutoScrollEdge)
        {
            // Near top edge — scroll up
            CategoryScrollViewer.ScrollToVerticalOffset(
                CategoryScrollViewer.VerticalOffset - AutoScrollStep);
        }
        else if (mousePos.Y > CategoryScrollViewer.ActualHeight - AutoScrollEdge)
        {
            // Near bottom edge — scroll down
            CategoryScrollViewer.ScrollToVerticalOffset(
                CategoryScrollViewer.VerticalOffset + AutoScrollStep);
        }
    }

    private void Categories_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DragFormat)) return;
        if (e.Data.GetData(DragFormat) is not ShortcutCategory draggedCategory) return;

        // Find the target category from drop position
        var collection = Categories as ObservableCollection<ShortcutCategory>;
        if (collection == null) return;

        // Find the category we're dropping onto
        var dropTarget = FindCategoryAtPosition(e);
        if (dropTarget == null || dropTarget == draggedCategory) return;

        int oldIndex = -1;
        int newIndex = -1;

        for (int i = 0; i < collection.Count; i++)
        {
            if (collection[i].Name == draggedCategory.Name) oldIndex = i;
            if (collection[i].Name == dropTarget.Name) newIndex = i;
        }

        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

        // Move the category
        collection.Move(oldIndex, newIndex);

        // Persist the new order
        PersistCategoryOrder(collection);

        e.Handled = true;
    }

    private ShortcutCategory? FindCategoryAtPosition(DragEventArgs e)
    {
        // Hit-test the drop position to find which category item we're over
        var point = e.GetPosition(CategoriesItemsControl);
        var hitElement = CategoriesItemsControl.InputHitTest(point) as DependencyObject;

        while (hitElement != null)
        {
            if (hitElement is FrameworkElement fe && fe.DataContext is ShortcutCategory cat)
                return cat;
            hitElement = System.Windows.Media.VisualTreeHelper.GetParent(hitElement);
        }

        return null;
    }

    private static StackPanel? FindParentStackPanel(DependencyObject element)
    {
        var parent = System.Windows.Media.VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is StackPanel sp) return sp;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    // ── Click-to-execute shortcuts ──

    private async void ShortcutRow_Click(object sender, MouseButtonEventArgs e)
    {
        DebugLogger.Log("=== ShortcutRow_Click FIRED ===");

        if (sender is not FrameworkElement element)
        {
            DebugLogger.Log("EARLY EXIT: sender is not FrameworkElement");
            return;
        }
        if (element.DataContext is not ShortcutEntry shortcut)
        {
            DebugLogger.Log($"EARLY EXIT: DataContext is {element.DataContext?.GetType().Name ?? "null"}, not ShortcutEntry");
            return;
        }

        DebugLogger.Log($"Shortcut clicked: keys=\"{shortcut.Keys}\", desc=\"{shortcut.Description}\"");
        e.Handled = true;

        // Get the last known foreground window from the detection service
        var detection = App.Services.GetService(typeof(WindowDetectionService)) as WindowDetectionService;
        var targetHwnd = detection?.LastForegroundHwnd ?? IntPtr.Zero;
        DebugLogger.Log($"LastForegroundHwnd = 0x{targetHwnd:X} (detection service null={detection == null})");

        if (targetHwnd == IntPtr.Zero)
        {
            DebugLogger.Log("EARLY EXIT: targetHwnd is IntPtr.Zero — no foreground window tracked");
            return;
        }

        var parentWindow = Window.GetWindow(this);
        if (parentWindow == null)
        {
            DebugLogger.Log("EARLY EXIT: parentWindow is null");
            return;
        }
        DebugLogger.Log($"Parent window type: {parentWindow.GetType().Name}");

        // Brief visual feedback — flash the row
        var originalOpacity = element.Opacity;
        element.Opacity = 0.5;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        timer.Tick += (_, _) =>
        {
            element.Opacity = originalOpacity;
            timer.Stop();
        };
        timer.Start();

        // Execute on the UI thread using async/await (not Task.Run)
        // This keeps AttachThreadInput working correctly with the UI thread ID
        var overlayHandle = new WindowInteropHelper(parentWindow).Handle;
        DebugLogger.Log($"Overlay HWND = 0x{overlayHandle:X}");

        // Focus the target window and send keys asynchronously.
        DebugLogger.Log("Calling ExecuteAsync...");
        var result = await ShortcutExecutionService.ExecuteAsync(targetHwnd, shortcut.Keys);
        DebugLogger.Log($"ExecuteAsync returned: {result}");

        // Wait for the target app to process the shortcut, then re-assert topmost
        await System.Threading.Tasks.Task.Delay(200);

        Win32Api.SetWindowPos(overlayHandle, Win32Api.HWND_TOPMOST,
            0, 0, 0, 0,
            Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
        DebugLogger.Log("Re-asserted topmost. Click handler complete.");
    }

    private void PersistCategoryOrder(ObservableCollection<ShortcutCategory> categories)
    {
        // Get the profile ID from the ViewModel
        var viewModel = DataContext as MainViewModel ??
                        (Window.GetWindow(this)?.DataContext as MainViewModel);
        if (viewModel?.CurrentProfile == null) return;

        var orderedNames = categories.Select(c => c.Name).ToList();
        var profileManager = App.Services.GetService(typeof(ProfileManager)) as ProfileManager;
        profileManager?.SaveCategoryOrderAsync(viewModel.CurrentProfile.ProfileId, orderedNames)
            .ConfigureAwait(false);
    }
}
