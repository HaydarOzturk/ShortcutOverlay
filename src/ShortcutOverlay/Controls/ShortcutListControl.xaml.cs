using System.Collections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShortcutOverlay.Models;
using ShortcutOverlay.Services;
using ShortcutOverlay.ViewModels;

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
