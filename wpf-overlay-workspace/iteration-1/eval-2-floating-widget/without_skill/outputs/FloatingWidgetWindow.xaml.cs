using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ShortcutOverlay
{
    /// <summary>
    /// Interaction logic for FloatingWidgetWindow.xaml
    /// Displays a transparent, draggable overlay window showing keyboard shortcuts grouped by category.
    /// </summary>
    public partial class FloatingWidgetWindow : Window
    {
        private bool _isDragging = false;
        private Point _dragStartPoint;

        public FloatingWidgetWindow()
        {
            InitializeComponent();
            InitializeShortcuts();
        }

        /// <summary>
        /// Initializes sample shortcut data grouped by category.
        /// Replace this with actual shortcut data from your application.
        /// </summary>
        private void InitializeShortcuts()
        {
            var categories = new ObservableCollection<ShortcutCategory>
            {
                new ShortcutCategory
                {
                    CategoryName = "Navigation",
                    Shortcuts = new ObservableCollection<Shortcut>
                    {
                        new Shortcut { Description = "Open File", Keys = "Ctrl+O" },
                        new Shortcut { Description = "Save File", Keys = "Ctrl+S" },
                        new Shortcut { Description = "New File", Keys = "Ctrl+N" },
                        new Shortcut { Description = "Close Window", Keys = "Ctrl+W" }
                    }
                },
                new ShortcutCategory
                {
                    CategoryName = "Editing",
                    Shortcuts = new ObservableCollection<Shortcut>
                    {
                        new Shortcut { Description = "Undo", Keys = "Ctrl+Z" },
                        new Shortcut { Description = "Redo", Keys = "Ctrl+Y" },
                        new Shortcut { Description = "Cut", Keys = "Ctrl+X" },
                        new Shortcut { Description = "Copy", Keys = "Ctrl+C" },
                        new Shortcut { Description = "Paste", Keys = "Ctrl+V" }
                    }
                },
                new ShortcutCategory
                {
                    CategoryName = "View",
                    Shortcuts = new ObservableCollection<Shortcut>
                    {
                        new Shortcut { Description = "Zoom In", Keys = "Ctrl++" },
                        new Shortcut { Description = "Zoom Out", Keys = "Ctrl+-" },
                        new Shortcut { Description = "Reset Zoom", Keys = "Ctrl+0" },
                        new Shortcut { Description = "Toggle Fullscreen", Keys = "F11" }
                    }
                },
                new ShortcutCategory
                {
                    CategoryName = "Search",
                    Shortcuts = new ObservableCollection<Shortcut>
                    {
                        new Shortcut { Description = "Find", Keys = "Ctrl+F" },
                        new Shortcut { Description = "Find Next", Keys = "F3" },
                        new Shortcut { Description = "Find Previous", Keys = "Shift+F3" },
                        new Shortcut { Description = "Replace", Keys = "Ctrl+H" }
                    }
                }
            };

            this.DataContext = new { ShortcutCategories = categories };
        }

        /// <summary>
        /// Handles window dragging when user clicks and drags the header area.
        /// </summary>
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _dragStartPoint = e.GetPosition(null);
                this.CaptureMouse();
            }
        }

        /// <summary>
        /// Updates window position during drag operation.
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(null);
                double offsetX = currentPoint.X - _dragStartPoint.X;
                double offsetY = currentPoint.Y - _dragStartPoint.Y;

                this.Left += offsetX;
                this.Top += offsetY;

                _dragStartPoint = currentPoint;
            }
        }

        /// <summary>
        /// Ends drag operation when mouse button is released.
        /// </summary>
        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isDragging)
            {
                _isDragging = false;
                this.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// Closes the widget when close button is clicked.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// Represents a single keyboard shortcut with description and key combination.
    /// </summary>
    public class Shortcut
    {
        public string Description { get; set; }
        public string Keys { get; set; }
    }

    /// <summary>
    /// Represents a category of shortcuts with a name and collection of shortcuts.
    /// </summary>
    public class ShortcutCategory
    {
        public string CategoryName { get; set; }
        public ObservableCollection<Shortcut> Shortcuts { get; set; }
    }
}
