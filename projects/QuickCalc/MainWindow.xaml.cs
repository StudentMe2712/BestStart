using System;
using System.Data;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickCalc;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private const int HOTKEY_ID = 9000;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_Q = 0x51;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _hwndSource;
    private string? _currentResult = "0";

    private readonly SolidColorBrush _placeholderColor = new((Color)ColorConverter.ConvertFromString("#999999"));
    private readonly SolidColorBrush _validResultColor = new((Color)ColorConverter.ConvertFromString("#4EC9B0"));
    private readonly SolidColorBrush _pendingResultColor = new((Color)ColorConverter.ConvertFromString("#666666"));

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(helper.Handle);
        _hwndSource?.AddHook(HwndHook);

        // Register Alt + Q as global hotkey
        if (!RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_ALT | MOD_NOREPEAT, VK_Q))
        {
            // Fallback without MOD_NOREPEAT if older OS
            RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_ALT, VK_Q);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            ToggleVisibility();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void ToggleVisibility()
    {
        if (IsVisible && IsActive)
        {
            Hide();
        }
        else
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InputTextBox.Focus();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (!string.IsNullOrWhiteSpace(_currentResult) && _currentResult != "Error")
            {
                try
                {
                    Clipboard.SetText(_currentResult);
                }
                catch
                {
                    // Ignore clipboard lock errors if any
                }
                Hide();
                e.Handled = true;
            }
        }
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string rawInput = InputTextBox.Text;

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            PlaceholderTextBlock.Visibility = Visibility.Visible;
            ResultTextBlock.Text = "= 0";
            ResultTextBlock.Foreground = _placeholderColor;
            _currentResult = "0";
            return;
        }

        PlaceholderTextBlock.Visibility = Visibility.Collapsed;
        EvaluateExpression(rawInput);
    }

    private void EvaluateExpression(string input)
    {
        try
        {
            // Normalize expression
            string normalized = input.Trim()
                .Replace(',', '.')
                .Replace('×', '*')
                .Replace('÷', '/');

            // Replace 'x' or 'X' with '*' when preceded or followed by digits, brackets, or spaces
            normalized = Regex.Replace(normalized, @"(?<=[\d\)\s])[xX](?=[\d\(\s])", "*");

            // Ensure floating-point division by appending .0 to integer literals without a decimal point
            // This prevents DataTable.Compute integer division (e.g. 10 / 4 => 2 instead of 2.5)
            string computeExpr = Regex.Replace(normalized, @"(?<![\d\.])(\d+)(?![\d\.])", "$1.0");

            // Evaluate expression using DataTable
            using var table = new DataTable();
            object? resultObj = table.Compute(computeExpr, null);

            if (resultObj != null && resultObj != DBNull.Value)
            {
                if (double.TryParse(resultObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                {
                    if (double.IsInfinity(num) || double.IsNaN(num))
                    {
                        ResultTextBlock.Text = "= Error";
                        ResultTextBlock.Foreground = _pendingResultColor;
                        _currentResult = null;
                        return;
                    }

                    // Format cleanly: remove trailing zeros after decimal point
                    string formatted = num.ToString("0.##########", CultureInfo.InvariantCulture);
                    ResultTextBlock.Text = $"= {formatted}";
                    ResultTextBlock.Foreground = _validResultColor;
                    _currentResult = formatted;
                    return;
                }
            }

            ResultTextBlock.Text = "= ...";
            ResultTextBlock.Foreground = _pendingResultColor;
            _currentResult = null;
        }
        catch
        {
            // Incomplete or invalid intermediate expression while typing
            ResultTextBlock.Text = "= ...";
            ResultTextBlock.Foreground = _pendingResultColor;
            _currentResult = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_hwndSource != null)
        {
            UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
        base.OnClosed(e);
    }
}
