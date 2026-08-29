using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DropZone.Models;

namespace DropZone;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private const int VK_SHIFT = 0x10;
    private const int VK_MENU = 0x12; // Alt key
    private const double DoubleTapThresholdMs = 350.0;
    private const double MarginFromCorner = 16.0;

    public ObservableCollection<FileItem> Files { get; } = new();

    private readonly DispatcherTimer _keyPollTimer;
    private bool _wasShiftPressed;
    private DateTime _lastShiftPressTime = DateTime.MinValue;
    private bool _isOpen;

    private Point _dragStartPoint;
    private FileItem? _draggedItem;
    private bool _isDragging;

    public MainWindow()
    {
        InitializeComponent();

        // Initially place off-screen and transparent
        Left = -10000;
        Top = -10000;
        Opacity = 0;
        _isOpen = false;

        FilesList.ItemsSource = Files;
        Files.CollectionChanged += Files_CollectionChanged;
        UpdatePlaceholderVisibility();

        // Setup timer to poll Shift/Alt key states across the system
        _keyPollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _keyPollTimer.Tick += KeyPollTimer_Tick;
        _keyPollTimer.Start();
    }

    private void KeyPollTimer_Tick(object? sender, EventArgs e)
    {
        bool isAltPressed = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        short shiftState = GetAsyncKeyState(VK_SHIFT);
        bool isShiftPressed = (shiftState & 0x8000) != 0;

        // If Alt is held (e.g. during Shift+Alt / Alt+Shift layout switch), ignore Shift and reset counter
        if (isAltPressed)
        {
            _lastShiftPressTime = DateTime.MinValue;
            _wasShiftPressed = isShiftPressed;
            return;
        }

        if (isShiftPressed && !_wasShiftPressed)
        {
            var now = DateTime.UtcNow;
            var elapsedMs = (now - _lastShiftPressTime).TotalMilliseconds;

            if (elapsedMs > 0 && elapsedMs <= DoubleTapThresholdMs)
            {
                // Double tap Shift detected! Toggle window state
                ToggleDropZone();
                _lastShiftPressTime = DateTime.MinValue;
            }
            else
            {
                _lastShiftPressTime = now;
            }
        }

        _wasShiftPressed = isShiftPressed;
    }

    private void ToggleDropZone()
    {
        if (_isOpen)
        {
            HideDropZone();
        }
        else
        {
            ShowDropZone();
        }
    }

    private void ShowDropZone()
    {
        // Position at bottom-right of the usable screen work area (above system tray / clock)
        var workArea = SystemParameters.WorkArea;

        Left = workArea.Left + workArea.Width - Width - MarginFromCorner;
        Top = workArea.Top + workArea.Height - Height - MarginFromCorner;

        _isOpen = true;
        Opacity = 1.0;
        Activate();
        Focus();
    }

    private void HideDropZone()
    {
        _isOpen = false;
        Opacity = 0.0;
        Left = -10000;
        Top = -10000;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideDropZone();
            e.Handled = true;
        }
    }

    private void Files_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdatePlaceholderVisibility();
    }

    private void UpdatePlaceholderVisibility()
    {
        if (Files.Count == 0)
        {
            PlaceholderPanel.Visibility = Visibility.Visible;
            FilesList.Visibility = Visibility.Collapsed;
        }
        else
        {
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            FilesList.Visibility = Visibility.Visible;
        }
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var droppedFiles = (string[]?)e.Data.GetData(DataFormats.FileDrop);
            if (droppedFiles != null)
            {
                foreach (var path in droppedFiles)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        bool exists = Files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase));
                        if (!exists)
                        {
                            Files.Add(new FileItem(path));
                        }
                    }
                }
            }
        }
    }

    private void FilesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject depObj)
        {
            var listBoxItem = FindVisualParent<ListBoxItem>(depObj);
            if (listBoxItem != null && listBoxItem.DataContext is FileItem item)
            {
                _draggedItem = item;
                _dragStartPoint = e.GetPosition(null);
                _isDragging = false;
            }
        }
    }

    private void FilesList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null && !_isDragging)
        {
            Point currentPos = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                try
                {
                    var fileToDrop = _draggedItem;
                    var dataObject = new DataObject(DataFormats.FileDrop, new string[] { fileToDrop.FilePath });
                    var result = DragDrop.DoDragDrop(FilesList, dataObject, DragDropEffects.Copy | DragDropEffects.Move);

                    if (result != DragDropEffects.None)
                    {
                        Files.Remove(fileToDrop);
                    }
                }
                finally
                {
                    _draggedItem = null;
                    _isDragging = false;
                }
            }
        }
    }

    private void FilesList_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject depObj)
        {
            var listBoxItem = FindVisualParent<ListBoxItem>(depObj);
            if (listBoxItem != null && listBoxItem.DataContext is FileItem item)
            {
                Files.Remove(item);
                e.Handled = true;
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent) return parent;
        return FindVisualParent<T>(parentObject);
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Application.Current.Shutdown();
    }
}
