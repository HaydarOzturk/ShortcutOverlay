# Overlay Windows Reference

XAML templates, transparency setup, Mica/Acrylic effects, and animations.

## Table of Contents

1. [Floating Widget Window](#1-floating-widget-window)
2. [Side Panel Window](#2-side-panel-window)
3. [System Tray Popup Window](#3-system-tray-popup-window)
4. [ShortcutListControl (Shared)](#4-shortcutlistcontrol)
5. [KeyComboDisplay Control](#5-keycombodisplay-control)
6. [Mica / Acrylic Backdrop Helper](#6-micaacrylic-backdrop-helper)
7. [Animation Helpers](#7-animation-helpers)
8. [Per-Monitor DPI Manifest](#8-per-monitor-dpi-manifest)
9. [Theme Support](#9-theme-support)

---

## 1. Floating Widget Window

Draggable, transparent, toggle-able with hotkey. Remembers its last position.

```xml
<Window x:Class="ShortcutOverlay.Views.FloatingWidgetWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:ShortcutOverlay.Controls"
        Title="ShortcutOverlay"
        Width="320" Height="450"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        MouseLeftButtonDown="Window_MouseLeftButtonDown">

    <Border CornerRadius="12"
            Background="{DynamicResource OverlayBackground}"
            BorderBrush="{DynamicResource OverlayBorder}"
            BorderThickness="1"
            Effect="{StaticResource DropShadowEffect}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- Header -->
                <RowDefinition Height="Auto"/>  <!-- Search -->
                <RowDefinition Height="*"/>     <!-- Shortcut list -->
                <RowDefinition Height="Auto"/>  <!-- Footer -->
            </Grid.RowDefinitions>

            <!-- Header: App name + dropdown -->
            <Border Grid.Row="0" Padding="12,8"
                    Background="{DynamicResource HeaderBackground}"
                    CornerRadius="12,12,0,0">
                <DockPanel>
                    <TextBlock Text="{Binding CurrentAppName}"
                               FontWeight="SemiBold" FontSize="14"
                               Foreground="{DynamicResource PrimaryText}"
                               VerticalAlignment="Center"/>
                    <ComboBox DockPanel.Dock="Right" HorizontalAlignment="Right"
                              ItemsSource="{Binding AllProfiles}"
                              DisplayMemberPath="DisplayName"
                              Width="30" Opacity="0.6"/>
                </DockPanel>
            </Border>

            <!-- Search bar -->
            <controls:SearchBox Grid.Row="1" Margin="12,8"
                                Text="{Binding SearchFilter, UpdateSourceTrigger=PropertyChanged}"/>

            <!-- Shortcut list (shared control) -->
            <controls:ShortcutListControl Grid.Row="2"
                                          Categories="{Binding FilteredCategories}"/>

            <!-- Footer: Edit + Settings buttons -->
            <StackPanel Grid.Row="3" Orientation="Horizontal"
                        HorizontalAlignment="Center" Margin="8">
                <Button Content="Edit" Command="{Binding EditProfileCommand}"
                        Style="{StaticResource FooterButton}" Margin="4,0"/>
                <Button Content="Settings" Command="{Binding OpenSettingsCommand}"
                        Style="{StaticResource FooterButton}" Margin="4,0"/>
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

**Code-behind (drag + topmost fix + Alt+Tab hiding):**

```csharp
public partial class FloatingWidgetWindow : Window, IOverlayMode
{
    public FloatingWidgetWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ForceTopmost();
        HideFromAltTab();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    public void ShowOverlay() { Show(); FadeIn(); }
    public void HideOverlay() { FadeOut(() => Hide()); }
    public void ToggleVisibility()
    {
        if (IsVisible) HideOverlay();
        else ShowOverlay();
    }

    // See win32-interop.md for ForceTopmost() and HideFromAltTab() implementations
}
```

---

## 2. Side Panel Window

Docked to left or right edge. Slides in on hover/hotkey, auto-hides when mouse leaves.

```xml
<Window x:Class="ShortcutOverlay.Views.SidePanelWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:ShortcutOverlay.Controls"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        MouseEnter="Window_MouseEnter"
        MouseLeave="Window_MouseLeave">

    <Border Background="{DynamicResource OverlayBackground}"
            BorderBrush="{DynamicResource OverlayBorder}"
            BorderThickness="0,0,1,0">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>

            <TextBlock Grid.Row="0" Text="{Binding CurrentAppName}"
                       FontWeight="SemiBold" FontSize="14"
                       Margin="12,12,12,4"
                       Foreground="{DynamicResource PrimaryText}"/>

            <controls:SearchBox Grid.Row="1" Margin="12,4"
                                Text="{Binding SearchFilter, UpdateSourceTrigger=PropertyChanged}"/>

            <controls:ShortcutListControl Grid.Row="2"
                                          Categories="{Binding FilteredCategories}"/>
        </Grid>
    </Border>
</Window>
```

**Positioning logic (code-behind):**

```csharp
public void DockToEdge(string side)
{
    var screenArea = SystemParameters.WorkArea;
    Height = screenArea.Height;
    Top = screenArea.Top;

    if (side == "right")
        Left = screenArea.Right - Width;
    else
        Left = screenArea.Left;
}
```

---

## 3. System Tray Popup Window

Uses `Hardcodet.NotifyIcon.Wpf` for the tray icon. Popup appears near the tray.

**App.xaml integration:**

```xml
<Application.Resources>
    <tb:TaskbarIcon x:Key="TrayIcon"
                    xmlns:tb="http://www.hardcodet.net/taskbar"
                    IconSource="/Resources/Icons/tray-icon.ico"
                    ToolTipText="ShortcutOverlay"
                    LeftClickCommand="{Binding ToggleOverlayCommand}"
                    DoubleClickCommand="{Binding OpenSettingsCommand}">
        <tb:TaskbarIcon.ContextMenu>
            <ContextMenu>
                <MenuItem Header="Show Overlay" Command="{Binding ToggleOverlayCommand}"/>
                <MenuItem Header="Settings" Command="{Binding OpenSettingsCommand}"/>
                <Separator/>
                <MenuItem Header="Quit" Command="{Binding QuitCommand}"/>
            </ContextMenu>
        </tb:TaskbarIcon.ContextMenu>
    </tb:TaskbarIcon>
</Application.Resources>
```

---

## 4. ShortcutListControl

Shared across all three display modes. Groups shortcuts by category with scrolling.

```xml
<UserControl x:Class="ShortcutOverlay.Controls.ShortcutListControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:ShortcutOverlay.Controls">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="12,0">
        <ItemsControl ItemsSource="{Binding Categories, RelativeSource={RelativeSource AncestorType=UserControl}}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <StackPanel Margin="0,8,0,0">
                        <!-- Category header -->
                        <TextBlock Text="{Binding Name}"
                                   FontSize="11" FontWeight="SemiBold"
                                   Foreground="{DynamicResource SecondaryText}"
                                   TextTransform="Uppercase"
                                   Margin="0,0,0,6"/>

                        <!-- Shortcuts in this category -->
                        <ItemsControl ItemsSource="{Binding Shortcuts}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Grid Margin="0,3">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                        </Grid.ColumnDefinitions>

                                        <controls:KeyComboDisplay
                                            Grid.Column="0"
                                            Keys="{Binding Keys}"
                                            Margin="0,0,10,0"/>

                                        <TextBlock Grid.Column="1"
                                                   Text="{Binding Description}"
                                                   Foreground="{DynamicResource PrimaryText}"
                                                   VerticalAlignment="Center"
                                                   TextTrimming="CharacterEllipsis"/>
                                    </Grid>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
    </ScrollViewer>
</UserControl>
```

---

## 5. KeyComboDisplay Control

Renders key combinations as styled badges (e.g., `[Ctrl]` `[Shift]` `[P]`).

```xml
<UserControl x:Class="ShortcutOverlay.Controls.KeyComboDisplay"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <ItemsControl ItemsSource="{Binding IndividualKeys, RelativeSource={RelativeSource AncestorType=UserControl}}">
        <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
                <WrapPanel/>
            </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Border Background="{DynamicResource KeyBadgeBackground}"
                        BorderBrush="{DynamicResource KeyBadgeBorder}"
                        BorderThickness="1"
                        CornerRadius="4"
                        Padding="6,2"
                        Margin="0,0,3,0">
                    <TextBlock Text="{Binding}"
                               FontFamily="Cascadia Code, Consolas, monospace"
                               FontSize="12"
                               Foreground="{DynamicResource KeyBadgeText}"/>
                </Border>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</UserControl>
```

**Code-behind to split "Ctrl+Shift+P" into ["Ctrl", "Shift", "P"]:**

```csharp
public partial class KeyComboDisplay : UserControl
{
    public static readonly DependencyProperty KeysProperty =
        DependencyProperty.Register(nameof(Keys), typeof(string), typeof(KeyComboDisplay),
            new PropertyMetadata(string.Empty, OnKeysChanged));

    public string Keys
    {
        get => (string)GetValue(KeysProperty);
        set => SetValue(KeysProperty, value);
    }

    public ObservableCollection<string> IndividualKeys { get; } = new();

    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (KeyComboDisplay)d;
        control.IndividualKeys.Clear();
        if (e.NewValue is string keys && !string.IsNullOrEmpty(keys))
        {
            foreach (var key in keys.Split('+', StringSplitOptions.TrimEntries))
            {
                control.IndividualKeys.Add(key);
            }
        }
    }
}
```

---

## 6. Mica / Acrylic Backdrop Helper

Apply modern Windows 11 backdrop effects. Falls back gracefully on Win10.

```csharp
using ShortcutOverlay.NativeInterop;
using System.Windows.Interop;

namespace ShortcutOverlay.Helpers;

public static class BackdropHelper
{
    /// <summary>
    /// Applies Mica or Acrylic backdrop to a WPF window.
    /// Only works on Windows 11 build 22523+.
    /// Call from OnSourceInitialized.
    /// </summary>
    public static bool TryApplyBackdrop(Window window, BackdropType type)
    {
        // Check Windows version
        if (Environment.OSVersion.Version.Build < 22523)
            return false;

        var handle = new WindowInteropHelper(window).Handle;
        int backdropType = type switch
        {
            BackdropType.Mica => Win32Api.DWMSBT_MAINWINDOW,
            BackdropType.Acrylic => Win32Api.DWMSBT_TRANSIENTWINDOW,
            BackdropType.MicaTabbed => Win32Api.DWMSBT_TABBEDWINDOW,
            _ => 0
        };

        int result = Win32Api.DwmSetWindowAttribute(
            handle,
            Win32Api.DWMWA_SYSTEMBACKDROP_TYPE,
            ref backdropType,
            sizeof(int));

        return result == 0; // S_OK
    }
}

public enum BackdropType
{
    Mica,
    Acrylic,
    MicaTabbed
}
```

---

## 7. Animation Helpers

Fade and slide animations for overlay show/hide transitions.

```csharp
using System.Windows.Media.Animation;

namespace ShortcutOverlay.Helpers;

public static class AnimationHelper
{
    public static void FadeIn(UIElement element, double durationMs = 200)
    {
        element.Opacity = 0;
        element.Visibility = Visibility.Visible;
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public static void FadeOut(UIElement element, double durationMs = 200, Action? onComplete = null)
    {
        var animation = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        if (onComplete != null)
        {
            animation.Completed += (_, _) => onComplete();
        }
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    public static void SlideIn(FrameworkElement element, string from = "right", double durationMs = 250)
    {
        var translation = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
        element.RenderTransform = translation;

        double startX = from == "right" ? element.ActualWidth : -element.ActualWidth;
        var animation = new DoubleAnimation(startX, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        translation.BeginAnimation(TranslateTransform.XProperty, animation);
    }
}
```

---

## 8. Per-Monitor DPI Manifest

Add `app.manifest` to the project to enable per-monitor DPI awareness.
Without this, the overlay is blurry on high-DPI monitors.

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">
        PerMonitorV2
      </dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">
        true/pm
      </dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

---

## 9. Theme Support

Dynamic resource keys for light/dark theming that follows the Windows system preference.

```xml
<!-- Resources/Styles/LightTheme.xaml -->
<ResourceDictionary>
    <SolidColorBrush x:Key="OverlayBackground" Color="#F0F0F0F0"/>  <!-- Semi-transparent white -->
    <SolidColorBrush x:Key="OverlayBorder" Color="#20000000"/>
    <SolidColorBrush x:Key="HeaderBackground" Color="#08000000"/>
    <SolidColorBrush x:Key="PrimaryText" Color="#FF1A1A1A"/>
    <SolidColorBrush x:Key="SecondaryText" Color="#FF666666"/>
    <SolidColorBrush x:Key="KeyBadgeBackground" Color="#FFFFFFFF"/>
    <SolidColorBrush x:Key="KeyBadgeBorder" Color="#FFD0D0D0"/>
    <SolidColorBrush x:Key="KeyBadgeText" Color="#FF333333"/>
</ResourceDictionary>

<!-- Resources/Styles/DarkTheme.xaml -->
<ResourceDictionary>
    <SolidColorBrush x:Key="OverlayBackground" Color="#F02D2D2D"/>
    <SolidColorBrush x:Key="OverlayBorder" Color="#20FFFFFF"/>
    <SolidColorBrush x:Key="HeaderBackground" Color="#08FFFFFF"/>
    <SolidColorBrush x:Key="PrimaryText" Color="#FFE0E0E0"/>
    <SolidColorBrush x:Key="SecondaryText" Color="#FF999999"/>
    <SolidColorBrush x:Key="KeyBadgeBackground" Color="#FF3D3D3D"/>
    <SolidColorBrush x:Key="KeyBadgeBorder" Color="#FF555555"/>
    <SolidColorBrush x:Key="KeyBadgeText" Color="#FFE0E0E0"/>
</ResourceDictionary>
```

**Switching themes at runtime:**

```csharp
public static class ThemeManager
{
    public static void ApplyTheme(string theme)
    {
        var actualTheme = theme == "auto" ? GetSystemTheme() : theme;
        var uri = new Uri($"Resources/Styles/{actualTheme}Theme.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = uri };

        // Replace the theme dictionary (keep index 0 for base styles)
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 1)
            merged[1] = dict;
        else
            merged.Add(dict);
    }

    private static string GetSystemTheme()
    {
        // Read from Registry: HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var value = key?.GetValue("AppsUseLightTheme");
        return value is int v && v == 0 ? "Dark" : "Light";
    }
}
```
