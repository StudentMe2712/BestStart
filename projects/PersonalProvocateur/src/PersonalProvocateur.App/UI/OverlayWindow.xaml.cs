using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using PersonalProvocateur.App.Services;
using PersonalProvocateur.App.Storage;

namespace PersonalProvocateur.App.UI;

/// <summary>
/// Full-screen transparent overlay window hosting the screen dimming and interactive mascot.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly OverlayCoordinator? _coordinator;
    private readonly SettingsStore? _settingsStore;
    private bool _isClickHandled;

    /// <summary>
    /// Gets or sets whether the window is permitted to close.
    /// Alt+F4 and other close signals are cancelled when this is false.
    /// </summary>
    public bool CanClose { get; set; }

    /// <summary>
    /// Gets whether a mascot click has already been handled to prevent double-clicking.
    /// </summary>
    public bool IsClickHandled => _isClickHandled;

    /// <summary>
    /// Initializes a new instance of <see cref="OverlayWindow"/>.
    /// </summary>
    /// <param name="coordinator">The overlay coordinator orchestrating sessions.</param>
    /// <param name="settingsStore">Application settings store for reading dimming opacity.</param>
    public OverlayWindow(OverlayCoordinator coordinator, SettingsStore settingsStore)
    {
        InitializeComponent();

        _coordinator = coordinator;
        _settingsStore = settingsStore;

        Loaded += OnLoaded;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>
    /// Parameterless constructor for designer support or testability.
    /// </summary>
    public OverlayWindow() : this(null!, null!)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateOverlayBounds();

        // Apply dimming opacity from settings
        double opacity = _settingsStore?.CurrentSettings?.DimmingOpacity ?? 0.60;
        opacity = Math.Clamp(opacity, 0.10, 0.95);
        byte alpha = (byte)(opacity * 255);
        DimmingRectangle.Fill = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));

        // Reset click handling flag
        _isClickHandled = false;

        // Position mascot randomly within safe screen bounds
        PositionMascot();

        // Start entrance peek animation
        Mascot.PlayPeekAnimation();
    }

    /// <summary>
    /// Positions the mascot control at a randomized safe position on the overlay canvas.
    /// </summary>
    /// <param name="random">Optional random generator for deterministic testing.</param>
    public void PositionMascot(Random? random = null)
    {
        double screenWidth = Width > 0 ? Width : SystemParameters.PrimaryScreenWidth;
        double screenHeight = Height > 0 ? Height : SystemParameters.PrimaryScreenHeight;

        double mascotWidth = !double.IsNaN(Mascot.Width) && Mascot.Width > 0 ? Mascot.Width : 260.0;
        double mascotHeight = !double.IsNaN(Mascot.Height) && Mascot.Height > 0 ? Mascot.Height : 260.0;

        var (left, top) = CalculateSafeMascotPosition(screenWidth, screenHeight, mascotWidth, mascotHeight, random);
        Canvas.SetLeft(Mascot, left);
        Canvas.SetTop(Mascot, top);
    }

    /// <summary>
    /// Calculates a random safe spawn position for the mascot on screen,
    /// respecting margins from screen edges and the Windows taskbar.
    /// </summary>
    /// <param name="screenWidth">Width of the target display.</param>
    /// <param name="screenHeight">Height of the target display.</param>
    /// <param name="mascotWidth">Width of the mascot control (default 260).</param>
    /// <param name="mascotHeight">Height of the mascot control (default 260).</param>
    /// <param name="random">Optional random generator for deterministic testing.</param>
    /// <returns>A tuple with safe (Left, Top) coordinates.</returns>
    public static (double Left, double Top) CalculateSafeMascotPosition(
        double screenWidth,
        double screenHeight,
        double mascotWidth = 260,
        double mascotHeight = 260,
        Random? random = null)
    {
        var rng = random ?? Random.Shared;

        // Minimum safe boundaries:
        // Left/Right: min 60-100 px (80 px used as default baseline margin)
        // Top: min 80 px
        // Bottom (Taskbar): min 120-160 px (140 px used as baseline margin)
        double minSafeLeft = 80.0;
        double maxSafeLeft = Math.Max(minSafeLeft, screenWidth - mascotWidth - 80.0);
        double minSafeTop = 80.0;
        double maxSafeTop = Math.Max(minSafeTop, screenHeight - mascotHeight - 140.0);

        // Select from 5 safe candidate spawn zones:
        // 0: Bottom-Right corner
        // 1: Bottom-Left corner
        // 2: Bottom-Center area
        // 3: Right-Side edge
        // 4: Left-Side edge
        int zone = rng.Next(5);

        double x, y;
        switch (zone)
        {
            case 0: // Bottom-Right
                x = screenWidth - mascotWidth - NextDouble(rng, 70.0, 130.0);
                y = screenHeight - mascotHeight - NextDouble(rng, 120.0, 160.0);
                break;
            case 1: // Bottom-Left
                x = NextDouble(rng, 70.0, 130.0);
                y = screenHeight - mascotHeight - NextDouble(rng, 120.0, 160.0);
                break;
            case 2: // Bottom-Center
                double centerX = (screenWidth - mascotWidth) / 2.0;
                x = centerX + NextDouble(rng, -100.0, 100.0);
                y = screenHeight - mascotHeight - NextDouble(rng, 120.0, 160.0);
                break;
            case 3: // Right-Side
                x = screenWidth - mascotWidth - NextDouble(rng, 70.0, 110.0);
                y = NextDouble(rng, 100.0, Math.Max(100.0, screenHeight - mascotHeight - 160.0));
                break;
            default: // Left-Side
                x = NextDouble(rng, 70.0, 110.0);
                y = NextDouble(rng, 100.0, Math.Max(100.0, screenHeight - mascotHeight - 160.0));
                break;
        }

        x = Math.Clamp(x, minSafeLeft, maxSafeLeft);
        y = Math.Clamp(y, minSafeTop, maxSafeTop);

        return (x, y);
    }

    private static double NextDouble(Random rng, double min, double max)
    {
        if (min >= max)
        {
            return min;
        }

        return min + (rng.NextDouble() * (max - min));
    }

    /// <summary>
    /// Handles user click on the mascot control with debounce protection.
    /// </summary>
    public void HandleMascotClick()
    {
        if (_isClickHandled)
        {
            return;
        }

        _isClickHandled = true;
        Mascot.PlayClickAnimation(() => _coordinator?.OnMascotClicked());
    }

    private void Mascot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        HandleMascotClick();
    }

    /// <summary>
    /// Safely closes the overlay window by setting CanClose to true before closing.
    /// Stops all running mascot animations and timers.
    /// </summary>
    public void SafeClose()
    {
        CanClose = true;
        Mascot?.StopAllAnimations();
        Close();
    }

    /// <summary>
    /// Displays notification when the video library has no playable files.
    /// </summary>
    public void ShowEmptyLibraryNotice()
    {
        Mascot?.StopAllAnimations();
        MascotCanvas.Visibility = Visibility.Collapsed;
        EmptyLibraryNotice.Visibility = Visibility.Visible;
    }

    private void CloseNoticeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator != null)
        {
            _coordinator.CompleteSession();
        }
        else
        {
            SafeClose();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.InvokeAsync(UpdateOverlayBounds);
        }
        else
        {
            UpdateOverlayBounds();
        }
    }

    private void UpdateOverlayBounds()
    {
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        PositionMascot();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!CanClose)
        {
            e.Cancel = true;
            return;
        }

        Mascot?.StopAllAnimations();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Mascot?.StopAllAnimations();
        base.OnClosed(e);
    }
}
